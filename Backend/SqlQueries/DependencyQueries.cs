namespace ActoEngine.WebApi.SqlQueries;

public static class DependencyQueries
{
    public const string DeleteDependencies = @"
        DELETE FROM Dependencies
        WHERE ProjectId = @ProjectId
          AND SourceId IN @SourceIds
          AND SourceType IN @SourceTypes";

    public const string InsertDependency = @"
        INSERT INTO Dependencies (ProjectId, SourceType, SourceId, TargetType, TargetId, DependencyType, ConfidenceScore)
        VALUES (@ProjectId, @SourceType, @SourceId, @TargetType, @TargetId, @DependencyType, @ConfidenceScore)";

    public const string GetDependents = @"
        SELECT * FROM Dependencies 
        WHERE ProjectId = @ProjectId 
          AND TargetType = @TargetType 
          AND TargetId = @TargetId";

    public const string GetDownstreamDependents = @"
        WITH RecursiveDeps AS (
            -- Anchor: Direct dependents of the changed entity
            SELECT 
                d.DependencyId,
                d.ProjectId,
                d.SourceType, -- The dependent (e.g., the SP)
                d.SourceId,
                d.TargetType, -- The dependency (e.g., the Table)
                d.TargetId,
                d.DependencyType,
                1 AS Depth,
                CAST(CONCAT(d.TargetType, ':', d.TargetId, '->', d.SourceType, ':', d.SourceId) AS NVARCHAR(MAX)) AS Path
            FROM Dependencies d
            WHERE d.ProjectId = @ProjectId 
              AND d.TargetType = @RootType 
              AND d.TargetId = @RootId

            UNION ALL

            -- Recursive: Dependents of the dependents
            SELECT 
                d.DependencyId,
                d.ProjectId,
                d.SourceType,
                d.SourceId,
                d.TargetType,
                d.TargetId,
                d.DependencyType,
                r.Depth + 1,
                CAST(CONCAT(r.Path, '->', d.SourceType, ':', d.SourceId) AS NVARCHAR(MAX))
            FROM Dependencies d
            INNER JOIN RecursiveDeps r 
                ON d.TargetType = r.SourceType 
                AND d.TargetId = r.SourceId
            WHERE d.ProjectId = @ProjectId
              AND r.Depth < 10 -- Safety brake for circular dependencies
        )
        SELECT 
            rd.*,
            CASE rd.SourceType
                WHEN 'TABLE' THEN t.TableName
                WHEN 'SP' THEN sp.ProcedureName
                WHEN 'VIEW' THEN v.ViewName
            END as EntityName,
            -- Get Owner/Context info here for scoring
            ctx.DataOwner,
            ctx.CriticalityLevel
        FROM RecursiveDeps rd
        LEFT JOIN TablesMetadata t ON rd.SourceType = 'TABLE' AND rd.SourceId = t.TableId
        LEFT JOIN SpMetadata sp ON rd.SourceType = 'SP' AND rd.SourceId = sp.SpId
        LEFT JOIN ViewsMetadata v ON rd.SourceType = 'VIEW' AND rd.SourceId = v.ViewId
        LEFT JOIN BusinessContext ctx ON rd.SourceType = ctx.EntityType AND rd.SourceId = ctx.EntityId AND rd.ProjectId = ctx.ProjectId
        ORDER BY rd.Depth, rd.SourceType";

    public const string ClearDependencies = "DELETE FROM Dependencies WHERE ProjectId = @ProjectId AND SourceType = @EntityType AND SourceId = @EntityId";
}
