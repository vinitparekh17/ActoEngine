-- ============================================
-- Migration: V001_CheckAndModifyDb.sql
-- Description: Check and modify database properties
-- Date: 2025-11-01
-- ============================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ActoEngine')
BEGIN
    ALTER DATABASE [ActoEngine] COLLATE SQL_Latin1_General_CP1_CI_AS;
END