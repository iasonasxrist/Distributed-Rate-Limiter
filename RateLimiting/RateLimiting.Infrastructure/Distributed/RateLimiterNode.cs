using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Options;

namespace RateLimiting.Infrastructure.Distributed;

public sealed class RateLimiterNode
{
    private readonly ConcurrentDictionary<string, RateLimiterPipeline> _pipelines = new();
    private readonly IReadOnlyList<RateLimiterAlgorithmOptions> _algorithmOptions;
    private readonly IRateLimiterFactory _factory;
    private readonly RateLimitingOptions _globalOptions;

    public RateLimiterNode(string nodeId, IReadOnlyList<RateLimiterAlgorithmOptions> algorithmOptions,
        IRateLimiterFactory factory, RateLimitingOptions globalOptions)
    {
        NodeId = nodeId;
        _algorithmOptions = algorithmOptions;
        _factory = factory;
        _globalOptions = globalOptions;
    }

    public string NodeId { get; }

    public RateLimitDecision Evaluate(RequestInfo requestInfo)
    {
        var clientKey = BuildClientKey(requestInfo);
        var pipeline = _pipelines.GetOrAdd(clientKey, _ => CreatePipeline());
        var result = pipeline.Evaluate(requestInfo);
        return result.Allowed
            ? RateLimitDecision.AllowedDecision(NodeId)
            : RateLimitDecision.DeniedDecision(result.RetryAfter, result.Algorithm, NodeId);
    }

    private RateLimiterPipeline CreatePipeline()
    {
        var limiters = _algorithmOptions
            .Where(o => o.Enabled)
            .Select(o => _factory.Create(o, _globalOptions))
            .ToArray();

        return new RateLimiterPipeline(limiters);
    }

    private static string BuildClientKey(RequestInfo requestInfo)
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
