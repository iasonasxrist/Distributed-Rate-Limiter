using System;
using RateLimiting.Domain.Contracts;
using StackExchange.Redis;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class RedisSlidingWindowRateLimiter : IRateLimiter
{
    private readonly IDatabase _database;
    private readonly long _windowSizeMs;
    private readonly int _maxRequests;

    private const string SlidingWindowScript = @"
local now = tonumber(ARGV[1])
local window = tonumber(ARGV[2])
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, now - window)
local count = redis.call('ZCARD', KEYS[1])
if count < tonumber(ARGV[3]) then
  redis.call('ZADD', KEYS[1], now, ARGV[4])
  redis.call('PEXPIRE', KEYS[1], window)
  return {1, 0}
end
local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
if oldest[2] then
  local retry = (tonumber(oldest[2]) + window) - now
  if retry < 0 then retry = 0 end
  return {0, retry}
end
return {0, window}
";

    public RedisSlidingWindowRateLimiter(IConnectionMultiplexer redis, string name, int maxRequests, TimeSpan windowSize)
    {
        if (redis == null) throw new ArgumentNullException(nameof(redis));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        if (maxRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequests));
        if (windowSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowSize));

        Name = name;
        _maxRequests = maxRequests;
        _windowSizeMs = (long)windowSize.TotalMilliseconds;
        _database = redis.GetDatabase();
    }

    public string Name { get; }

    public RateLimitCheckResult Evaluate(RequestInfo requestInfo)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var key = BuildKey(requestInfo);
        var member = BuildMember(requestInfo, now);

        var result = (RedisResult[])_database.ScriptEvaluate(
            SlidingWindowScript,
            new RedisKey[] { key },
            new RedisValue[] { now, _windowSizeMs, _maxRequests, member });

        var allowed = (long)result[0] == 1;
        var retryMs = (long)result[1];

        return allowed
            ? RateLimitCheckResult.Allow(Name)
            : RateLimitCheckResult.Deny(TimeSpan.FromMilliseconds(retryMs), Name);
    }

    private RedisKey BuildKey(RequestInfo requestInfo)
    {
        var clientKey = RateLimitingKeyBuilder.Build(requestInfo);
        return $"rl:{Name}:{clientKey}";
    }

    private static string BuildMember(RequestInfo requestInfo, long now)
    {
        var requestId = string.IsNullOrWhiteSpace(requestInfo.RequestId) ? Guid.NewGuid().ToString("N") : requestInfo.RequestId;
        return $"{now}:{requestId}";
    }
}
