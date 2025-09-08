using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish;

public sealed class SwishClient : ISwishClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SwishClient>? _logger;

    public SwishClient(HttpClient httpClient, ILogger<SwishClient>? logger = null)
    {
        _http = httpClient;
        _logger = logger;
    }

    public static HttpClient CreateHttpClient(
        Uri baseAddress,
        string apiKey,
        string secret,
        HttpMessageHandler? innerHandler = null)
    {
        // Pipeline: HMAC -> RateLimiter -> (inner or default)
        var pipeline = new HmacSigningHandler(apiKey, secret)
        {
            InnerHandler = new RateLimitingHandler(maxConcurrency: 4, minDelayBetweenCalls: TimeSpan.FromMilliseconds(100))
            {
                InnerHandler = innerHandler ?? new HttpClientHandler()
            }
        };

        var http = new HttpClient(pipeline) { BaseAddress = baseAddress };
        return http;
    }

    // Exempelmetod (placeholder)
    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Calling Ping endpoint...");
        var res = await _http.GetAsync("/ping", ct);
        res.EnsureSuccessStatusCode();
        var payload = await res.Content.ReadAsStringAsync(ct);
        _logger?.LogInformation("Ping OK, length={Length}", payload.Length);
        return payload;
    }

    // --- Nya interface-metoder (dummy-implementationer) ---
    public Task<string> CreatePaymentAsync(object request, CancellationToken ct = default)
        => Task.FromResult("NOT_IMPLEMENTED");

    public Task<string> RefundPaymentAsync(object request, CancellationToken ct = default)
        => Task.FromResult("NOT_IMPLEMENTED");

    public Task<string> GetPaymentStatusAsync(string paymentRequestToken, CancellationToken ct = default)
        => Task.FromResult("NOT_IMPLEMENTED");
}
