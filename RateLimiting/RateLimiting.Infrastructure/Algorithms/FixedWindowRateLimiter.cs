using System;
using RateLimiting.Domain.Contracts;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class FixedWindowRateLimiter : IRateLimiter
{
    private readonly object _lockObj = new();
    private readonly long _windowSizeMs;
    private readonly int _maxRequests;
    private long _windowStart;
    private int _requestCount;

    public FixedWindowRateLimiter(string name, int maxRequests, TimeSpan windowSize)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        if (maxRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequests));
        if (windowSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowSize));

        Name = name;
        _maxRequests = maxRequests;
        _windowSizeMs = (long)windowSize.TotalMilliseconds;
        _windowStart = NowMs;
    }

    public string Name { get; }

    private static long NowMs => Environment.TickCount64;

    public RateLimitCheckResult Evaluate(RequestInfo requestInfo)
    {
        lock (_lockObj)
        {
            var now = NowMs;
            var elapsed = now - _windowStart;

            if (elapsed >= _windowSizeMs)
            {
                _windowStart = now;
                _requestCount = 0;
            }

            if (_requestCount < _maxRequests)
            {
                _requestCount++;
                return RateLimitCheckResult.Allow(Name);
            }

            var waitMs = Math.Max(0, _windowSizeMs - elapsed);
            return RateLimitCheckResult.Deny(TimeSpan.FromMilliseconds(waitMs), Name);
        }
    }
}
