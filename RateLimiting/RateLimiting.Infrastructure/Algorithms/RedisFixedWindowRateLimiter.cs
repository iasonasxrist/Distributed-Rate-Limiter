using System;
using RateLimiting.Domain.Contracts;
using StackExchange.Redis;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class RedisFixedWindowRateLimiter : IRateLimiter
{
    private readonly IDatabase _database;
    private readonly long _windowSizeMs;
    private readonly int _maxRequests;

    private static readonly LuaScript FixedWindowScript = LuaScript.Prepare(@"
local current = redis.call('INCR', KEYS[1])
if current == 1 then
  redis.call('PEXPIRE', KEYS[1], ARGV[1])
end
if current <= tonumber(ARGV[2]) then
  return {1, 0}
end
local now = tonumber(ARGV[3])
local window = tonumber(ARGV[1])
local resetAt = (math.floor(now / window) + 1) * window
local retry = resetAt - now
if retry < 0 then retry = 0 end
return {0, retry}
");

    public RedisFixedWindowRateLimiter(IConnectionMultiplexer redis, string name, int maxRequests, TimeSpan windowSize)
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
        var bucket = now / _windowSizeMs;
        var key = BuildKey(requestInfo, bucket);

        var result = (RedisResult[])_database.ScriptEvaluate(
            FixedWindowScript,
            new RedisKey[] { key },
            new RedisValue[] { _windowSizeMs, _maxRequests, now });

        var allowed = (long)result[0] == 1;
        var retryMs = (long)result[1];

        return allowed
            ? RateLimitCheckResult.Allow(Name)
            : RateLimitCheckResult.Deny(TimeSpan.FromMilliseconds(retryMs), Name);
    }

    private RedisKey BuildKey(RequestInfo requestInfo, long bucket)
    {
        var clientKey = RateLimitingKeyBuilder.Build(requestInfo);
        return $"rl:{Name}:{clientKey}:{bucket}";
    }
}
