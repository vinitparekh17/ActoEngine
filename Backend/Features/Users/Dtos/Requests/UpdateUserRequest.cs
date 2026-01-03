namespace ActoEngine.WebApi.Models.Requests.Users;

public class UpdateUserRequest
{
    public int UserId { get; set; }
    public string? FullName { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
}

