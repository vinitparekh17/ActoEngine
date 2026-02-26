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
namespace ActoEngine.WebApi.Features.Projects
{
    public class ProjectSyncService(
        ProjectRepository projectRepository,
        SchemaRepository schemaRepository,
        IDbConnectionFactory connectionFactory,
        SchemaService schemaService,
        ClientRepository clientRepository,
        ProjectClientRepository projectClientRepository,
        DependencyOrchestrationService dependencyOrchestrationService,
        ILogicalFkService logicalFkService,
        ILogger<ProjectSyncService> logger,
        IConfiguration configuration,
        IHostEnvironment environment,
        IServiceScopeFactory serviceScopeFactory)
    {
        private readonly ProjectRepository _projectRepository = projectRepository;
        private readonly SchemaRepository _schemaRepository = schemaRepository;
        private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
        private readonly SchemaService _schemaService = schemaService;
        private readonly ClientRepository _clientRepository = clientRepository;
        private readonly ProjectClientRepository _projectClientRepository = projectClientRepository;
        private readonly DependencyOrchestrationService _dependencyOrchestrationService = dependencyOrchestrationService;
        private readonly ILogicalFkService _logicalFkService = logicalFkService;
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
            var tables = tablesWithSchema.Select(t => t.TableName);
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
            var tableIdMap = tables.ToDictionary(t => t.TableName, t => t.TableId);

            // Use the table names we already have
            foreach (var (tableName, _) in tablesWithSchema)
            {
                if (tableIdMap.TryGetValue(tableName, out var tableId))
                {
                    var columns = await ReadColumnsFromTargetAsync(targetConn, tableName);
                    var count = await _schemaRepository.SyncColumnsAsync(tableId, columns, dbConn, transaction);
                    totalColumns += count;
                }
            }

            return totalColumns;
        }

        private static async Task<IEnumerable<ColumnMetadata>> ReadColumnsFromTargetAsync(SqlConnection targetConn, string tableName)
        {
            using var cmd = new SqlCommand(SchemaSyncQueries.GetTargetColumns, targetConn);
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

