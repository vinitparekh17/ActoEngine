using ActoEngine.WebApi.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text;

namespace ActoEngine.WebApi.Services.Intelligence;

public interface IDependencyAnalysisService
{
    /// <summary>
/// Extracts database dependencies referenced by the provided SQL definition.
/// </summary>
/// <param name="sqlDefinition">The SQL text to analyze; may be null or whitespace.</param>
/// <param name="sourceEntityId">Identifier of the entity that contains the SQL (used as the dependency source).</param>
/// <param name="sourceType">Type of the source entity (used as the dependency source type).</param>
/// <returns>A list of Dependency objects discovered in the SQL. Tables are returned with TargetType "TABLE" and a DependencyType reflecting the modification context (e.g., "SELECT", "UPDATE"); invoked stored procedures are returned with TargetType "SP" and DependencyType "EXEC". An empty list is returned when the input is null/whitespace or no dependencies are found.</returns>
List<Dependency> ExtractDependencies(string sqlDefinition, int sourceEntityId, string sourceType);
}

public class DependencyAnalysisService(ILogger<DependencyAnalysisService> logger) : IDependencyAnalysisService
{
    private readonly ILogger<DependencyAnalysisService> _logger = logger;

    /// <summary>
    /// Extracts referenced database objects from a SQL definition into Dependency records.
    /// </summary>
    /// <param name="sqlDefinition">The SQL script or statement to analyze for dependencies.</param>
    /// <param name="sourceEntityId">Identifier of the source entity; assigned to each Dependency.SourceId.</param>
    /// <param name="sourceType">Label describing the source entity type; assigned to each Dependency.SourceType.</param>
    /// <returns>A list of Dependency objects representing discovered tables (TargetType = "TABLE") and stored procedures (TargetType = "SP"), or an empty list if no dependencies are found or the input is empty.</returns>
    public List<Dependency> ExtractDependencies(string sqlDefinition, int sourceEntityId, string sourceType)
    {
        var dependencies = new List<Dependency>();

        if (string.IsNullOrWhiteSpace(sqlDefinition)) return dependencies;

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
}

internal class SqlDependencyVisitor : TSqlFragmentVisitor
{
    public List<(string Name, string ModificationType)> TableReferences { get; } = new();
    public List<string> ProcedureReferences { get; } = new();
    // Storing (TableName, ColumnName, FullIdentifier)
    public List<(string TableName, string ColumnName, string FullIdentifier)> ColumnReferences { get; } = new();

    private readonly Stack<string> _contextStack = new();

    // We track the current "Clause" to distinguish FROM (Read) vs UPDATE Targets (Write)
    private bool _inFromClause = false;
    private bool _inJoin = false;

    /// <summary>
    /// Visits an INSERT specification and records the target table, any explicit columns, and the insert source; insert source expressions (VALUES/SELECT) are treated as read (SELECT) context.
    /// </summary>
    /// <param name="node">The INSERT specification AST node to visit.</param>
    public override void Visit(InsertSpecification node)
    {
        _contextStack.Push("INSERT");

        // Visit Target (The table being inserted into)
        if (node.Target != null) node.Target.Accept(this);

        // Visit Columns (if explicit)
        if (node.Columns != null)
        {
            foreach (var c in node.Columns) c.Accept(this);
        }

        // Everything else (SELECT/VALUES) is essentially "Reading" from other sources
        // So we switch context for the rest of the query
        _contextStack.Push("SELECT");

        if (node.InsertSource != null) node.InsertSource.Accept(this);

        _contextStack.Pop(); // Pop SELECT
        _contextStack.Pop(); // Pop INSERT
    }

    /// <summary>
    /// Visits an UPDATE statement, recording the modification context and traversing the target, SET clauses, FROM clause, and WHERE clause to capture dependency information.
    /// </summary>
    /// <param name="node">The UPDATE specification AST node to visit.</param>
    public override void Visit(UpdateSpecification node)
    {
        _contextStack.Push("UPDATE");

        // The Target is being Modified
        if (node.Target != null) node.Target.Accept(this);

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

        if (node.FromClause != null) node.FromClause.Accept(this);
        if (node.WhereClause != null) node.WhereClause.Accept(this);

        _inFromClause = false;
        _contextStack.Pop(); // Pop SELECT
    }

    /// <summary>
    /// Visit a DELETE statement node to record table and column references with correct modification context.
    /// </summary>
    /// <param name="node">The DELETE specification AST node to inspect; its Target, FromClause, and WhereClause are visited.</param>
    public override void Visit(DeleteSpecification node)
    {
        _contextStack.Push("DELETE");
        
        // Visit target table (being deleted from)
        if (node.Target != null) node.Target.Accept(this);
        
        // Pop DELETE before processing FROM/WHERE as SELECT (matches UPDATE pattern)
        _contextStack.Pop();
        _contextStack.Push("SELECT");
        
        // FROM and WHERE clauses are reads
        _inFromClause = true;
        if (node.FromClause != null) node.FromClause.Accept(this);
        if (node.WhereClause != null) node.WhereClause.Accept(this);
        _inFromClause = false;
        
        _contextStack.Pop(); // Remove SELECT
    }

