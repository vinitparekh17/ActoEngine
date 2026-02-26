-- ============================================
-- Migration: V013_DetectionMetadata.sql
-- Description: Add detection persistence metadata to LogicalForeignKeys and Projects
-- Date: 2026-02-24
-- ============================================

BEGIN TRY
    BEGIN TRANSACTION;

    -- ── LogicalForeignKeys: detection result columns ──

    ALTER TABLE LogicalForeignKeys
    ADD DetectionReason  NVARCHAR(1000) NULL,
        DiscoveryMethods NVARCHAR(200)  NULL,
        RejectedScore    DECIMAL(5,2)   NULL;

    -- ── Expand DiscoveryMethod CHECK to include auto-detected methods ──

    DECLARE @ConstraintName NVARCHAR(256);
    SELECT @ConstraintName = name
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('LogicalForeignKeys')
      AND definition LIKE '%DiscoveryMethod%';

    IF @ConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE LogicalForeignKeys DROP CONSTRAINT [' + @ConstraintName + ']');
    END

    ALTER TABLE LogicalForeignKeys
    ADD CONSTRAINT CK_LogicalForeignKeys_DiscoveryMethod
    CHECK (DiscoveryMethod IN ('MANUAL', 'NAME_CONVENTION', 'SP_JOIN', 'CORROBORATED'));

    -- ── Projects: detection staleness tracking ──

    ALTER TABLE Projects
    ADD LastDetectionRunAt         DATETIME2    NULL,
        DetectionAlgorithmVersion  NVARCHAR(20) NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
