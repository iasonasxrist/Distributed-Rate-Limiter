using System;

namespace RateLimiting.Domain.Contracts;

/// <summary>
/// Represents the outcome of a single rate limiting algorithm evaluation.
/// </summary>
public readonly record struct RateLimitCheckResult(bool Allowed, TimeSpan RetryAfter, string Algorithm)
{
    public static RateLimitCheckResult Allow(string algorithm) => new(true, TimeSpan.Zero, algorithm);

    public static RateLimitCheckResult Deny(TimeSpan retryAfter, string algorithm) => new(false, retryAfter, algorithm);
}
