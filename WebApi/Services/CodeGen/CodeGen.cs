using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.CodeGen;

public interface ICodeGenService
{
    Task<GeneratedSpResponse> GenerateStoredProcedure(SpGenerationRequest req);
    Task<TableSchemaResponse> GetTableSchema(TableSchemaRequest req);
}

public class CodeGenService(ISchemaSyncRepository schemaRepo, IProjectRepository projectRepo) : ICodeGenService
{
    private readonly ISchemaSyncRepository _schemaRepo = schemaRepo;
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
        if (!pkCols.Any())
        {
            response.Warnings.Add("⚠️ No PK found. CUD operations may not work correctly.");
        }

        if (req.Type == SpType.Cud)
        {
            response.StoredProcedure = GenerateCudSp(req);
        }
        else if (req.Type == SpType.Select)
        {
            response.StoredProcedure = GenerateSelectSp(req);
        }

        return Task.FromResult(response);
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

        if (opts.Filters.Any())
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
        var project = await _projectRepo.GetByIdAsync(req.ProjectId) ?? throw new InvalidOperationException($"Project with ID {req.ProjectId} not found.");
        return await _schemaRepo.ReadTableSchema(project.ConnectionString, req.TableName);
    }
}