using ActoEngine.WebApi.Infrastructure.Database;

namespace ActoEngine.WebApi.Features.ErDiagram;

/// <summary>
/// Service contract for ER diagram data assembly
/// </summary>
public interface IErDiagramService
{
    Task<ErDiagramResponse?> GetNeighborhoodAsync(int projectId, int focusTableId, int hops, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assembles ER diagram data by combining physical FK + logical FK relationships
/// into a neighborhood graph around a focus table.
/// </summary>
public class ErDiagramService(
    IErDiagramRepository repository,
    ILogger<ErDiagramService> logger) : IErDiagramService
{
    /// <summary>
    /// Build a neighborhood graph: start at focusTableId, follow FK edges up to N hops.
    /// </summary>
    public async Task<ErDiagramResponse?> GetNeighborhoodAsync(
        int projectId, int focusTableId, int hops, CancellationToken cancellationToken = default)
    {
        var boundedHops = Math.Clamp(hops, 0, 10);

        // Verify focus table exists and belongs to the project
        var focusTable = await repository.GetTableInfoAsync(projectId, focusTableId, cancellationToken);

        if (focusTable == null) return null;

        // Track visited tables and their depth
        var visited = new Dictionary<int, int> { { focusTableId, 0 } };
        var frontier = new HashSet<int> { focusTableId };

        // Get ALL physical FK edges for this project (efficient single query)
        var physicalEdgesTask = repository.GetPhysicalFksAsync(projectId, cancellationToken);
        var logicalEdgesTask = repository.GetLogicalFksAsync(projectId, cancellationToken);
        await Task.WhenAll(physicalEdgesTask, logicalEdgesTask);
        var physicalEdges = await physicalEdgesTask;
        var logicalEdges = await logicalEdgesTask;

        // BFS expansion: walk FK edges to discover neighbors
        for (int depth = 1; depth <= boundedHops; depth++)
        {
            var nextFrontier = new HashSet<int>();

            foreach (var tableId in frontier)
            {
                // Physical FK neighbors (both directions)
                var physicalNeighbors = physicalEdges
                    .Where(e => e.SourceTableId == tableId || e.TargetTableId == tableId)
                    .SelectMany(e => new[] { e.SourceTableId, e.TargetTableId })
                    .Where(id => !visited.ContainsKey(id));

                foreach (var neighbor in physicalNeighbors)
                {
                    if (visited.TryAdd(neighbor, depth))
                        nextFrontier.Add(neighbor);
                }

                // Logical FK neighbors (both directions)
                var logicalNeighbors = logicalEdges
                    .Where(e => e.SourceTableId == tableId || e.TargetTableId == tableId)
                    .SelectMany(e => new[] { e.SourceTableId, e.TargetTableId })
                    .Where(id => !visited.ContainsKey(id));

                foreach (var neighbor in logicalNeighbors)
                {
                    if (visited.TryAdd(neighbor, depth))
                        nextFrontier.Add(neighbor);
                }
            }

            frontier = nextFrontier;
            if (frontier.Count == 0) break;
        }

        var tableIds = visited.Keys.ToList();

        // Load table + column data for all visited tables
        var tablesTask = repository.GetTablesByIdsAsync(tableIds, cancellationToken);
        var columnsTask = repository.GetColumnsByTableIdsAsync(tableIds, cancellationToken);
        await Task.WhenAll(tablesTask, columnsTask);
        var tables = await tablesTask;
        var columns = await columnsTask;

        var columnsByTable = columns.GroupBy(c => c.TableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build nodes
        var nodes = tables.Select(t => new ErNode
        {
            TableId = t.TableId,
            TableName = t.TableName,
            SchemaName = t.SchemaName,
            Depth = visited.GetValueOrDefault(t.TableId, 0),
            Columns = [.. columnsByTable.GetValueOrDefault(t.TableId, [])
                .Select(c => new ErColumn
                {
                    ColumnId = c.ColumnId,
                    ColumnName = c.ColumnName,
                    DataType = c.DataType,
                    IsPrimaryKey = c.IsPrimaryKey,
                    IsForeignKey = c.IsForeignKey,
                    IsNullable = c.IsNullable
                })]
        }).ToList();

        // Build edges: only include edges where both endpoints are in our visited set

        var edges = new List<ErEdge>();

        foreach (var edge in physicalEdges)
        {
            if (!visited.ContainsKey(edge.SourceTableId) || !visited.ContainsKey(edge.TargetTableId))
                continue;

            edges.Add(new ErEdge
            {
                Id = $"phys-{edge.SourceTableId}-{edge.SourceColumnId}-{edge.TargetTableId}-{edge.TargetColumnId}",
                SourceTableId = edge.SourceTableId,
                SourceColumnId = edge.SourceColumnId,
                SourceColumnName = edge.SourceColumnName,
                TargetTableId = edge.TargetTableId,
                TargetColumnId = edge.TargetColumnId,
                TargetColumnName = edge.TargetColumnName,
                RelationshipType = "PHYSICAL"
            });
        }

        foreach (var edge in logicalEdges)
        {
            if (!visited.ContainsKey(edge.SourceTableId) || !visited.ContainsKey(edge.TargetTableId))
                continue;

            edges.Add(new ErEdge
            {
                Id = $"logical-{edge.LogicalForeignKeyId}",
                SourceTableId = edge.SourceTableId,
                SourceColumnId = edge.SourceColumnId,
                SourceColumnName = edge.SourceColumnName,
                TargetTableId = edge.TargetTableId,
                TargetColumnId = edge.TargetColumnId,
                TargetColumnName = edge.TargetColumnName,
                RelationshipType = "LOGICAL",
                Status = edge.Status,
                ConfidenceScore = edge.ConfidenceScore,
                LogicalFkId = edge.LogicalForeignKeyId,
                DiscoveryMethod = edge.DiscoveryMethod,
                ConfirmedAt = edge.ConfirmedAt,
                ConfirmedBy = edge.ConfirmedBy,
                CreatedAt = edge.CreatedAt
            });
        }

        logger.LogInformation(
            "ER diagram for table {FocusTableId}: {NodeCount} nodes, {EdgeCount} edges ({Hops} hops)",
            focusTableId, nodes.Count, edges.Count, boundedHops);

        return new ErDiagramResponse
        {
            FocusTableId = focusTableId,
            Nodes = nodes,
            Edges = edges
        };
    }
}


