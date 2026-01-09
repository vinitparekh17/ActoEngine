-- ============================================
-- Migration: V007_TblContextLayer.sql
-- Description: Add context and documentation layer
-- Date: 2025-11-01
-- ============================================

-- ============================================
-- 1. EntityContext - Core context storage
-- ============================================
CREATE TABLE EntityContext (
    ContextId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    EntityType NVARCHAR(50) NOT NULL CHECK (EntityType IN ('TABLE', 'COLUMN', 'SP', 'FUNCTION', 'VIEW')),
    EntityId INT NOT NULL,
    EntityName NVARCHAR(255) NOT NULL,
    
    -- Core Context Fields (Universal)
    Purpose NVARCHAR(MAX),
    BusinessImpact NVARCHAR(MAX),
    DataOwner NVARCHAR(100),
    CriticalityLevel INT DEFAULT 3 CHECK (CriticalityLevel BETWEEN 1 AND 5),
    BusinessDomain NVARCHAR(100),
    
    -- Column-specific
    Sensitivity NVARCHAR(50) DEFAULT 'PUBLIC' 
        CHECK (Sensitivity IN ('PUBLIC', 'INTERNAL', 'PII', 'FINANCIAL', 'SENSITIVE')),
    DataSource NVARCHAR(500),
    ValidationRules NVARCHAR(MAX), -- JSON format
    
    -- Table-specific
    RetentionPolicy NVARCHAR(255),
    
    -- SP-specific
    DataFlow NVARCHAR(MAX),
    Frequency NVARCHAR(50) CHECK (Frequency IN ('REALTIME', 'HOURLY', 'DAILY', 'BATCH', 'ADHOC') OR Frequency IS NULL),
    IsDeprecated BIT DEFAULT 0,
    DeprecationReason NVARCHAR(MAX),
    ReplacedBy NVARCHAR(255),
    
    -- Metadata
    IsContextStale BIT DEFAULT 0,
    LastReviewedAt DATETIME2,
    ReviewedBy INT,
    LastContextUpdate DATETIME2,
    ContextUpdatedBy INT,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    
    -- Foreign Keys
    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE,
    FOREIGN KEY (ReviewedBy) REFERENCES Users(UserID),
    FOREIGN KEY (ContextUpdatedBy) REFERENCES Users(UserID),
    
    -- Indexes
    UNIQUE (ProjectId, EntityType, EntityId),
    INDEX IX_EntityContext_EntityType (EntityType, EntityId),
    INDEX IX_EntityContext_ProjectId (ProjectId),
    INDEX IX_EntityContext_Stale (ProjectId, IsContextStale),
    INDEX IX_EntityContext_Domain (BusinessDomain)
);
GO

-- ============================================
-- 2. EntityExperts - Who knows what
-- ============================================
CREATE TABLE EntityExperts (
    ExpertId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    EntityType NVARCHAR(50) NOT NULL CHECK (EntityType IN ('TABLE', 'COLUMN', 'SP', 'FUNCTION', 'VIEW')),
    EntityId INT NOT NULL,
    UserId INT NOT NULL,
    ExpertiseLevel NVARCHAR(20) NOT NULL 
        CHECK (ExpertiseLevel IN ('OWNER', 'EXPERT', 'FAMILIAR', 'CONTRIBUTOR')),
    Notes NVARCHAR(500),
    AddedAt DATETIME2 DEFAULT GETUTCDATE(),
    AddedBy INT,
    
    -- Foreign Keys
    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(UserID) ON DELETE CASCADE,
    FOREIGN KEY (AddedBy) REFERENCES Users(UserID),
    
    -- Constraints
    UNIQUE (ProjectId, EntityType, EntityId, UserId),
    
    -- Indexes
    INDEX IX_EntityExperts_Entity (EntityType, EntityId),
    INDEX IX_EntityExperts_User (UserId),
    INDEX IX_EntityExperts_Project (ProjectId)
);
GO

-- ============================================
-- 3. ContextHistory - Audit trail
-- ============================================
CREATE TABLE ContextHistory (
    HistoryId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    EntityType NVARCHAR(50) NOT NULL,
    EntityId INT NOT NULL,
    FieldName NVARCHAR(100) NOT NULL,
    OldValue NVARCHAR(MAX),
    NewValue NVARCHAR(MAX),
    ChangedBy INT NOT NULL,
    ChangedAt DATETIME2 DEFAULT GETUTCDATE(),
    ChangeReason NVARCHAR(500),
    
    -- Foreign Keys
    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE,
    FOREIGN KEY (ChangedBy) REFERENCES Users(UserID),
    
    -- Indexes
    INDEX IX_ContextHistory_ProjectId (ProjectId),
    INDEX IX_ContextHistory_Entity (EntityType, EntityId),
    INDEX IX_ContextHistory_ChangedAt (ChangedAt DESC),
    INDEX IX_ContextHistory_ChangedBy (ChangedBy)
);
GO

-- ============================================
-- 4. ContextReviewRequests - Review workflow
-- ============================================
CREATE TABLE ContextReviewRequests (
    RequestId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    EntityType NVARCHAR(50) NOT NULL,
    EntityId INT NOT NULL,
    RequestedBy INT NOT NULL,
    AssignedTo INT,
    Status NVARCHAR(20) DEFAULT 'PENDING' 
        CHECK (Status IN ('PENDING', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')),
    Reason NVARCHAR(500),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CompletedAt DATETIME2,
    
    -- Foreign Keys
    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE NO ACTION,
    FOREIGN KEY (RequestedBy) REFERENCES Users(UserID),
    FOREIGN KEY (AssignedTo) REFERENCES Users(UserID),
    
    -- Indexes
    INDEX IX_ReviewRequests_Status (Status),
    INDEX IX_ReviewRequests_ProjectId (ProjectId),
    INDEX IX_ReviewRequests_AssignedTo (AssignedTo, Status),
    INDEX IX_ReviewRequests_Entity (EntityType, EntityId)
);
GO