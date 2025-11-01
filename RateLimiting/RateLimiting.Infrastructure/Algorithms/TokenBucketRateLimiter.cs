using System;
using RateLimiting.Domain.Contracts;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly object _lockObj = new();
    private readonly int _capacity;
    private readonly double _refillRatePerMs;
    private double _availableTokens;
    private long _lastRefillTimestamp;

    public TokenBucketRateLimiter(string name, int capacity, double refillRatePerSecond)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (refillRatePerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond));

        Name = name;
        _capacity = capacity;
        _refillRatePerMs = refillRatePerSecond / 1000d;
        _availableTokens = capacity;
        _lastRefillTimestamp = NowMs;
    }

    public string Name { get; }

    private static long NowMs => Environment.TickCount64;

    public RateLimitCheckResult Evaluate(RequestInfo requestInfo)
    {
        lock (_lockObj)
        {
            var now = NowMs;
            Refill(now);

            if (_availableTokens >= 1d)
            {
                _availableTokens -= 1d;
                return RateLimitCheckResult.Allow(Name);
            }

            var requiredTokens = 1d - _availableTokens;
            var waitMs = requiredTokens / _refillRatePerMs;
            return RateLimitCheckResult.Deny(TimeSpan.FromMilliseconds(waitMs), Name);
        }
    }

    private void Refill(long now)
    {
        if (now <= _lastRefillTimestamp)
        {
            return;
        }

        var elapsedMs = now - _lastRefillTimestamp;
        var refillTokens = elapsedMs * _refillRatePerMs;
        if (refillTokens > 0)
        {
            _availableTokens = Math.Min(_capacity, _availableTokens + refillTokens);
            _lastRefillTimestamp = now;
        }
    }
}
