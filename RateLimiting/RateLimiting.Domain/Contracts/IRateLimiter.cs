namespace RateLimiting.Domain.Contracts;

/// <summary>
/// Represents a rate limiting algorithm that can evaluate a request and decide whether it should be throttled.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Gets the unique name of the algorithm instance.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates whether the supplied request is allowed to proceed.
    /// </summary>
    /// <param name="requestInfo">The request metadata.</param>
    /// <returns>The outcome of the rate limiting decision.</returns>
    RateLimitCheckResult Evaluate(RequestInfo requestInfo);
}
