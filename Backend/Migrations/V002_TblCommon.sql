
-- ============================================
-- Migration: V002_TblCommon.sql
-- Description: Create common tables for users, projects, and clients
-- Date: 2025-10-15
-- ============================================

USE ActoEngine;
GO

-- ============================================
-- 1. ROLES TABLE
-- ============================================
CREATE TABLE Roles (
    RoleId INT PRIMARY KEY IDENTITY(1,1),
    RoleName NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(255),
    IsSystem BIT DEFAULT 0,  -- System roles cannot be deleted
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT, -- Circular dependency if FK to Users, so leaving as INT for now or handling later. Original V009 had FK to Users.
    UpdatedAt DATETIME2,
    UpdatedBy INT
);

CREATE INDEX IX_Roles_RoleName ON Roles(RoleName);
CREATE INDEX IX_Roles_IsActive ON Roles(IsActive);

-- ============================================
-- 2. PERMISSIONS TABLE
-- ============================================
CREATE TABLE Permissions (
    PermissionId INT PRIMARY KEY IDENTITY(1,1),
    PermissionKey NVARCHAR(100) NOT NULL UNIQUE,
    Resource NVARCHAR(50) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    Description NVARCHAR(255),
    Category NVARCHAR(50),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE()
);

CREATE INDEX IX_Permissions_Resource ON Permissions(Resource);
CREATE INDEX IX_Permissions_PermissionKey ON Permissions(PermissionKey);
CREATE UNIQUE INDEX UQ_Permissions_Resource_Action ON Permissions(Resource, Action);

-- ============================================
-- 3. ROLEPERMISSIONS JUNCTION TABLE
-- ============================================
CREATE TABLE RolePermissions (
    RolePermissionId INT PRIMARY KEY IDENTITY(1,1),
    RoleId INT NOT NULL,
    PermissionId INT NOT NULL,
    GrantedAt DATETIME2 DEFAULT GETDATE(),
    GrantedBy INT,
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId)
        REFERENCES Roles(RoleId) ON DELETE CASCADE,
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId)
        REFERENCES Permissions(PermissionId) ON DELETE CASCADE,
    CONSTRAINT UQ_RolePermissions UNIQUE (RoleId, PermissionId)
);

CREATE INDEX IX_RolePermissions_RoleId ON RolePermissions(RoleId);
CREATE INDEX IX_RolePermissions_PermissionId ON RolePermissions(PermissionId);

-- ============================================
-- 4. USERS TABLE
-- ============================================
CREATE TABLE Users (
    UserID INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100),
    IsActive BIT DEFAULT 1,
    Role NVARCHAR(50) DEFAULT 'User', -- Kept for backward compatibility as per V009 notes, or should we remove? V009 said "Keep existing Role column".
    RoleId INT NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(50),
    UpdatedAt DATETIME2,
    UpdatedBy NVARCHAR(50),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE INDEX IX_Users_RoleId ON Users(RoleId);

CREATE INDEX IX_Users_Fullname ON Users(FullName);

-- ============================================
-- 2. TOKEN SESSIONS TABLE
-- ============================================
CREATE TABLE TokenSessions (
    UserID int NOT NULL,
    SessionToken nvarchar(255) NOT NULL,
    SessionExpiresAt datetime2 NOT NULL,
    RefreshToken nvarchar(255) NOT NULL,
    RefreshExpiresAt datetime2 NOT NULL,
    CONSTRAINT PK_TokenSessions PRIMARY KEY (UserID),
    CONSTRAINT FK_TokenSessions_Users FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE INDEX IX_TokenSessions_SessionToken ON TokenSessions(SessionToken);
CREATE INDEX IX_TokenSessions_RefreshToken ON TokenSessions(RefreshToken);
CREATE INDEX IX_TokenSessions_UserID ON TokenSessions(UserID);

-- ============================================
-- 3. PROJECTS TABLE
-- ============================================
CREATE TABLE Projects (
    ProjectId INT PRIMARY KEY IDENTITY(1,1),
    ProjectName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255),
    DatabaseName NVARCHAR(100),
    IsLinked BIT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    SyncStatus NVARCHAR(500),
    SyncProgress INT,
    LastSyncAttempt DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID)
);

CREATE INDEX IX_Projects_CreatedBy ON Projects(CreatedBy);
CREATE INDEX IX_Projects_ProjectName ON Projects(ProjectName);

-- ============================================
-- 4. CLIENTS TABLE
-- ============================================
CREATE TABLE Clients (
    ClientId INT PRIMARY KEY IDENTITY(1,1),
    ClientName NVARCHAR(100) NOT NULL UNIQUE,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID)
);

CREATE INDEX IX_Clients_ClientName ON Clients(ClientName);

CREATE TABLE ProjectClients (
    ProjectClientId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    ClientId INT NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID),
    CONSTRAINT FK_ProjectClients_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE,
    CONSTRAINT FK_ProjectClients_Clients FOREIGN KEY (ClientId) REFERENCES Clients(ClientId) ON DELETE CASCADE,
    CONSTRAINT UQ_ProjectClients_ProjectId_ClientId UNIQUE (ProjectId, ClientId)
);

CREATE INDEX IX_ProjectClients_ProjectId ON ProjectClients(ProjectId);
CREATE INDEX IX_ProjectClients_ClientId ON ProjectClients(ClientId);
