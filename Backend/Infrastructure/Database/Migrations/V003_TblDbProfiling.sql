-- ============================================
-- Migration: V003_TblDbProfiling.sql
-- Description: Add target database's schema details with additional context fields
-- Date: 2025-10-03
-- ============================================

-- ============================================
-- 1. TABLES METADATA
-- ============================================
CREATE TABLE TablesMetadata (
    TableId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    TableName NVARCHAR(100) NOT NULL,
    SchemaName NVARCHAR(100) NOT NULL DEFAULT 'dbo',
    Description NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),

    -- Context Layer
    Purpose NVARCHAR(MAX),
    BusinessDomain NVARCHAR(100),
    CriticalityLevel INT DEFAULT 3 CHECK (CriticalityLevel BETWEEN 1 AND 5),
    RetentionPolicy NVARCHAR(255),
    LastReviewedAt DATETIME2,
    ReviewedBy INT REFERENCES Users(UserID),
    ContextCompleteness AS (
        CASE 
            WHEN Purpose IS NOT NULL AND BusinessDomain IS NOT NULL THEN 100
            WHEN Purpose IS NOT NULL THEN 60
            ELSE 0
        END
    ) PERSISTED,

    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId)
);

CREATE INDEX IX_TablesMetadata_ProjectId ON TablesMetadata(ProjectId);

-- ============================================
-- 2. COLUMNS METADATA
-- ============================================
CREATE TABLE ColumnsMetadata (
    ColumnId INT PRIMARY KEY IDENTITY(1,1),
    TableId INT NOT NULL,
    ColumnName NVARCHAR(100) NOT NULL,
    DataType NVARCHAR(50) NOT NULL,
    MaxLength INT,
    Precision INT,
    Scale INT,
    IsNullable BIT DEFAULT 1,
    IsPrimaryKey BIT DEFAULT 0,
    IsForeignKey BIT DEFAULT 0,
    DefaultValue NVARCHAR(255),
    Description NVARCHAR(2028),
    ColumnOrder INT,

    -- Context Layer
    Purpose NVARCHAR(MAX),
    BusinessImpact NVARCHAR(MAX),
    ValidationRules NVARCHAR(MAX), -- JSON
    Sensitivity NVARCHAR(50) DEFAULT 'PUBLIC'
        CHECK (Sensitivity IN ('PUBLIC', 'INTERNAL', 'PII', 'FINANCIAL', 'SENSITIVE')),
    DataSource NVARCHAR(500),
    LastContextUpdate DATETIME2,
    ContextUpdatedBy INT REFERENCES Users(UserID),
    IsContextStale BIT DEFAULT 0,

    FOREIGN KEY (TableId) REFERENCES TablesMetadata(TableId)
);

CREATE INDEX IX_ColumnsMetadata_TableId ON ColumnsMetadata(TableId);

-- ============================================
-- 3. FOREIGN KEYS METADATA
-- ============================================
CREATE TABLE ForeignKeyMetadata (
    ForeignKeyId INT PRIMARY KEY IDENTITY(1,1),
    TableId INT NOT NULL,
    ColumnId INT NOT NULL,
    ReferencedTableId INT NOT NULL,
    ReferencedColumnId INT NOT NULL,
    ForeignKeyName NVARCHAR(255),
    OnDeleteAction NVARCHAR(50) DEFAULT 'NO ACTION',
    OnUpdateAction NVARCHAR(50) DEFAULT 'NO ACTION',

    FOREIGN KEY (TableId) REFERENCES TablesMetadata(TableId),
    FOREIGN KEY (ColumnId) REFERENCES ColumnsMetadata(ColumnId),
    FOREIGN KEY (ReferencedTableId) REFERENCES TablesMetadata(TableId),
    FOREIGN KEY (ReferencedColumnId) REFERENCES ColumnsMetadata(ColumnId)
);

CREATE INDEX IX_ForeignKeyMetadata_TableId ON ForeignKeyMetadata(TableId);
CREATE INDEX IX_ForeignKeyMetadata_ReferencedTableId ON ForeignKeyMetadata(ReferencedTableId);

-- ============================================
-- 4. INDEX METADATA
-- ============================================
CREATE TABLE IndexMetadata (
    IndexId INT PRIMARY KEY IDENTITY(1,1),
    TableId INT NOT NULL,
    IndexName NVARCHAR(100) NOT NULL,
    IsUnique BIT DEFAULT 0,
    IsPrimaryKey BIT DEFAULT 0,
    FOREIGN KEY (TableId) REFERENCES TablesMetadata(TableId)
);

CREATE INDEX IX_IndexMetadata_TableId ON IndexMetadata(TableId);


-- 5. INDEX COLUMNS METADATA
CREATE TABLE IndexColumnsMetadata (
    IndexColumnId INT PRIMARY KEY IDENTITY(1,1),
    IndexId INT NOT NULL,
    ColumnId INT NOT NULL,
    ColumnOrder INT,
    FOREIGN KEY (IndexId) REFERENCES IndexMetadata(IndexId),
    FOREIGN KEY (ColumnId) REFERENCES ColumnsMetadata(ColumnId)
);

