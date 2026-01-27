-- ============================================
-- Migration: V008_VwContextLayer.sql
-- Description: Create views for unified context layer and context coverage summary
-- Date: 2025-11-25
-- ============================================

USE ActoEngine;
GO
-- ============================================
-- 1. Unified Context View
-- ============================================
CREATE VIEW vw_UnifiedContext AS
-- Tables
SELECT 
    'TABLE' as EntityType,
    tm.TableId as EntityId,
    tm.TableName as EntityName,
    tm.ProjectId,
    tm.Purpose,
    tm.BusinessDomain,

    tm.CriticalityLevel,
    NULL as DataFlow,
    NULL as Frequency,
    NULL as IsDeprecated,
    tm.RetentionPolicy,
    NULL as Sensitivity,
    tm.LastReviewedAt,
    tm.ReviewedBy,
    NULL as LastContextUpdate,
    NULL as ContextUpdatedBy,
    NULL as IsContextStale,
    tm.ContextCompleteness as CompletenessScore,
    tm.CreatedAt
FROM TablesMetadata tm

UNION ALL

-- Columns
SELECT 
    'COLUMN' as EntityType,
    cm.ColumnId as EntityId,
    cm.ColumnName as EntityName,
    tm.ProjectId,
    cm.Purpose,
    NULL as BusinessDomain,

    NULL as CriticalityLevel,
    NULL as DataFlow,
    NULL as Frequency,
    NULL as IsDeprecated,
    NULL as RetentionPolicy,
    cm.Sensitivity,
    NULL as LastReviewedAt,
    NULL as ReviewedBy,
    cm.LastContextUpdate,
    cm.ContextUpdatedBy,
    cm.IsContextStale,
    CASE 
        WHEN cm.Purpose IS NOT NULL AND cm.Sensitivity IS NOT NULL THEN 100
        WHEN cm.Purpose IS NOT NULL THEN 70
        ELSE 0
    END as CompletenessScore,
    NULL as CreatedAt
FROM ColumnsMetadata cm
JOIN TablesMetadata tm ON cm.TableId = tm.TableId

UNION ALL

-- Stored Procedures
SELECT 
    'SP' as EntityType,
    sm.SpId as EntityId,
    sm.ProcedureName as EntityName,
    sm.ProjectId,
    sm.Purpose,
    NULL as BusinessDomain,

    NULL as CriticalityLevel,
    sm.DataFlow,
    sm.Frequency,
    sm.IsDeprecated,
    NULL as RetentionPolicy,
    NULL as Sensitivity,
    sm.LastReviewedAt,
    sm.ReviewedBy,
    NULL as LastContextUpdate,
    NULL as ContextUpdatedBy,
    NULL as IsContextStale,
    CASE 
        WHEN sm.Purpose IS NOT NULL AND sm.DataFlow IS NOT NULL THEN 100
        WHEN sm.Purpose IS NOT NULL THEN 60
        ELSE 0
    END as CompletenessScore,
    sm.CreatedAt
FROM SpMetadata sm

UNION ALL

-- Functions
SELECT 
    'FUNCTION' as EntityType,
    fm.FunctionId as EntityId,
    fm.FunctionName as EntityName,
    fm.ProjectId,
    fm.Purpose,
    NULL as BusinessDomain,

    NULL as CriticalityLevel,
    fm.DataFlow,
    NULL as Frequency,
    fm.IsDeprecated,
    NULL as RetentionPolicy,
    NULL as Sensitivity,
    fm.LastReviewedAt,
    fm.ReviewedBy,
    NULL as LastContextUpdate,
    NULL as ContextUpdatedBy,
    NULL as IsContextStale,
    CASE 
        WHEN fm.Purpose IS NOT NULL AND fm.DataFlow IS NOT NULL THEN 100
        WHEN fm.Purpose IS NOT NULL THEN 60
        ELSE 0
    END as CompletenessScore,
    fm.CreatedAt
FROM FunctionMetadata fm;
GO

