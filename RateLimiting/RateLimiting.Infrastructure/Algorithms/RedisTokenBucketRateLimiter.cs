using System;
using RateLimiting.Domain.Contracts;
using StackExchange.Redis;

namespace RateLimiting.Infrastructure.Algorithms;

public sealed class RedisTokenBucketRateLimiter : IRateLimiter
{
    private readonly IDatabase _database;
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;
    private readonly long _ttlMs;

    private static readonly LuaScript TokenBucketScript = LuaScript.Prepare(@"
local capacity = tonumber(ARGV[1])
local refill = tonumber(ARGV[2])
local now = tonumber(ARGV[3])
local ttl = tonumber(ARGV[4])
local data = redis.call('HMGET', KEYS[1], 'tokens', 'ts')
local tokens = tonumber(data[1])
local ts = tonumber(data[2])
if tokens == nil then
  tokens = capacity
  ts = now
end
if now > ts then
  local delta = (now - ts) / 1000.0
  tokens = math.min(capacity, tokens + (delta * refill))
  ts = now
end
local allowed = 0
local retry = 0
if tokens >= 1 then
  tokens = tokens - 1
  allowed = 1
else
  local needed = 1 - tokens
  retry = math.ceil((needed / refill) * 1000)
end
redis.call('HMSET', KEYS[1], 'tokens', tokens, 'ts', ts)
redis.call('PEXPIRE', KEYS[1], ttl)
return {allowed, retry}
");

    public RedisTokenBucketRateLimiter(IConnectionMultiplexer redis, string name, int capacity, double refillRatePerSecond)
    {
        if (redis == null) throw new ArgumentNullException(nameof(redis));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (refillRatePerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond));

        Name = name;
        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
        _ttlMs = Math.Max(1000, (long)Math.Ceiling(capacity / refillRatePerSecond * 1000 * 2));
        _database = redis.GetDatabase();
    }

    public string Name { get; }

    public RateLimitCheckResult Evaluate(RequestInfo requestInfo)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var key = BuildKey(requestInfo);

        var result = (RedisResult[])_database.ScriptEvaluate(
            TokenBucketScript,
            new RedisKey[] { key },
            new RedisValue[] { _capacity, _refillRatePerSecond, now, _ttlMs });

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
}
