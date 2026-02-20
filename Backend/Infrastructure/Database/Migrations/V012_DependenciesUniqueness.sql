-- ============================================
-- Migration: V012_DependenciesUniqueness.sql
-- Description: Enforce dependency uniqueness and remove pre-existing duplicates
-- Date: 2026-02-19
-- ============================================

IF OBJECT_ID('dbo.Dependencies', 'U') IS NULL
BEGIN
    RETURN;
END

BEGIN TRY
    BEGIN TRAN;

    ;WITH RankedDependencies AS (
        SELECT
            DependencyId,
            ROW_NUMBER() OVER (
                PARTITION BY ProjectId, SourceType, SourceId, TargetType, TargetId, DependencyType
                ORDER BY ConfidenceScore DESC, DependencyId DESC
            ) AS RowNum
        FROM dbo.Dependencies
    )
    DELETE d
    FROM dbo.Dependencies d
    INNER JOIN RankedDependencies rd ON rd.DependencyId = d.DependencyId
    WHERE rd.RowNum > 1;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_Dependencies_Uniqueness'
          AND object_id = OBJECT_ID('dbo.Dependencies')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_Dependencies_Uniqueness
            ON dbo.Dependencies (
                ProjectId,
                SourceType,
                SourceId,
                TargetType,
                TargetId,
                DependencyType
            );
    END

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH
