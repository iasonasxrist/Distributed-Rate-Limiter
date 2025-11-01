using System.ComponentModel.DataAnnotations;

namespace RateLimiting.Infrastructure.Options;

public sealed class RateLimiterAlgorithmOptions
{
    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public RateLimiterAlgorithmType Type { get; set; } = RateLimiterAlgorithmType.SlidingWindow;

    [Range(1, int.MaxValue)]
    public int MaxRequests { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Capacity for algorithms such as token bucket (maximum stored tokens) or queue size for leaky bucket.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Capacity { get; set; } = 20;

    /// <summary>
    /// Refill rate (tokens/requests per second) used by burst-friendly algorithms like token bucket.
    /// </summary>
    [Range(typeof(double), "0.0001", "1E+09")]
    public double RefillRatePerSecond { get; set; } = 5;

    public bool Enabled { get; set; } = true;
}
