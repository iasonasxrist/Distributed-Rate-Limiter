using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Options;

namespace RateLimiting.Infrastructure.Distributed;

public sealed class DistributedRateLimiter : IDistributedRateLimiter
{
    private readonly IReadOnlyList<RateLimiterNode> _nodes;
    private int _nextNodeIndex = -1;

    public DistributedRateLimiter(RateLimitingOptions options, IRateLimiterFactory factory)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var algorithms = options.Algorithms.Count == 0
            ? new List<RateLimiterAlgorithmOptions>
            {
                new()
                {
                    Name = "sliding-window",
                    Type = RateLimiterAlgorithmType.SlidingWindow,
                    MaxRequests = options.DefaultMaxRequests,
                    WindowSeconds = options.DefaultWindowSeconds,
                    Enabled = true
                }
            }
            : options.Algorithms.Where(a => a.Enabled).ToList();

        if (algorithms.Count == 0)
        {
            throw new InvalidOperationException("At least one enabled rate limiter algorithm must be configured.");
        }

        var nodeCount = Math.Max(1, options.ClusterNodeCount);
        var nodes = new List<RateLimiterNode>(nodeCount);
        for (var i = 0; i < nodeCount; i++)
        {
            nodes.Add(new RateLimiterNode($"node-{i + 1}", algorithms, factory, options));
        }

        _nodes = nodes;
    }

    public RateLimitDecision ShouldAllow(RequestInfo requestInfo)
    {
        if (_nodes.Count == 0)
        {
            return RateLimitDecision.AllowedDecision("local");
        }

        var index = Math.Abs(Interlocked.Increment(ref _nextNodeIndex)) % _nodes.Count;
        var node = _nodes[index];
        return node.Evaluate(requestInfo);
    }
}
