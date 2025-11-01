using System;
using System.Collections.Generic;
using RateLimiting.Domain.Contracts;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly object _lockObj = new();
    private readonly Queue<long> _events = new();
    private readonly long _windowSizeMs;
    private readonly int _maxRequests;

    public SlidingWindowRateLimiter(string name, int maxRequests, TimeSpan windowSize)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        if (maxRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequests));
        if (windowSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowSize));

        Name = name;
        _maxRequests = maxRequests;
        _windowSizeMs = (long)windowSize.TotalMilliseconds;
    }

    public string Name { get; }

    //use monotonic clock to avoid wall-clock jumps
    private static long NowMs => Environment.TickCount64;

    public RateLimitCheckResult Evaluate(RequestInfo requestInfo)
    {
        lock (_lockObj)
        {
            var now = NowMs;
            EvictExpired(now);

            if (_events.Count < _maxRequests)
            {
                _events.Enqueue(now);
                return RateLimitCheckResult.Allow(Name);
            }

            var oldest = _events.Peek();
            var waitMs = Math.Max(0, (oldest + _windowSizeMs) - now);
            return RateLimitCheckResult.Deny(TimeSpan.FromMilliseconds(waitMs), Name);
        }
    }

    private void EvictExpired(long now)
    {
        while (_events.Count > 0 && now - _events.Peek() >= _windowSizeMs)
        {
            _events.Dequeue();
        }
    }
}
