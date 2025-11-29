using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RulesService;

public sealed class RulesSyncWorker(ILogger<RulesSyncWorker> logger,  HttpClient httpClient, IConfiguration configuration) : BackgroundService
{
    private ILogger<RulesSyncWorker> _logger = logger;
    private HttpClient _httpClient = httpClient;
    private IConfiguration _configuration = configuration;
    private string _policiesKey = configuration["Etcd:PoliciesKey"] ?? "/v2/keys/rl/policies.json";
    protected override  async Task ExecuteAsync(CancellationToken stoppingToken)
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
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task SyncOnce(CancellationToken stoppingToken)
    {
        using var resp = await _httpClient.GetAsync(_policiesKey, stoppingToken);
        resp.EnsureSuccessStatusCode();
            // TODO : Deserialize the information for policies   
        
            var root =  JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!root.RootElement.TryGetProperty("node", out var node) ||
                !node.TryGetProperty("value", out var valueEl))
            {
                _logger.LogWarning("etcd response missing node/value");
                return;
            }

    }
}