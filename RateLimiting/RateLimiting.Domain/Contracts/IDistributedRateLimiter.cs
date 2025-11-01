namespace RateLimiting.Domain.Contracts;

/// <summary>
/// Represents a distributed rate limiter that coordinates multiple nodes/algorithms.
/// </summary>
public interface IDistributedRateLimiter
{
    RateLimitDecision ShouldAllow(RequestInfo requestInfo);
}
