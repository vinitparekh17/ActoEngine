namespace ActoX.Application.DTOs
{
    public class VerifyConnectionRequest
    {
        public required string Server { get; set; }
        public required string DatabaseName { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class LinkProjectRequest
    {
        public int ProjectId { get; set; }
        public required string ProjectName { get; set; }
        public required string Description { get; set; }
        public required string DatabaseName { get; set; }
        public required string ConnectionString { get; set; }
    }

    public class ProjectResponse
    {
        public int ProjectId { get; set; }
        public required string Message { get; set; }
    }
}