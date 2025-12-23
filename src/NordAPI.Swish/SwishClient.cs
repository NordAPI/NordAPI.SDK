using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NordAPI.Swish.Errors;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish;

/// <summary>
/// Lightweight and secure .NET SDK client for Swish payment and refund operations.
/// Provides HMAC signing, optional mTLS, and basic retry/backoff for transient errors.
/// </summary>
public sealed class SwishClient : ISwishClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SwishClient>? _logger;
    private readonly SwishOptions _options = new();

    // Default JSON settings for Swish API communication (camelCase, case-insensitive)
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Creates a fully configured <see cref="HttpClient"/> pipeline with HMAC, rate limiting and mTLS.
    /// Use this when not relying on ASP.NET Core DI extensions.
    /// </summary>
    public static HttpClient CreateHttpClient(
        Uri baseAddress,
        string apiKey,
        string secret,
        HttpMessageHandler? innerHandler = null)
    {
        // Innermost transport w/ optional mTLS (reads SWISH_PFX_PATH / SWISH_PFX_PASSWORD)
        var mtlsHandler = SwishMtlsHandlerFactory.Create();

        // Build pipeline: HMAC -> RateLimit -> (custom inner?) -> mTLS transport
        var pipeline = new HmacSigningHandler(apiKey, secret)
        {
            InnerHandler = new RateLimitingHandler(maxConcurrency: 4, minDelayBetweenCalls: TimeSpan.FromMilliseconds(100))
            {
                InnerHandler = innerHandler ?? mtlsHandler
            }
        };

        return new HttpClient(pipeline) { BaseAddress = baseAddress };
    }

    /// <summary>
    /// Construct the client with a pre-configured <see cref="HttpClient"/>.
    /// </summary>
    public SwishClient(HttpClient httpClient, SwishOptions? options = null, ILogger<SwishClient>? logger = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        if (options is not null) _options = options;
    }

    // =======================================================================
    // Retry-safe HTTP executor (uses a request factory to recreate the request)
    // =======================================================================

    /// <summary>
    /// Executes an HTTP request with basic retry/backoff for transient errors and maps
    /// HTTP status codes to SDK exceptions. A request factory is used so the request
    /// can be recreated on each retry (safe for streamed content).
    /// </summary>
    private async Task<T> SendWithPolicyAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        bool isCreate = false,
        CancellationToken ct = default)
    {
        const int maxAttempts = 3;
        int attempt = 0;
        Exception? lastEx = null;

        // Generate Idempotency-Key once per operation (reused across all retry attempts)
        string? idempotencyKey = isCreate ? Guid.NewGuid().ToString("N") : null;

        while (attempt < maxAttempts)
        {
            attempt++;
            using var request = requestFactory();

            // Add Idempotency-Key for create operations if not already present
            if (isCreate && idempotencyKey is not null && !request.Headers.Contains("Idempotency-Key"))
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            try
            {
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                                                .ConfigureAwait(false);

                var body = response.Content is null
                    ? null
                    : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // Success
                if ((int)response.StatusCode is >= 200 and < 300)
                {
                    if (typeof(T) == typeof(string))
                        return (T)(object)(body ?? string.Empty);

                    if (string.IsNullOrWhiteSpace(body))
                        return default!;

                    var obj = JsonSerializer.Deserialize<T>(body, DefaultJsonSerializerOptions)!;
                    return obj;
                }

                // Non-transient error mapping
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new SwishAuthException("Authentication/authorization failed.", response.StatusCode, body);

                if (response.StatusCode is HttpStatusCode.BadRequest or (HttpStatusCode)422 /* Unprocessable Entity */)
                {
                    var apiErr = SwishApiError.TryParse(body);
                    var msg = apiErr is null ? "Validation failed." : $"Validation failed: {apiErr}";
                    throw new SwishValidationException(msg, response.StatusCode, body);
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new SwishConflictException("Conflict (possibly duplicate/idempotency collision).", response.StatusCode, body);

                // Transient → retry
                if (response.StatusCode is HttpStatusCode.RequestTimeout
                    or (HttpStatusCode)429
                    or HttpStatusCode.InternalServerError
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout)
                {
                    throw new SwishTransientException($"Transient HTTP {(int)response.StatusCode}.", response.StatusCode, body);
                }

                // Fallback
                throw new SwishException($"Unexpected HTTP {(int)response.StatusCode}.", response.StatusCode, body);
            }
            catch (SwishTransientException ex) when (attempt < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(BackoffDelay(attempt), ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(BackoffDelay(attempt), ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(BackoffDelay(attempt), ct).ConfigureAwait(false);
            }
        }

        throw new SwishTransientException($"Request failed after {maxAttempts} attempts.", null, null, lastEx);
    }

    private static TimeSpan BackoffDelay(int attempt)
    {
        // Exponential backoff with jitter: ~200ms, 400ms, 800ms (+0–100ms)
        var baseMs = 200 * (int)Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, 100);
        return TimeSpan.FromMilliseconds(baseMs + jitter);
    }

    // =======================================================================
    // ISwishClient implementation
    // =======================================================================

    /// <summary>
    /// Simple ping/health probe. Returns raw string payload from the endpoint.
    /// </summary>
    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Calling /ping...");
        using var res = await _http.GetAsync("/ping", ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var payload = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger?.LogInformation("Ping OK (bytes={Length}).", payload.Length);
        return payload;
    }

    /// <summary>
    /// Creates a Swish payment. Adds an Idempotency-Key header automatically.
    /// </summary>
    public async Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, DefaultJsonSerializerOptions);
        return await SendWithPolicyAsync<CreatePaymentResponse>(
            () => new HttpRequestMessage(HttpMethod.Post, "/payments")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            },
            isCreate: true,
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the status of a previously created payment.
    /// </summary>
    public async Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default)
    {
        return await SendWithPolicyAsync<CreatePaymentResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, $"/payments/{paymentId}"),
            isCreate: false,
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a refund for a previously created payment. Adds an Idempotency-Key header automatically.
    /// </summary>
    public async Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, DefaultJsonSerializerOptions);
        return await SendWithPolicyAsync<CreateRefundResponse>(
            () => new HttpRequestMessage(HttpMethod.Post, "/refunds")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            },
            isCreate: true,
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the status of a previously created refund.
    /// </summary>
    public async Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default)
    {
        return await SendWithPolicyAsync<CreateRefundResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, $"/refunds/{refundId}"),
            isCreate: false,
            ct: ct).ConfigureAwait(false);
    }
}

