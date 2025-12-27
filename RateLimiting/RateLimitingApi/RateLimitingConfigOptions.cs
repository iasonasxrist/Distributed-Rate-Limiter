namespace RateLimitingApi;

public sealed class RateLimitingConfigOptions
{
    public string RedisKey { get; set; } = "rate-limiting:options";

    public int PollSeconds { get; set; } = 10;
}
