namespace ActoEngine.WebApi.Sql.Queries;

public static class SeedDataQueries
{
    public const string InsertDefaultProject = @"
        IF NOT EXISTS (SELECT 1 FROM Projects WHERE ProjectName = 'Default Project')
        BEGIN
            INSERT INTO Projects (ProjectName, CreatedAt, CreatedBy)
            VALUES ('Default Project', GETUTCDATE(), @UserId);
        END
        SELECT ProjectId FROM Projects WHERE ProjectName = 'Default Project';";

    public const string InsertDefaultClient = @"
        IF NOT EXISTS (SELECT 1 FROM Clients WHERE ClientName = 'Default Client')
        BEGIN
            INSERT INTO Clients (ClientName, IsActive, CreatedAt, CreatedBy, ProjectId)
            VALUES ('Default Client', 1, GETUTCDATE(), @UserId, @ProjectId);
        END
        SELECT ClientId FROM Clients WHERE ClientName = 'Default Client';";
}