using System;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Algorithms;
using RateLimiting.Infrastructure.Options;

namespace RateLimiting.Infrastructure.Distributed;

public sealed class DefaultRateLimiterFactory : IRateLimiterFactory
{
    public IRateLimiter Create(RateLimiterAlgorithmOptions options, RateLimitingOptions globalOptions)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException($"Attempted to create disabled rate limiter '{options.Name}'.");
        }

        var maxRequests = options.MaxRequests > 0 ? options.MaxRequests : globalOptions.DefaultMaxRequests;
        var windowSeconds = options.WindowSeconds > 0 ? options.WindowSeconds : globalOptions.DefaultWindowSeconds;

        return options.Type switch
        {
            RateLimiterAlgorithmType.SlidingWindow =>
                new SlidingWindowRateLimiter(options.Name, maxRequests, TimeSpan.FromSeconds(windowSeconds)),
            RateLimiterAlgorithmType.FixedWindow =>
                new FixedWindowRateLimiter(options.Name, maxRequests, TimeSpan.FromSeconds(windowSeconds)),
            RateLimiterAlgorithmType.TokenBucket =>
                new TokenBucketRateLimiter(options.Name, options.Capacity > 0 ? options.Capacity : maxRequests,
                    options.RefillRatePerSecond > 0 ? options.RefillRatePerSecond : maxRequests / (double)windowSeconds),
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported rate limiter type '{options.Type}'.")
        };
    }
}
