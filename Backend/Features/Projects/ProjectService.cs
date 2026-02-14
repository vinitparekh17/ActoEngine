using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Features.Schema;
using Microsoft.Data.SqlClient;
using System.Data;
using ActoEngine.WebApi.Infrastructure.Security;
using ActoEngine.WebApi.Features.Clients;
using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.Projects.Dtos.Requests;
using ActoEngine.WebApi.Features.Projects.Dtos.Responses;
using ActoEngine.WebApi.Features.ProjectClients;
using System.Threading;
using System.Collections.Generic;
namespace ActoEngine.WebApi.Features.Projects
{
    public interface IProjectService
    {
        Task<SyncStatus?> GetSyncStatusAsync(int projectId);
        Task<ConnectionResponse> VerifyConnectionAsync(VerifyConnectionRequest request);
        Task<ProjectResponse> LinkProjectAsync(LinkProjectRequest request, int userId);
        Task<ProjectResponse> ReSyncProjectAsync(ReSyncProjectRequest request, int userId);
        Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, int userId);
        Task<PublicProjectDto?> GetProjectByIdAsync(int projectId);
        Task<IEnumerable<PublicProjectDto>> GetAllProjectsAsync();
        Task<bool> UpdateProjectAsync(int projectId, Project project, int userId);
        Task<bool> DeleteProjectAsync(int projectId, int userId);
        Task<ProjectStatsResponse?> GetProjectStatsAsync(int projectId);
        Task<IEnumerable<ProjectMemberDto>> GetProjectMembersAsync(int projectId, CancellationToken cancellationToken = default);
        Task<IEnumerable<int>> GetUserProjectMembershipsAsync(int userId, CancellationToken cancellationToken = default);
        Task AddProjectMemberAsync(int projectId, int userId, int addedBy, CancellationToken cancellationToken = default);
        Task RemoveProjectMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    }

    public class ProjectService(
        IProjectRepository projectRepository,
        ISchemaRepository schemaRepository,
        IDbConnectionFactory connectionFactory,
        ISchemaService schemaService,
        IClientRepository clientRepository,
        IProjectClientRepository projectClientRepository,
        IDependencyOrchestrationService dependencyOrchestrationService,
        ILogger<ProjectService> logger,
        IConfiguration configuration,
        IHostEnvironment environment) : IProjectService
    {
        private readonly IProjectRepository _projectRepository = projectRepository;
        private readonly ISchemaRepository _schemaRepository = schemaRepository;
        private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
        private readonly ISchemaService _schemaService = schemaService;
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly IProjectClientRepository _projectClientRepository = projectClientRepository;
        private readonly IDependencyOrchestrationService _dependencyOrchestrationService = dependencyOrchestrationService;
        private readonly ILogger<ProjectService> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IHostEnvironment _environment = environment;

        /// <summary>
        /// Verifies a database connection using secure credential handling.
        /// Uses SqlConnectionStringBuilder to avoid string concatenation vulnerabilities
        /// and SqlCredential for credential separation from connection string.
        /// Returns detailed connection result with server version or actionable error information.
        /// </summary>
        public async Task<ConnectionResponse> VerifyConnectionAsync(VerifyConnectionRequest request)
        {
            var response = new ConnectionResponse { TestedAt = DateTime.UtcNow };

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

                // Get server version for compatibility info
                response.ServerVersion = conn.ServerVersion;
                response.IsValid = true;
                response.Message = "Connection successful";

                return response;
            }
            catch (SqlException ex)
            {
                // SECURITY: Do not log credentials - only log server and database
                _logger.LogWarning(ex, "Failed to verify connection to database {DatabaseName} on server {Server}:{Port}. SQL Error: {ErrorNumber}",
                    request.DatabaseName, request.Server, request.Port, ex.Number);

                return MapSqlExceptionToResponse(ex, response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to verify connection to database {DatabaseName} on server {Server}:{Port}",
                    request.DatabaseName, request.Server, request.Port);

                response.IsValid = false;
                response.Message = "Connection failed. Please check your connection details.";
                response.ErrorCode = "UNKNOWN_ERROR";
                response.Errors.Add(ex.Message);

                return response;
            }
        }

        /// <summary>
        /// Maps SQL exceptions to user-friendly error codes and messages.
        /// </summary>
        private static ConnectionResponse MapSqlExceptionToResponse(SqlException ex, ConnectionResponse response)
        {
            response.IsValid = false;

            // Map SQL error numbers to user-friendly messages
            // Reference: https://docs.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors
            switch (ex.Number)
            {
                // Login failed errors
                case 18456:
                    response.Message = "Authentication failed. Please check your username and password.";
                    response.ErrorCode = "AUTH_FAILED";
                    response.Errors.Add("Login failed for the specified user.");
                    break;

                // Network/connectivity errors
                case -1:
                case 53:
                case 10060:
                    response.Message = "Cannot reach server. Please verify the server address and port are correct.";
                    response.ErrorCode = "NETWORK_UNREACHABLE";
                    response.Errors.Add("A network-related or instance-specific error occurred.");
                    break;

                // SQL Server not found / named instance issues
                case -2:
                case 2:
                    response.Message = "Server not found or connection timed out. Verify the server name and ensure SQL Server is running.";
                    response.ErrorCode = "SERVER_NOT_FOUND";
                    response.Errors.Add("Timeout or server instance not found.");
                    break;

                // Database does not exist
                case 4060:
                    response.Message = "Database not found. Please verify the database name.";
                    response.ErrorCode = "DATABASE_NOT_FOUND";
                    response.Errors.Add($"Cannot open database. The database may not exist.");
                    break;

                // Access denied / permission issues
                case 916:
                case 229:
                    response.Message = "Access denied. The user does not have permission to access this database.";
                    response.ErrorCode = "ACCESS_DENIED";
                    response.Errors.Add("Insufficient permissions to access the database.");
                    break;

                // TLS/SSL handshake errors - common with SQL Server 2012 without TLS 1.2 patches
                case 20:
                case 10054:
                    response.Message = "Connection security error. If using SQL Server 2012, ensure TLS 1.2 updates are installed.";
                    response.ErrorCode = "TLS_HANDSHAKE_FAILED";
                    response.HelpLink = "https://support.microsoft.com/en-us/topic/kb3135244-tls-1-2-support-for-microsoft-sql-server-e4472ef8-90a9-13c1-e4d8-44aad198cdbe";
                    response.Errors.Add("SSL/TLS handshake failed. SQL Server 2012 requires SP4 + cumulative update for TLS 1.2.");
                    break;

                // Encryption-related errors
                case 10:
                case 42:
                    response.Message = "Encryption configuration error. Check if SQL Server is configured for SSL/TLS.";
                    response.ErrorCode = "ENCRYPTION_ERROR";
                    response.Errors.Add("Connection encryption negotiation failed.");
                    break;

                default:
                    response.Message = $"Connection failed (Error {ex.Number}). Please check your connection details.";
                    response.ErrorCode = "SQL_ERROR";
                    response.Errors.Add(ex.Message);
                    break;
            }

            return response;
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
            _ = Task.Run(async () => await SyncSchemaWithProgressAsync(projectId, request.ConnectionString, userId));

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
                _ = Task.Run(async () => await SyncSchemaWithProgressAsync(request.ProjectId, request.ConnectionString, userId));

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

                    await _projectRepository.UpdateSyncStatusAsync(projectId, "Completed", 100);

                    // Set IsLinked to true after successful sync
                    await _projectRepository.UpdateIsLinkedAsync(projectId, true);

                    _logger.LogInformation("Schema sync completed successfully for project {ProjectId}. Project is now linked.", projectId);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Schema sync failed for project {ProjectId}", projectId);
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
            // Same-server sync uses 3-part naming (DbName.schema.table), 
            // so we don't need the connection string - the SP gets DatabaseName from Projects table
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

            var userIdParam = cmd.CreateParameter();
            userIdParam.ParameterName = "@UserId";
            userIdParam.Value = userId;
            cmd.Parameters.Add(userIdParam);

            await cmd.ExecuteNonQueryAsync();
        }
        private async Task<bool> IsSameServerAsync(string targetConnectionString)
        {
            var dbConnectionString = _configuration.GetConnectionString("DefaultConnection");

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

        // private string BuildConnectionString(VerifyConnectionRequest request)
        // {
        //     return $"Server={request.Server};Database={request.DatabaseName};User Id={request.Username};Password={request.Password};TrustServerCertificate=True";
        // }

        public async Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, int userId)
        {
            try
            {
                var project = new Project(
                    request.ProjectName,
                    request.DatabaseName,
                    DateTime.UtcNow,
                    userId,
                    request.Description,
                    request.DatabaseType)
                {
                    IsLinked = false // New projects start unlinked
                };

                var projectId = await _projectRepository.CreateProjectWithOwnerAsync(project);

                _logger.LogInformation("Created new project {ProjectName} with ID {ProjectId} for user {UserId}. Project not linked to database yet. Owner added atomically.", request.ProjectName, projectId, userId);
                return new ProjectResponse { ProjectId = projectId, Message = "Project created successfully. Use Link endpoint to connect to a database." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project {ProjectName} for user {UserId}", request.ProjectName, userId);
                throw;
            }
        }

        public async Task<PublicProjectDto?> GetProjectByIdAsync(int projectId)
        {
            try
            {
                return await _projectRepository.GetByIdAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<IEnumerable<PublicProjectDto>> GetAllProjectsAsync()
        {
            try
            {
                return await _projectRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                throw;
            }
        }

        public async Task<bool> UpdateProjectAsync(int projectId, Project project, int userId)
        {
            try
            {
                project.ProjectId = projectId;
                project.UpdatedAt = DateTime.UtcNow;
                project.UpdatedBy = userId;
                return await _projectRepository.UpdateAsync(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {ProjectId} for user {UserId}", projectId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteProjectAsync(int projectId, int userId)
        {
            try
            {
                return await _projectRepository.DeleteAsync(projectId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId} for user {UserId}", projectId, userId);
                throw;
            }
        }

        public async Task<ProjectStatsResponse?> GetProjectStatsAsync(int projectId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    return null;
                }

                return await _projectRepository.GetProjectStatsAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stats for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectMemberDto>> GetProjectMembersAsync(int projectId, CancellationToken cancellationToken = default)
        {
            return await _projectRepository.GetProjectMembersAsync(projectId, cancellationToken);
        }

        public async Task<IEnumerable<int>> GetUserProjectMembershipsAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _projectRepository.GetUserProjectMembershipsAsync(userId, cancellationToken);
        }

        public async Task AddProjectMemberAsync(int projectId, int userId, int addedBy, CancellationToken cancellationToken = default)
        {
            await _projectRepository.AddProjectMemberAsync(projectId, userId, addedBy, cancellationToken);
            _logger.LogInformation("Added user {UserId} to project {ProjectId} by admin {AdminId}", userId, projectId, addedBy);
        }

        public async Task RemoveProjectMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
        {
            await _projectRepository.RemoveProjectMemberAsync(projectId, userId, cancellationToken);
            _logger.LogInformation("Removed user {UserId} from project {ProjectId}", userId, projectId);
        }
    }
}