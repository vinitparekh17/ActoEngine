-- V026: Add PatchSignature to PatchHistory

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PatchHistory') AND name = 'PatchSignature')
BEGIN
    ALTER TABLE PatchHistory ADD PatchSignature NVARCHAR(200) NULL;
END
