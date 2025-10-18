using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Algorithms;
using RateLimiting.Infrastructure.Options;

namespace RateLimitingApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        
        builder.Services.AddOptions<RateLimitingOptions>()
            .Bind(builder.Configuration.GetSection("RateLimiting"))
            .ValidateDataAnnotations()
            .Validate(o=>o.MaxRequests> 0, "MaxRequests")
            .Validate(o=>o.WindowSeconds>0, "WindowSeconds");
        
        
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<RateLimitingOptions>>();
            return new SlidingWindowRateLimiter(cfg.Value.MaxRequests,TimeSpan.FromSeconds(2));
        });
        var rl1 = new SlidingWindowRateLimiter(2, TimeSpan.FromSeconds(2));
        var rl2 = new SlidingWindowRateLimiter(2, TimeSpan.FromSeconds(2));
        for (int i=1; i <= 10; i++)
        {
            RequestInfo requestInfo = new()
            {
                RequestId = Guid.NewGuid().ToString(),
                Path = "$/api/demo{i}",
                Method = "GET",
                UserId = "u42"
            };
            
            var ok = rl1.TryIsAllowed( out var retryAfter, requestInfo);
            
            Console.WriteLine(ok
                ? $"{i}: allowed"
                : $"{i}: denied — retry after ~{retryAfter.TotalSeconds:F1}s");

            if (i % 3 == 0)
            {
                Console.WriteLine("Sleeping 20s...");
                Thread.Sleep(TimeSpan.FromSeconds(20));
            }    
        }
        var app = builder.Build();

        app.UseSlidingWindowRateLimiting();
        
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