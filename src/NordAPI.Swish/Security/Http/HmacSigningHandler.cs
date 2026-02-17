using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using NordAPI.Swish.Internal;

namespace NordAPI.Swish.Security.Http;

/// <summary>
/// Optional NordAPI Security Hardening: applies HMAC signature + timestamp + nonce.
/// Not required by official Swish spec. Useful for internal validation or proxies.
/// </summary>
internal sealed class HmacSigningHandler : DelegatingHandler
{
    private readonly string _apiKey;
    private readonly string _secret;
    private readonly ISystemClock _clock;

    public const string HeaderSignature = "X-Swish-Signature";
    public const string HeaderTimestamp = "X-Swish-Timestamp";
    public const string HeaderNonce = "X-Swish-Nonce";
    public const string HeaderApiKey = "X-Swish-Api-Key";

    public HmacSigningHandler(string apiKey, string secret, ISystemClock? clock = null)
    {
        _apiKey = apiKey;
        _secret = secret;
        _clock = clock ?? new SystemClock();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ts = _clock.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Nonce.New();

        var method = request.Method.Method.ToUpperInvariant();
        var pathAndQuery = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

        var canonical = $"{method}\n{pathAndQuery}\n{ts}\n{nonce}\n{body}";
        var sig = ComputeHmac(_secret, canonical);

        request.Headers.TryAddWithoutValidation(HeaderApiKey, _apiKey);
        request.Headers.TryAddWithoutValidation(HeaderTimestamp, ts);
        request.Headers.TryAddWithoutValidation(HeaderNonce, nonce);
        request.Headers.TryAddWithoutValidation(HeaderSignature, sig);
        request.Headers.Authorization ??= new AuthenticationHeaderValue("HMAC", _apiKey);

        return await base.SendAsync(request, cancellationToken);
    }

    public static string ComputeHmac(string secret, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes);
    }
}
