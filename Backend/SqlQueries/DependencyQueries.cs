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
            -- Depth 1: direct dependents
            SELECT
                d.ProjectId,
        
                d.SourceType AS SourceEntityType,
                d.SourceId   AS SourceEntityId,
        
                d.TargetType AS TargetEntityType,
                d.TargetId   AS TargetEntityId,
        
                d.DependencyType,
                1 AS Depth,
        
                CAST(
                    CONCAT(
                        d.TargetType, ':', d.TargetId,
                        '->',
                        d.SourceType, ':', d.SourceId
                    ) AS NVARCHAR(MAX)
                ) AS PathKey
            FROM Dependencies d
            WHERE d.ProjectId = @ProjectId
              AND d.TargetType = @RootEntityType
              AND d.TargetId = @RootEntityId
        
            UNION ALL
        
            -- Transitive dependents
            SELECT
                d.ProjectId,
        
                d.SourceType AS SourceEntityType,
                d.SourceId   AS SourceEntityId,
        
                d.TargetType AS TargetEntityType,
                d.TargetId   AS TargetEntityId,
        
                d.DependencyType,
                r.Depth + 1,
        
                CAST(
                    CONCAT(
                        r.PathKey,
                        '->',
                        d.SourceType, ':', d.SourceId
                    ) AS NVARCHAR(MAX)
                )
            FROM Dependencies d
            INNER JOIN RecursiveDeps r
                ON d.TargetType = r.SourceEntityType
               AND d.TargetId = r.SourceEntityId
            WHERE d.ProjectId = @ProjectId
              AND r.Depth < 10
              AND r.PathKey NOT LIKE
                  CONCAT('%', d.SourceType, ':', d.SourceId, '%')
        )
        
        SELECT
            rd.SourceEntityType,
            rd.SourceEntityId,
            rd.TargetEntityType,
            rd.TargetEntityId,
            rd.DependencyType,
            rd.Depth,
        
            -- Source Name
            COALESCE(
                ctx.EntityName, 
                CASE 
                    WHEN rd.SourceEntityType = 'TABLE' THEN tms.TableName
                    WHEN rd.SourceEntityType = 'SP' THEN sps.ProcedureName
                    WHEN rd.SourceEntityType = 'FUNCTION' THEN fns.FunctionName
                    ELSE NULL 
                END
            ) AS SourceEntityName,

            -- Target Name
            COALESCE(
                ctxt.EntityName, 
                CASE 
                    WHEN rd.TargetEntityType = 'TABLE' THEN tmt.TableName
                    WHEN rd.TargetEntityType = 'SP' THEN spt.ProcedureName
                    WHEN rd.TargetEntityType = 'FUNCTION' THEN fnt.FunctionName
                    ELSE NULL 
                END
            ) AS TargetEntityName,
            
            ctx.CriticalityLevel AS SourceCriticalityLevel
        FROM RecursiveDeps rd
        
        -- Source Joins
        LEFT JOIN EntityContext ctx
            ON ctx.ProjectId = rd.ProjectId
           AND ctx.EntityType = rd.SourceEntityType
           AND ctx.EntityId = rd.SourceEntityId
        LEFT JOIN TablesMetadata tms
            ON rd.SourceEntityType = 'TABLE' 
           AND tms.ProjectId = rd.ProjectId 
           AND tms.TableId = rd.SourceEntityId
        LEFT JOIN SpMetadata sps
            ON rd.SourceEntityType = 'SP'
           AND sps.ProjectId = rd.ProjectId
           AND sps.SpId = rd.SourceEntityId
        LEFT JOIN FunctionMetadata fns
            ON rd.SourceEntityType = 'FUNCTION'
           AND fns.ProjectId = rd.ProjectId
           AND fns.FunctionId = rd.SourceEntityId

        -- Target Joins
        LEFT JOIN EntityContext ctxt
            ON ctxt.ProjectId = rd.ProjectId
           AND ctxt.EntityType = rd.TargetEntityType
           AND ctxt.EntityId = rd.TargetEntityId
        LEFT JOIN TablesMetadata tmt
            ON rd.TargetEntityType = 'TABLE' 
           AND tmt.ProjectId = rd.ProjectId 
           AND tmt.TableId = rd.TargetEntityId
        LEFT JOIN SpMetadata spt
            ON rd.TargetEntityType = 'SP'
           AND spt.ProjectId = rd.ProjectId
           AND spt.SpId = rd.TargetEntityId
        LEFT JOIN FunctionMetadata fnt
            ON rd.TargetEntityType = 'FUNCTION'
           AND fnt.ProjectId = rd.ProjectId
           AND fnt.FunctionId = rd.TargetEntityId

        ORDER BY rd.Depth;";

    public const string ClearDependencies = "DELETE FROM Dependencies WHERE ProjectId = @ProjectId AND SourceType = @EntityType AND SourceId = @EntityId";
}
