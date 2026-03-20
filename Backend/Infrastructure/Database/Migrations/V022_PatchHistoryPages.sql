-- V022: Add PatchHistoryPages join table for multi-page patch support
-- A single PatchHistory row can now reference N pages

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PatchHistoryPages')
BEGIN
    CREATE TABLE PatchHistoryPages (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        PatchId     INT NOT NULL FOREIGN KEY REFERENCES PatchHistory(PatchId) ON DELETE CASCADE,
        DomainName  NVARCHAR(200) NOT NULL,
        PageName    NVARCHAR(200) NOT NULL,
        IsNewPage   BIT NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_PatchHistoryPages_Lookup
        ON PatchHistoryPages(PatchId, DomainName, PageName);
END
