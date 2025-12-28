namespace LoadTesting;

internal readonly record struct LoadTestOptions(string Url, int TotalRequests, int Concurrency, string ClientId, ClientIdMode ClientIdMode);
