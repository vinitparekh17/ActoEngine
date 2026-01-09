namespace ActoEngine.WebApi.Features.Context;

/// <summary>
/// SQL queries for Context operations
/// </summary>
public static class ContextQueries
{
    #region Entity Context CRUD

    public const string GetContext = @"
        SELECT 
            ContextId,
            ProjectId,
            EntityType,
            EntityId,
            EntityName,
            Purpose,
            BusinessImpact,
            DataOwner,
            CriticalityLevel,
            BusinessDomain,
            Sensitivity,
            DataSource,
            ValidationRules,
            RetentionPolicy,
            DataFlow,
            Frequency,
            IsDeprecated,
            DeprecationReason,
            ReplacedBy,
            IsContextStale,
            LastReviewedAt,
            ReviewedBy,
            LastContextUpdate,
            ContextUpdatedBy,
            CreatedAt
        FROM EntityContext
        WHERE ProjectId = @ProjectId 
          AND EntityType = @EntityType 
          AND EntityId = @EntityId;";

    public const string GetContextBatchBase = @"
        SELECT 
            ContextId,
            ProjectId,
            EntityType,
            EntityId,
            EntityName,
            Purpose,
            BusinessImpact,
            DataOwner,
            CriticalityLevel,
            BusinessDomain,
            Sensitivity,
            DataSource,
            ValidationRules,
            RetentionPolicy,
            DataFlow,
            Frequency,
            IsDeprecated,
            DeprecationReason,
            ReplacedBy,
            IsContextStale,
            LastReviewedAt,
            ReviewedBy,
            LastContextUpdate,
            ContextUpdatedBy,
            CreatedAt
        FROM EntityContext
        WHERE ProjectId = @ProjectId
        ORDER BY EntityType, EntityId;";

    public const string GetContextByName = @"
        SELECT * FROM EntityContext
        WHERE ProjectId = @ProjectId 
          AND EntityType = @EntityType 
          AND EntityName = @EntityName;";

    public const string InsertContext = @"
        INSERT INTO EntityContext (
            ProjectId, EntityType, EntityId, EntityName,
            Purpose, BusinessImpact, DataOwner, CriticalityLevel,
            BusinessDomain, Sensitivity, DataSource, ValidationRules,
            RetentionPolicy, DataFlow, Frequency, IsDeprecated,
            DeprecationReason, ReplacedBy, LastContextUpdate,
            ContextUpdatedBy, CreatedAt, IsContextStale
        )
        VALUES (
            @ProjectId, @EntityType, @EntityId, @EntityName,
            @Purpose, @BusinessImpact, @DataOwner, @CriticalityLevel,
            @BusinessDomain, @Sensitivity, @DataSource, @ValidationRules,
            @RetentionPolicy, @DataFlow, @Frequency, @IsDeprecated,
            @DeprecationReason, @ReplacedBy, GETUTCDATE(),
            @UserId, GETUTCDATE(), 0
        );

        SELECT * FROM EntityContext
        WHERE ProjectId = @ProjectId
          AND EntityType = @EntityType
          AND EntityId = @EntityId;";

    public const string UpdateContext = @"
        UPDATE EntityContext
        SET Purpose = @Purpose,
            BusinessImpact = @BusinessImpact,
            DataOwner = @DataOwner,
            CriticalityLevel = @CriticalityLevel,
            BusinessDomain = @BusinessDomain,
            Sensitivity = @Sensitivity,
            DataSource = @DataSource,
            ValidationRules = @ValidationRules,
            RetentionPolicy = @RetentionPolicy,
            DataFlow = @DataFlow,
            Frequency = @Frequency,
            IsDeprecated = @IsDeprecated,
            DeprecationReason = @DeprecationReason,
            ReplacedBy = @ReplacedBy,
            LastContextUpdate = GETUTCDATE(),
            ContextUpdatedBy = @UserId,
            IsContextStale = 0
        WHERE ProjectId = @ProjectId
          AND EntityType = @EntityType
          AND EntityId = @EntityId;

        SELECT * FROM EntityContext
        WHERE ProjectId = @ProjectId
          AND EntityType = @EntityType
          AND EntityId = @EntityId;";

    public const string MarkContextStale = @"
        UPDATE EntityContext
        SET IsContextStale = 1
        WHERE ProjectId = @ProjectId 
          AND EntityType = @EntityType 
          AND EntityId = @EntityId;";

    public const string MarkContextFresh = @"
        UPDATE EntityContext
        SET IsContextStale = 0,
            LastReviewedAt = GETUTCDATE(),
            ReviewedBy = @UserId
        WHERE ProjectId = @ProjectId 
          AND EntityType = @EntityType 
          AND EntityId = @EntityId;";

    #endregion

    #region Entity Experts

