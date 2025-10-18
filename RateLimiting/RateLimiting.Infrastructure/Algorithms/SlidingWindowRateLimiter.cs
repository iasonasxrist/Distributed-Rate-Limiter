using RateLimiting.Domain.Contracts;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class SlidingWindowRateLimiter
{
    public readonly long _windowsSize;

    public readonly int _maxRequests;

    private readonly Queue<RequestInfo> _events = new();

    private readonly object _lockObj = new();

    //use monotonic clock to avoid wall-clock jumps
    private static long NowMs => Environment.TickCount64;


    public SlidingWindowRateLimiter(int maxRequests, TimeSpan windowsSize)
    {
        if (maxRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequests));
        if (windowsSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowsSize));
        _maxRequests = maxRequests;
        _windowsSize = (long)windowsSize.TotalMilliseconds;
    }

    /// <summary>
    /// Try to validate a request
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public bool TryIsAllowed(out TimeSpan retryAfter,  RequestInfo requestInfo)
    {
        lock (_lockObj)
        {
            var now = NowMs;
            EvictExpired(now);

            if (_events.Count < _maxRequests)
            {
                _events.Enqueue(new RequestInfo(
                    RequestId: requestInfo.RequestId,
                    TimestampMs: requestInfo.TimestampMs,
                    Path: requestInfo.Path,
                    Method: requestInfo.Method,
                    UserId: requestInfo.UserId,
                    ApiKey: requestInfo.ApiKey,
                    CorrelationId: requestInfo.CorrelationId));
                retryAfter = TimeSpan.Zero;
                return true;
            }

            var oldest = _events.Peek().TimestampMs;
            var waitMs = Math.Max(0, (oldest + _windowsSize) - now);
            retryAfter = TimeSpan.FromMilliseconds(waitMs);
            return false;
        }
    }

    private void EvictExpired(long now)
    {
        while (_events.Count > 0 && now - _events.Peek().TimestampMs >= _windowsSize)
        {
            _events.Dequeue();
        }
    }
}