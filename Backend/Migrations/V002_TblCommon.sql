
-- ============================================
-- Migration: V002_TblCommon.sql
-- Description: Create common tables for users, projects, and clients
-- Date: 2025-10-15
-- ============================================

USE ActoEngine;
GO

-- ============================================
-- 1. USERS TABLE
-- ============================================
CREATE TABLE Users (
    UserID INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100),
    IsActive BIT DEFAULT 1,
    Role NVARCHAR(50) DEFAULT 'User',
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(50),
    UpdatedAt DATETIME2,
    UpdatedBy NVARCHAR(50)
);

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
    ProjectId INT NOT NULL REFERENCES Projects(ProjectId),
    ClientName NVARCHAR(100) NOT NULL UNIQUE,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2,
    UpdatedBy INT REFERENCES Users(UserID)
);

CREATE INDEX IX_Clients_ProjectId ON Clients(ProjectId);