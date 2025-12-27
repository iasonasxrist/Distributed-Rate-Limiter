using System;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Algorithms;
using RateLimiting.Infrastructure.Options;
using StackExchange.Redis;

namespace RateLimiting.Infrastructure.Distributed;

public sealed class DefaultRateLimiterFactory : IRateLimiterFactory
{
    private readonly IConnectionMultiplexer _redis;

    public DefaultRateLimiterFactory(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

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
                new RedisSlidingWindowRateLimiter(_redis, options.Name, maxRequests, TimeSpan.FromSeconds(windowSeconds)),
            RateLimiterAlgorithmType.FixedWindow =>
                new RedisFixedWindowRateLimiter(_redis, options.Name, maxRequests, TimeSpan.FromSeconds(windowSeconds)),
            RateLimiterAlgorithmType.TokenBucket =>
                new RedisTokenBucketRateLimiter(_redis, options.Name, options.Capacity > 0 ? options.Capacity : maxRequests,
                    options.RefillRatePerSecond > 0 ? options.RefillRatePerSecond : maxRequests / (double)windowSeconds),
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported rate limiter type '{options.Type}'.")
        };
    }
}