    public const string GetExpert = @"
        SELECT
            ee.ExpertId,
            ee.ProjectId,
            ee.EntityType,
            ee.EntityId,
            ee.UserId,
            ee.ExpertiseLevel,
            ee.Notes,
            ee.AddedAt,
            ee.AddedBy
        FROM EntityExperts ee
        WHERE ee.ProjectId = @ProjectId
          AND ee.EntityType = @EntityType
          AND ee.EntityId = @EntityId
          AND ee.UserId = @UserId;";

    public const string GetExperts = @"
        SELECT
            ee.ExpertId,
            ee.ProjectId,
            ee.EntityType,
            ee.EntityId,
            ee.UserId,
            ee.ExpertiseLevel,
            ee.Notes,
            ee.AddedAt,
            ee.AddedBy,
            u.UserID,
            u.Username,
            u.FullName
        FROM EntityExperts ee
        JOIN Users u ON ee.UserId = u.UserID
        WHERE ee.ProjectId = @ProjectId
          AND ee.EntityType = @EntityType
          AND ee.EntityId = @EntityId
        ORDER BY
            CASE ee.ExpertiseLevel
                WHEN 'OWNER' THEN 1
                WHEN 'EXPERT' THEN 2
                WHEN 'FAMILIAR' THEN 3
                WHEN 'CONTRIBUTOR' THEN 4
                ELSE 5
            END;";

    public const string InsertExpert = @"
        INSERT INTO EntityExperts (
            ProjectId, EntityType, EntityId, UserId,
            ExpertiseLevel, Notes, AddedBy, AddedAt
        )
        VALUES (
            @ProjectId, @EntityType, @EntityId, @UserId,
            @ExpertiseLevel, @Notes, @AddedBy, GETUTCDATE()
        );";

    public const string UpdateExpert = @"
        UPDATE EntityExperts
        SET ExpertiseLevel = @ExpertiseLevel,
            Notes = @Notes
        WHERE ProjectId = @ProjectId
          AND EntityType = @EntityType
          AND EntityId = @EntityId
          AND UserId = @UserId;";

    public const string RemoveExpert = @"
        DELETE FROM EntityExperts
        WHERE ProjectId = @ProjectId 
          AND EntityType = @EntityType 
          AND EntityId = @EntityId 
          AND UserId = @UserId;";

    public const string GetUserExpertise = @"
        SELECT 
            ee.EntityType,
            ee.EntityId,
            ee.ExpertiseLevel,
            ec.EntityName,
            ec.Purpose,
            ec.BusinessDomain
        FROM EntityExperts ee
        LEFT JOIN EntityContext ec ON 
            ee.ProjectId = ec.ProjectId AND
            ee.EntityType = ec.EntityType AND
            ee.EntityId = ec.EntityId
        WHERE ee.UserId = @UserId 
          AND ee.ProjectId = @ProjectId
        ORDER BY 
            CASE ee.ExpertiseLevel
                WHEN 'OWNER' THEN 1
                WHEN 'EXPERT' THEN 2
                ELSE 3
            END;";

    #endregion

    #region Context History

    public const string RecordContextChange = @"
        INSERT INTO ContextHistory (
            EntityType, EntityId, ProjectId, FieldName,
            OldValue, NewValue, ChangedBy, ChangeReason, ChangedAt
        )
        VALUES (
            @EntityType, @EntityId, @ProjectId, @FieldName,
            @OldValue, @NewValue, @ChangedBy, @ChangeReason, GETUTCDATE()
        );";

    public const string GetContextHistory = @"
        SELECT
            ch.HistoryId,
            ch.ProjectId,
            ch.EntityType,
            ch.EntityId,
            ch.FieldName,
            ch.OldValue,
            ch.NewValue,
            ch.ChangedBy,
            ch.ChangedAt,
            ch.ChangeReason,
            u.Username,
            u.FullName
        FROM ContextHistory ch
        JOIN Users u ON ch.ChangedBy = u.UserID
        WHERE ch.EntityType = @EntityType
          AND ch.EntityId = @EntityId
          AND ch.ProjectId = @ProjectId
        ORDER BY ch.ChangedAt DESC;";

    #endregion

    #region Context Statistics
    public const string GetContextGaps = @"
            SELECT TOP (@Limit)
            'TABLE' as EntityType,
            t.TableId as EntityId,
            t.TableName as EntityName,
            t.CriticalityLevel as Priority,
            'Critical table without documentation' as Reason,
            COUNT(DISTINCT fk.ForeignKeyId) as DependencyCount
        FROM TablesMetadata t
        LEFT JOIN EntityContext ec ON ec.EntityId = t.TableId AND ec.EntityType = 'TABLE'
        LEFT JOIN ForeignKeyMetadata fk ON fk.ReferencedTableId = t.TableId
        WHERE t.ProjectId = @ProjectId
          AND ec.ContextId IS NULL
          AND t.CriticalityLevel >= 3
        GROUP BY t.TableId, t.TableName, t.CriticalityLevel
        ORDER BY t.CriticalityLevel DESC, COUNT(DISTINCT fk.ForeignKeyId) DESC";

