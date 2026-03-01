-- ============================================
-- Migration: V014_RemoveContextReview.sql
-- Description: Remove context review feature
-- Date: 2026-02-28
-- ============================================

-- 1. Drop ContextReviewRequests table
IF OBJECT_ID('[dbo].[ContextReviewRequests]', 'U') IS NOT NULL
    DROP TABLE [dbo].[ContextReviewRequests];
GO

-- 2. Remove review-related columns from EntityContext
-- First drop constraints/indexes if they exist
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EntityContext_Stale' AND object_id = OBJECT_ID('[dbo].[EntityContext]'))
    DROP INDEX IX_EntityContext_Stale ON [dbo].[EntityContext];
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK__EntityCon__Revie__3D5E1FD2') 
    ALTER TABLE [dbo].[EntityContext] DROP CONSTRAINT [FK__EntityCon__Revie__3D5E1FD2]; 
GO

-- Safer way to drop FK by inspecting sys.foreign_keys
DECLARE @EntityContextId int = OBJECT_ID('[dbo].[EntityContext]');
DECLARE @UsersId int = OBJECT_ID('[dbo].[Users]');
DECLARE @ConstraintName nvarchar(200);

SELECT @ConstraintName = name 
FROM sys.foreign_keys 
WHERE parent_object_id = @EntityContextId 
  AND referenced_object_id = @UsersId 
  AND name LIKE '%ReviewedBy%';

IF @ConstraintName IS NOT NULL
BEGIN
    DECLARE @sql1 nvarchar(max) = 'ALTER TABLE [dbo].[EntityContext] DROP CONSTRAINT ' + QUOTENAME(@ConstraintName);
    EXEC(@sql1);
END
GO

-- In case the referenced table/schema differs or name is completely unexpected,
-- drop any FK defined on the ReviewedBy column directly.
DECLARE @EntityContextId2 int = OBJECT_ID('[dbo].[EntityContext]');
DECLARE @fkname nvarchar(200);

SELECT @fkname = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = @EntityContextId2
  AND c.name = 'ReviewedBy';

IF @fkname IS NOT NULL
BEGIN
    DECLARE @sql2 nvarchar(max) = 'ALTER TABLE [dbo].[EntityContext] DROP CONSTRAINT ' + QUOTENAME(@fkname);
    EXEC(@sql2);
END
GO

-- Drop columns
-- Remove any default constraint on IsContextStale before dropping the column
DECLARE @EntityContextId3 int = OBJECT_ID('[dbo].[EntityContext]');
DECLARE @df nvarchar(200);

SELECT @df = name
FROM sys.default_constraints
WHERE parent_object_id = @EntityContextId3
  AND col_name(parent_object_id, parent_column_id) = 'IsContextStale';

IF @df IS NOT NULL
BEGIN
    DECLARE @sql3 nvarchar(max) = 'ALTER TABLE [dbo].[EntityContext] DROP CONSTRAINT ' + QUOTENAME(@df);
    EXEC(@sql3);
END
GO

IF COL_LENGTH('[dbo].[EntityContext]', 'IsContextStale') IS NOT NULL
    ALTER TABLE [dbo].[EntityContext] DROP COLUMN [IsContextStale];
GO

IF COL_LENGTH('[dbo].[EntityContext]', 'LastReviewedAt') IS NOT NULL
    ALTER TABLE [dbo].[EntityContext] DROP COLUMN [LastReviewedAt];
GO

IF COL_LENGTH('[dbo].[EntityContext]', 'ReviewedBy') IS NOT NULL
    ALTER TABLE [dbo].[EntityContext] DROP COLUMN [ReviewedBy];
GO
