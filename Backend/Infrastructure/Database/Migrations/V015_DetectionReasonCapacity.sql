-- ============================================
-- Migration: V015_DetectionReasonCapacity.sql
-- Description: Increase LogicalForeignKeys.DetectionReason capacity
-- Date: 2026-03-02
-- ============================================

IF OBJECT_ID('[dbo].[LogicalForeignKeys]', 'U') IS NOT NULL
BEGIN
    DECLARE @DetectionReasonLengthBytes INT = COL_LENGTH('[dbo].[LogicalForeignKeys]', 'DetectionReason');

    -- COL_LENGTH returns bytes:
    -- NVARCHAR(4000) => 8000, NVARCHAR(MAX) => -1
    IF @DetectionReasonLengthBytes IS NOT NULL
       AND @DetectionReasonLengthBytes > 0
       AND @DetectionReasonLengthBytes < 8000
    BEGIN
        ALTER TABLE [dbo].[LogicalForeignKeys]
        ALTER COLUMN [DetectionReason] NVARCHAR(4000) NULL;
    END
END
