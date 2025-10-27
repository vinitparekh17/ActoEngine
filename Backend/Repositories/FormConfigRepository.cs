using ActoEngine.WebApi.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ActoEngine.WebApi.Repositories
{
    public class FormConfigRepository
    {
        private readonly string _connectionString;

        public FormConfigRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection");
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        // ========================================
        // FormConfig CRUD Operations
        // ========================================

        public async Task<FormConfig?> GetByIdAsync(string id, int userId)
        {
            using var connection = CreateConnection();

            // Get the config JSON
            var configJson = await connection.QueryFirstOrDefaultAsync<string?>(
                @"SELECT fc.ConfigJson
                  FROM FormConfigs fc
                  INNER JOIN Projects p ON fc.ProjectId = p.Id
                  WHERE fc.Id = @Id AND p.CreatedBy = @UserId",
                new { Id = id, UserId = userId });

            if (string.IsNullOrEmpty(configJson))
                return null;

            // Deserialize the config
            var config = System.Text.Json.JsonSerializer.Deserialize<FormConfig>(configJson);
            return config;
        }

        public async Task<List<FormConfigListItem>> GetByProjectIdAsync(int projectId, int userId)
        {
            using var connection = CreateConnection();

            var configs = await connection.QueryAsync<FormConfigListItem>(
                @"SELECT fc.Id, fc.ProjectId, fc.FormName, fc.TableName, fc.Title,
                  FROM FormConfigs fc
                  INNER JOIN Projects p ON fc.ProjectId = p.Id
                  WHERE fc.ProjectId = @ProjectId AND p.CreatedBy = @UserId
                  ORDER BY fc.UpdatedAt DESC",
                new { ProjectId = projectId, UserId = userId });

            return configs.ToList();
        }

        public async Task<FormConfig> SaveAsync(FormConfig config, string configJson, int userId)
        {
            using var connection = CreateConnection();

            // Check if this is an update or insert
            var existingId = await connection.QueryFirstOrDefaultAsync<string?>(
                @"SELECT Id FROM FormConfigs
                  WHERE Id = @Id AND ProjectId = @ProjectId",
                new { config.Id, config.ProjectId });

            if (existingId != null)
            {
                // Update existing
                await connection.ExecuteAsync(
                    @"UPDATE FormConfigs
                      SET TableName = @TableName, FormName = @FormName, Title = @Title,
                          Description = @Description, ConfigJson = @ConfigJson,
                          UpdatedAt = GETUTCDATE()
                      WHERE Id = @Id",
                    new
                    {
                        config.Id,
                        config.TableName,
                        config.FormName,
                        config.Title,
                        config.Description,
                        ConfigJson = configJson
                    });
            }
            else
            {
                // Insert new
                await connection.ExecuteAsync(
                    @"INSERT INTO FormConfigs (ProjectId, TableName, FormName, Title, Description,
                                               ConfigJson, CreatedBy, CreatedAt, UpdatedAt)
                      VALUES (@ProjectId, @TableName, @FormName, @Title, @Description,
                              @ConfigJson, @CreatedBy, GETUTCDATE(), GETUTCDATE())",
                    new
                    {
                        config.ProjectId,
                        config.TableName,
                        config.FormName,
                        config.Title,
                        config.Description,
                        ConfigJson = configJson,
                        CreatedBy = userId
                    });
            }
            return config;
        }
        public async Task<bool> DeleteAsync(string id, int userId)
        {
            using var connection = CreateConnection();

            var affectedRows = await connection.ExecuteAsync(
                @"DELETE fc FROM FormConfigs fc
                  INNER JOIN Projects p ON fc.ProjectId = p.Id
                  WHERE fc.Id = @Id AND p.CreatedBy = @UserId",
                new { Id = id, UserId = userId });

            return affectedRows > 0;
        }

        // ========================================
        // Denormalized Data Operations
        // ========================================

        public async Task SaveDenormalizedDataAsync(FormConfig config)
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Delete existing denormalized data
                await connection.ExecuteAsync(
                    "DELETE FROM FormFields WHERE FormGroupId IN (SELECT Id FROM FormGroups WHERE FormConfigId = (SELECT Id FROM FormConfigs WHERE Id = @FormId))",
                    new { FormId = config.Id }, transaction);

                await connection.ExecuteAsync(
                    "DELETE FROM FormGroups WHERE FormConfigId = (SELECT Id FROM FormConfigs WHERE Id = @FormId)",
                    new { FormId = config.Id }, transaction);

                // Insert groups and fields
                foreach (var group in config.Groups)
                {
                    var groupId = await connection.QuerySingleAsync<int>(
                        @"INSERT INTO FormGroups (FormConfigId, GroupId, Title, Description, Layout,
                                                  OrderIndex, Collapsible, Collapsed)
                          OUTPUT INSERTED.Id
                          VALUES ((SELECT Id FROM FormConfigs WHERE Id = @FormId), @GroupId, @Title,
                                  @Description, @Layout, @OrderIndex, @Collapsible, @Collapsed)",
                        new
                        {
                            FormId = config.Id,
                            GroupId = group.Id,
                            group.Title,
                            group.Description,
                            Layout = group.Layout ?? "row",
                            OrderIndex = group.Order,
                            group.Collapsible,
                            group.Collapsed
                        }, transaction);

                    // Insert fields for this group
                    foreach (var field in group.Fields)
                    {
                        await connection.ExecuteAsync(
                            @"INSERT INTO FormFields (FormGroupId, FieldId, ColumnName, DataType, Label,
                                                      InputType, Placeholder, DefaultValue, HelpText,
                                                      ColSize, OrderIndex, ValidationRules,
                                                      IncludeInInsert, IncludeInUpdate, IsPrimaryKey,
                                                      IsIdentity, IsForeignKey, OptionsJson)
                              VALUES (@FormGroupId, @FieldId, @ColumnName, @DataType, @Label,
                                      @InputType, @Placeholder, @DefaultValue, @HelpText,
                                      @ColSize, @OrderIndex, @ValidationRules,
                                      @IncludeInInsert, @IncludeInUpdate, @IsPrimaryKey,
                                      @IsIdentity, @IsForeignKey, @OptionsJson)",
                            new
                            {
                                FormGroupId = groupId,
                                FieldId = field.Id,
                                field.ColumnName,
                                DataType = field.DataType ?? "nvarchar",
                                field.Label,
                                InputType = field.InputType.ToString(),
                                field.Placeholder,
                                field.DefaultValue,
                                field.HelpText,
                                ColSize = (int)field.ColSize,
                                OrderIndex = field.Order,
                                ValidationRules = System.Text.Json.JsonSerializer.Serialize(new
                                {
                                    field.Required,
                                    field.MinLength,
                                    field.MaxLength,
                                    field.Min,
                                    field.Max,
                                    field.Pattern,
                                    field.ErrorMessage
                                }),
                                field.IncludeInInsert,
                                field.IncludeInUpdate,
                                field.IsPrimaryKey,
                                field.IsIdentity,
                                field.IsForeignKey,
                                OptionsJson = field.Options != null ?
                                    System.Text.Json.JsonSerializer.Serialize(field.Options) : null
                            }, transaction);
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}