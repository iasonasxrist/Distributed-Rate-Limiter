using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using LoadTesting;

var defaults = new LoadTestOptions(
    Url :"http://localhost:8080/WeatherForecast",
    TotalRequests :200,
    Concurrency :2,
    ClientId :"load-test-id",
    ClientIdMode.Unique);


LoadTestOptions options;
if (args.Length == 0)
{
    Console.WriteLine("Interactive load test setup (press Enter to accept) .");
    options = new LoadTestOptions(
        Url: PromptString("URL", defaults.Url),
        TotalRequests: PromptInt("TotalRequests", defaults.TotalRequests),
        Concurrency: PromptInt("Concurrency", defaults.Concurrency),
        ClientId: PromptOptionalString("X-Client-Id base (empty=auto)",defaults.ClientId ),
        ClientIdMode:PromptClientIdMode(defaults.ClientIdMode)
        );
}
else
{
    options = new LoadTestOptions(
        Url: args.Length>0 ? args[0] : defaults.Url,
        TotalRequests: args.Length > 1 && Int32.TryParse( args[1], out var parsedTotal) ? parsedTotal: defaults.TotalRequests,
        Concurrency: args.Length>2 && Int32.TryParse( args[2], out var parsedConcurrency) ? parsedConcurrency: defaults.Concurrency ,
        ClientId: args.Length>3 ? args[3]: defaults.ClientId,
        ClientIdMode: args.Length> 4 ? ParseClientIdMode(args[4]) :defaults.ClientIdMode);
}

var url = options.Url;
var totalRequests = options.TotalRequests;
var concurrency = options.Concurrency;
var clientId = options.ClientId;
var clientIdMode = options.ClientIdMode;

Console.WriteLine("Load test settings: ");
Console.WriteLine($"URL: {url}");
Console.WriteLine($"TotalRequests: {totalRequests}");
Console.WriteLine($"Concurrency: {concurrency}");
Console.WriteLine($"X-Client-Id: {clientId}");
Console.WriteLine($"  X-Client-Id mode: {clientIdMode}");

using var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(url);
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
if (clientIdMode == ClientIdMode.Single)
{
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Id", clientId);
}

var stopwatch = Stopwatch.StartNew();
var semaphore = new SemaphoreSlim(concurrency, concurrency);
var statusCounts = new ConcurrentDictionary<int, int>();
var durations = new ConcurrentBag<long>();
var requestCounter = 0;
var tasks = Enumerable.Range(0, totalRequests).Select(async _ =>
{
    await semaphore.WaitAsync();

    try
    {
        var requestStopWatch = Stopwatch.StartNew();
        using var response = clientIdMode == ClientIdMode.Single
            ? await httpClient.GetAsync(url)
            : await SendWithClientIdAsync(
                httpClient,
                url,
                clientId,
                clientIdMode,
                Interlocked.Increment(ref requestCounter));
        requestStopWatch.Stop();
        requestStopWatch.Stop();

        durations.Add(requestStopWatch.ElapsedMilliseconds);
        statusCounts.AddOrUpdate((int)response?.StatusCode, 1, (key, value) => value + 1);

    }
    catch (Exception ex)
    {
        statusCounts.AddOrUpdate(-1, 1, (_, count) => count + 1);
        Console.WriteLine($"Request failed :{ex.Message}");
    }
    finally
    {
        semaphore.Release();
    }
}).ToArray();

await Task.WhenAll(tasks);
stopwatch.Stop();


var orderedStatuses = statusCounts.OrderBy(kvp => kvp.Key).ToList();
var totalElapsedMs = stopwatch.ElapsedMilliseconds;
var avgLatency = durations.Count > 0 ? durations.Average() : 0;
var p95Latency = durations.Count > 0
    ? durations.OrderBy(x => x).Skip((int)(durations.Count * 0.95)).FirstOrDefault()
    : 0;

Console.WriteLine("\nResults:");
Console.WriteLine($"  Total time: {totalElapsedMs} ms");
Console.WriteLine($"  Avg latency: {avgLatency:F2} ms");
Console.WriteLine($"  P95 latency: {p95Latency} ms");
Console.WriteLine("  Status codes:");
foreach (var (status, count) in orderedStatuses)
{
    var label = status == -1 ? "error" : status.ToString();
    Console.WriteLine($"    {label}: {count}");
}

static string PromptString(string prompt, string defaultValue)
{ 
    Console.Write($"[{prompt}] [{defaultValue}]: ");
    var input = Console.ReadLine();
    return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
}

static int PromptInt(string prompt, int defaultValue)
{
    Console.Write($"[{prompt}] [{defaultValue}]: ");
    var input = Console.ReadLine();
    return Int32.TryParse(input, out var result) ? result : defaultValue;
}

static string PromptOptionalString(string label, string defaultValue)
{
    Console.Write($"{label} [{defaultValue}]: ");
    var input = Console.ReadLine();
    return string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim();
}

static ClientIdMode PromptClientIdMode(ClientIdMode defaultMode)
{
    Console.Write($"X-Client-Id mode [{defaultMode}] (single|unique|auto): ");
    var input = Console.ReadLine();
    return string.IsNullOrWhiteSpace(input) ? defaultMode : ParseClientIdMode(input);
}

static ClientIdMode ParseClientIdMode(string input)
{
    var trimmed = input.Trim();
    if (trimmed.Equals("unique", StringComparison.OrdinalIgnoreCase))
    {
        return ClientIdMode.Unique;
    }

    if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
    {
        return ClientIdMode.Auto;
    }

    return ClientIdMode.Single;
}

static Task<HttpResponseMessage> SendWithClientIdAsync(
    HttpClient httpClient,
    string url,
    string baseClientId,
    ClientIdMode clientIdMode,
    int index)
{
    var resolvedBase = string.IsNullOrWhiteSpace(baseClientId) ? $"client-{Guid.NewGuid():N}" : baseClientId;
    var clientIdValue = clientIdMode == ClientIdMode.Auto
        ? $"client-{Guid.NewGuid():N}"
        : $"{resolvedBase}-{index}";
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("X-Client-Id", clientIdValue);
    return httpClient.SendAsync(request);
}