    public const string GetContextCoverage = @"
        WITH TableStats AS (
            SELECT 
                'TABLE' as EntityType,
                COUNT(*) as Total,
                SUM(CASE WHEN ec.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
                AVG(CASE 
                    WHEN ec.Purpose IS NOT NULL 
                        AND ec.BusinessDomain IS NOT NULL 
                        AND ec.DataOwner IS NOT NULL THEN 100.0
                    WHEN ec.Purpose IS NOT NULL THEN 60.0
                    ELSE 0.0
                END) as AvgCompleteness
            FROM TablesMetadata tm
            LEFT JOIN EntityContext ec ON 
                ec.ProjectId = tm.ProjectId AND
                ec.EntityType = 'TABLE' AND
                ec.EntityId = tm.TableId
            WHERE tm.ProjectId = @ProjectId
        ),
        ColumnStats AS (
            SELECT 
                'COLUMN' as EntityType,
                COUNT(*) as Total,
                SUM(CASE WHEN ec.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
                AVG(CASE 
                    WHEN ec.Purpose IS NOT NULL 
                        AND ec.Sensitivity IS NOT NULL THEN 100.0
                    WHEN ec.Purpose IS NOT NULL THEN 70.0
                    ELSE 0.0
                END) as AvgCompleteness
            FROM ColumnsMetadata cm
            JOIN TablesMetadata tm ON cm.TableId = tm.TableId
            LEFT JOIN EntityContext ec ON 
                ec.ProjectId = tm.ProjectId AND
                ec.EntityType = 'COLUMN' AND
                ec.EntityId = cm.ColumnId
            WHERE tm.ProjectId = @ProjectId
        ),
        SpStats AS (
            SELECT 
                'SP' as EntityType,
                COUNT(*) as Total,
                SUM(CASE WHEN ec.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
                AVG(CASE 
                    WHEN ec.Purpose IS NOT NULL 
                        AND ec.DataFlow IS NOT NULL THEN 100.0
                    WHEN ec.Purpose IS NOT NULL THEN 60.0
                    ELSE 0.0
                END) as AvgCompleteness
            FROM SpMetadata sm
            LEFT JOIN EntityContext ec ON 
                ec.ProjectId = sm.ProjectId AND
                ec.EntityType = 'SP' AND
                ec.EntityId = sm.SpId
            WHERE sm.ProjectId = @ProjectId
        )
        SELECT * FROM TableStats
        UNION ALL
        SELECT * FROM ColumnStats
        UNION ALL
        SELECT * FROM SpStats;";

    public const string GetStaleContextEntities = @"
        SELECT 
            ec.EntityType,
            ec.EntityId,
            ec.EntityName,
            ec.LastContextUpdate,
            ec.LastReviewedAt,
            DATEDIFF(day, ec.LastContextUpdate, GETUTCDATE()) as DaysSinceUpdate
        FROM EntityContext ec
        WHERE ec.ProjectId = @ProjectId 
          AND ec.IsContextStale = 1
        ORDER BY DaysSinceUpdate DESC;";

    public const string GetTopDocumentedEntities = @"
        SELECT TOP (@Limit)
            ec.EntityType,
            ec.EntityId,
            ec.EntityName,
            ec.Purpose,
            ec.BusinessDomain,
            ec.DataOwner,
            ec.CriticalityLevel,
            CASE 
                WHEN ec.Purpose IS NOT NULL 
                    AND ec.BusinessDomain IS NOT NULL 
                    AND ec.DataOwner IS NOT NULL THEN 100
                WHEN ec.Purpose IS NOT NULL THEN 60
                ELSE 0
            END as CompletenessScore,
            ISNULL(COUNT(ee.ExpertId), 0) as ExpertCount
        FROM EntityContext ec
        LEFT JOIN EntityExperts ee ON 
            ee.EntityType = ec.EntityType 
            AND ee.EntityId = ec.EntityId
            AND ee.ProjectId = @ProjectId
        WHERE ec.ProjectId = @ProjectId 
          AND ec.Purpose IS NOT NULL
        GROUP BY 
            ec.EntityType,
            ec.EntityId,
            ec.EntityName,
            ec.Purpose,
            ec.BusinessDomain,
            ec.DataOwner,
            ec.CriticalityLevel
        ORDER BY CompletenessScore DESC, CriticalityLevel DESC;";

    public const string GetCriticalUndocumented = @"
        -- CTE for FK counts
        WITH FKCounts AS (
            SELECT 
                fk.ReferencedTableId,
                COUNT(*) as ReferenceCount
            FROM ForeignKeyMetadata fk
            GROUP BY fk.ReferencedTableId
        ),
        -- CTE for SP version counts
        SpVersionCounts AS (
            SELECT 
                vh.SpId,
                COUNT(*) as VersionCount
            FROM SpVersionHistory vh
            GROUP BY vh.SpId
        )
        -- Critical tables without context
        SELECT 
            'TABLE' as EntityType,
            tm.TableId as EntityId,
            tm.TableName as EntityName,
            'High usage table without documentation' as Reason,
            ISNULL(fk.ReferenceCount, 0) as ReferenceCount
        FROM TablesMetadata tm
        LEFT JOIN EntityContext ec ON 
            ec.ProjectId = tm.ProjectId AND
            ec.EntityType = 'TABLE' AND
            ec.EntityId = tm.TableId
        LEFT JOIN FKCounts fk ON fk.ReferencedTableId = tm.TableId
        WHERE tm.ProjectId = @ProjectId 
          AND ec.Purpose IS NULL
          AND ISNULL(fk.ReferenceCount, 0) >= 3
        
        UNION ALL
        
        -- Critical SPs without context
        SELECT 
            'SP' as EntityType,
            sm.SpId as EntityId,
            sm.ProcedureName as EntityName,
            'Frequently modified SP without documentation' as Reason,
            ISNULL(sv.VersionCount, 0) as VersionCount
        FROM SpMetadata sm
        LEFT JOIN EntityContext ec ON 
            ec.ProjectId = sm.ProjectId AND
            ec.EntityType = 'SP' AND
            ec.EntityId = sm.SpId
        LEFT JOIN SpVersionCounts sv ON sv.SpId = sm.SpId
        WHERE sm.ProjectId = @ProjectId 
          AND ec.Purpose IS NULL
          AND ISNULL(sv.VersionCount, 0) >= 3
        
        ORDER BY ReferenceCount DESC;";

    #endregion

    #region Review Requests

    public const string CreateReviewRequest = @"
        INSERT INTO ContextReviewRequests (
            ProjectId, EntityType, EntityId, RequestedBy, AssignedTo,
            Reason, Status, CreatedAt
        )
        VALUES (
            @ProjectId, @EntityType, @EntityId, @RequestedBy, @AssignedTo,
            @Reason, 'PENDING', GETUTCDATE()
        );

        SELECT SCOPE_IDENTITY();";

    public const string GetPendingReviewRequests = @"
        SELECT
            crr.*,
            ec.EntityName,
            ec.Purpose,
            u1.Username as RequestedByName,
            u2.Username as AssignedToName
        FROM ContextReviewRequests crr
        LEFT JOIN EntityContext ec ON
            crr.EntityType = ec.EntityType AND
            crr.EntityId = ec.EntityId AND
            crr.ProjectId = ec.ProjectId
        JOIN Users u1 ON crr.RequestedBy = u1.UserID
        LEFT JOIN Users u2 ON crr.AssignedTo = u2.UserID
        WHERE crr.Status = 'PENDING'
          AND (crr.AssignedTo = @UserId OR @UserId IS NULL)
        ORDER BY crr.CreatedAt DESC;";

    public const string CompleteReviewRequest = @"
        UPDATE ContextReviewRequests
        SET Status = 'COMPLETED',
            CompletedAt = GETUTCDATE()
        WHERE RequestId = @RequestId;";

    #endregion

    #region Smart Suggestions

    public const string GetPotentialExperts = @"
        -- Users who recently modified this entity
        WITH RecentModifiers AS (
            SELECT DISTINCT 
                vh.CreatedBy as UserId,
                COUNT(*) as ModificationCount,
                MAX(vh.CreatedAt) as LastModified
            FROM SpVersionHistory vh
            WHERE vh.SpId = @EntityId
              AND @EntityType = 'SP'
            GROUP BY vh.CreatedBy
            
            UNION
            
            SELECT DISTINCT 
                ch.ChangedBy as UserId,
                COUNT(*) as ModificationCount,
                MAX(ch.ChangedAt) as LastModified
            FROM ContextHistory ch
            WHERE ch.EntityId = @EntityId
              AND ch.EntityType = @EntityType
            GROUP BY ch.ChangedBy
        )
        SELECT TOP 5
            u.UserID,
            u.Username,
            u.FullName,
            rm.ModificationCount,
            rm.LastModified,
            'Modified ' + CAST(rm.ModificationCount AS NVARCHAR) + ' times' as Reason
        FROM RecentModifiers rm
        JOIN Users u ON rm.UserId = u.UserID
        ORDER BY rm.ModificationCount DESC, rm.LastModified DESC;";

    #endregion
}