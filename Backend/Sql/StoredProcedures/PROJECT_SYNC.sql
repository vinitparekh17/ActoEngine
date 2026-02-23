-- Idempotently create PROJECT_SYNC if it does not exist.
IF OBJECT_ID(N'[dbo].[PROJECT_SYNC]', N'P') IS NULL
BEGIN
    EXEC(N'
        CREATE PROCEDURE [dbo].[PROJECT_SYNC]
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT 1 AS [Status];
        END
    ');
END;
