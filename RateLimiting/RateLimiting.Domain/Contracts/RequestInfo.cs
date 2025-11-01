namespace RateLimiting.Domain.Contracts;

public record struct RequestInfo(
        string RequestId,
        long TimestampMs,
        string? Path = null,
        string? Method = null,
        string? UserId = null,
        string? ApiKey = null,
        string? CorrelationId = null,
        string? ClientId = null
    );