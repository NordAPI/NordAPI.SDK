using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http;

/// <summary>
/// HttpClientHandler that attaches a client certificate (mTLS).
/// </summary>
public sealed class ClientCertificateHandler : HttpClientHandler
{
    public ClientCertificateHandler(X509Certificate2 clientCertificate)
    {
        ClientCertificates.Add(clientCertificate);
        SslProtocols = System.Security.Authentication.SslProtocols.Tls13
                     | System.Security.Authentication.SslProtocols.Tls12;
    }
}
