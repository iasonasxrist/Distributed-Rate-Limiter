using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Options;

namespace RateLimitingApi;

public sealed class DistributedRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DistributedRateLimitingMiddleware> _logger;
    private readonly IDistributedRateLimiter _rateLimiter;
    private readonly RateLimitingOptions _options;

    public DistributedRateLimitingMiddleware(
        RequestDelegate next,
        ILogger<DistributedRateLimitingMiddleware> logger,
        IDistributedRateLimiter rateLimiter,
        IOptions<RateLimitingOptions> options)
    {
        _next = next;
        _logger = logger;
        _rateLimiter = rateLimiter;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestInfo = BuildRequestInfo(context, _options.ClientIdHeader);
        var decision = _rateLimiter.ShouldAllow(requestInfo);

        if (!decision.Allowed)
        {
            await DenyRequestAsync(context, decision);
            return;
        }

        await _next(context);
    }

    private static RequestInfo BuildRequestInfo(HttpContext context, string clientIdHeader)
    {
        var headers = context.Request.Headers;
        headers.TryGetValue(clientIdHeader, out var clientIdValues);
        headers.TryGetValue("X-Api-Key", out var apiKeyValues);
        headers.TryGetValue("X-Correlation-Id", out var correlationValues);

        return new RequestInfo(
            RequestId: context.TraceIdentifier,
            TimestampMs: Environment.TickCount64,
            Path: context.Request.Path,
            Method: context.Request.Method,
            UserId: context.User?.Identity?.IsAuthenticated == true ? context.User.Identity?.Name : null,
            ApiKey: apiKeyValues.FirstOrDefault(),
            CorrelationId: correlationValues.FirstOrDefault(),
            ClientId: clientIdValues.FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString());
    }

    private Task DenyRequestAsync(HttpContext context, RateLimitDecision decision)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] =
            Math.Ceiling(decision.RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Node"] = decision.NodeId;
        if (!string.IsNullOrWhiteSpace(decision.Algorithm))
        {
            context.Response.Headers["X-RateLimit-Algorithm"] = decision.Algorithm;
        }

        _logger.LogWarning(
            "Request throttled by {Algorithm} on {Node}. Retry after {RetryAfter}s", decision.Algorithm,
            decision.NodeId, decision.RetryAfter.TotalSeconds);

        return context.Response.WriteAsync("Too many requests. Please retry later.");
    }
}

public static class DistributedRateLimitingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDistributedRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<DistributedRateLimitingMiddleware>();
}
