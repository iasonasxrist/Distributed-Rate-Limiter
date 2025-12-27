using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Distributed;
using RateLimiting.Infrastructure.Options;
using StackExchange.Redis;

namespace RateLimitingApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddHttpClient();
        builder.Services.AddOptions<RateLimitingOptions>()
            .Bind(builder.Configuration.GetSection("RateLimiting"))
            .ValidateDataAnnotations()
            .Validate(options => options.Algorithms.Count == 0 || options.Algorithms.All(a => !string.IsNullOrWhiteSpace(a.Name)),
                "Algorithm name must be provided")
            .Validate(options => options.ClusterNodeCount > 0, "ClusterNodeCount must be positive");
        builder.Services.AddOptions<RateLimitingConfigOptions>()
            .Bind(builder.Configuration.GetSection("RateLimitingConfig"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RedisKey), "RedisKey must be provided");


        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379"));
        builder.Services.AddSingleton<RedisRateLimitingOptionsProvider>();
        builder.Services.AddSingleton<IRateLimitingOptionsProvider>(sp => sp.GetRequiredService<RedisRateLimitingOptionsProvider>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RedisRateLimitingOptionsProvider>());
        builder.Services.AddSingleton<IRateLimiterFactory, DefaultRateLimiterFactory>();
        builder.Services.AddSingleton<IDistributedRateLimiter>(sp =>
        {
            var cfg = sp.GetRequiredService<IRateLimitingOptionsProvider>();
            var factory = sp.GetRequiredService<IRateLimiterFactory>();
            return new DistributedRateLimiter(cfg, factory);
        });
        var app = builder.Build();

        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedOptions.KnownNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedOptions);
        app.UseDistributedRateLimiting();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
