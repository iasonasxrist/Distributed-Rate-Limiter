using System.ComponentModel.DataAnnotations;

namespace RateLimiting.Infrastructure.Options;

public sealed class RateLimitingOptions
{
    [Range(1, int.MaxValue, ErrorMessage = "Cluster must contain at least one node")]
    public int ClusterNodeCount { get; set; } = 2;

    [Required]
    [MinLength(1)]
    public string ClientIdHeader { get; set; } = "X-Client-Id";

    [Range(1, int.MaxValue)]
    public int DefaultWindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int DefaultMaxRequests { get; set; } = 20;

    public IList<RateLimiterAlgorithmOptions> Algorithms { get; init; } = new List<RateLimiterAlgorithmOptions>();
}
