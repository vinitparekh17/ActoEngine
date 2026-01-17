-- Setup app_user for ActoEngine database
-- This script is idempotent and can be safely re-run

USE master;
GO

-- Create or update login
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'app_user')
BEGIN
    CREATE LOGIN app_user WITH PASSWORD = '$(DB_PASSWORD)';
    PRINT 'Created login app_user';
END
ELSE
BEGIN
    -- Update password if login exists (supports password rotation)
    ALTER LOGIN app_user WITH PASSWORD = '$(DB_PASSWORD)';
    PRINT 'Updated password for login app_user';
END
GO

USE [$(DB_NAME)];
GO

-- Create user if not exists
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'app_user')
BEGIN
    CREATE USER app_user FOR LOGIN app_user;
    PRINT 'Created user app_user';
END
GO

-- Grant roles idempotently (check membership before adding)
-- db_datareader: SELECT on all tables
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_datareader' AND m.name = 'app_user'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER app_user;
    PRINT 'Added app_user to db_datareader';
END
GO

-- db_datawriter: INSERT, UPDATE, DELETE on all tables
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_datawriter' AND m.name = 'app_user'
)
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER app_user;
    PRINT 'Added app_user to db_datawriter';
END
GO

-- Grant specific schema permissions instead of db_ddladmin (least privilege)
-- These are needed for EF Core migrations if app_user runs them
-- Note: For production, consider running migrations with a separate migration account
GRANT CREATE TABLE TO app_user;
GRANT CREATE PROCEDURE TO app_user;
GRANT CREATE VIEW TO app_user;
GRANT CREATE FUNCTION TO app_user;
GRANT ALTER ON SCHEMA::dbo TO app_user;
GRANT REFERENCES ON SCHEMA::dbo TO app_user;
GO

PRINT 'Database user setup completed successfully';
GO
