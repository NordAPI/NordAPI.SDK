namespace NordAPI.Swish;

public interface ISwishClient
{
    Task<string> CreatePaymentAsync(object request, CancellationToken ct = default);
    Task<string> RefundPaymentAsync(object request, CancellationToken ct = default);
    Task<string> GetPaymentStatusAsync(string paymentRequestToken, CancellationToken ct = default);
}

