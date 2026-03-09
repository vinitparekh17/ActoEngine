using ActoEngine.WebApi.Features.Clients;
using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.LogicalFk;
using ActoEngine.WebApi.Features.ProjectClients;
using ActoEngine.WebApi.Features.Projects.Dtos.Requests;
using ActoEngine.WebApi.Features.Schema;
using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Dapper;
namespace ActoEngine.WebApi.Features.Projects
{
    public class ProjectSyncService(
        IProjectRepository projectRepository,
        ISchemaRepository schemaRepository,
        IDbConnectionFactory connectionFactory,
        ISchemaService schemaService,
        IClientRepository clientRepository,
        IProjectClientRepository projectClientRepository,
        IDependencyOrchestrationService dependencyOrchestrationService,
        ILogicalFkService logicalFkService,
        ILfkThrottleService lfkThrottleService,
        ILogger<ProjectSyncService> logger,
        IConfiguration configuration,
        IHostEnvironment environment,
        IServiceScopeFactory serviceScopeFactory)
    {
        private readonly IProjectRepository _projectRepository = projectRepository;
        private readonly ISchemaRepository _schemaRepository = schemaRepository;
        private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
        private readonly ISchemaService _schemaService = schemaService;
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly IProjectClientRepository _projectClientRepository = projectClientRepository;
        private readonly IDependencyOrchestrationService _dependencyOrchestrationService = dependencyOrchestrationService;
        private readonly ILogicalFkService _logicalFkService = logicalFkService;
        private readonly ILfkThrottleService _lfkThrottleService = lfkThrottleService;
        private readonly ILogger<ProjectSyncService> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IHostEnvironment _environment = environment;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

        /// <summary>
        /// Verifies a database connection using secure credential handling.
        /// Uses SqlConnectionStringBuilder to avoid string concatenation vulnerabilities
        /// and SqlCredential for credential separation from connection string.
        /// </summary>
        public async Task<bool> VerifyConnectionAsync(VerifyConnectionRequest request)
        {
            try
            {
                // SECURITY: Use SqlConnectionStringBuilder instead of string concatenation
                // to prevent injection vulnerabilities and plaintext credential exposure
                var builder = SecureConnectionBuilder.BuildConnectionString(
                    request.Server,
                    request.Port,
                    request.DatabaseName,
                    request.Encrypt,
                    request.TrustServerCertificate,
                    request.ConnectionTimeout,
                    request.ApplicationName,
                    _environment);

                // SECURITY: Use SqlCredential to separate credentials from connection string
                // This prevents credentials from being embedded in the connection string
                var credential = SecureConnectionBuilder.CreateCredential(
                    request.Username,
                    request.Password);

                using var conn = new SqlConnection(builder.ConnectionString, credential);
                await conn.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                // SECURITY: Do not log credentials - only log server and database
                _logger.LogWarning(ex, "Failed to verify connection to database {DatabaseName} on server {Server}:{Port}",
                    request.DatabaseName, request.Server, request.Port);
                return false;
            }
        }

        public async Task<ProjectResponse> LinkProjectAsync(LinkProjectRequest request, int userId)
        {
            // Extract database name from connection string
            var builder = new SqlConnectionStringBuilder(request.ConnectionString);
            var databaseName = builder.InitialCatalog;

            var project = new Project
            {
                ProjectId = request.ProjectId,
                ProjectName = databaseName, // Use database name as project name
                Description = string.Empty,
                DatabaseName = databaseName,
                IsActive = true,
                IsLinked = false // Will be set to true after successful sync
            };

            var projectId = await _projectRepository.AddOrUpdateProjectAsync(project, userId);

            // Start background sync using connection string temporarily
            StartBackgroundSync(projectId, request.ConnectionString, userId);

            return new ProjectResponse
            {
                ProjectId = projectId,
                Message = "Project linking started. Schema sync in progress. Connection string will not be stored.",
                SyncJobId = projectId
            };
        }

        public async Task<ProjectResponse> ReSyncProjectAsync(ReSyncProjectRequest request, int userId)
        {
            try
            {
                // Verify project exists
                var project = await _projectRepository.GetByIdAsync(request.ProjectId) ?? throw new InvalidOperationException($"Project with ID {request.ProjectId} not found.");
                _logger.LogInformation("Starting re-sync for project {ProjectId}. Connection string provided temporarily.", request.ProjectId);

                // Start background sync using connection string temporarily
                StartBackgroundSync(request.ProjectId, request.ConnectionString, userId);

                return new ProjectResponse
                {
                    ProjectId = request.ProjectId,
                    Message = "Project re-sync started. Schema update in progress. Connection string will not be stored.",
                    SyncJobId = request.ProjectId
                };
            }
            catch (Exception ex)
            {
                var redactedMessage = SecurityHelper.RedactConnectionString(ex.Message);
                _logger.LogError(ex, "Error starting re-sync for project {ProjectId}. Error: {ErrorMessage}", request.ProjectId, redactedMessage);
                // We re-throw but the controller should also handle/redact if it exposes details.
                // However, ReSyncProjectAsync throws, controller catches InvalidOperationException.
                // If this is a SQL exception with string, it might leak.
                // It's safer to wrap or just let global handler redact (but global handler doesn't know context).
                // User asked to "replace their messages with a redacted summary... before logging or returning".
                throw new InvalidOperationException($"Re-sync failed: {redactedMessage}", ex);
            }
        }

        public async Task<int> ReSyncEntitiesAsync(ResyncEntitiesRequest request, int userId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId) 
                    ?? throw new InvalidOperationException($"Project {request.ProjectId} not found.");

                // Keep it synchronous (Tier 1 is designed to map only to specific entity targets)
                using var dbConn = await _connectionFactory.CreateConnectionAsync();
                using var targetConn = new SqlConnection(request.ConnectionString);
                await targetConn.OpenAsync();

                int syncedCount = 0;
                var analyzeAfterCommit = new List<(int SpId, string Definition)>();

                // Grab existing SPs / Tables from our metadata
                var storedTables = await _schemaRepository.GetStoredTablesAsync(request.ProjectId);
                var storedSps = await _schemaService.GetSpHashesAsync(request.ProjectId);
                
                using var transaction = dbConn.BeginTransaction();
                
                try
                {
                    foreach (var entity in request.Entities)
                    {
                        if (entity.EntityType == ResyncEntityType.TABLE)
                        {
                            var table = storedTables.FirstOrDefault(t => t.TableName == entity.EntityName && t.SchemaName == entity.SchemaName);
                            if (table == null) continue; // Skip if table wasn't previously synced

                            // Clear existing columns & constraints manually inside the transaction
                            await dbConn.ExecuteAsync("DELETE FROM ColumnsMetadata WHERE TableId = @TableId", new { table.TableId }, transaction);
                            
                            // 1. Fetch Columns
                            var columns = await ReadColumnsFromTargetAsync(targetConn, table.SchemaName ?? "dbo", table.TableName);
                            await _schemaRepository.SyncColumnsAsync(table.TableId, columns, dbConn, transaction);

                            // 2. Clear FKs involving this table and re-sync
                            await dbConn.ExecuteAsync(
                                "DELETE FROM ForeignKeyMetadata WHERE TableId = @TableId OR ReferencedTableId = @TableId", 
                                new { table.TableId }, transaction);
                                
                            var foreignKeys = await _schemaService.GetForeignKeysAsync(request.ConnectionString, [(table.SchemaName ?? "dbo", table.TableName)]);
                            await _schemaRepository.SyncForeignKeysAsync(request.ProjectId, foreignKeys, dbConn, transaction);

                            syncedCount++;
                        }
                        else if (entity.EntityType == ResyncEntityType.SP)
                        {
                            var spInfo = storedSps.FirstOrDefault(sp =>
                                sp.ProcedureName.Equals(entity.EntityName, StringComparison.OrdinalIgnoreCase) &&
                                sp.SchemaName.Equals(entity.SchemaName, StringComparison.OrdinalIgnoreCase));
                            if (spInfo == null) continue;

                            // Scoped IA cleanup: Clear impact dependencies for this source SP only
                            await dbConn.ExecuteAsync("DELETE FROM Dependencies WHERE SourceId = @SourceId AND SourceType = 'StoredProcedure'", new { SourceId = spInfo.SpId }, transaction);

                            // Fetch SP definition
                            using var cmd = new SqlCommand(@"
                                SELECT p.modify_date, OBJECT_DEFINITION(p.object_id) AS Definition
                                FROM sys.procedures p
                                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                                WHERE p.name = @SpName AND s.name = @SchemaName", targetConn);
                            cmd.Parameters.AddWithValue("@SchemaName", entity.SchemaName);
                            cmd.Parameters.AddWithValue("@SpName", entity.EntityName);
                            using var reader = await cmd.ExecuteReaderAsync();
                            
                            if (await reader.ReadAsync() && !reader.IsDBNull(1))
                            {
                                var modifyDate = reader.GetDateTime(0);
                                var definition = reader.GetString(1);
                                var hash = _schemaService.NormalizeAndHashDefinition(definition);

                                // Always update to reflect the fresh resync and dependency wipe
                                await _schemaService.UpdateSpDefinitionAndHashAsync(
                                    request.ProjectId,
                                    spInfo.SpId,
                                    definition,
                                    hash,
                                    modifyDate,
                                    dbConn,
                                    transaction);
                                analyzeAfterCommit.Add((spInfo.SpId, definition));
                                syncedCount++;
                            }
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }

                foreach (var item in analyzeAfterCommit)
                {
                    await _dependencyOrchestrationService.AnalyzeStoredProcedureAsync(request.ProjectId, item.SpId, item.Definition);
                }

                // Fire throttled background detection for Logical FKs (does not block this request)
                _lfkThrottleService.TryQueueDetection(request.ProjectId);

                return syncedCount;
            }
            catch (Exception ex)
            {
                var redactedMessage = SecurityHelper.RedactConnectionString(ex.Message);
                _logger.LogError("Error executing targeted ReSyncEntitiesAsync for project {ProjectId}: {Error}", request.ProjectId, redactedMessage);
                throw new InvalidOperationException($"Targeted re-sync failed: {redactedMessage}", ex);
            }
        }

        public async Task<SchemaDiffResponse> GetSchemaDiffAsync(int projectId, string connectionString)
        {
            var diffResponse = new SchemaDiffResponse();
            using var targetConn = new SqlConnection(connectionString);
            await targetConn.OpenAsync();

            // 1. Fetch source items
            var sourceTables = await targetConn.QueryAsync<dynamic>(SchemaSyncQueries.GetTargetTablesWithSchema);
            var sourceSpDates = await _schemaService.GetStoredProcedureModifyDatesAsync(connectionString);
            var sourceSpNames = sourceSpDates
                .Select(x => $"{(string)x.SchemaName}.{(string)x.ProcedureName}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 2. Fetch our stored items (excluding soft-deleted)
            var storedTables = await _schemaRepository.GetStoredTablesAsync(projectId);
            var storedSps = await _schemaService.GetSpHashesAsync(projectId);

            // 3. Diff Tables (Only Add / Remove for now)
            var storedTableKeys = storedTables.Select(t => $"{t.SchemaName}.{t.TableName}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sourceTableKeys = sourceTables.Select(t => $"{t.SchemaName}.{t.TableName}").ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var src in sourceTables)
            {
                if (!storedTableKeys.Contains($"{src.SchemaName}.{src.TableName}"))
                {
                    diffResponse.Tables.Added.Add(new DiffEntityItem { SchemaName = src.SchemaName, EntityName = src.TableName });
                }
            }

            foreach (var st in storedTables)
            {
                if (!sourceTableKeys.Contains($"{st.SchemaName}.{st.TableName}"))
                {
                    diffResponse.Tables.Removed.Add(new DiffEntityItem { SchemaName = st.SchemaName ?? "dbo", EntityName = st.TableName });
                }
            }

            // 4. Diff SPs (Add / Remove / Modify via Timestamp + Hash)
            var storedSpKeys = storedSps
                .Select(sp => $"{sp.SchemaName}.{sp.ProcedureName}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var srcSp in sourceSpDates)
            {
                string spName = (string)srcSp.ProcedureName;
                string spSchema = (string?)srcSp.SchemaName ?? "dbo";
                var spKey = $"{spSchema}.{spName}";
                if (!storedSpKeys.Contains(spKey))
                {
                    diffResponse.StoredProcedures.Added.Add(new DiffEntityItem { SchemaName = spSchema, EntityName = spName });
                }
                else
                {
                    var stSp = storedSps.First(x =>
                        x.ProcedureName.Equals(spName, StringComparison.OrdinalIgnoreCase) &&
                        x.SchemaName.Equals(spSchema, StringComparison.OrdinalIgnoreCase));

                    if (srcSp.SourceModifyDate == null)
                    {
                        continue;
                    }

                    var sourceModifyDate = (DateTime)srcSp.SourceModifyDate;
                    if (!stSp.SourceModifyDate.HasValue || sourceModifyDate > stSp.SourceModifyDate.Value)
                    {
                        // Timestamp is newer -> fetch definition from target and check hash
                        using var cmd = new SqlCommand("SELECT OBJECT_DEFINITION(OBJECT_ID(@SpName))", targetConn);
                        cmd.Parameters.AddWithValue("@SpName", $"[{spSchema}].[{spName}]");
                        var rawDefObj = await cmd.ExecuteScalarAsync();
                        var rawDef = rawDefObj as string;

                        if (!string.IsNullOrEmpty(rawDef))
                        {
                            var newHash = _schemaService.NormalizeAndHashDefinition(rawDef);
                            if (string.IsNullOrWhiteSpace(stSp.DefinitionHash))
                            {
                                await _schemaService.UpdateSpDefinitionAndHashAsync(projectId, stSp.SpId, rawDef, newHash, sourceModifyDate);
                            }
                            else if (newHash != stSp.DefinitionHash)
                            {
                                diffResponse.StoredProcedures.Modified.Add(new DiffEntityItem 
                                { 
                                    SchemaName = spSchema, 
                                    EntityName = spName, 
                                    Reason = "definition_changed" 
                                });
                            }
                            else
                            {
                                // Edge Case: Timestamp changed (e.g., recompilation or trivial change), but actual normalized code didn't.
                                // We silently update the ModifyDate so we don't keep checking it on every diff!
                                await _schemaService.UpdateSpDefinitionAndHashAsync(projectId, stSp.SpId, rawDef, newHash, sourceModifyDate);
                            }
                        }
                    }
                }
            }

            foreach (var stSp in storedSps)
            {
                if (!sourceSpNames.Contains($"{stSp.SchemaName}.{stSp.ProcedureName}"))
                {
                    diffResponse.StoredProcedures.Removed.Add(new DiffEntityItem { SchemaName = stSp.SchemaName, EntityName = stSp.ProcedureName });
                }
            }

            return diffResponse;
        }

        public async Task<int> ApplyDiffAsync(ApplyDiffRequest request, int userId)
        {
            int appliedCount = 0;

            var storedTables = await _schemaRepository.GetStoredTablesAsync(request.ProjectId);
            var storedSps = await _schemaService.GetSpHashesAsync(request.ProjectId);

            var toUpsert = new List<ResyncEntityItem>();
            toUpsert.AddRange(request.AddEntities);
            toUpsert.AddRange(request.UpdateEntities);
            var analyzeAfterCommit = new List<(int SpId, string Definition)>();

            using var dbConn = await _connectionFactory.CreateConnectionAsync();
            using var transaction = dbConn.BeginTransaction();
            try
            {
                // 1. Process Removals (Soft Delete) in the same transaction
                foreach (var removal in request.RemoveEntities)
                {
                    if (removal.EntityType == ResyncEntityType.TABLE)
                    {
                        var targetTable = storedTables.FirstOrDefault(x => x.TableName == removal.EntityName && x.SchemaName == removal.SchemaName);
                        if (targetTable != null)
                        {
                            await dbConn.ExecuteAsync(
                                SchemaSyncQueries.SoftDeleteTable,
                                new { request.ProjectId, targetTable.TableId },
                                transaction);
                            appliedCount++;
                        }
                    }
                    else if (removal.EntityType == ResyncEntityType.SP)
                    {
                        var targetSp = storedSps.FirstOrDefault(x => x.ProcedureName == removal.EntityName && x.SchemaName == removal.SchemaName);
                        if (targetSp != null)
                        {
                            await dbConn.ExecuteAsync(
                                SchemaSyncQueries.SoftDeleteSp,
                                new { request.ProjectId, targetSp.SpId },
                                transaction);
                            appliedCount++;
                        }
                    }
                }

                // 2. Process Additions and Updates
                if (toUpsert.Count > 0)
                {
                    using var targetConn = new SqlConnection(request.ConnectionString);
                    await targetConn.OpenAsync();

                    foreach (var entity in toUpsert)
                    {
                        if (entity.EntityType == ResyncEntityType.TABLE)
                        {
                            var tInfo = storedTables.FirstOrDefault(t => t.TableName == entity.EntityName && t.SchemaName == entity.SchemaName);
                            int tableId;
                            if (tInfo == null)
                            {
                                tableId = await dbConn.ExecuteScalarAsync<int>(
                                    "INSERT INTO TablesMetadata (ProjectId, TableName, SchemaName) OUTPUT INSERTED.TableId VALUES (@ProjectId, @TableName, @SchemaName)",
                                    new { request.ProjectId, entity.EntityName, entity.SchemaName }, transaction);
                            }
                            else
                            {
                                tableId = tInfo.TableId;
                                await dbConn.ExecuteAsync("DELETE FROM ColumnsMetadata WHERE TableId = @TableId", new { tableId }, transaction);
                            }

                            var columns = await ReadColumnsFromTargetAsync(targetConn, entity.SchemaName, entity.EntityName);
                            await _schemaRepository.SyncColumnsAsync(tableId, columns, dbConn, transaction);

                            await dbConn.ExecuteAsync(
                                "DELETE FROM ForeignKeyMetadata WHERE TableId = @TableId OR ReferencedTableId = @TableId",
                                new { tableId }, transaction);

                            var foreignKeys = await _schemaService.GetForeignKeysAsync(request.ConnectionString, [(entity.SchemaName, entity.EntityName)]);
                            await _schemaRepository.SyncForeignKeysAsync(request.ProjectId, foreignKeys, dbConn, transaction);
                            appliedCount++;
                        }
                        else if (entity.EntityType == ResyncEntityType.SP)
                        {
                            var spInfo = storedSps.FirstOrDefault(sp => sp.ProcedureName == entity.EntityName && sp.SchemaName == entity.SchemaName);

                            using var cmd = new SqlCommand(@"
                                SELECT p.modify_date, OBJECT_DEFINITION(p.object_id) AS Definition
                                FROM sys.procedures p
                                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                                WHERE p.name = @SpName AND s.name = @SchemaName", targetConn);
                            cmd.Parameters.AddWithValue("@SchemaName", entity.SchemaName);
                            cmd.Parameters.AddWithValue("@SpName", entity.EntityName);
                            using var reader = await cmd.ExecuteReaderAsync();

                            if (await reader.ReadAsync() && !reader.IsDBNull(1))
                            {
                                var modifyDate = reader.GetDateTime(0);
                                var definition = reader.GetString(1);
                                var hash = _schemaService.NormalizeAndHashDefinition(definition);

                                if (spInfo == null)
                                {
                                    var insertedSpId = await dbConn.ExecuteScalarAsync<int>(
                                        "INSERT INTO SpMetadata (ProjectId, ProcedureName, SchemaName, Definition, CreatedDate, DefinitionHash, SourceModifyDate) " +
                                        "OUTPUT INSERTED.SpId VALUES (@ProjectId, @ProcedureName, @SchemaName, @Definition, GETUTCDATE(), @DefinitionHash, @SourceModifyDate)",
                                        new { request.ProjectId, ProcedureName = entity.EntityName, entity.SchemaName, Definition = definition, DefinitionHash = hash, SourceModifyDate = modifyDate },
                                        transaction);

                                    analyzeAfterCommit.Add((insertedSpId, definition));
                                }
                                else
                                {
                                    await dbConn.ExecuteAsync("DELETE FROM Dependencies WHERE SourceId = @SourceId AND SourceType = 'StoredProcedure'", new { SourceId = spInfo.SpId }, transaction);
                                    await _schemaService.UpdateSpDefinitionAndHashAsync(
                                        request.ProjectId,
                                        spInfo.SpId,
                                        definition,
                                        hash,
                                        modifyDate,
                                        dbConn,
                                        transaction);
                                    analyzeAfterCommit.Add((spInfo.SpId, definition));
                                }

                                appliedCount++;
                            }
                        }
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            foreach (var item in analyzeAfterCommit)
            {
                await _dependencyOrchestrationService.AnalyzeStoredProcedureAsync(request.ProjectId, item.SpId, item.Definition);
            }

            _lfkThrottleService.TryQueueDetection(request.ProjectId);

            return appliedCount;
        }

        public async Task<SyncStatus?> GetSyncStatusAsync(int projectId)
        {
            return await _projectRepository.GetSyncStatusAsync(projectId);
        }

        private void StartBackgroundSync(int projectId, string targetConnectionString, int userId)
        {
            var scopeFactory = _serviceScopeFactory;
            var logger = _logger;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var scopedSyncService = ActivatorUtilities.CreateInstance<ProjectSyncService>(scope.ServiceProvider);
                    await scopedSyncService.SyncSchemaWithProgressAsync(projectId, targetConnectionString, userId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start background schema sync for project {ProjectId}", projectId);
                }
            });
        }

        private async Task SyncSchemaWithProgressAsync(int projectId, string targetConnectionString, int userId)
        {
            try
            {
                await _projectRepository.UpdateSyncStatusAsync(projectId, "Started", 0);

                var isSameServer = await IsSameServerAsync(targetConnectionString);

                using var dbConn = await _connectionFactory.CreateConnectionAsync();
                using var transaction = dbConn.BeginTransaction();

                try
                {
                    if (isSameServer)
                    {
                        _logger.LogInformation("Target database is on the same server. Using same-server sync for project {ProjectId}", projectId);
                        await SyncViaSameServerAsync(projectId, targetConnectionString, userId, dbConn, transaction);
                    }
                    else
                    {
                        _logger.LogInformation("Target database is on a different server. Using cross-server sync for project {ProjectId}", projectId);
                        await SyncViaCrossServerAsync(projectId, targetConnectionString, userId, dbConn, transaction);
                    }

                    transaction.Commit();

                    // Trigger Dependency Analysis (Fault-tolerant integration)
                    // Analysis failures do not fail the schema sync
                    try
                    {
                        await _projectRepository.UpdateSyncStatusAsync(projectId, "Analyzing Dependencies...", 95);
                        await _dependencyOrchestrationService.AnalyzeProjectAsync(projectId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Dependency analysis failed for project {ProjectId}. Schema sync remains successful.", projectId);
                        // Do not re-throw, allow sync to complete
                    }

                    // Trigger Logical FK Detection (Fault-tolerant)
                    // Detection failures do not fail the schema sync
                    await SyncHelpers.PerformFaultTolerantLogicalFkDetectionAsync(
                        projectId, _projectRepository, _logicalFkService, _logger);

                    await _projectRepository.UpdateSyncStatusAsync(projectId, "Completed", 100);

                    // Set IsLinked to true after successful sync
                    await _projectRepository.UpdateIsLinkedAsync(projectId, true);

                    _logger.LogInformation("Schema sync completed successfully for project {ProjectId}. Project is now linked.", projectId);
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                var redactedMessage = SecurityHelper.RedactConnectionString(ex.Message);
                _logger.LogError(ex, "Schema sync failed for project {ProjectId}", projectId);
                await _projectRepository.UpdateSyncStatusAsync(projectId, $"Failed: {redactedMessage}", -1);
            }
        }

        /// <summary>
        /// Synchronizes tables, columns, foreign keys, and stored procedures from a target database into the project's metadata and updates sync progress.
        /// </summary>
        /// <param name="projectId">The identifier of the project to synchronize.</param>
        /// <param name="targetConnectionString">Connection string for the target database to read schema from.</param>
        /// <param name="userId">Identifier of the user initiating the sync; used when creating or linking the global default client and recording ownership.</param>
        /// <param name="dbConn">Active connection to the Actox metadata database where schema changes are persisted.</param>
        /// <param name="transaction">Database transaction on <paramref name="dbConn"/> used to group persisted changes.</param>
        /// <exception cref="InvalidOperationException">Thrown if creation of the global "Default Client" succeeds but the created client cannot be retrieved.</exception>
        private async Task SyncViaCrossServerAsync(
            int projectId,
            string targetConnectionString,
            int userId,
            IDbConnection dbConn,
            IDbTransaction transaction)
        {
            using var targetConn = new SqlConnection(targetConnectionString);
            _logger.LogInformation("Opening connection to target database for project {ProjectId}", projectId);
            await targetConn.OpenAsync();

            // Step 1: Sync Tables
            await UpdateSyncProgress(dbConn, transaction, projectId, "Syncing tables...", 10);
            var tablesWithSchema = await _schemaService.GetAllTablesWithSchemaAsync(targetConnectionString);
            var tableCount = await _schemaRepository.SyncTablesAsync(projectId, tablesWithSchema, dbConn, transaction);
            await UpdateSyncProgress(dbConn, transaction, projectId, $"Synced {tableCount} tables", 33);

            // Step 2: Sync Columns
            await UpdateSyncProgress(dbConn, transaction, projectId, "Syncing columns...", 40);
            var columnCount = await SyncColumnsForAllTablesAsync(projectId, tablesWithSchema, targetConn, dbConn, transaction);
            await UpdateSyncProgress(dbConn, transaction, projectId, $"Synced {columnCount} columns", 66);

            // Step 3: Sync Foreign Keys
            await UpdateSyncProgress(dbConn, transaction, projectId, "Syncing foreign keys...", 67);
            var tables = tablesWithSchema.Select(t => $"{t.SchemaName}.{t.TableName}");
            var foreignKeys = await _schemaService.GetForeignKeysAsync(targetConnectionString, tables);
            var fkCount = await _schemaRepository.SyncForeignKeysAsync(projectId, foreignKeys, dbConn, transaction);
            await UpdateSyncProgress(dbConn, transaction, projectId, $"Synced {fkCount} foreign keys", 70);

            // Step 4: Sync SPs
            await UpdateSyncProgress(dbConn, transaction, projectId, "Syncing stored procedures...", 89);
            var procedures = await _schemaService.GetStoredProceduresAsync(targetConnectionString);

            // Get or create global "Default Client"
            var defaultClient = await _clientRepository.GetByNameAsync("Default Client");
            if (defaultClient == null)
            {
                _logger.LogInformation("Global 'Default Client' not found, creating it now");
                var newClient = new Client
                {
                    ClientName = "Default Client",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };
                var clientId = await _clientRepository.CreateAsync(newClient);
                defaultClient = await _clientRepository.GetByIdAsync(clientId) ?? throw new InvalidOperationException("Failed to create Default Client");
            }

            // Ensure "Default Client" is linked to this project
            var isLinked = await _projectClientRepository.IsLinkedAsync(projectId, transaction, defaultClient.ClientId);
            if (!isLinked)
            {
                _logger.LogInformation("Linking 'Default Client' (ID: {ClientId}) to project {ProjectId}", defaultClient.ClientId, projectId);
                await _projectClientRepository.LinkAsync(projectId, transaction, defaultClient.ClientId, userId);
            }

            var spCount = await _schemaRepository.SyncStoredProceduresAsync(projectId, defaultClient.ClientId, procedures, userId, dbConn, transaction);
            await UpdateSyncProgress(dbConn, transaction, projectId, $"Synced {spCount} procedures", 100);
        }

        /// <summary>
        /// Synchronizes column metadata for the provided tables that exist in the project and reports how many columns were synchronized.
        /// </summary>
        /// <param name="projectId">The project identifier whose table mappings are used to locate target table IDs.</param>
        /// <param name="tablesWithSchema">A sequence of tuples containing table names and schema names from the target database; only tables that match the project's tables are processed.</param>
        /// <param name="targetConn">An open SQL connection to the target database to read column metadata from.</param>
        /// <param name="dbConn">A database connection to the application's metadata store used for persisting synced columns.</param>
        /// <param name="transaction">The transaction context to use when writing metadata to the application's metadata store.</param>
        /// <returns>The total number of columns that were synchronized for the project.</returns>
        private async Task<int> SyncColumnsForAllTablesAsync(
            int projectId,
            IEnumerable<(string TableName, string SchemaName)> tablesWithSchema,
            SqlConnection targetConn,
            IDbConnection dbConn,
            IDbTransaction transaction)
        {
            var totalColumns = 0;

            // Fetch all table IDs in a single query
            var tables = await _schemaRepository.GetProjectTablesAsync(projectId, dbConn, transaction);
            var tableIdMap = tables.ToDictionary(
                t => $"{t.SchemaName}.{t.TableName}",
                t => t.TableId,
                StringComparer.OrdinalIgnoreCase);

            // Use the table names we already have
            foreach (var (tableName, schemaName) in tablesWithSchema)
            {
                if (tableIdMap.TryGetValue($"{schemaName}.{tableName}", out var tableId))
                {
                    var columns = await ReadColumnsFromTargetAsync(targetConn, schemaName, tableName);
                    var count = await _schemaRepository.SyncColumnsAsync(tableId, columns, dbConn, transaction);
                    totalColumns += count;
                }
            }

            return totalColumns;
        }

        private static async Task<IEnumerable<ColumnMetadata>> ReadColumnsFromTargetAsync(SqlConnection targetConn, string schemaName, string tableName)
        {
            using var cmd = new SqlCommand(SchemaSyncQueries.GetTargetColumns, targetConn);
            cmd.Parameters.AddWithValue("@SchemaName", schemaName);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            using var reader = await cmd.ExecuteReaderAsync();

            var columns = new List<ColumnMetadata>();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnMetadata
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.GetInt16(2),
                    Precision = reader.GetByte(3),
                    Scale = reader.GetByte(4),
                    IsNullable = reader.GetBoolean(5),
                    IsPrimaryKey = reader.GetBoolean(6),
                    IsForeignKey = reader.GetBoolean(7),
                    ColumnOrder = reader.GetInt32(8)
                });
            }
            return columns;
        }

        private static async Task SyncViaSameServerAsync(
            int projectId,
            string targetConnectionString,
            int userId,
            IDbConnection dbConn,
            IDbTransaction transaction)
        {
            var builder = new SqlConnectionStringBuilder(targetConnectionString);
            var databaseName = builder.InitialCatalog;

            using var cmd = dbConn.CreateCommand() as SqlCommand ?? throw new InvalidOperationException("Failed to create SqlCommand.");
            cmd.Transaction = transaction as SqlTransaction;
            if (cmd.Transaction == null)
            {
                throw new InvalidOperationException("Transaction is not a SqlTransaction or is null.");
            }

            cmd.CommandText = "SyncSchemaMetadata";
            cmd.CommandType = CommandType.StoredProcedure;

            var projectIdParam = cmd.CreateParameter();
            projectIdParam.ParameterName = "@ProjectId";
            projectIdParam.Value = projectId;
            cmd.Parameters.Add(projectIdParam);

            var dbNameParam = cmd.CreateParameter();
            dbNameParam.ParameterName = "@DatabaseName";
            dbNameParam.Value = databaseName;
            cmd.Parameters.Add(dbNameParam);

            var userIdParam = cmd.CreateParameter();
            userIdParam.ParameterName = "@UserId";
            userIdParam.Value = userId;
            cmd.Parameters.Add(userIdParam);

            await cmd.ExecuteNonQueryAsync();
        }
        private async Task<bool> IsSameServerAsync(string targetConnectionString)
        {
            var dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(dbConnectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            }

            using var targetConn = new SqlConnection(targetConnectionString);
            using var dbConn = new SqlConnection(dbConnectionString);

            await targetConn.OpenAsync();
            await dbConn.OpenAsync();

            using var targetCmd = new SqlCommand(SchemaSyncQueries.GetServerName, targetConn);
            using var actoxCmd = new SqlCommand(SchemaSyncQueries.GetServerName, dbConn);

            var targetServer = await targetCmd.ExecuteScalarAsync() as string;
            var actoxServer = await actoxCmd.ExecuteScalarAsync() as string;

            if (string.IsNullOrEmpty(targetServer) || string.IsNullOrEmpty(actoxServer))
            {
                return false;
            }

            return targetServer.Equals(actoxServer, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task UpdateSyncProgress(
            IDbConnection connection,
            IDbTransaction transaction,
            int projectId,
            string status,
            int progress)
        {
            using var cmd = connection.CreateCommand() as SqlCommand ?? throw new InvalidOperationException("Failed to create SqlCommand.");
            cmd.Transaction = transaction as SqlTransaction;
            if (cmd.Transaction == null)
            {
                throw new InvalidOperationException("Transaction is not a SqlTransaction or is null.");
            }
            cmd.CommandText = SchemaSyncQueries.UpdateSyncStatus;

            var projectIdParam = cmd.CreateParameter();
            projectIdParam.ParameterName = "@ProjectId";
            projectIdParam.Value = projectId;
            cmd.Parameters.Add(projectIdParam);

            var statusParam = cmd.CreateParameter();
            statusParam.ParameterName = "@Status";
            statusParam.Value = status;
            cmd.Parameters.Add(statusParam);

            var progressParam = cmd.CreateParameter();
            progressParam.ParameterName = "@Progress";
            progressParam.Value = progress;
            cmd.Parameters.Add(progressParam);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}

