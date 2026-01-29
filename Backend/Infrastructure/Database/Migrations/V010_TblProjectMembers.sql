-- ============================================
-- Migration: V010_TblProjectMembers.sql
-- Description: Junction table for project membership (N users : M projects)
-- Date: 2026-01-26
-- ============================================

-- ============================================
-- ProjectMembers - Simple N:M junction table
-- Note: Expert/Owner/Contributor roles are managed in EntityExperts table
-- ============================================
CREATE TABLE ProjectMembers (
    ProjectMemberId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    UserId INT NOT NULL,
    AddedAt DATETIME2 DEFAULT GETUTCDATE(),
    AddedBy INT NULL,
    
    -- Foreign Keys
    CONSTRAINT FK_ProjectMembers_Projects FOREIGN KEY (ProjectId) 
        REFERENCES Projects(ProjectId) ON DELETE CASCADE,
    CONSTRAINT FK_ProjectMembers_Users FOREIGN KEY (UserId) 
        REFERENCES Users(UserID) ON DELETE NO ACTION,
    CONSTRAINT FK_ProjectMembers_AddedBy FOREIGN KEY (AddedBy) 
        REFERENCES Users(UserID) ON DELETE SET NULL,
    
    -- Unique constraint: one membership record per user-project pair
    CONSTRAINT UQ_ProjectMembers_ProjectUser UNIQUE (ProjectId, UserId)
);

-- Indexes for common queries
CREATE INDEX IX_ProjectMembers_ProjectId ON ProjectMembers(ProjectId);
CREATE INDEX IX_ProjectMembers_UserId ON ProjectMembers(UserId);
GO
