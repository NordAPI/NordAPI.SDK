namespace NordAPI.Swish;

/// <summary>
/// Represents a request to create a new Swish payment.
/// </summary>
/// <param name="PayerAlias">The payer's Swish alias (usually a phone number starting with country code).</param>
/// <param name="PayeeAlias">The recipient's Swish alias or merchant ID.</param>
/// <param name="Amount">The payment amount as a string (Swish expects a fixed decimal format).</param>
/// <param name="Currency">The ISO currency code (e.g., "SEK").</param>
/// <param name="Message">Optional message to include with the payment request.</param>
/// <param name="CallbackUrl">The URL Swish will call back upon payment status change.</param>
public sealed record CreatePaymentRequest(
    string PayerAlias,
    string PayeeAlias,
    string Amount,
    string Currency,
    string Message,
    string CallbackUrl
);

/// <summary>
/// Represents the response returned after creating or querying a Swish payment.
/// </summary>
/// <param name="Id">Unique identifier of the Swish payment.</param>
/// <param name="Status">Current status of the payment (e.g., "CREATED", "PAID", "ERROR").</param>
/// <param name="ErrorCode">Optional Swish error code if the operation failed.</param>
/// <param name="ErrorMessage">Optional human-readable description of the error.</param>
public sealed record CreatePaymentResponse(
    string Id,
    string Status,
    string? ErrorCode = null,
    string? ErrorMessage = null
);
