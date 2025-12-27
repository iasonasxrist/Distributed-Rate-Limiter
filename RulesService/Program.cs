using RulesService;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// Typed client tied to RulesSyncWorker
builder.Services.AddHttpClient<RulesSyncWorker>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(cfg["Etcd:BaseUrl"] ?? "http://etcd:2379");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379"));

builder.Services.AddHostedService<RulesSyncWorker>();

await builder.Build().RunAsync();
