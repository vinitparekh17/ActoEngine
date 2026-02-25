using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.LogicalFk;
using ActoEngine.WebApi.Features.Schema;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ActoEngine.Tests.Builders;

/// <summary>
/// Fluent builder that centralises <see cref="LogicalFkService"/> construction
/// for tests. Mock setup for every dependency lives here, keeping individual
/// test classes focused on the scenario under test.
/// </summary>
public sealed class LogicalFkServiceBuilder
{
    private int _projectId = 1;
    private List<DetectionColumnInfo> _columns = [];
    private List<StoredProcedureMetadataDto> _procedures = [];
    private int? _procedureCount;
    private List<JoinConditionInfo> _joinConditions = [];
    private HashSet<string> _physicalKeys = [];
    private HashSet<string> _logicalKeys = [];
    private DetectionConfig _config = new();
    private bool _throwOnJoinExtraction;

    public static LogicalFkServiceBuilder Create() => new();

    public LogicalFkServiceBuilder WithProjectId(int projectId) { _projectId = projectId; return this; }

    public LogicalFkServiceBuilder WithColumns(List<DetectionColumnInfo> columns) { _columns = columns; return this; }

    public LogicalFkServiceBuilder WithProcedures(int count)
    {
        _procedureCount = count;
        _procedures = [];
        return this;
    }

    public LogicalFkServiceBuilder WithProcedures(List<StoredProcedureMetadataDto> procedures)
    {
        _procedureCount = null;
        _procedures = procedures;
        return this;
    }

    public LogicalFkServiceBuilder WithJoinConditions(List<JoinConditionInfo> joinConditions) { _joinConditions = joinConditions; return this; }

    public LogicalFkServiceBuilder WithPhysicalKeys(HashSet<string> keys) { _physicalKeys = keys; return this; }

    public LogicalFkServiceBuilder WithLogicalKeys(HashSet<string> keys) { _logicalKeys = keys; return this; }

    public LogicalFkServiceBuilder WithConfig(DetectionConfig config) { _config = config; return this; }

    public LogicalFkServiceBuilder ThrowOnJoinExtraction() { _throwOnJoinExtraction = true; return this; }

    public LogicalFkService Build()
    {
        var procedures = _procedureCount.HasValue
            ? Enumerable.Range(1, _procedureCount.Value)
                .Select(i => new StoredProcedureMetadataDto
                {
                    SpId = i,
                    ProjectId = _projectId,
                    ClientId = 1,
                    ProcedureName = $"sp_{i}",
                    Definition = "SELECT 1"
                })
                .ToList()
            : _procedures;

        var logicalFkRepo = Substitute.For<ILogicalFkRepository>();
        logicalFkRepo.GetColumnsForDetectionAsync(_projectId, Arg.Any<CancellationToken>()).Returns(_columns);
        logicalFkRepo.GetAllPhysicalFkPairsAsync(_projectId, Arg.Any<CancellationToken>()).Returns(_physicalKeys);
        logicalFkRepo.GetAllLogicalFkCanonicalKeysAsync(_projectId, Arg.Any<CancellationToken>()).Returns(_logicalKeys);

        var dependencyRepo = Substitute.For<IDependencyRepository>();

        var schemaRepo = Substitute.For<ISchemaRepository>();
        schemaRepo.GetStoredStoredProceduresAsync(_projectId).Returns(procedures);

        var analysisService = Substitute.For<IDependencyAnalysisService>();
        if (_throwOnJoinExtraction)
        {
            analysisService.ExtractJoinConditions(Arg.Any<string>())
                .Returns(_ => throw new InvalidOperationException("parser error"));
        }
        else
        {
            analysisService.ExtractJoinConditions(Arg.Any<string>())
                .Returns(_joinConditions);
        }

        var calculator = new ConfidenceCalculator(_config);
        var logger = Substitute.For<ILogger<LogicalFkService>>();

        return new LogicalFkService(
            logicalFkRepo,
            dependencyRepo,
            schemaRepo,
            analysisService,
            calculator,
            logger);
    }

    /// <summary>
    /// Convenience helper to create a <see cref="DetectionColumnInfo"/>
    /// with sensible defaults, shared across all test classes.
    /// </summary>
    public static DetectionColumnInfo Col(
        int columnId,
        int tableId,
        string tableName,
        string columnName,
        string dataType,
        bool isPk = false,
        bool isFk = false,
        bool isUnique = false)
    {
        return new DetectionColumnInfo
        {
            ColumnId = columnId,
            TableId = tableId,
            TableName = tableName,
            ColumnName = columnName,
            DataType = dataType,
            IsPrimaryKey = isPk,
            IsForeignKey = isFk,
            IsUnique = isUnique
        };
    }
}
