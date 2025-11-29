using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesService;
using StackExchange.Redis;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(ctx.Configuration.GetConnectionString("Redis") ?? "redis:6379"));
        services.AddHttpClient(); // for etcd calls
        services.AddHostedService<RulesSyncWorker>();
    })
    .Build()
    .Run();
