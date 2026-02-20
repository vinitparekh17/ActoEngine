using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text;

namespace ActoEngine.WebApi.Features.ImpactAnalysis;

public interface IDependencyAnalysisService
{
    List<Dependency> ExtractDependencies(string sqlDefinition, int sourceEntityId, string sourceType);

    /// <summary>
    /// Parse a SQL definition and extract JOIN ON condition column pairs.
    /// Used by logical FK detection to find implicit relationships.
    /// </summary>
    List<JoinConditionInfo> ExtractJoinConditions(string sqlDefinition);
}

public class DependencyAnalysisService(ILogger<DependencyAnalysisService> logger) : IDependencyAnalysisService
{
    private readonly ILogger<DependencyAnalysisService> _logger = logger;

    public List<Dependency> ExtractDependencies(string sqlDefinition, int sourceEntityId, string sourceType)
    {
        var dependencies = new List<Dependency>();

        if (string.IsNullOrWhiteSpace(sqlDefinition))
        {
            return dependencies;
        }

        // 1. Parse
        var parser = new TSql160Parser(true);
        using var reader = new StringReader(sqlDefinition);
        var fragment = parser.Parse(reader, out var errors);

        if (errors.Count > 0)
        {
            _logger.LogWarning("Parser found {Count} errors in entity {Id}. Parsing best effort.", errors.Count, sourceEntityId);
        }

        // 2. Visit
        if (fragment == null)
        {
            _logger.LogWarning("Parser returned null fragment for entity {Id}. Skipping analysis.", sourceEntityId);
            return dependencies;
        }

        var visitor = new SqlDependencyVisitor();
        fragment.Accept(visitor);

        // 3. Map Tables (with null checks and deduplication)
        foreach (var (tableName, modType) in visitor.TableReferences
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Distinct())
        {
            dependencies.Add(new Dependency
            {
                SourceId = sourceEntityId,
                SourceType = sourceType,
                TargetName = tableName,
                TargetType = "TABLE",
                DependencyType = modType
            });
        }

        // 4. Map SPs (with null checks and deduplication)
        foreach (var spName in visitor.ProcedureReferences
            .Where(sp => !string.IsNullOrWhiteSpace(sp))
            .Distinct())
        {
            dependencies.Add(new Dependency
            {
                SourceId = sourceEntityId,
                SourceType = sourceType,
                TargetName = spName,
                TargetType = "SP",
                DependencyType = "EXEC"
            });
        }

        // 5. Map Columns (Optional: If you want column-level granularity later)
        // visitor.ColumnReferences...

        return dependencies;
    }

    public List<JoinConditionInfo> ExtractJoinConditions(string sqlDefinition)
    {
        if (string.IsNullOrWhiteSpace(sqlDefinition))
        {
            return [];
        }

        var parser = new TSql160Parser(true);
        using var reader = new StringReader(sqlDefinition);
        var fragment = parser.Parse(reader, out _);

        if (fragment == null)
        {
            return [];
        }

        var visitor = new SqlDependencyVisitor();
        fragment.Accept(visitor);

        return visitor.JoinConditions;
    }
}

internal class SqlDependencyVisitor : TSqlFragmentVisitor
{
    public List<(string Name, string ModificationType)> TableReferences { get; } = [];
    public List<string> ProcedureReferences { get; } = [];
    // Storing (TableName, ColumnName, FullIdentifier)
    public List<(string TableName, string ColumnName, string FullIdentifier)> ColumnReferences { get; } = [];
    /// <summary>
    /// JOIN ON condition pairs extracted from equality comparisons within JOIN contexts.
    /// </summary>
    public List<JoinConditionInfo> JoinConditions { get; } = [];

    private readonly Stack<string> _contextStack = new();

    // Alias → real table name mapping (case-insensitive)
    private readonly Dictionary<string, string> _aliasMap = new(StringComparer.OrdinalIgnoreCase);

    // We track the current "Clause" to distinguish FROM (Read) vs UPDATE Targets (Write)
    private bool _inFromClause = false;
    private bool _inJoin = false;
    private bool _inJoinCondition = false;

