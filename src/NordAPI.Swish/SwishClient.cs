using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish;

public sealed class SwishClient : ISwishClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SwishClient>? _logger;
    private readonly SwishOptions _options;

    public SwishClient(HttpClient httpClient, SwishOptions? options = null, ILogger<SwishClient>? logger = null)
    {
        _http = httpClient;
        _logger = logger;
        _options = options ?? new SwishOptions();
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

    // --- Ping (placeholder) ---
    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Calling Ping endpoint...");
        var res = await _http.GetAsync("/ping", ct);
        res.EnsureSuccessStatusCode();
        var payload = await res.Content.ReadAsStringAsync(ct);
        _logger?.LogInformation("Ping OK, length={Length}", payload.Length);
        return payload;
    }

    // --- Payments ---
    public async Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        var url = _options.PaymentsPath;
        _logger?.LogInformation("POST {Url}", url);

        using var res = await _http.PostAsJsonAsync(url, request, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Swish CreatePayment failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {err}");
        }

        var payload = await res.Content.ReadFromJsonAsync<CreatePaymentResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Empty CreatePaymentResponse");
        return payload;
    }

    public async Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default)
    {
        var url = $"{_options.PaymentsPath}/{paymentId}";
        _logger?.LogInformation("GET {Url}", url);

        using var res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Swish GetPaymentStatus failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {err}");
        }

        var payload = await res.Content.ReadFromJsonAsync<CreatePaymentResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Empty GetPaymentStatus response");
        return payload;
    }

    // --- Refunds ---
    public async Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default)
    {
        var url = _options.RefundsPath;
        _logger?.LogInformation("POST {Url}", url);

        using var res = await _http.PostAsJsonAsync(url, request, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Swish CreateRefund failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {err}");
        }

        var payload = await res.Content.ReadFromJsonAsync<CreateRefundResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Empty CreateRefundResponse");
        return payload;
    }

    public async Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default)
    {
        var url = $"{_options.RefundsPath}/{refundId}";
        _logger?.LogInformation("GET {Url}", url);

        using var res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Swish GetRefundStatus failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {err}");
        }

        var payload = await res.Content.ReadFromJsonAsync<CreateRefundResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Empty GetRefundStatus response");
        return payload;
    }
}

