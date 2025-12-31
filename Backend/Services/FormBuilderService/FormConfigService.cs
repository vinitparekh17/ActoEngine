using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using FluentValidation;
using System.Text.Json;

namespace ActoEngine.WebApi.Services.FormBuilderService
{
    public interface IFormConfigService
    {
        Task<FormConfig> SaveAsync(FormConfig config, int userId);
        Task<FormConfig?> LoadAsync(string id, int userId);
        Task<List<FormConfigListItem>> GetByProjectIdAsync(int projectId, int userId);
        Task<bool> DeleteAsync(string id, int userId);
    }
    public class FormConfigService(
        FormConfigRepository repository,
        IProjectRepository projectRepository,
        IValidator<FormConfig> validator,
        ILogger<FormConfigService> logger) : IFormConfigService
    {
        public async Task<FormConfig> SaveAsync(FormConfig config, int userId)
        {
            // Verify user has access to project
            var project = await projectRepository.GetByIdAsync(config.ProjectId);
            if (project == null || project.CreatedBy != userId)
            {
                throw new UnauthorizedAccessException("Access denied to project");
            }

            // Validate config
            var validationResult = await validator.ValidateAsync(config);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new ValidationException($"Validation failed: {errors}");
            }

            // Assign server IDs to temp- prefixed items
            config = AssignServerIds(config);

            // Set metadata
            config.CreatedAt ??= DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;

            // Serialize to JSON
            var configJson = JsonSerializer.Serialize(config);

            // Save to database
            var savedConfig = await repository.SaveAsync(config, configJson, userId);

            // Save denormalized data for querying
            await repository.SaveDenormalizedDataAsync(savedConfig);

            logger.LogInformation("Saved form configuration {FormId} for project {ProjectId}",
                config.Id, config.ProjectId);

            return savedConfig;
        }

        public async Task<FormConfig?> LoadAsync(string id, int userId)
        {
            var config = await repository.GetByIdAsync(id, userId);

            if (config != null)
            {
                logger.LogInformation("Loaded form configuration {FormId} for user {UserId}",
                    id, userId);
            }

            return config;
        }

        public async Task<List<FormConfigListItem>> GetByProjectIdAsync(int projectId, int userId)
        {
            // Verify user has access to project
            var project = await projectRepository.GetByIdAsync(projectId);
            if (project == null || project.CreatedBy != userId)
            {
                throw new UnauthorizedAccessException("Access denied to project");
            }

            var configs = await repository.GetByProjectIdAsync(projectId, userId);

            logger.LogInformation("Retrieved {Count} form configurations for project {ProjectId}",
                configs.Count, projectId);

            return configs;
        }

        public async Task<bool> DeleteAsync(string id, int userId)
        {
            var success = await repository.DeleteAsync(id, userId);

            if (success)
            {
                logger.LogInformation("Deleted form configuration {FormId} for user {UserId}",
                    id, userId);
            }

            return success;
        }

        private static FormConfig AssignServerIds(FormConfig config)
        {
            // Assign server ID to config if it's temp
            if (!string.IsNullOrEmpty(config.Id) && config.Id.StartsWith("temp-"))
            {
                config.Id = Guid.NewGuid().ToString();
            }

            // Assign server IDs to groups
            if (config.Groups != null)
            {
                foreach (var group in config.Groups)
                {
                    if (!string.IsNullOrEmpty(group.Id) && group.Id.StartsWith("temp-"))
                    {
                        group.Id = Guid.NewGuid().ToString();
                    }

                    // Assign server IDs to fields
                    if (group.Fields != null)
                    {
                        foreach (var field in group.Fields)
                        {
                            if (!string.IsNullOrEmpty(field.Id) && field.Id.StartsWith("temp-"))
                            {
                                field.Id = Guid.NewGuid().ToString();
                            }
                        }
                    }
                }
            }

            return config;
        }
    }

    // ============================================
    // FluentValidation for FormConfig
    // ============================================
    public class FormConfigValidator : AbstractValidator<FormConfig>
    {
        public FormConfigValidator()
        {
            RuleFor(x => x.ProjectId)
                .GreaterThan(0)
                .WithMessage("Project ID must be greater than 0");

            RuleFor(x => x.TableName)
                .NotEmpty()
                .WithMessage("Table name is required")
                .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
                .WithMessage("Table name must be a valid SQL identifier");

            RuleFor(x => x.FormName)
                .NotEmpty()
                .WithMessage("Form name is required")
                .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
                .WithMessage("Form name must be a valid JavaScript identifier");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(200)
                .WithMessage("Title cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Description cannot exceed 500 characters");

            RuleFor(x => x.Groups)
                .NotEmpty()
                .WithMessage("At least one group is required");

            RuleForEach(x => x.Groups)
                .SetValidator(new FormGroupValidator());
        }
    }

    public class FormGroupValidator : AbstractValidator<FormGroup>
    {
        public FormGroupValidator()
        {
            RuleFor(x => x.Title)
                .MaximumLength(200)
                .WithMessage("Group title cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Group description cannot exceed 500 characters");

            RuleFor(x => x.Layout)
                .Must(layout => layout == "row" || layout == "column")
                .WithMessage("Layout must be 'row' or 'column'");

            RuleFor(x => x.Fields)
                .NotEmpty()
                .WithMessage("Group must contain at least one field");

            RuleForEach(x => x.Fields)
                .SetValidator(new FormFieldValidator());
        }
    }

    public class FormFieldValidator : AbstractValidator<FormField>
    {
        public FormFieldValidator()
        {
            RuleFor(x => x.ColumnName)
                .NotEmpty()
                .WithMessage("Column name is required")
                .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
                .WithMessage("Column name must be a valid SQL identifier");

            RuleFor(x => x.Label)
                .NotEmpty()
                .WithMessage("Label is required")
                .MaximumLength(200)
                .WithMessage("Label cannot exceed 200 characters");

            RuleFor(x => x.DataType)
                .NotEmpty()
                .WithMessage("Data type is required");

            RuleFor(x => x.Placeholder)
                .MaximumLength(200)
                .WithMessage("Placeholder cannot exceed 200 characters");

            RuleFor(x => x.DefaultValue)
                .MaximumLength(200)
                .WithMessage("Default value cannot exceed 200 characters");

            RuleFor(x => x.HelpText)
                .MaximumLength(500)
                .WithMessage("Help text cannot exceed 500 characters");

            RuleFor(x => x.ColSize)
                .Must(size => new[] { 1, 2, 3, 4, 6, 12 }.Contains((int)size))
                .WithMessage("Column size must be 1, 2, 3, 4, 6, or 12");

            RuleFor(x => x.MinLength)
                .GreaterThan(0)
                .When(x => x.MinLength.HasValue)
                .WithMessage("Minimum length must be greater than 0");

            RuleFor(x => x.MaxLength)
                .GreaterThan(0)
                .When(x => x.MaxLength.HasValue)
                .WithMessage("Maximum length must be greater than 0");

            RuleFor(x => x.MaxLength)
                .GreaterThanOrEqualTo(x => x.MinLength)
                .When(x => x.MinLength.HasValue && x.MaxLength.HasValue)
                .WithMessage("Maximum length must be greater than or equal to minimum length");
        }
    }
}