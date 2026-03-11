-- ============================================
-- Migration: V018_RemoveDescriptionColumns.sql
-- Description: Drop unused Description column from schema metadata tables
-- Date: 2026-03-11
-- ============================================

-- Drop Description from TablesMetadata
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TablesMetadata') AND name = 'Description')
BEGIN
    ALTER TABLE TablesMetadata DROP COLUMN [Description];
END
GO

-- Drop Description from ColumnsMetadata
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ColumnsMetadata') AND name = 'Description')
BEGIN
    ALTER TABLE ColumnsMetadata DROP COLUMN [Description];
END
GO

-- Drop Description from SpMetadata
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SpMetadata') AND name = 'Description')
BEGIN
    ALTER TABLE SpMetadata DROP COLUMN [Description];
END
GO

-- Drop Description from FunctionMetadata
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FunctionMetadata') AND name = 'Description')
BEGIN
    ALTER TABLE FunctionMetadata DROP COLUMN [Description];
END
GO
