using System;

namespace NordAPI.Swish;

public sealed class SwishOptions
{
    /// <summary>Bas-URL till Swish API (eller stub/testmiljö).</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Publik nyckel/identifierare för HMAC.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Delad hemlighet för HMAC-signering.</summary>
    public string? Secret { get; set; }

    /// <summary>Valfri timeout för HttpClient (om du vill använda den senare).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
