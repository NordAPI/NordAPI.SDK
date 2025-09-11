using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish;

public interface ISwishClient
{
    // Hälso/prov-anrop (implementerat i SwishClient)
    Task<string> PingAsync(CancellationToken ct = default);

    // Payments
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);
    Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);

    // Refunds
    Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default);
    Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default);
}
