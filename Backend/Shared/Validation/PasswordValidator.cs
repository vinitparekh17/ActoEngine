namespace ActoEngine.WebApi.Shared.Validation;

public interface IPasswordValidator
{
    (bool isValid, string? errorMessage) ValidatePassword(string password);
}

public class PasswordValidator : IPasswordValidator
{
    public (bool isValid, string? errorMessage) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password is required");
        }

        if (password.Length < 12)
        {
            return (false, "Password must be at least 12 characters long");
        }

        if (password.Length > 100)
        {
            return (false, "Password must not exceed 100 characters");
        }

        if (!password.Any(char.IsUpper))
        {
            return (false, "Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            return (false, "Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            return (false, "Password must contain at least one digit");
        }

        if (password.All(char.IsLetterOrDigit))
        {
            return (false, "Password must contain at least one special character");
        }

        return (true, null);
    }
}
