using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RateLimiting.Infrastructure.Options;
using StackExchange.Redis;

namespace RateLimitingApi;

public sealed class RedisRateLimitingOptionsProvider : BackgroundService, IRateLimitingOptionsProvider
{
    private readonly ILogger<RedisRateLimitingOptionsProvider> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _redisKey;
    private readonly TimeSpan _pollInterval;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly object _lock = new();
    private RateLimitingOptions _currentOptions;
    private long _version;

    public RedisRateLimitingOptionsProvider(
        ILogger<RedisRateLimitingOptionsProvider> logger,
        IConnectionMultiplexer redis,
        IOptions<RateLimitingOptions> defaultOptions,
        IOptions<RateLimitingConfigOptions> configOptions)
    {
        _logger = logger;
        _redis = redis;
        _currentOptions = defaultOptions.Value;
        _redisKey = configOptions.Value.RedisKey;
        _pollInterval = TimeSpan.FromSeconds(Math.Max(1, configOptions.Value.PollSeconds));
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    }

    public RateLimitingOptions GetCurrentOptions()
    {
        lock (_lock)
        {
            return _currentOptions;
        }
    }

    public long Version => Interlocked.Read(ref _version);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(_redisKey);
        if (!value.HasValue)
        {
            return;
        }

        RateLimitingOptions? updated;
        try
        {
            updated = JsonSerializer.Deserialize<RateLimitingOptions>(value!, _serializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse rate limiting options from Redis key {RedisKey}", _redisKey);
            return;
        }

        if (updated == null)
        {
            _logger.LogWarning("Rate limiting options in Redis key {RedisKey} were empty", _redisKey);
            return;
        }

        lock (_lock)
        {
            _currentOptions = updated;
            Interlocked.Increment(ref _version);
        }
    }
}
