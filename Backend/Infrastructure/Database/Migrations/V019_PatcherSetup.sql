-- V019: Update Patcher Setup
-- Adds project-level patch configuration and patch generation history

-- Step 1: Add patch config columns to Projects
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Projects') AND name = 'ProjectRootPath')
    ALTER TABLE Projects ADD ProjectRootPath NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Projects') AND name = 'ViewDirPath')
    ALTER TABLE Projects ADD ViewDirPath NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Projects') AND name = 'ScriptDirPath')
    ALTER TABLE Projects ADD ScriptDirPath NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Projects') AND name = 'PatchDownloadPath')
    ALTER TABLE Projects ADD PatchDownloadPath NVARCHAR(500) NULL;

-- Step 2: Create PatchHistory table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PatchHistory')
BEGIN
    CREATE TABLE PatchHistory (
        PatchId        INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId      INT NOT NULL FOREIGN KEY REFERENCES Projects(ProjectId),
        PageName       NVARCHAR(200) NOT NULL,
        DomainName     NVARCHAR(200) NOT NULL,
        SpNames        NVARCHAR(MAX) NOT NULL,       -- JSON array of SP names included
        IsNewPage      BIT NOT NULL DEFAULT 0,
        PatchFilePath  NVARCHAR(500) NULL,            -- Full path to generated zip
        GeneratedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        GeneratedBy    INT NULL FOREIGN KEY REFERENCES Users(UserID) ON DELETE SET NULL,
        Status         NVARCHAR(50) NOT NULL DEFAULT 'Generated'  -- Generated | Downloaded | Applied
    );

    CREATE INDEX IX_PatchHistory_Project_Page
        ON PatchHistory(ProjectId, DomainName, PageName);
END
