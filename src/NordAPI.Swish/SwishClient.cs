using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NordAPI.Swish.Errors;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish;

public sealed class SwishClient : ISwishClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SwishClient>? _logger;
    private readonly SwishOptions _options = new();

    public SwishClient(HttpClient httpClient, SwishOptions? options = null, ILogger<SwishClient>? logger = null)
    {
        _http = httpClient;
        _logger = logger;
        if (options is not null) _options = options;
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

    // ========================== Policy-helper INNE I KLASSEN ==========================

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Central HTTP-helper som sätter Idempotency-Key för create, hanterar retry på transienta fel
    /// och mappar felkoder till våra SwishException-typer.
    /// </summary>
    private async Task<T> SendWithPolicyAsync<T>(
        HttpRequestMessage request,
        bool isCreate = false,
        CancellationToken ct = default)
    {
        // 1) Idempotency-Key för create-operationer (om inte redan satt)
        if (isCreate && !request.Headers.Contains("Idempotency-Key"))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        }

        const int maxAttempts = 3;
        int attempt = 0;
        Exception? lastEx = null;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                using var response = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct);

                var body = response.Content == null
                    ? null
                    : await response.Content.ReadAsStringAsync(ct);

                // 2xx → success
                if ((int)response.StatusCode is >= 200 and < 300)
                {
                    if (typeof(T) == typeof(string))
                        return (T)(object)(body ?? string.Empty);

                    if (string.IsNullOrWhiteSpace(body))
                        return default!;

                    var obj = JsonSerializer.Deserialize<T>(body, _json)!;
                    return obj;
                }

                // Felmappning
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new SwishAuthException("Authentication/authorization failed", response.StatusCode, body);

                if (response.StatusCode is HttpStatusCode.BadRequest or (HttpStatusCode)422)
                {
                    var apiErr = SwishApiError.TryParse(body);
                    var msg = apiErr is null ? "Validation failed" : $"Validation failed: {apiErr}";
                    throw new SwishValidationException(msg, response.StatusCode, body);
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new SwishConflictException("Conflict (possibly duplicate/idempotent key collision)", response.StatusCode, body);

                // Transienta fel → retry
                if (response.StatusCode is HttpStatusCode.RequestTimeout
                    or (HttpStatusCode)429
                    or HttpStatusCode.InternalServerError
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout)
                {
                    throw new SwishTransientException($"Transient HTTP {(int)response.StatusCode}", response.StatusCode, body);
                }

                // Annat oväntat fel
                throw new SwishException($"Unexpected HTTP {(int)response.StatusCode}", response.StatusCode, body);
            }
            catch (SwishTransientException ex) when (attempt < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(BackoffDelay(attempt), ct);
                continue;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(BackoffDelay(attempt), ct);
                continue;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(BackoffDelay(attempt), ct);
                continue;
            }
        }

        throw new SwishTransientException($"Request failed after {maxAttempts} attempts", null, null, lastEx);
    }

    private static TimeSpan BackoffDelay(int attempt)
    {
        var baseMs = 200 * (int)Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, 100);
        return TimeSpan.FromMilliseconds(baseMs + jitter);
    }

    // ======================== /Policy-helper ======================================

    // === Exempelmetod (demo) ===
    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Calling Ping endpoint...");
        var res = await _http.GetAsync("/ping", ct);
        res.EnsureSuccessStatusCode();
        var payload = await res.Content.ReadAsStringAsync(ct);
        _logger?.LogInformation("Ping OK, length={Length}", payload.Length);
        return payload;
    }

    // === ISwishClient-implementation (payments) ===
    public async Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, _json);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await SendWithPolicyAsync<CreatePaymentResponse>(msg, isCreate: true, ct);
    }

    // OBS: matchar ditt interface (returnerar CreatePaymentResponse)
    public async Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Get, $"/payments/{paymentId}");
        return await SendWithPolicyAsync<CreatePaymentResponse>(msg, isCreate: false, ct);
    }

    // === ISwishClient-implementation (refunds) ===
    public async Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, _json);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/refunds")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await SendWithPolicyAsync<CreateRefundResponse>(msg, isCreate: true, ct);
    }

    // OBS: matchar ditt interface (returnerar CreateRefundResponse)
    public async Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Get, $"/refunds/{refundId}");
        return await SendWithPolicyAsync<CreateRefundResponse>(msg, isCreate: false, ct);
    }
}
