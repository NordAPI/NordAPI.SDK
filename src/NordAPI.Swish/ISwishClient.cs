using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish;

/// <summary>
/// Defines the contract for a Swish API client used to create and query payments and refunds.
/// </summary>
public interface ISwishClient
{
    /// <summary>
    /// Performs a lightweight health or connectivity check against the Swish API.
    /// </summary>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>Returns a string response (e.g., "pong" or similar health indicator).</returns>
    Task<string> PingAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new Swish payment request.
    /// </summary>
    /// <param name="request">The payment request details.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="CreatePaymentResponse"/> containing the payment details.</returns>
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current status of a Swish payment.
    /// </summary>
    /// <param name="paymentId">The unique payment ID returned when creating the payment.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="CreatePaymentResponse"/> with the latest payment status.</returns>
    Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new refund for a previously completed Swish payment.
    /// </summary>
    /// <param name="request">The refund request details.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="CreateRefundResponse"/> with refund confirmation details.</returns>
    Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current status of a Swish refund.
    /// </summary>
    /// <param name="refundId">The unique refund ID returned when creating the refund.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="CreateRefundResponse"/> with the latest refund status.</returns>
    Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default);
}

