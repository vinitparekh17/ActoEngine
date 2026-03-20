IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'PatchName' AND Object_ID = Object_ID(N'PatchHistory'))
BEGIN
    ALTER TABLE PatchHistory
    ADD PatchName NVARCHAR(100) NULL;
END
