/*
  V027: Snippet Library tables with Full-Text Search and quality signals.
  
  FIX NOTES:
  1. Corrected logic error where IX_Snippets_UpdatedBy check was guarding IX_SnippetFavorites_SnippetId.
  2. Fixed duplicate check on SnippetTags (the first block was checking for SnippetTags but creating Snippets).
  3. Standardized index guards to ensure the check name matches the creation name.
*/

-- 1. Create Snippets Table
IF OBJECT_ID('dbo.Snippets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Snippets (
        SnippetId       INT NOT NULL IDENTITY(1,1),
        Title           NVARCHAR(200)  NOT NULL,
        Description     NVARCHAR(500)  NULL,
        Code            NVARCHAR(MAX)  NOT NULL,
        Language        NVARCHAR(50)   NOT NULL,
        Notes           NVARCHAR(MAX)  NULL,
        CopyCount       INT            NOT NULL DEFAULT 0,
        CreatedBy       INT            NOT NULL,
        UpdatedBy       INT            NULL,
        CreatedAt       DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt       DATETIME2      NULL,
        IsActive        BIT            NOT NULL DEFAULT 1,
        CONSTRAINT PK_Snippets PRIMARY KEY (SnippetId),
        CONSTRAINT CK_Snippets_Code_NotBlank CHECK (LEN(LTRIM(RTRIM(Code))) > 0),
        CONSTRAINT FK_Snippets_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserID),
        CONSTRAINT FK_Snippets_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES Users(UserID)
    );
END

-- 2. Create SnippetTags Table
IF OBJECT_ID('dbo.SnippetTags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SnippetTags (
        SnippetTagId    INT PRIMARY KEY IDENTITY(1,1),
        SnippetId       INT            NOT NULL,
        TagName         NVARCHAR(50)   NOT NULL,
        CONSTRAINT FK_SnippetTags_SnippetId FOREIGN KEY (SnippetId) REFERENCES Snippets(SnippetId) ON DELETE CASCADE
    );
END

-- 3. Create SnippetFavorites Table
IF OBJECT_ID('dbo.SnippetFavorites', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SnippetFavorites (
        SnippetFavoriteId  INT PRIMARY KEY IDENTITY(1,1),
        SnippetId          INT NOT NULL,
        UserId             INT NOT NULL,
        CreatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_SnippetFavorites_SnippetId FOREIGN KEY (SnippetId) REFERENCES Snippets(SnippetId) ON DELETE CASCADE,
        CONSTRAINT FK_SnippetFavorites_UserId FOREIGN KEY (UserId) REFERENCES Users(UserID),
        CONSTRAINT UQ_SnippetFavorites_Snippet_User UNIQUE (SnippetId, UserId)
    );
END

-- --- INDEXES ---

-- SnippetTags Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SnippetTags_SnippetId' AND object_id = OBJECT_ID('dbo.SnippetTags'))
BEGIN
    CREATE INDEX IX_SnippetTags_SnippetId ON dbo.SnippetTags(SnippetId);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SnippetTags_TagName' AND object_id = OBJECT_ID('dbo.SnippetTags'))
BEGIN
    CREATE INDEX IX_SnippetTags_TagName ON dbo.SnippetTags(TagName);
END

-- Snippets Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Snippets_Language' AND object_id = OBJECT_ID('dbo.Snippets'))
BEGIN
    CREATE INDEX IX_Snippets_Language ON dbo.Snippets(Language) WHERE IsActive = 1;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Snippets_CreatedBy' AND object_id = OBJECT_ID('dbo.Snippets'))
BEGIN
    CREATE INDEX IX_Snippets_CreatedBy ON dbo.Snippets(CreatedBy) WHERE IsActive = 1;
END

-- FIXED: Was checking for UpdatedBy but creating SnippetId index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Snippets_UpdatedBy' AND object_id = OBJECT_ID('dbo.Snippets'))
BEGIN
    CREATE INDEX IX_Snippets_UpdatedBy ON dbo.Snippets(UpdatedBy) WHERE IsActive = 1 AND UpdatedBy IS NOT NULL;
END

-- SnippetFavorites Indexes
-- FIXED: Guard name now matches Index name
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SnippetFavorites_SnippetId' AND object_id = OBJECT_ID('dbo.SnippetFavorites'))
BEGIN
    CREATE INDEX IX_SnippetFavorites_SnippetId ON dbo.SnippetFavorites(SnippetId);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SnippetFavorites_UserId' AND object_id = OBJECT_ID('dbo.SnippetFavorites'))
BEGIN
    CREATE INDEX IX_SnippetFavorites_UserId ON dbo.SnippetFavorites(UserId);
END

-- --- FULL-TEXT SEARCH ---

-- Full-Text Search on Title + Description
--IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'SnippetFTCatalog')
--BEGIN
--    CREATE FULLTEXT CATALOG SnippetFTCatalog;
--END

--IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('dbo.Snippets'))
--BEGIN
--    CREATE FULLTEXT INDEX ON dbo.Snippets(Title, Description)
--        KEY INDEX PK_Snippets
--        ON SnippetFTCatalog
--        WITH CHANGE_TRACKING AUTO;
--END