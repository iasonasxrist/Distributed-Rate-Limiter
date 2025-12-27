namespace RateLimiting.Infrastructure.Options;

public interface IRateLimitingOptionsProvider
{
    RateLimitingOptions GetCurrentOptions();

    long Version { get; }
}
