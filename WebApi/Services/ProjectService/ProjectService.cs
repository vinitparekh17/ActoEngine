using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Sql.Queries;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ActoEngine.WebApi.Services.ProjectService
{
    public interface IProjectService
    {
        Task<SyncStatus?> GetSyncStatusAsync(int projectId);
        Task<bool> VerifyConnectionAsync(VerifyConnectionRequest request);
        Task<ProjectResponse> LinkProjectAsync(LinkProjectRequest request, int userId);
        Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, int userId);
        Task<Project?> GetProjectByIdAsync(int projectId, int userId);
        Task<IEnumerable<Project>> GetAllProjectsAsync(int userId, int offset = 0, int limit = 50);
        Task<bool> UpdateProjectAsync(int projectId, Project project, int userId);
        Task<bool> DeleteProjectAsync(int projectId, int userId);
    }

    public class ProjectService(IProjectRepository projectRepository, ISchemaSyncRepository schemaSyncRepository,
    IDbConnectionFactory connectionFactory, ILogger<ProjectService> logger, IConfiguration configuration) : IProjectService
    {
        private readonly IProjectRepository _projectRepository = projectRepository;
        private readonly ISchemaSyncRepository _schemaSyncRepository = schemaSyncRepository;
        private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
        private readonly ILogger<ProjectService> _logger = logger;
        private readonly IConfiguration _configuration = configuration;

        public async Task<bool> VerifyConnectionAsync(VerifyConnectionRequest request)
        {
            var connectionString = $"Server={request.Server},{request.Port};Database={request.DatabaseName};User Id={request.Username};Password={request.Password};TrustServerCertificate=True";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    _logger.LogInformation("Successfully verified connection to database {DatabaseName} on server {Server}", request.DatabaseName, request.Server);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to verify connection to database {DatabaseName} on server {Server}", request.DatabaseName, request.Server);
                return false;
            }
        }

        public async Task<ProjectResponse> LinkProjectAsync(LinkProjectRequest request, int userId)
        {
            var project = new Project
            {
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                Description = request.Description,
                DatabaseName = request.DatabaseName,
                ConnectionString = request.ConnectionString,
                ClientId = request.ClientId,
                IsActive = true
            };

            var projectId = await _projectRepository.AddOrUpdateProjectAsync(project, userId);

            // Start background sync
            _ = Task.Run(async () => await SyncSchemaWithProgressAsync(projectId, request.ClientId, request.ConnectionString, userId));

            return new ProjectResponse
            {
                ProjectId = projectId,
                Message = "Project linking started. Schema sync in progress.",
                SyncJobId = projectId
            };
        }

        public async Task<SyncStatus?> GetSyncStatusAsync(int projectId)
        {
            return await _projectRepository.GetSyncStatusAsync(projectId);
        }

        private async Task SyncSchemaWithProgressAsync(int projectId, int clientId, string targetConnectionString, int userId)
        {
            try
            {
                await _projectRepository.UpdateSyncStatusAsync(projectId, "Started", 0);

                var isSameServer = await IsSameServerAsync(targetConnectionString);

                using var actoxConn = await _connectionFactory.CreateConnectionAsync();
                using var transaction = actoxConn.BeginTransaction();

                try
                {
                    if (isSameServer)
                    {
                        _logger.LogInformation("Target database is on the same server. Using same-server sync for project {ProjectId}", projectId);
                        await SyncViaSameServerAsync(projectId, targetConnectionString, userId, actoxConn, transaction);
                    }
                    else
                    {
                        _logger.LogInformation("Target database is on a different server. Using cross-server sync for project {ProjectId}", projectId);
                        await SyncViaCrossServerAsync(projectId, clientId, targetConnectionString, userId, actoxConn, transaction);
                    }

                    transaction.Commit();
                    await _projectRepository.UpdateSyncStatusAsync(projectId, "Completed", 100);

                    _logger.LogInformation("Schema sync completed successfully for project {ProjectId}", projectId);
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
                await _projectRepository.UpdateSyncStatusAsync(projectId, $"Failed: {ex.Message}", -1);
            }
        }

        private async Task SyncViaCrossServerAsync(
            int projectId,
            int clientId,
            string targetConnectionString,
            int userId,
            IDbConnection actoxConn,
            IDbTransaction transaction)
        {
            using var targetConn = new SqlConnection(targetConnectionString);
            _logger.LogInformation("Opening connection to target database for project {ProjectId}", projectId);
            await targetConn.OpenAsync();

            // Step 1: Sync Tables
            await UpdateSyncProgress(actoxConn, transaction, projectId, "Syncing tables...", 10);
            var tables = await ReadTablesFromTargetAsync(targetConn);
            var tableCount = await _schemaSyncRepository.SyncTablesAsync(projectId, tables, actoxConn, transaction);
            await UpdateSyncProgress(actoxConn, transaction, projectId, $"Synced {tableCount} tables", 33);

            // Step 2: Sync Columns
            await UpdateSyncProgress(actoxConn, transaction, projectId, "Syncing columns...", 40);
            var columnCount = await SyncColumnsForAllTablesAsync(projectId, targetConn, actoxConn, transaction);
            await UpdateSyncProgress(actoxConn, transaction, projectId, $"Synced {columnCount} columns", 66);

            // Step 3: Sync SPs
            await UpdateSyncProgress(actoxConn, transaction, projectId, "Syncing stored procedures...", 70);
            var procedures = await ReadStoredProceduresFromTargetAsync(targetConn);
            var spCount = await _schemaSyncRepository.SyncStoredProceduresAsync(projectId, clientId, procedures, userId, actoxConn, transaction);
            await UpdateSyncProgress(actoxConn, transaction, projectId, $"Synced {spCount} procedures", 100);
        }

        private static async Task<IEnumerable<string>> ReadTablesFromTargetAsync(SqlConnection targetConn)
        {
            using var cmd = new SqlCommand(SchemaSyncQueries.GetTargetTables, targetConn);
            using var reader = await cmd.ExecuteReaderAsync();

            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }

        private async Task<int> SyncColumnsForAllTablesAsync(
            int projectId,
            SqlConnection targetConn,
            IDbConnection actoxConn,
            IDbTransaction transaction)
        {
            var totalColumns = 0;
            var tables = await _schemaSyncRepository.GetProjectTablesAsync(projectId, actoxConn, transaction);

            foreach (var (tableId, tableName) in tables)
            {
                var columns = await ReadColumnsFromTargetAsync(targetConn, tableName);
                var count = await _schemaSyncRepository.SyncColumnsAsync(tableId, columns, actoxConn, transaction);
                totalColumns += count;
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

        private static async Task<IEnumerable<StoredProcedureMetadata>> ReadStoredProceduresFromTargetAsync(SqlConnection targetConn)
        {
            using var cmd = new SqlCommand(SchemaSyncQueries.GetTargetStoredProcedures, targetConn);
            using var reader = await cmd.ExecuteReaderAsync();

            var procedures = new List<StoredProcedureMetadata>();
            while (await reader.ReadAsync())
            {
                procedures.Add(new StoredProcedureMetadata
                {
                    ProcedureName = reader.GetString(0),
                    Definition = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }
            return procedures;
        }

        private static async Task SyncViaSameServerAsync(
            int projectId,
            string targetConnectionString,
            int userId,
            IDbConnection actoxConn,
            IDbTransaction transaction)
        {
            var builder = new SqlConnectionStringBuilder(targetConnectionString);
            var databaseName = builder.InitialCatalog;

            using var cmd = actoxConn.CreateCommand() as SqlCommand ?? throw new InvalidOperationException("Failed to create SqlCommand.");
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
            var actoxConnectionString = _configuration.GetConnectionString("DefaultConnection");

            using var targetConn = new SqlConnection(targetConnectionString);
            using var actoxConn = new SqlConnection(actoxConnectionString);

            await targetConn.OpenAsync();
            await actoxConn.OpenAsync();

            using var targetCmd = new SqlCommand(SchemaSyncQueries.GetServerName, targetConn);
            using var actoxCmd = new SqlCommand(SchemaSyncQueries.GetServerName, actoxConn);

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
                    request.ClientId,
                    request.DatabaseName,
                    request.ConnectionString,
                    DateTime.UtcNow,
                    userId,
                    request.Description,
                    request.DatabaseType);

                var projectId = await _projectRepository.CreateAsync(project);
                _logger.LogInformation("Created new project {ProjectName} with ID {ProjectId} for user {UserId}", request.ProjectName, projectId, userId);
                return new ProjectResponse { ProjectId = projectId, Message = "Project created successfully" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project {ProjectName} for user {UserId}", request.ProjectName, userId);
                throw;
            }
        }

        public async Task<Project?> GetProjectByIdAsync(int projectId, int userId)
        {
            try
            {
                return await _projectRepository.GetByIdAsync(projectId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project {ProjectId} for user {UserId}", projectId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<Project>> GetAllProjectsAsync(int userId, int offset = 0, int limit = 50)
        {
            try
            {
                return await _projectRepository.GetAllAsync(userId, offset, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects for user {UserId}", userId);
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
    }
}