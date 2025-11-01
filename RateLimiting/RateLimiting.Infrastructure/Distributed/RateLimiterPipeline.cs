using System.Collections.Generic;
using RateLimiting.Domain.Contracts;

namespace RateLimiting.Infrastructure.Distributed;

internal sealed class RateLimiterPipeline
{
    private readonly IReadOnlyList<IRateLimiter> _rateLimiters;

    public RateLimiterPipeline(IReadOnlyList<IRateLimiter> rateLimiters)
    {
        _rateLimiters = rateLimiters;
    }

    public RateLimitCheckResult Evaluate(RequestInfo requestInfo)
    {
        foreach (var limiter in _rateLimiters)
        {
            var result = limiter.Evaluate(requestInfo);
            if (!result.Allowed)
            {
                return result;
            }
        }

        // All limiters allowed the request
        return RateLimitCheckResult.Allow(_rateLimiters.Count > 0 ? _rateLimiters[^1].Name : string.Empty);
    }
}
