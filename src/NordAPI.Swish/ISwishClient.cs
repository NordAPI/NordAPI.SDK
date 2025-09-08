namespace NordAPI.Swish;

public interface ISwishClient
{
    // Payments
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);
    Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);

    // Refunds
    Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default);
    Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default);
}
