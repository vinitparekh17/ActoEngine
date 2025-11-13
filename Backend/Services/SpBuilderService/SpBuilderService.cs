using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.SpBuilder;

public interface ISpBuilderService
{
    Task<GeneratedSpResponse> GenerateStoredProcedure(SpGenerationRequest req);
    Task<TableSchemaResponse> GetTableSchema(TableSchemaRequest req);
}

public class SpBuilderService(ISchemaRepository schemaRepo, IProjectRepository projectRepo) : ISpBuilderService
{
    private readonly ISchemaRepository _schemaRepo = schemaRepo;
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly SpTemplateRenderer _renderer = new();

    public Task<GeneratedSpResponse> GenerateStoredProcedure(SpGenerationRequest req)
    {
        var response = new GeneratedSpResponse
        {
            TableName = req.TableName,
            Type = req.Type,
            Warnings = [],
            GeneratedAt = DateTime.UtcNow
        };

        var pkCols = req.Columns.Where(c => c.IsPrimaryKey).ToList();
        if (pkCols.Count == 0)
        {
            response.Warnings.Add("⚠️ No PK found. CUD operations may not work correctly.");
        }

        // Generate stored procedure based on type
        response.StoredProcedure = req.Type switch
        {
            SpType.Cud => GenerateCudSp(req),
            SpType.Select => GenerateSelectSp(req),
            _ => GenerateUnsupportedSp(req, response)
        };

        return Task.FromResult(response);
    }

    private GeneratedSpItem GenerateUnsupportedSp(SpGenerationRequest req, GeneratedSpResponse response)
    {
        response.Warnings.Add($"⚠️ Unsupported SpType '{req.Type}' provided. No stored procedure generated.");
        
        return new GeneratedSpItem
        {
            SpName = $"Unsupported_{req.TableName}",
            SpType = "Unsupported",
            Code = $"-- ERROR: Unsupported SpType '{req.Type}' was requested for table '{req.TableName}'.\n-- Supported types: Cud, Select",
            FileName = $"Unsupported_{req.TableName}.sql",
            Description = $"Unsupported SpType '{req.Type}' was requested. Please use 'Cud' or 'Select'."
        };
    }

    private GeneratedSpItem GenerateCudSp(SpGenerationRequest req)
    {
        var opts = req.CudOptions ?? new CudSpOptions();
        var code = _renderer.RenderCud(req.TableName, req.Columns, opts);

        return new GeneratedSpItem
        {
            SpName = $"{opts.SpPrefix}_{req.TableName}_CUD",
            SpType = "CUD",
            Code = code,
            FileName = $"{opts.SpPrefix}_{req.TableName}_CUD.sql",
            Description = "Handles Create, Update, Delete operations. Pass @Action = 'C'/'U'/'D'"
        };
    }

    private GeneratedSpItem GenerateSelectSp(SpGenerationRequest req)
    {
        var opts = req.SelectOptions ?? new SelectSpOptions();
        var code = _renderer.RenderSelect(req.TableName, req.Columns, opts);

        var desc = opts.IncludePagination
            ? "Select with filters and pagination"
            : "Select with optional filters";

        if (opts.Filters.Count != 0)
            desc += $". Filters: {string.Join(", ", opts.Filters.Select(f => f.ColumnName))}";

        return new GeneratedSpItem
        {
            SpName = $"{opts.SpPrefix}_{req.TableName}_Select",
            SpType = "Select",
            Code = code,
            FileName = $"{opts.SpPrefix}_{req.TableName}_Select.sql",
            Description = desc
        };
    }

    public async Task<TableSchemaResponse> GetTableSchema(TableSchemaRequest req)
    {
        // Use cached schema metadata instead of querying the target database
        return await _schemaRepo.GetStoredTableSchemaAsync(req.ProjectId, req.TableName);
    }
}