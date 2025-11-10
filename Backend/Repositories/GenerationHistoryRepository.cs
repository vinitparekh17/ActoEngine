using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ActoEngine.WebApi.Repositories
{
    public interface IGenerationHistoryRepository
    {
        Task SaveAsync(GenerationHistory history);
        Task<List<GenerationHistory>> GetByFormConfigIdAsync(string formConfigId, int userId);
        Task<GenerationHistory?> GetLatestAsync(string formConfigId, int userId);
        Task<List<GenerationHistory>> GetRecentAsync(int userId, int limit = 10);
    }

    public class GenerationHistoryRepository : IGenerationHistoryRepository
    {
        private readonly string _connectionString;

        public GenerationHistoryRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection");
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task SaveAsync(GenerationHistory history)
        {
            using var connection = CreateConnection();

            history.GeneratedAt = DateTime.UtcNow;

            await connection.ExecuteAsync(
                @"INSERT INTO GenerationHistory (FormConfigId, GenerationType, HtmlGenerated,
                                                 JavaScriptGenerated, SpGenerated, FieldCount,
                                                 GroupCount, Warnings, GeneratedBy, GeneratedAt)
                  VALUES (@FormConfigId, @GenerationType, @HtmlGenerated, @JavaScriptGenerated,
                          @SpGenerated, @FieldCount, @GroupCount, @Warnings, @GeneratedBy, @GeneratedAt)",
                history);
        }

        public async Task<List<GenerationHistory>> GetByFormConfigIdAsync(string formConfigId, int userId)
        {
            using var connection = CreateConnection();

            var history = await connection.QueryAsync<GenerationHistory>(
                @"SELECT gh.Id, gh.FormConfigId, gh.GenerationType, gh.HtmlGenerated,
                         gh.JavaScriptGenerated, gh.SpGenerated, gh.FieldCount, gh.GroupCount,
                         gh.Warnings, gh.GeneratedBy, gh.GeneratedAt
                  FROM GenerationHistory gh
                  INNER JOIN FormConfigs fc ON gh.FormConfigId = fc.Id
                  INNER JOIN Projects p ON fc.ProjectId = p.Id
                  WHERE gh.FormConfigId = @FormConfigId AND p.CreatedBy = @UserId
                  ORDER BY gh.GeneratedAt DESC",
                new { FormConfigId = formConfigId, UserId = userId });

            return [.. history];
        }

        public async Task<GenerationHistory?> GetLatestAsync(string formConfigId, int userId)
        {
            using var connection = CreateConnection();

            return await connection.QueryFirstOrDefaultAsync<GenerationHistory>(
                @"SELECT TOP 1 gh.Id, gh.FormConfigId, gh.GenerationType, gh.HtmlGenerated,
                           gh.JavaScriptGenerated, gh.SpGenerated, gh.FieldCount, gh.GroupCount,
                           gh.Warnings, gh.GeneratedBy, gh.GeneratedAt
                  FROM GenerationHistory gh
                  INNER JOIN FormConfigs fc ON gh.FormConfigId = fc.Id
                  INNER JOIN Projects p ON fc.ProjectId = p.Id
                  WHERE gh.FormConfigId = @FormConfigId AND p.CreatedBy = @UserId
                  ORDER BY gh.GeneratedAt DESC",
                new { FormConfigId = formConfigId, UserId = userId });
        }

        public async Task<List<GenerationHistory>> GetRecentAsync(int userId, int limit = 10)
        {
            using var connection = CreateConnection();

            var history = await connection.QueryAsync<GenerationHistory>(
                @"SELECT TOP (@Limit) gh.Id, gh.FormConfigId, gh.GenerationType, gh.HtmlGenerated,
                         gh.JavaScriptGenerated, gh.SpGenerated, gh.FieldCount, gh.GroupCount,
                         gh.Warnings, gh.GeneratedBy, gh.GeneratedAt,
                         fc.FormName, fc.Title
                  FROM GenerationHistory gh
                  INNER JOIN FormConfigs fc ON gh.FormConfigId = fc.Id
                  INNER JOIN Projects p ON fc.ProjectId = p.Id
                  WHERE p.CreatedBy = @UserId
                  ORDER BY gh.GeneratedAt DESC",
                new { UserId = userId, Limit = limit });

            return [.. history];
        }
    }

    // ============================================
    // Generation History Model
    // ============================================
    public class GenerationHistory
    {
        public int Id { get; set; }
        public string FormConfigId { get; set; } = string.Empty;
        public string GenerationType { get; set; } = string.Empty; // 'Full', 'Preview'
        public bool HtmlGenerated { get; set; }
        public bool JavaScriptGenerated { get; set; }
        public bool SpGenerated { get; set; }
        public int FieldCount { get; set; }
        public int GroupCount { get; set; }
        public string? Warnings { get; set; } // JSON array
        public int? GeneratedBy { get; set; }
        public DateTime GeneratedAt { get; set; }

        // Additional properties for queries
        public string? FormName { get; set; }
        public string? Title { get; set; }
    }
}