namespace ActoEngine.WebApi.Models.Requests.Users;

public class ChangePasswordRequest
{
    public int UserId { get; set; }
    public string NewPassword { get; set; } = default!;
}

