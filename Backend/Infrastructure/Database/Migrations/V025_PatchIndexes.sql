-- V025: Create high-performance patcher indexes

-- 1. Unique index on PatchHistoryPages (prevents duplicates)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PatchHistoryPages_Lookup' AND object_id = OBJECT_ID('PatchHistoryPages'))
    DROP INDEX IX_PatchHistoryPages_Lookup ON PatchHistoryPages;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PatchHistoryPages_Patch_Page' AND object_id = OBJECT_ID('PatchHistoryPages'))
    DROP INDEX UX_PatchHistoryPages_Patch_Page ON PatchHistoryPages;

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PatchHistoryPages')
BEGIN
    CREATE UNIQUE INDEX UX_PatchHistoryPages_Patch_Page
        ON PatchHistoryPages(PatchId, DomainName, PageName);
END

-- 2. Lookup index on PatchHistoryPages for GetLatestPatch
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PatchHistoryPages_Domain_Page_Patch' AND object_id = OBJECT_ID('PatchHistoryPages'))
    DROP INDEX IX_PatchHistoryPages_Domain_Page_Patch ON PatchHistoryPages;

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PatchHistoryPages')
BEGIN
    CREATE INDEX IX_PatchHistoryPages_Domain_Page_Patch
        ON PatchHistoryPages(DomainName, PageName, PatchId);
END

-- 3. History index on PatchHistory for deterministic sort and fast lookup
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PatchHistory_Project_Page' AND object_id = OBJECT_ID('PatchHistory'))
    DROP INDEX IX_PatchHistory_Project_Page ON PatchHistory;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PatchHistory_Project_GeneratedAt_PatchId' AND object_id = OBJECT_ID('PatchHistory'))
    DROP INDEX IX_PatchHistory_Project_GeneratedAt_PatchId ON PatchHistory;

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PatchHistory')
BEGIN
    CREATE INDEX IX_PatchHistory_Project_GeneratedAt_PatchId
        ON PatchHistory(ProjectId, GeneratedAt DESC, PatchId DESC);
END