    public override void Visit(InsertSpecification node)
    {
        _contextStack.Push("INSERT");

        // Visit Target (The table being inserted into)
        if (node.Target != null)
        {
            node.Target.Accept(this);
        }

        // Visit Columns (if explicit)
        if (node.Columns != null)
        {
            foreach (var c in node.Columns)
            {
                c.Accept(this);
            }
        }

        // Everything else (SELECT/VALUES) is essentially "Reading" from other sources
        // So we switch context for the rest of the query
        _contextStack.Push("SELECT");

        if (node.InsertSource != null)
        {
            node.InsertSource.Accept(this);
        }

        _contextStack.Pop(); // Pop SELECT
        _contextStack.Pop(); // Pop INSERT
    }

    public override void Visit(UpdateSpecification node)
    {
        _contextStack.Push("UPDATE");

        // The Target is being Modified
        if (node.Target != null)
        {
            node.Target.Accept(this);
        }

        // Visit SET clause - expressions are reads (right-hand side of assignments)
        _contextStack.Pop(); // Pop UPDATE temporarily
        _contextStack.Push("SELECT"); // SET expressions are reads

        if (node.SetClauses != null)
        {
            foreach (var setClause in node.SetClauses)
            {
                setClause.Accept(this);
            }
        }

        // The FROM and WHERE clauses are also for reading/joining
        // Continue with SELECT context
        _inFromClause = true;

        if (node.FromClause != null)
        {
            node.FromClause.Accept(this);
        }

        if (node.WhereClause != null)
        {
            node.WhereClause.Accept(this);
        }

        _inFromClause = false;
        _contextStack.Pop(); // Pop SELECT
    }

    public override void Visit(DeleteSpecification node)
    {
        _contextStack.Push("DELETE");

        // Visit target table (being deleted from)
        if (node.Target != null)
        {
            node.Target.Accept(this);
        }

        // Pop DELETE before processing FROM/WHERE as SELECT (matches UPDATE pattern)
        _contextStack.Pop();
        _contextStack.Push("SELECT");

        // FROM and WHERE clauses are reads
        _inFromClause = true;
        if (node.FromClause != null)
        {
            node.FromClause.Accept(this);
        }

        if (node.WhereClause != null)
        {
            node.WhereClause.Accept(this);
        }

        _inFromClause = false;

        _contextStack.Pop(); // Remove SELECT
    }

    public override void Visit(NamedTableReference node)
    {
        string? tableName = GetFullTableName(node);

        // Default to the current stack context
        string modificationType = _contextStack.Count > 0 ? _contextStack.Peek() : "SELECT";

        // Override: If we are in a FROM clause or JOIN, it's always a Read (SELECT)
        // unless it is the explicit target of an UPDATE/DELETE (which is handled by visiting Target separately)
        if (_inFromClause || _inJoin)
        {
            modificationType = "SELECT";
        }

        if (tableName != null)
        {
            TableReferences.Add((tableName, modificationType));

            // Track alias → real table name for JOIN condition resolution
            if (node.Alias != null && !string.IsNullOrWhiteSpace(node.Alias.Value))
            {
                _aliasMap[node.Alias.Value] = tableName;
            }
        }
        base.Visit(node);
    }

    public override void Visit(QualifiedJoin node)
    {
        // Mark that we are inside a join, so any tables found are READs
        bool wasInJoin = _inJoin;
        _inJoin = true;

        // Visit the table references first (populates alias map)
        node.FirstTableReference?.Accept(this);
        node.SecondTableReference?.Accept(this);

        // Now visit the ON condition with join-condition context
        bool wasInCondition = _inJoinCondition;
        _inJoinCondition = true;
        node.SearchCondition?.Accept(this);
        _inJoinCondition = wasInCondition;

        _inJoin = wasInJoin;
        // Don't call base.Visit — we already visited all children
    }

    public override void Visit(UnqualifiedJoin node)
    {
        bool wasInJoin = _inJoin;
        _inJoin = true;
        base.Visit(node);
        _inJoin = wasInJoin;
    }

