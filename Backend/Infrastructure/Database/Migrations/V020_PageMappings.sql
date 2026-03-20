-- V020: PageMappings for extension detection review workflow
-- Stores page->stored procedure mappings with review status and shared/page-specific semantics

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PageMappings')
BEGIN
    CREATE TABLE PageMappings (
        MappingId         INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId         INT NOT NULL,
        DomainName        NVARCHAR(255) NOT NULL,
        PageName          NVARCHAR(255) NOT NULL,
        StoredProcedure   NVARCHAR(255) NOT NULL,
        Confidence        FLOAT NULL,
        Source            NVARCHAR(32) NOT NULL
            CONSTRAINT CK_PageMappings_Source CHECK (Source IN ('extension', 'sql-analysis', 'manual')),
        Status            NVARCHAR(16) NOT NULL
            CONSTRAINT DF_PageMappings_Status DEFAULT 'candidate'
            CONSTRAINT CK_PageMappings_Status CHECK (Status IN ('candidate', 'approved', 'ignored')),
        MappingType       NVARCHAR(16) NOT NULL
            CONSTRAINT DF_PageMappings_MappingType DEFAULT 'page_specific'
            CONSTRAINT CK_PageMappings_MappingType CHECK (MappingType IN ('page_specific', 'shared')),
        CreatedAt         DATETIME2 NOT NULL
            CONSTRAINT DF_PageMappings_CreatedAt DEFAULT GETUTCDATE(),
        UpdatedAt         DATETIME2 NOT NULL
            CONSTRAINT DF_PageMappings_UpdatedAt DEFAULT GETUTCDATE(),
        ReviewedBy        INT NULL,
        ReviewedAt        DATETIME2 NULL,

        CONSTRAINT FK_PageMappings_Projects FOREIGN KEY (ProjectId)
            REFERENCES Projects(ProjectId) ON DELETE CASCADE,
        CONSTRAINT FK_PageMappings_Users_ReviewedBy FOREIGN KEY (ReviewedBy)
            REFERENCES Users(UserID),
        CONSTRAINT UQ_PageMappings
            UNIQUE (ProjectId, DomainName, PageName, StoredProcedure, Source)
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PageMappings_Project_Status'
      AND object_id = OBJECT_ID('PageMappings')
)
BEGIN
    CREATE INDEX IX_PageMappings_Project_Status
        ON PageMappings (ProjectId, Status);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PageMappings_Project_Page_Status'
      AND object_id = OBJECT_ID('PageMappings')
)
BEGIN
    CREATE INDEX IX_PageMappings_Project_Page_Status
        ON PageMappings (ProjectId, DomainName, PageName, Status);
END
