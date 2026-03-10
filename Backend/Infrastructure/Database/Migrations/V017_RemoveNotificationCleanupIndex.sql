-- ============================================
-- Migration: V017_RemoveNotificationCleanupIndex.sql
-- Description: Drops the unused IX_Notifications_IsRead_CreatedAt index from the Notifications table.
-- ============================================

BEGIN TRANSACTION;
BEGIN TRY

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Notifications_IsRead_CreatedAt'
          AND object_id = OBJECT_ID(N'dbo.Notifications'))
    BEGIN
        DROP INDEX IX_Notifications_IsRead_CreatedAt ON dbo.Notifications;
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH;
