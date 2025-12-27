using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RateLimiting.Domain.Contracts;
using RateLimiting.Infrastructure.Options;

namespace RateLimiting.Infrastructure.Distributed;

public sealed class DistributedRateLimiter : IDistributedRateLimiter
{
    private readonly IRateLimitingOptionsProvider _optionsProvider;
    private readonly IRateLimiterFactory _factory;
    private readonly object _syncRoot = new();
    private IReadOnlyList<RateLimiterNode> _nodes = Array.Empty<RateLimiterNode>();
    private int _nextNodeIndex = -1;
    private long _optionsVersion = -1;

    public DistributedRateLimiter(IRateLimitingOptionsProvider optionsProvider, IRateLimiterFactory factory)
    {
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        RebuildNodes(_optionsProvider.GetCurrentOptions());
        _optionsVersion = _optionsProvider.Version;
    }

    public RateLimitDecision ShouldAllow(RequestInfo requestInfo)
    {
        EnsureLatestNodes();

        if (_nodes.Count == 0)
        {
            return RateLimitDecision.AllowedDecision("local");
        }

        var index = (int)((uint)Interlocked.Increment(ref _nextNodeIndex) % (uint)_nodes.Count);
        var node = _nodes[index];
        return node.Evaluate(requestInfo);
    }

    private void EnsureLatestNodes()
    {
        var version = _optionsProvider.Version;
        if (version == _optionsVersion)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (version == _optionsVersion)
            {
                return;
            }

            RebuildNodes(_optionsProvider.GetCurrentOptions());
            _optionsVersion = version;
            _nextNodeIndex = -1;
        }
    }

    private void RebuildNodes(RateLimitingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var configuredAlgorithms = options.Algorithms ?? new List<RateLimiterAlgorithmOptions>();

        var algorithms = configuredAlgorithms.Count == 0
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
            : configuredAlgorithms.Where(a => a.Enabled).ToList();

        if (algorithms.Count == 0)
        {
            throw new InvalidOperationException("At least one enabled rate limiter algorithm must be configured.");
        }

        var nodeCount = Math.Max(1, options.ClusterNodeCount);
        var nodes = new List<RateLimiterNode>(nodeCount);
        for (var i = 0; i < nodeCount; i++)
        {
            nodes.Add(new RateLimiterNode($"node-{i + 1}", algorithms, _factory, options));
        }

        _nodes = nodes;
    }
}
