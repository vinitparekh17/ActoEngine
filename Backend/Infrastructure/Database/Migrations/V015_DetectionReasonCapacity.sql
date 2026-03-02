-- ============================================
-- Migration: V015_DetectionReasonCapacity.sql
-- Description: Increase LogicalForeignKeys.DetectionReason capacity
-- Date: 2026-03-02
-- ============================================

IF OBJECT_ID('[dbo].[LogicalForeignKeys]', 'U') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[LogicalForeignKeys]
    ALTER COLUMN [DetectionReason] NVARCHAR(4000) NULL;
END