    /// <summary>
    /// Records the full table name referenced by <paramref name="node"/> and appends it to <see cref="TableReferences"/> with a modification type derived from the current visitation context; references inside FROM clauses or JOINs are recorded with modification type "SELECT".
    /// </summary>
    /// <param name="node">The named table reference being visited.</param>
    public override void Visit(NamedTableReference node)
    {
        string tableName = GetFullTableName(node);

        // Default to the current stack context
        string modificationType = _contextStack.Count > 0 ? _contextStack.Peek() : "SELECT";

        // Override: If we are in a FROM clause or JOIN, it's always a Read (SELECT)
        // unless it is the explicit target of an UPDATE/DELETE (which is handled by visiting Target separately)
        if (_inFromClause || _inJoin)
        {
            modificationType = "SELECT";
        }

        TableReferences.Add((tableName, modificationType));
        base.Visit(node);
    }

    /// <summary>
    /// Temporarily marks traversal as being inside a JOIN so tables encountered while visiting this node are treated as reads.
    /// </summary>
    /// <param name="node">The qualified join node being visited.</param>
    public override void Visit(QualifiedJoin node)
    {
        // Mark that we are inside a join, so any tables found are READs
        bool wasInJoin = _inJoin;
        _inJoin = true;
        base.Visit(node);
        _inJoin = wasInJoin;
    }

    /// <summary>
    /// Marks traversal as occurring inside a JOIN while visiting the given join node, restoring the previous join state after visiting.
    /// </summary>
    /// <param name="node">The unqualified join node to visit.</param>
    public override void Visit(UnqualifiedJoin node)
    {
        bool wasInJoin = _inJoin;
        _inJoin = true;
        base.Visit(node);
        _inJoin = wasInJoin;
    }

    /// <summary>
    /// Visits an EXECUTE specification, extracts the target stored procedure's full name if present, and adds it to <see cref="ProcedureReferences"/>.
    /// </summary>
    /// <param name="node">The EXECUTE specification node to inspect for a procedure reference.</param>
    public override void Visit(ExecuteSpecification node)
    {
        if (node.ExecutableEntity is ExecutableProcedureReference procRef)
        {
            if (procRef.ProcedureReference is ProcedureReferenceName prn &&
                prn.ProcedureReference is ProcedureReference pr &&
                pr.Name != null)
            {
                string procName = GetFullObjectName(pr.Name);
                ProcedureReferences.Add(procName);
            }
        }
        base.Visit(node);
    }

    /// <summary>
    /// Extracts multipart column references from the given ColumnReferenceExpression and records them as (table, column, full identifier) tuples in ColumnReferences.
    /// </summary>
    /// <param name="node">The column reference AST node to examine; only processed when it is a regular column with at least two multipart identifiers.</param>
    /// <remarks>
    /// When multiple identifier parts are present, the last part is recorded as the column name and the immediately preceding part is used as the table name. The full multipart identifier is stored as a dot-separated string. Alias resolution is not performed.
    /// </remarks>
    public override void Visit(ColumnReferenceExpression node)
    {
        if (node.ColumnType == ColumnType.Regular && node.MultiPartIdentifier != null)
        {
            var parts = node.MultiPartIdentifier.Identifiers;
            if (parts.Count >= 2)
            {
                // (1) Last identifier is the column
                var columnName = parts[parts.Count - 1].Value;
                
                // (2) Fallback: use the nearest prior identifier as table name (alias resolution not yet implemented)
                var tableName = parts[parts.Count - 2].Value;

                // (3) Preserve full multipart information
                var fullIdentifier = string.Join(".", parts.Select(p => p.Value));

                // (4) Note: Full alias resolution is optional for future work
                ColumnReferences.Add((tableName, columnName, fullIdentifier));
            }
        }
        base.Visit(node);
    }

    /// <summary>
    /// Builds the full table name from a NamedTableReference, including optional database and schema qualifiers.
    /// </summary>
    /// <param name="node">The table reference to produce a fully qualified name for.</param>
    /// <returns>The fully qualified table name (e.g., "Database.Schema.Table"), or null if the provided node is null or has no schema object.</returns>
    private static string GetFullTableName(NamedTableReference node)
    {
        return GetFullObjectName(node.SchemaObject);
    }

    /// <summary>
    /// Builds the full object name from a SchemaObjectName including optional database and schema qualifiers.
    /// </summary>
    /// <param name="schemaObject">The schema object to format; may include DatabaseIdentifier, SchemaIdentifier, and BaseIdentifier.</param>
    /// <returns>The concatenated name in the form "database.schema.object" with missing qualifiers omitted, or <c>null</c> if <paramref name="schemaObject"/> is null.</returns>
    private static string? GetFullObjectName(SchemaObjectName schemaObject)
    {
        if (schemaObject == null) return null;

        var sb = new StringBuilder();
        
        if (schemaObject.DatabaseIdentifier != null)
        {
            sb.Append(schemaObject.DatabaseIdentifier.Value).Append(".");
        }

        if (schemaObject.SchemaIdentifier != null)
        {
            sb.Append(schemaObject.SchemaIdentifier.Value).Append(".");
        }

        sb.Append(schemaObject.BaseIdentifier.Value);
        
        return sb.ToString();
    }
}