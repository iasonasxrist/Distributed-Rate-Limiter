using RulesService;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options => options.BackgroundServiceExceptionBehavior =  BackgroundServiceExceptionBehavior.Ignore); 

builder.Services.AddHttpClient<RulesSyncWorker>((sp, client) => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
    var options = ConfigurationOptions.Parse(connectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddHostedService<RulesSyncWorker>();

await builder.Build().RunAsync();
