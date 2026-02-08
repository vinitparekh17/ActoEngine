using System.Collections.Concurrent;

namespace ActoEngine.WebApi.Features.Projects;

public sealed class SseConnectionHandle
{
    public SseConnectionHandle(int userId, int projectId, string connectionKey, CancellationTokenSource cancellationTokenSource)
    {
        UserId = userId;
        ProjectId = projectId;
        ConnectionKey = connectionKey;
        CancellationTokenSource = cancellationTokenSource;
    }

    public int UserId { get; }

    public int ProjectId { get; }

    public string ConnectionKey { get; }

    public CancellationTokenSource CancellationTokenSource { get; }

    public CancellationToken Token => CancellationTokenSource.Token;
}

/// <summary>
/// Manages active SSE connections to prevent duplicate connections per user/project.
/// When a user opens multiple tabs, only the most recent connection stays active.
/// </summary>
public class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeConnections = new();
    private readonly ILogger<SseConnectionManager> _logger;

    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a new SSE connection and cancels any existing connection for the same user/project.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="projectId">Project ID</param>
    /// <param name="cancellationToken">Cancellation token for the new connection</param>
    /// <returns>A handle that tracks the registered connection and its cancellation token source.</returns>
    public SseConnectionHandle RegisterConnection(int userId, int projectId, CancellationToken cancellationToken)
    {
        var key = GetConnectionKey(userId, projectId);
        
        // Create a new CTS that can be cancelled independently
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // If there's an existing connection, cancel it
        if (_activeConnections.TryRemove(key, out var existingCts))
        {
            _logger.LogInformation(
                "Closing existing SSE connection for user {UserId}, project {ProjectId} (new tab opened)",
                userId, projectId);
            existingCts.Cancel();
            existingCts.Dispose();
        }

        // Register the new connection
        _activeConnections[key] = cts;
        _logger.LogInformation(
            "Registered new SSE connection for user {UserId}, project {ProjectId}",
            userId, projectId);

        return new SseConnectionHandle(userId, projectId, key, cts);
    }

    /// <summary>
    /// Unregisters a connection when it closes normally.
    /// </summary>
    public void UnregisterConnection(SseConnectionHandle? handle)
    {
        if (handle == null)
        {
            return;
        }

        if (!_activeConnections.TryGetValue(handle.ConnectionKey, out var currentCts) ||
            !ReferenceEquals(currentCts, handle.CancellationTokenSource))
        {
            return;
        }

        if (_activeConnections.TryRemove(handle.ConnectionKey, out var removedCts))
        {
            if (ReferenceEquals(removedCts, handle.CancellationTokenSource))
            {
                removedCts.Dispose();
                _logger.LogInformation(
                    "Unregistered SSE connection for user {UserId}, project {ProjectId}",
                    handle.UserId, handle.ProjectId);
            }
            else
            {
                _activeConnections[handle.ConnectionKey] = removedCts;
            }
        }
    }

    private static string GetConnectionKey(int userId, int projectId) => $"{userId}:{projectId}";
}
