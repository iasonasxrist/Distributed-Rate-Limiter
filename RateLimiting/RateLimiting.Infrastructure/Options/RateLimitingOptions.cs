using System.ComponentModel.DataAnnotations;

namespace RateLimiting.Infrastructure.Options;

public sealed class RateLimitingOptions
{ 
    [Range(1, int.MaxValue, ErrorMessage = "MaxRequests must be > 0")]
    public int MaxRequests { get; set; } = 2;

    [Range(1, int.MaxValue, ErrorMessage = "WindowSeconds must be > 0")]
    public int WindowSeconds { get; set; } = 20;
    
}