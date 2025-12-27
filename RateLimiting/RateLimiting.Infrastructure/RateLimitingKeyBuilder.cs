using RateLimiting.Domain.Contracts;

namespace RateLimiting.Infrastructure;

public static class RateLimitingKeyBuilder
{
    public static string Build(RequestInfo requestInfo)
    {
        if (!string.IsNullOrWhiteSpace(requestInfo.ApiKey))
        {
            return $"api:{requestInfo.ApiKey}";
        }

        if (!string.IsNullOrWhiteSpace(requestInfo.UserId))
        {
            return $"user:{requestInfo.UserId}";
        }

        if (!string.IsNullOrWhiteSpace(requestInfo.ClientId))
        {
            return $"client:{requestInfo.ClientId}";
        }

        return $"anon:{requestInfo.RequestId}";
    }
}
