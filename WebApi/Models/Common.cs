namespace ActoEngine.WebApi.Models;

public class ApiResponse<T>
{
    public bool Status { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Success(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Status = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> Failure(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Status = false,
            Message = message,
            Errors = errors ?? []
        };
    }
}
public class MessageResponse
{
    public required string Message { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = "Unauthorized";
    public string Message { get; set; } = default!;
    public string Timestamp { get; set; } = default!;
    public string Path { get; set; } = default!;
}