CREATE INDEX IX_IndexColumnsMetadata_IndexId ON IndexColumnsMetadata(IndexId);
CREATE INDEX IX_IndexColumnsMetadata_ColumnId ON IndexColumnsMetadata(ColumnId);

-- ============================================
-- 6. STORED PROCEDURE METADATA
-- ============================================
CREATE TABLE SpMetadata (
    SpId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    ClientId INT NOT NULL DEFAULT 0,
    SchemaName NVARCHAR(100) NOT NULL DEFAULT 'dbo',
    ProcedureName NVARCHAR(100) NOT NULL,
    Definition NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(50),
    UpdatedAt DATETIME2,
    UpdatedBy NVARCHAR(50),

    -- Context Layer
    Purpose NVARCHAR(MAX),
    DataFlow NVARCHAR(MAX),
    Frequency NVARCHAR(50) CHECK (Frequency IN ('REALTIME','HOURLY','DAILY','BATCH','ADHOC') OR Frequency IS NULL),
    IsDeprecated BIT DEFAULT 0,
    DeprecationReason NVARCHAR(MAX),
    ReplacedBy NVARCHAR(255),
    LastReviewedAt DATETIME2,
    ReviewedBy INT REFERENCES Users(UserID),

    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId),
    FOREIGN KEY (ClientId) REFERENCES Clients(ClientId)
);

CREATE INDEX IX_SpMetadata_ProjectId ON SpMetadata(ProjectId);
CREATE INDEX IX_SpMetadata_ClientId ON SpMetadata(ClientId);

-- ============================================
-- 7. STORED PROCEDURE PARAMS
-- ============================================
CREATE TABLE SpParamMetadata (
    SpParamMetadataId INT PRIMARY KEY IDENTITY(1,1),
    SpId INT NOT NULL,
    ParameterName NVARCHAR(100) NOT NULL,
    DataType NVARCHAR(50) NOT NULL,
    ParamMaxLength INT,
    IsOutput BIT DEFAULT 0,
    ParamOrder INT,
    FOREIGN KEY (SpId) REFERENCES SpMetadata(SpId)
);

CREATE INDEX IX_SpParamMetadata_SpId ON SpParamMetadata(SpId);

-- ============================================
-- 8. FUNCTION METADATA
-- ============================================
CREATE TABLE FunctionMetadata (
    FunctionId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    FunctionName NVARCHAR(100) NOT NULL,
    Definition NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(50),
    UpdatedAt DATETIME2,
    UpdatedBy NVARCHAR(50),

    -- Context Layer
    Purpose NVARCHAR(MAX),
    DataFlow NVARCHAR(MAX),
    IsDeprecated BIT DEFAULT 0,
    DeprecationReason NVARCHAR(MAX),
    ReplacedBy NVARCHAR(255),
    LastReviewedAt DATETIME2,
    ReviewedBy INT REFERENCES Users(UserID),

    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId)
);

CREATE INDEX IX_FunctionMetadata_ProjectId ON FunctionMetadata(ProjectId);

-- ============================================
-- 9. FUNCTION PARAMS
-- ============================================
CREATE TABLE FunctionParamMetadata (
    FunctionParamMetadataId INT PRIMARY KEY IDENTITY(1,1),
    FunctionId INT NOT NULL,
    ParameterName NVARCHAR(100) NOT NULL,
    DataType NVARCHAR(50) NOT NULL,
    ParamMaxLength INT,
    IsOutput BIT DEFAULT 0,
    ParamOrder INT,
    FOREIGN KEY (FunctionId) REFERENCES FunctionMetadata(FunctionId)
);

CREATE INDEX IX_FunctionParamMetadata_FunctionId ON FunctionParamMetadata(FunctionId);

-- ============================================
-- 10. SP VERSION HISTORY
-- ============================================
CREATE TABLE SpVersionHistory (
    SpVersionId INT PRIMARY KEY IDENTITY(1,1),
    SpId INT NOT NULL,
    VersionNumber INT NOT NULL,
    Definition NVARCHAR(MAX) NOT NULL,
    ChangeNotes NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    FOREIGN KEY (SpId) REFERENCES SpMetadata(SpId)
);

CREATE INDEX IX_SpVersionHistory_SpId ON SpVersionHistory(SpId);

-- ============================================
-- 11. FUNCTION VERSION HISTORY
-- ============================================
CREATE TABLE FunctionVersionHistory (
    FunctionVersionId INT PRIMARY KEY IDENTITY(1,1),
    FunctionId INT NOT NULL,
    VersionNumber INT NOT NULL,
    Definition NVARCHAR(MAX) NOT NULL,
    ChangeNotes NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    FOREIGN KEY (FunctionId) REFERENCES FunctionMetadata(FunctionId)
);

CREATE INDEX IX_FunctionVersionHistory_FunctionId ON FunctionVersionHistory(FunctionId);

 
