using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ActoEngine.WebApi.Repositories
{
    public class CodeTemplateRepository
    {
        private readonly string _connectionString;

        public CodeTemplateRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection");
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<CodeTemplate?> GetTemplateAsync(string type, string framework, string? version = null)
        {
            using var connection = CreateConnection();

            var query = @"SELECT Id, TemplateName, TemplateType, Framework, Version,
                                 TemplateContent, Description, IsActive, CreatedAt, UpdatedAt
                          FROM CodeTemplates
                          WHERE TemplateType = @Type AND Framework = @Framework AND IsActive = 1";

            if (!string.IsNullOrEmpty(version))
            {
                query += " AND Version = @Version";
            }
            else
            {
                query += " ORDER BY Version DESC";
            }

            return await connection.QueryFirstOrDefaultAsync<CodeTemplate>(
                query, new { Type = type, Framework = framework, Version = version });
        }

        public async Task<List<CodeTemplate>> GetTemplatesAsync(string? type = null, string? framework = null)
        {
            using var connection = CreateConnection();

            var query = @"SELECT Id, TemplateName, TemplateType, Framework, Version,
                                 TemplateContent, Description, IsActive, CreatedAt, UpdatedAt
                          FROM CodeTemplates
                          WHERE IsActive = 1";

            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(type))
            {
                query += " AND TemplateType = @Type";
                parameters.Add("Type", type);
            }

            if (!string.IsNullOrEmpty(framework))
            {
                query += " AND Framework = @Framework";
                parameters.Add("Framework", framework);
            }

            query += " ORDER BY TemplateType, Framework, Version DESC";

            var templates = await connection.QueryAsync<CodeTemplate>(query, parameters);
            return [.. templates];
        }

        public async Task<CodeTemplate?> GetByNameAsync(string templateName)
        {
            using var connection = CreateConnection();

            return await connection.QueryFirstOrDefaultAsync<CodeTemplate>(
                @"SELECT Id, TemplateName, TemplateType, Framework, Version,
                         TemplateContent, Description, IsActive, CreatedAt, UpdatedAt
                  FROM CodeTemplates
                  WHERE TemplateName = @TemplateName AND IsActive = 1",
                new { TemplateName = templateName });
        }

        public async Task<CodeTemplate> SaveAsync(CodeTemplate template)
        {
            using var connection = CreateConnection();

            // Check if template exists
            var existing = await connection.QueryFirstOrDefaultAsync<CodeTemplate>(
                "SELECT Id FROM CodeTemplates WHERE TemplateName = @TemplateName",
                new { template.TemplateName });

            if (existing != null)
            {
                // Update
                await connection.ExecuteAsync(
                    @"UPDATE CodeTemplates
                      SET TemplateType = @TemplateType, Framework = @Framework, Version = @Version,
                          TemplateContent = @TemplateContent, Description = @Description,
                          IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                      WHERE TemplateName = @TemplateName",
                    template);
            }
            else
            {
                // Insert
                template.CreatedAt = DateTime.UtcNow;
                template.UpdatedAt = DateTime.UtcNow;

                await connection.ExecuteAsync(
                    @"INSERT INTO CodeTemplates (TemplateName, TemplateType, Framework, Version,
                                                 TemplateContent, Description, IsActive, CreatedAt, UpdatedAt)
                      VALUES (@TemplateName, @TemplateType, @Framework, @Version,
                              @TemplateContent, @Description, @IsActive, @CreatedAt, @UpdatedAt)",
                    template);
            }

            return template;
        }

        public async Task<bool> DeleteAsync(string templateName)
        {
            using var connection = CreateConnection();

            var affectedRows = await connection.ExecuteAsync(
                "DELETE FROM CodeTemplates WHERE TemplateName = @TemplateName",
                new { TemplateName = templateName });

            return affectedRows > 0;
        }

        public async Task<bool> DeactivateAsync(string templateName)
        {
            using var connection = CreateConnection();

            var affectedRows = await connection.ExecuteAsync(
                @"UPDATE CodeTemplates
                  SET IsActive = 0, UpdatedAt = GETUTCDATE()
                  WHERE TemplateName = @TemplateName",
                new { TemplateName = templateName });

            return affectedRows > 0;
        }
    }

    // ============================================
    // Code Template Model
    // ============================================
    public class CodeTemplate
    {
        public int Id { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty; // 'HTML', 'JavaScript', 'SP'
        public string Framework { get; set; } = string.Empty; // 'Bootstrap5', 'jQuery', etc.
        public string Version { get; set; } = string.Empty;
        public string TemplateContent { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}