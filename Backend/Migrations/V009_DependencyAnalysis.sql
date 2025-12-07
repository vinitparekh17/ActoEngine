CREATE TABLE Dependencies (
    DependencyId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL,
    
    -- Source (The thing relying on something)
    SourceType NVARCHAR(50) NOT NULL, -- 'SP', 'TABLE', 'FUNCTION', 'JOB'
    SourceId INT NOT NULL,
    
    -- Target (The thing being relied upon)
    TargetType NVARCHAR(50) NOT NULL, -- 'TABLE', 'COLUMN', 'SP'
    TargetId INT NOT NULL,
    
    -- Metadata
    DependencyType NVARCHAR(50) NOT NULL, -- 'FK', 'SELECT', 'INSERT', 'UPDATE', 'EXEC'
    ConfidenceScore DECIMAL(5,2) DEFAULT 1.0, -- 1.0 for FKs, 0.8 for Regex hits
    DiscoveredAt DATETIME2 DEFAULT GETDATE(),
    DiscoveredBy NVARCHAR(50), -- 'FK_SCAN', 'REGEX_ANALYSIS', 'MANUAL'

    FOREIGN KEY (ProjectId) REFERENCES Projects(ProjectId),
    INDEX IX_Dependencies_Source (SourceType, SourceId),
    INDEX IX_Dependencies_Target (TargetType, TargetId)
);