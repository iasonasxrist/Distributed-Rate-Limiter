namespace RateLimiting.Infrastructure.Options;

public enum RateLimiterAlgorithmType
{
    SlidingWindow,
    FixedWindow,
    TokenBucket
}
