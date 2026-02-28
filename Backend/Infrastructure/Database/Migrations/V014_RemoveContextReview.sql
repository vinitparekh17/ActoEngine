-- ============================================
-- Migration: V014_RemoveContextReview.sql
-- Description: Remove context review feature
-- Date: 2026-02-28
-- ============================================

-- 1. Drop ContextReviewRequests table
IF OBJECT_ID('ContextReviewRequests', 'U') IS NOT NULL
    DROP TABLE ContextReviewRequests;
GO

-- 2. Remove review-related columns from EntityContext
-- First drop constraints/indexes if they exist
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EntityContext_Stale' AND object_id = OBJECT_ID('EntityContext'))
    DROP INDEX IX_EntityContext_Stale ON EntityContext;
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK__EntityCon__Revie__3D5E1FD2') -- Check migration V007 for exact name if possible, or use dynamic SQL
    ALTER TABLE EntityContext DROP CONSTRAINT FK__EntityCon__Revie__3D5E1FD2; 
GO

-- Safer way to drop FK by inspecting sys.foreign_keys
DECLARE @ConstraintName nvarchar(200)
SELECT @ConstraintName = name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('EntityContext') AND referenced_object_id = OBJECT_ID('Users') AND name LIKE '%ReviewedBy%'
IF @ConstraintName IS NOT NULL
    EXEC('ALTER TABLE EntityContext DROP CONSTRAINT ' + @ConstraintName);
GO

-- In case the referenced table/schema differs or name is completely unexpected,
-- drop any FK defined on the ReviewedBy column directly.
DECLARE @fkname nvarchar(200);
SELECT @fkname = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID('EntityContext')
  AND c.name = 'ReviewedBy';
IF @fkname IS NOT NULL
    EXEC('ALTER TABLE EntityContext DROP CONSTRAINT ' + @fkname);
GO

-- Drop columns
-- Remove any default constraint on IsContextStale before dropping the column
DECLARE @df nvarchar(200);
SELECT @df = name
FROM sys.default_constraints
WHERE parent_object_id = OBJECT_ID('EntityContext')
  AND col_name(parent_object_id, parent_column_id) = 'IsContextStale';
IF @df IS NOT NULL
    EXEC('ALTER TABLE EntityContext DROP CONSTRAINT ' + @df);
GO

IF COL_LENGTH('EntityContext', 'IsContextStale') IS NOT NULL
    ALTER TABLE EntityContext DROP COLUMN IsContextStale;
GO

IF COL_LENGTH('EntityContext', 'LastReviewedAt') IS NOT NULL
    ALTER TABLE EntityContext DROP COLUMN LastReviewedAt;
GO

IF COL_LENGTH('EntityContext', 'ReviewedBy') IS NOT NULL
    ALTER TABLE EntityContext DROP COLUMN ReviewedBy;
GO
