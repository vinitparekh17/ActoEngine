-- ============================================
-- Migration: V011_LogicalForeignKeys.sql
-- Description: Add logical (user-curated + auto-detected) foreign key relationships
-- Date: 2026-02-14
-- ============================================

CREATE TABLE LogicalForeignKeys (
    LogicalFkId      INT PRIMARY KEY IDENTITY(1,1),
    ProjectId        INT NOT NULL,

    -- Source side (the table with the FK column)
    SourceTableId    INT NOT NULL,
    SourceColumnIds  NVARCHAR(200) NOT NULL,  -- JSON array: [colId1] or [colId1, colId2]

    -- Target side (the referenced table)
    TargetTableId    INT NOT NULL,
    TargetColumnIds  NVARCHAR(200) NOT NULL,  -- JSON array: [colId1] or [colId1, colId2]

    -- Discovery metadata
    DiscoveryMethod  NVARCHAR(50) NOT NULL DEFAULT 'MANUAL'
        CHECK (DiscoveryMethod IN ('MANUAL', 'NAME_CONVENTION')),
    ConfidenceScore  DECIMAL(5,2) DEFAULT 1.0,

    -- User curation workflow: SUGGESTED → CONFIRMED / REJECTED
    Status           NVARCHAR(20) NOT NULL DEFAULT 'SUGGESTED'
        CHECK (Status IN ('SUGGESTED', 'CONFIRMED', 'REJECTED')),
    ConfirmedBy      INT NULL,
    ConfirmedAt      DATETIME2 NULL,
    Notes            NVARCHAR(500),

    -- Metadata
    CreatedAt        DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy        INT NULL,

    -- Foreign Keys
    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId) ON DELETE CASCADE,
    FOREIGN KEY (SourceTableId) REFERENCES TablesMetadata(TableId),
    FOREIGN KEY (TargetTableId) REFERENCES TablesMetadata(TableId),
    FOREIGN KEY (ConfirmedBy) REFERENCES Users(UserID),

    -- Prevent duplicate logical FK entries
    UNIQUE (ProjectId, SourceTableId, SourceColumnIds, TargetTableId, TargetColumnIds),

    -- Indexes
    INDEX IX_LogicalForeignKeys_ProjectId (ProjectId),
    INDEX IX_LogicalForeignKeys_SourceTable (SourceTableId),
    INDEX IX_LogicalForeignKeys_TargetTable (TargetTableId),
    INDEX IX_LogicalForeignKeys_Status (ProjectId, Status)
);
