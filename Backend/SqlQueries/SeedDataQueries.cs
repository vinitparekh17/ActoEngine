namespace ActoEngine.WebApi.SqlQueries;

public static class SeedDataQueries
{
    public const string InsertDefaultProject = @"
        MERGE Projects WITH (HOLDLOCK) AS target
        USING (VALUES ('Default Project', GETUTCDATE(), @UserId)) AS source (ProjectName, CreatedAt, CreatedBy)
        ON target.ProjectName = source.ProjectName
        WHEN MATCHED THEN
            -- no-op update to allow OUTPUT to return the existing ProjectId
            UPDATE SET ProjectName = target.ProjectName
        WHEN NOT MATCHED THEN
            INSERT (ProjectName, CreatedAt, CreatedBy)
            VALUES (source.ProjectName, source.CreatedAt, source.CreatedBy)
        OUTPUT inserted.ProjectId;";

    public const string InsertDefaultClient = @"
        MERGE Clients WITH (HOLDLOCK) AS target
        USING (VALUES ('Default Client', 1, GETUTCDATE(), @UserId, @ProjectId)) AS source (ClientName, IsActive, CreatedAt, CreatedBy, ProjectId)
        ON target.ClientName = source.ClientName AND target.ProjectId = source.ProjectId
        WHEN MATCHED THEN
            -- no-op update to allow OUTPUT to return the existing ClientId
            UPDATE SET ClientName = target.ClientName
        WHEN NOT MATCHED THEN
            INSERT (ClientName, IsActive, CreatedAt, CreatedBy, ProjectId)
            VALUES (source.ClientName, source.IsActive, source.CreatedAt, source.CreatedBy, source.ProjectId)
        OUTPUT inserted.ClientId;";
}