-- ============================================
-- 2. Context Coverage Summary View
-- ============================================
CREATE VIEW vw_ContextCoverage AS
WITH TableStats AS (
    SELECT 
        tm.ProjectId,
        'TABLE' as EntityType,
        COUNT(*) as Total,
        SUM(CASE WHEN tm.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
        AVG(ISNULL(tm.ContextCompleteness, 0)) as AvgCompleteness
    FROM TablesMetadata tm
    GROUP BY tm.ProjectId
),
ColumnStats AS (
    SELECT 
        tm.ProjectId,
        'COLUMN' as EntityType,
        COUNT(*) as Total,
        SUM(CASE WHEN cm.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
        AVG(CASE 
            WHEN cm.Purpose IS NOT NULL AND cm.Sensitivity IS NOT NULL THEN 100.0
            WHEN cm.Purpose IS NOT NULL THEN 70.0
            ELSE 0.0
        END) as AvgCompleteness
    FROM ColumnsMetadata cm
    JOIN TablesMetadata tm ON cm.TableId = tm.TableId
    GROUP BY tm.ProjectId
),
SpStats AS (
    SELECT 
        sm.ProjectId,
        'SP' as EntityType,
        COUNT(*) as Total,
        SUM(CASE WHEN sm.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
        AVG(CASE 
            WHEN sm.Purpose IS NOT NULL AND sm.DataFlow IS NOT NULL THEN 100.0
            WHEN sm.Purpose IS NOT NULL THEN 60.0
            ELSE 0.0
        END) as AvgCompleteness
    FROM SpMetadata sm
    GROUP BY sm.ProjectId
),
FunctionStats AS (
    SELECT 
        fm.ProjectId,
        'FUNCTION' as EntityType,
        COUNT(*) as Total,
        SUM(CASE WHEN fm.Purpose IS NOT NULL THEN 1 ELSE 0 END) as Documented,
        AVG(CASE 
            WHEN fm.Purpose IS NOT NULL AND fm.DataFlow IS NOT NULL THEN 100.0
            WHEN fm.Purpose IS NOT NULL THEN 60.0
            ELSE 0.0
        END) as AvgCompleteness
    FROM FunctionMetadata fm
    GROUP BY fm.ProjectId
)
SELECT ProjectId, EntityType, Total, Documented, 
       CAST(CASE WHEN Total > 0 THEN (Documented * 100.0 / Total) ELSE 0 END AS DECIMAL(5,2)) as CoveragePercentage,
       CAST(ISNULL(AvgCompleteness, 0) AS DECIMAL(5,2)) as AvgCompleteness
FROM TableStats
UNION ALL
SELECT ProjectId, EntityType, Total, Documented, 
       CAST(CASE WHEN Total > 0 THEN (Documented * 100.0 / Total) ELSE 0 END AS DECIMAL(5,2)),
       CAST(ISNULL(AvgCompleteness, 0) AS DECIMAL(5,2))
FROM ColumnStats
UNION ALL
SELECT ProjectId, EntityType, Total, Documented, 
       CAST(CASE WHEN Total > 0 THEN (Documented * 100.0 / Total) ELSE 0 END AS DECIMAL(5,2)),
       CAST(ISNULL(AvgCompleteness, 0) AS DECIMAL(5,2))
FROM SpStats
UNION ALL
SELECT ProjectId, EntityType, Total, Documented, 
       CAST(CASE WHEN Total > 0 THEN (Documented * 100.0 / Total) ELSE 0 END AS DECIMAL(5,2)),
       CAST(ISNULL(AvgCompleteness, 0) AS DECIMAL(5,2))
FROM FunctionStats;
GO

-- ============================================
-- 3. Expert Directory View
-- ============================================
CREATE VIEW vw_ExpertDirectory AS
SELECT 
    ee.ProjectId,
    u.UserID,
    u.Username,
    u.FullName,
    COUNT(DISTINCT CASE WHEN ee.EntityType = 'TABLE' THEN ee.EntityId END) as TablesExpertOn,
    COUNT(DISTINCT CASE WHEN ee.EntityType = 'COLUMN' THEN ee.EntityId END) as ColumnsExpertOn,
    COUNT(DISTINCT CASE WHEN ee.EntityType = 'SP' THEN ee.EntityId END) as SPsExpertOn,
    COUNT(DISTINCT CASE WHEN ee.EntityType = 'FUNCTION' THEN ee.EntityId END) as FunctionsExpertOn,
    COUNT(DISTINCT CASE WHEN ee.ExpertiseLevel = 'OWNER' THEN ee.EntityId END) as OwnedEntities,
    MAX(ee.AddedAt) as LastExpertiseAdded
FROM EntityExperts ee
JOIN Users u ON ee.UserId = u.UserID
GROUP BY ee.ProjectId, u.UserID, u.Username, u.FullName;
GO