    public override void Visit(ExecuteSpecification node)
    {
        if (node.ExecutableEntity is ExecutableProcedureReference procRef)
        {
            if (procRef.ProcedureReference is ProcedureReferenceName prn &&
                prn.ProcedureReference is ProcedureReference pr &&
                pr.Name != null)
            {
                string? procName = GetFullObjectName(pr.Name);
                if (procName != null)
                {
                    ProcedureReferences.Add(procName);
                }
            }
        }
        base.Visit(node);
    }

    public override void Visit(ColumnReferenceExpression node)
    {
        if (node.ColumnType == ColumnType.Regular && node.MultiPartIdentifier != null)
        {
            var parts = node.MultiPartIdentifier.Identifiers;
            if (parts.Count >= 2)
            {
                // (1) Last identifier is the column
                var columnName = parts[parts.Count - 1].Value;

                // (2) Resolve alias or use the nearest prior identifier as table name
                var rawTableRef = parts[parts.Count - 2].Value;
                var tableName = _aliasMap.TryGetValue(rawTableRef, out var realName)
                    ? realName
                    : rawTableRef;

                // (3) Preserve full multipart information
                var fullIdentifier = string.Join(".", parts.Select(p => p.Value));

                ColumnReferences.Add((tableName, columnName, fullIdentifier));
            }
        }
        base.Visit(node);
    }

    /// <summary>
    /// Captures equality comparisons (a.col = b.col) within JOIN ON conditions.
    /// </summary>
    public override void Visit(BooleanComparisonExpression node)
    {
        if (_inJoinCondition && node.ComparisonType == BooleanComparisonType.Equals)
        {
            var left = ExtractColumnRef(node.FirstExpression);
            var right = ExtractColumnRef(node.SecondExpression);

            if (left != null && right != null)
            {
                JoinConditions.Add(new JoinConditionInfo
                {
                    LeftTable = left.Value.Table,
                    LeftColumn = left.Value.Column,
                    RightTable = right.Value.Table,
                    RightColumn = right.Value.Column
                });
            }
        }
        base.Visit(node);
    }

    private (string Table, string Column)? ExtractColumnRef(ScalarExpression expr)
    {
        if (expr is ColumnReferenceExpression colRef &&
            colRef.ColumnType == ColumnType.Regular &&
            colRef.MultiPartIdentifier?.Identifiers.Count >= 2)
        {
            var parts = colRef.MultiPartIdentifier.Identifiers;
            var column = parts[parts.Count - 1].Value;
            var rawTable = parts[parts.Count - 2].Value;

            // Resolve alias to real table name
            var table = _aliasMap.TryGetValue(rawTable, out var realName)
                ? realName
                : rawTable;

            return (table, column);
        }
        return null;
    }

    private static string? GetFullTableName(NamedTableReference node)
    {
        return GetFullObjectName(node.SchemaObject);
    }

    private static string? GetFullObjectName(SchemaObjectName schemaObject)
    {
        if (schemaObject == null)
        {
            return null;
        }

        var sb = new StringBuilder();

        if (schemaObject.DatabaseIdentifier != null)
        {
            sb.Append(schemaObject.DatabaseIdentifier.Value).Append('.');
        }

        if (schemaObject.SchemaIdentifier != null)
        {
            sb.Append(schemaObject.SchemaIdentifier.Value).Append('.');
        }

        if (schemaObject.BaseIdentifier != null)
        {
            sb.Append(schemaObject.BaseIdentifier.Value);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents a column equality pair found in a JOIN ON condition.
/// e.g., "Orders JOIN Customers ON o.CustomerId = c.Id" 
/// → LeftTable=Orders, LeftColumn=CustomerId, RightTable=Customers, RightColumn=Id
/// </summary>
public class JoinConditionInfo
{
    public required string LeftTable { get; set; }
    public required string LeftColumn { get; set; }
    public required string RightTable { get; set; }
    public required string RightColumn { get; set; }
}