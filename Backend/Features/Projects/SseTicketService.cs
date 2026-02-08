using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;

namespace ActoEngine.WebApi.Features.Projects;

/// <summary>
/// Service for generating and validating short-lived one-time tickets for SSE connections.
/// Tickets are stored in distributed cache with a 30-second TTL and can only be used once.
/// </summary>
public interface ISseTicketService
{
    /// <summary>
    /// Generates a new one-time ticket for SSE connection
    /// </summary>
    /// <param name="userId">User ID requesting the ticket</param>
    /// <param name="projectId">Project ID for the SSE stream</param>
    /// <returns>Base64URL-encoded ticket string</returns>
    Task<string> GenerateTicketAsync(int userId, int projectId);

    /// <summary>
    /// Validates and consumes a ticket (one-time use)
    /// </summary>
    /// <param name="ticket">Ticket to validate</param>
    /// <returns>Ticket metadata if valid, null if invalid/expired/already used</returns>
    Task<SseTicketMetadata?> ValidateAndConsumeTicketAsync(string ticket);
}

public class SseTicketMetadata
{
    public int UserId { get; set; }
    public int ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SseTicketService(IDistributedCache cache, ILogger<SseTicketService> logger) : ISseTicketService
{
    private readonly IDistributedCache _cache = cache;
    private readonly ILogger<SseTicketService> _logger = logger;
    private const int TicketTtlSeconds = 30;
    private const string CacheKeyPrefix = "sse_ticket:";

    public async Task<string> GenerateTicketAsync(int userId, int projectId)
    {
        // Generate cryptographically secure random ticket (32 bytes = 256 bits)
        var ticketBytes = new byte[32];
        RandomNumberGenerator.Fill(ticketBytes);
        var ticket = Convert.ToBase64String(ticketBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('='); // Base64URL encoding

        var metadata = new SseTicketMetadata
        {
            UserId = userId,
            ProjectId = projectId,
            CreatedAt = DateTime.UtcNow
        };

        // Store in cache with 30-second TTL
        var cacheKey = $"{CacheKeyPrefix}{ticket}";
        var serializedMetadata = System.Text.Json.JsonSerializer.Serialize(metadata);

        await _cache.SetStringAsync(cacheKey, serializedMetadata, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(TicketTtlSeconds)
        });

        _logger.LogDebug("Generated SSE ticket for user {UserId}, project {ProjectId}, expires in {Ttl}s",
            userId, projectId, TicketTtlSeconds);

        return ticket;
    }

    public async Task<SseTicketMetadata?> ValidateAndConsumeTicketAsync(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        var cacheKey = $"{CacheKeyPrefix}{ticket}";

        try
        {
            // Retrieve ticket metadata from cache
            var serializedMetadata = await _cache.GetStringAsync(cacheKey);

            if (serializedMetadata == null)
            {
                _logger.LogWarning("SSE ticket validation failed: ticket not found or expired");
                return null;
            }

            // Immediately delete the ticket to ensure one-time use
            await _cache.RemoveAsync(cacheKey);

            var metadata = System.Text.Json.JsonSerializer.Deserialize<SseTicketMetadata>(serializedMetadata);

            if (metadata == null)
            {
                _logger.LogError("SSE ticket deserialization failed");
                return null;
            }

            _logger.LogInformation("SSE ticket validated and consumed for user {UserId}, project {ProjectId}",
                metadata.UserId, metadata.ProjectId);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SSE ticket");
            return null;
        }
    }
}
