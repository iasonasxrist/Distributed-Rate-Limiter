using System;

namespace RateLimiting.Domain.Contracts;

/// <summary>
/// Represents the final decision of a distributed rate limiting pipeline.
/// </summary>
public readonly record struct RateLimitDecision(
    bool Allowed,
    TimeSpan RetryAfter,
    string Algorithm,
    string NodeId)
{
    public static RateLimitDecision AllowedDecision(string nodeId) => new(true, TimeSpan.Zero, string.Empty, nodeId);

    public static RateLimitDecision DeniedDecision(TimeSpan retryAfter, string algorithm, string nodeId) =>
        new(false, retryAfter, algorithm, nodeId);
}
