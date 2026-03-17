-- V021: Extension auth PKCE codes and independent token sessions

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExtensionAuthCodes')
BEGIN
    CREATE TABLE ExtensionAuthCodes (
        AuthCodeId          INT IDENTITY(1,1) PRIMARY KEY,
        UserID              INT NOT NULL,
        ClientId            NVARCHAR(200) NOT NULL,
        RedirectUri         NVARCHAR(500) NOT NULL,
        CodeHash            NVARCHAR(255) NOT NULL,
        CodeChallenge       NVARCHAR(255) NOT NULL,
        CodeChallengeMethod NVARCHAR(16) NOT NULL,
        State               NVARCHAR(255) NULL,
        ExpiresAt           DATETIME2 NOT NULL,
        ConsumedAt          DATETIME2 NULL,
        CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_ExtensionAuthCodes_Users FOREIGN KEY (UserID)
            REFERENCES Users(UserID) ON DELETE CASCADE,
        CONSTRAINT UQ_ExtensionAuthCodes_CodeHash UNIQUE (CodeHash)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ExtensionAuthCodes_User_Client'
      AND object_id = OBJECT_ID('ExtensionAuthCodes')
)
BEGIN
    CREATE INDEX IX_ExtensionAuthCodes_User_Client
        ON ExtensionAuthCodes(UserID, ClientId, ExpiresAt);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExtensionTokenSessions')
BEGIN
    CREATE TABLE ExtensionTokenSessions (
        SessionId        INT IDENTITY(1,1) PRIMARY KEY,
        UserID           INT NOT NULL,
        ClientId         NVARCHAR(200) NOT NULL,
        AccessToken      NVARCHAR(255) NOT NULL,
        AccessExpiresAt  DATETIME2 NOT NULL,
        RefreshToken     NVARCHAR(255) NOT NULL,
        RefreshExpiresAt DATETIME2 NOT NULL,
        CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        RevokedAt        DATETIME2 NULL,

        CONSTRAINT FK_ExtensionTokenSessions_Users FOREIGN KEY (UserID)
            REFERENCES Users(UserID) ON DELETE CASCADE,
        CONSTRAINT UQ_ExtensionTokenSessions_AccessToken UNIQUE (AccessToken),
        CONSTRAINT UQ_ExtensionTokenSessions_RefreshToken UNIQUE (RefreshToken)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ExtensionTokenSessions_User_Client'
      AND object_id = OBJECT_ID('ExtensionTokenSessions')
)
BEGIN
    CREATE INDEX IX_ExtensionTokenSessions_User_Client
        ON ExtensionTokenSessions(UserID, ClientId, RevokedAt);
END
