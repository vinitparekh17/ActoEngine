using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;
using Dapper;

namespace ActoEngine.WebApi.Repositories
{
    public interface IFormConfigRepository
    {
        Task<FormConfig?> GetByIdAsync(string id, int userId);
        Task<FormConfig?> GetByIdOrNameAsync(string idOrName, int userId);
        Task<int?> GetIdByProjectAndFormNameAsync(int projectId, string formName, int userId);
        Task<List<FormConfigListItem>> GetByProjectIdAsync(int projectId, int userId);
        Task<FormConfig> SaveAsync(FormConfig config, string configJson, int userId);
        Task<(FormConfig config, int formId)> SaveWithIdAsync(FormConfig config, string configJson, int projectId);
        Task SaveDenormalizedDataAsync(FormConfig config);

        /// <summary>
        /// Gets all form configs for a project without filtering by user
        /// Needed for admin operations
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        Task<List<FormConfigListItem>> GetByProjectIdWithoutUserFilterAsync(int projectId);
        Task<bool> DeleteAsync(string id, int userId);
    }

    public class FormConfigRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<FormConfigRepository> logger)
        : BaseRepository(connectionFactory, logger), IFormConfigRepository
    {
        // ========================================
        // FormConfig CRUD Operations
        // ========================================

        public async Task<FormConfig?> GetByIdAsync(string id, int userId)
        {
            var configJson = await QueryFirstOrDefaultAsync<string?>(
                FormConfigSqlQueries.GetById,
                new { Id = id, UserId = userId });

            if (string.IsNullOrEmpty(configJson))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<FormConfig>(configJson);
        }

        public async Task<FormConfig?> GetByIdOrNameAsync(string idOrName, int userId)
        {
            var configJson = await QueryFirstOrDefaultAsync<string?>(
                FormConfigSqlQueries.GetByIdOrName,
                new { IdOrName = idOrName, UserId = userId });

            if (string.IsNullOrEmpty(configJson))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<FormConfig>(configJson);
        }

        public async Task<int?> GetIdByProjectAndFormNameAsync(int projectId, string formName, int userId)
        {
            return await ExecuteScalarAsync<int?>(
                FormConfigSqlQueries.GetIdByProjectAndFormName,
                new { ProjectId = projectId, FormName = formName, UserId = userId });
        }

        public async Task<List<FormConfigListItem>> GetByProjectIdAsync(int projectId, int userId)
        {
            var configs = await QueryAsync<FormConfigListItem>(
                FormConfigSqlQueries.GetByProjectId,
                new { ProjectId = projectId, UserId = userId });

            return [.. configs];
        }

        public async Task<List<FormConfigListItem>> GetByProjectIdWithoutUserFilterAsync(int projectId)
        {
            var configs = await QueryAsync<FormConfigListItem>(
                FormConfigSqlQueries.GetByProjectIdWithoutUserFilter,
                new { ProjectId = projectId });

            return [.. configs];
        }

        public async Task<(FormConfig config, int formId)> SaveWithIdAsync(FormConfig config, string configJson, int projectId)
        {
            // Check if form already exists
            var existingId = await ExecuteScalarAsync<int?>(
                FormConfigSqlQueries.CheckExistsByProjectAndFormName,
                new { ProjectId = projectId, config.FormName });

            int formId;

            if (existingId.HasValue)
            {
                // Update existing
                await ExecuteAsync(
                    FormConfigSqlQueries.Update,
                    new
                    {
                        Id = existingId.Value,
                        config.TableName,
                        config.Title,
                        config.Description,
                        ConfigJson = configJson
                    });
                formId = existingId.Value;
            }
            else
            {
                // Insert new
                formId = await ExecuteScalarAsync<int>(
                    FormConfigSqlQueries.Insert,
                    new
                    {
                        ProjectId = projectId,
                        config.TableName,
                        config.FormName,
                        config.Title,
                        config.Description,
                        ConfigJson = configJson
                    });
            }

            config.Id = formId.ToString();
            return (config, formId);
        }

        public async Task<FormConfig> SaveAsync(FormConfig config, string configJson, int userId)
        {
            // Check if this is an update or insert
            var existingId = await ExecuteScalarAsync<string?>(
                FormConfigSqlQueries.CheckExistsById,
                new { config.Id, config.ProjectId });

            if (existingId != null)
            {
                // Update existing
                await ExecuteAsync(
                    FormConfigSqlQueries.UpdateWithFormName,
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
                await ExecuteAsync(
                    FormConfigSqlQueries.InsertWithCreatedBy,
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
            var affectedRows = await ExecuteAsync(
                FormConfigSqlQueries.Delete,
                new { Id = id, UserId = userId });

            return affectedRows > 0;
        }

        // ========================================
        // Denormalized Data Operations
        // ========================================

        public async Task SaveDenormalizedDataAsync(FormConfig config)
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // Delete existing denormalized data
                await connection.ExecuteAsync(
                    FormConfigSqlQueries.DeleteFormFields,
                    new { FormId = config.Id },
                    transaction);

                await connection.ExecuteAsync(
                    FormConfigSqlQueries.DeleteFormGroups,
                    new { FormId = config.Id },
                    transaction);

                // Insert groups and fields
                foreach (var group in config.Groups)
                {
                    var groupId = await connection.QuerySingleAsync<int>(
                        FormConfigSqlQueries.InsertFormGroup,
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
                        },
                        transaction);

                    // Insert fields for this group
                    foreach (var field in group.Fields)
                    {
                        await connection.ExecuteAsync(
                            FormConfigSqlQueries.InsertFormField,
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
                                OptionsJson = field.Options != null
                                    ? System.Text.Json.JsonSerializer.Serialize(field.Options)
                                    : null
                            },
                            transaction);
                    }
                }

                return true;
            });
        }
    }
}
