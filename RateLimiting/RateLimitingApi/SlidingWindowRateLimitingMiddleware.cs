using System.Globalization;
using Microsoft.Extensions.Options;
using RateLimiting.Infrastructure.Algorithms;
using RateLimiting.Infrastructure.Options;

namespace RateLimitingApi;

public class SlidingWindowRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SlidingWindowRateLimitingMiddleware> _logger;
    private readonly SlidingWindowRateLimiter _limiter;
    private readonly RateLimitingOptions _options;


    public SlidingWindowRateLimitingMiddleware(
        RequestDelegate next,
        ILogger<SlidingWindowRateLimitingMiddleware> logger,
        SlidingWindowRateLimiter limiter,
        IOptions<RateLimitingOptions> options)
    {
        _next = next;
        _logger = logger;
        _limiter = limiter;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var requestId = context.TraceIdentifier;
        var userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.Identity?.Name
            : null;
        var allowed = _limiter.TryIsAllowed(
            requestId: requestId,
            out var retryAfter,
            path: context.Request.Path,
            method: context.Request.Method,
            userId: userId);

        if (!allowed)
        {
            // Deny with 429 + Retry-After (seconds)
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] =
                Math.Ceiling(retryAfter).ToString(CultureInfo.InvariantCulture);

            // Optional informational headers
            context.Response.Headers["X-RateLimit-Limit"] = _options.MaxRequests.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers["X-RateLimit-Window-Seconds"] =
                _options.WindowSeconds.ToString(CultureInfo.InvariantCulture);

            await context.Response.WriteAsync("Too many requests. Please retry later.");
            _logger.LogDebug("Rate limited {Method} {Path}. retryAfter={RetryAfter}s",
                context.Request.Method, context.Request.Path, retryAfter);
            return;
        }

        await _next(context);
    }
}

public static class SlidingWindowRateLimitingMiddlewareExtensions
    {
        public static IApplicationBuilder UseSlidingWindowRateLimiting(this IApplicationBuilder app)=> app.UseMiddleware<SlidingWindowRateLimitingMiddleware>();

    }








