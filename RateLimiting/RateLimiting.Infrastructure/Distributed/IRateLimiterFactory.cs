using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Options;

namespace RateLimiting.Infrastructure.Distributed;

public interface IRateLimiterFactory
{
    IRateLimiter Create(RateLimiterAlgorithmOptions options, RateLimitingOptions globalOptions);
}
