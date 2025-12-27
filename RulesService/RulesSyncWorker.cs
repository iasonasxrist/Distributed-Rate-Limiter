using System.Text.Json;
using StackExchange.Redis;

namespace RulesService;

public sealed class RulesSyncWorker(
    ILogger<RulesSyncWorker> logger,
    HttpClient httpClient,
    IConfiguration configuration,
    IConnectionMultiplexer redis) : BackgroundService
{
    private readonly ILogger<RulesSyncWorker> _logger = logger;
    private readonly IConnectionMultiplexer _redis = redis;
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _etcdBaseUrl = configuration["Etcd:BaseUrl"]!;
    private readonly string _policiesKey = configuration["Etcd:PoliciesKey"] ?? "/v2/keys/rl/policies.json";
    private readonly string _redisKey = configuration["RulesSync:RedisKey"] ?? "rate-limiting:options";
    private readonly string _notificationChannel =
        configuration["RulesSync:NotificationChannel"] ?? "rate-limiting:options:updated";
    private readonly TimeSpan _pollInterval =
        TimeSpan.FromSeconds(Math.Max(1, int.Parse(configuration["Etcd:PollSeconds"] ?? "10")));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SyncOnce(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnce(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Sync rules failed");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task SyncOnce(CancellationToken stoppingToken)
    {
        var requestUri = new Uri(new Uri(_etcdBaseUrl), _policiesKey);
        var resp = await GetWithRetryAsync(requestUri, stoppingToken);
        resp.EnsureSuccessStatusCode();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Etcd request failed with status {StatusCode} for {RequestUri}",
                resp.StatusCode,
                requestUri);
            return;
        }
        using var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(stoppingToken));
        if (!root.RootElement.TryGetProperty("node", out var node) ||
            !node.TryGetProperty("value", out var valueEl))
        {
            _logger.LogWarning("etcd response missing node/value");
            return;
        }

        var policiesJson = valueEl.GetString();
        if (string.IsNullOrWhiteSpace(policiesJson))
        {
            _logger.LogWarning("etcd response contained empty policy value");
            return;
        }

        var db = _redis.GetDatabase();
        await db.StringSetAsync(_redisKey, policiesJson);
        await db.PublishAsync(_notificationChannel!, "updated");
        _logger.LogInformation("Rate limiting policies synced to Redis key {RedisKey}", _redisKey);
    }
    
    private async Task<HttpResponseMessage> GetWithRetryAsync(Uri requestUri, CancellationToken stoppingToken)
    {
        const int maxAttempts = 3;
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _httpClient.GetAsync(requestUri, stoppingToken);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Etcd request attempt {Attempt} failed; retrying in {DelaySeconds}s",
                    attempt,
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
            }
        }

        return await _httpClient.GetAsync(requestUri, stoppingToken);
    }
}
