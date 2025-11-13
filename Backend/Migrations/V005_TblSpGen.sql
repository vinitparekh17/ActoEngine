-- ============================================
-- Migration: V005_TblSpGen.sql
-- Description: Create tables for stored procedure and template generation tracking
-- Date: 2025-10-15
-- ============================================

USE ActoEngine;
GO;

-- ============================================
-- 1. TEMPLATE TYPES
-- ============================================
CREATE TABLE TemplateType (
    TemplateTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TemplateTypeName NVARCHAR(100) NOT NULL UNIQUE, -- e.g., 'StoredProcedure', 'Function', etc.
    Description NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID)
);

CREATE INDEX IX_TemplateType_CreatedAt ON TemplateType(CreatedAt);
CREATE INDEX IX_TemplateType_UpdatedAt ON TemplateType(UpdatedAt);

-- ============================================
-- 2. TEMPLATES
-- ============================================
CREATE TABLE Templates (
    TemplateId INT IDENTITY(1,1) PRIMARY KEY,
    TemplateName NVARCHAR(100), -- 'BasicInsert', 'BasicUpdate', etc.
    TemplateContent NVARCHAR(MAX), -- Template with placeholders
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID)
);

CREATE INDEX IX_Templates_CreatedAt ON Templates(CreatedAt);
CREATE INDEX IX_Templates_UpdatedAt ON Templates(UpdatedAt);

-- ============================================
-- 3. GENERATION HISTORY
-- ============================================
CREATE TABLE GenerationHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProjectId INT REFERENCES Projects(ProjectId),
    TemplateId INT REFERENCES Templates(TemplateId),
    TemplateTypeId INT REFERENCES TemplateType(TemplateTypeId),
    GeneratedAt DATETIME2 DEFAULT GETDATE(),
    GeneratedBy INT REFERENCES Users(UserID),
    Description NVARCHAR(255) -- Optional description of the generation event
);

CREATE INDEX IX_GenerationHistory_ProjectId ON GenerationHistory(ProjectId);
CREATE INDEX IX_GenerationHistory_TemplateId ON GenerationHistory(TemplateId);
CREATE INDEX IX_GenerationHistory_TemplateTypeId ON GenerationHistory(TemplateTypeId);

-- ============================================
-- 4. VALIDATION RULES
-- ============================================
CREATE TABLE ValidationRules (
    RuleId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    RuleName NVARCHAR(100) NOT NULL,
    RuleDefinition NVARCHAR(MAX) NOT NULL, -- JSON/YAML DSL for AND/OR conditions
    AppliesToTableId INT, -- Optional link to a table
    AppliesToColumnId INT, -- Optional link to a column
    Description NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID),
    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId),
    FOREIGN KEY (AppliesToTableId) REFERENCES TablesMetadata(TableId),
    FOREIGN KEY (AppliesToColumnId) REFERENCES ColumnsMetadata(ColumnId)
);

CREATE INDEX IX_ValidationRules_ProjectId ON ValidationRules(ProjectId);
CREATE INDEX IX_ValidationRules_AppliesToTableId ON ValidationRules(AppliesToTableId);
CREATE INDEX IX_ValidationRules_AppliesToColumnId ON ValidationRules(AppliesToColumnId);
CREATE INDEX IX_ValidationRules_CreatedAt ON ValidationRules(CreatedAt);
CREATE INDEX IX_ValidationRules_UpdatedAt ON ValidationRules(UpdatedAt);