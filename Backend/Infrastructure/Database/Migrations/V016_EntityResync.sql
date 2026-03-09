-- ============================================
-- Migration: V016_EntityResync.sql
-- Description: Add columns for entity-level resync (soft deletes, hash detection) and Notifications table.
-- Date: 2026-03-08
-- ============================================

BEGIN TRANSACTION;
BEGIN TRY

    -- 1. Add Soft Delete columns to TablesMetadata (column-by-column idempotency)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsDeleted' AND Object_ID = Object_ID(N'dbo.TablesMetadata'))
    BEGIN
        ALTER TABLE TablesMetadata
        ADD IsDeleted BIT NOT NULL DEFAULT 0;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeletedAt' AND Object_ID = Object_ID(N'dbo.TablesMetadata'))
    BEGIN
        ALTER TABLE TablesMetadata
        ADD DeletedAt DATETIME2 NULL;
    END

    -- 2. Add Hash and Soft Delete columns to SpMetadata (column-by-column idempotency)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DefinitionHash' AND Object_ID = Object_ID(N'dbo.SpMetadata'))
    BEGIN
        ALTER TABLE SpMetadata
        ADD DefinitionHash VARCHAR(64) NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SourceModifyDate' AND Object_ID = Object_ID(N'dbo.SpMetadata'))
    BEGIN
        ALTER TABLE SpMetadata
        ADD SourceModifyDate DATETIME2 NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsDeleted' AND Object_ID = Object_ID(N'dbo.SpMetadata'))
    BEGIN
        ALTER TABLE SpMetadata
        ADD IsDeleted BIT NOT NULL DEFAULT 0;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeletedAt' AND Object_ID = Object_ID(N'dbo.SpMetadata'))
    BEGIN
        ALTER TABLE SpMetadata
        ADD DeletedAt DATETIME2 NULL;
    END

    IF EXISTS (SELECT 1 FROM sys.columns
               WHERE Name = N'SourceModifyDate'
                 AND Object_ID = Object_ID(N'dbo.SpMetadata'))
    BEGIN
        UPDATE SpMetadata
        SET SourceModifyDate = GETUTCDATE()
        WHERE SourceModifyDate IS NULL;

        ALTER TABLE SpMetadata
        ALTER COLUMN SourceModifyDate DATETIME2 NOT NULL;
    END

    -- 3. Create Notifications table
    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Notifications]') AND type in (N'U'))
    BEGIN
        CREATE TABLE Notifications (
            NotificationId INT PRIMARY KEY IDENTITY(1,1),
            UserId INT NOT NULL,
            ProjectId INT NULL, 
            Type NVARCHAR(50) NOT NULL,
            Title NVARCHAR(200) NOT NULL,
            Message NVARCHAR(500) NOT NULL,
            IsRead BIT NOT NULL DEFAULT 0,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            ReadAt DATETIME2 NULL,

            CONSTRAINT FK_Notifications_UserId FOREIGN KEY (UserId) REFERENCES Users(UserID) ON DELETE CASCADE,
            CONSTRAINT FK_Notifications_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE
        );

        -- Index for fast user polling (unread first)
        CREATE INDEX IX_Notifications_UserId_IsRead ON Notifications(UserId, IsRead, CreatedAt DESC);
        -- Index for cleanup jobs
        CREATE INDEX IX_Notifications_IsRead_CreatedAt ON Notifications(IsRead, CreatedAt);
    END

    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Notifications]') AND type in (N'U'))
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Notifications_UserId')
        BEGIN
            ALTER TABLE Notifications DROP CONSTRAINT FK_Notifications_UserId;
        END

        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Notifications_ProjectId')
        BEGIN
            ALTER TABLE Notifications DROP CONSTRAINT FK_Notifications_ProjectId;
        END

        ALTER TABLE Notifications
            ADD CONSTRAINT FK_Notifications_UserId FOREIGN KEY (UserId) REFERENCES Users(UserID) ON DELETE CASCADE;

        ALTER TABLE Notifications
            ADD CONSTRAINT FK_Notifications_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_Notifications_UserId_IsRead'
              AND object_id = OBJECT_ID(N'dbo.Notifications'))
        BEGIN
            CREATE INDEX IX_Notifications_UserId_IsRead ON Notifications(UserId, IsRead, CreatedAt DESC);
        END

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_Notifications_IsRead_CreatedAt'
              AND object_id = OBJECT_ID(N'dbo.Notifications'))
        BEGIN
            CREATE INDEX IX_Notifications_IsRead_CreatedAt ON Notifications(IsRead, CreatedAt);
        END
    END
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH;
