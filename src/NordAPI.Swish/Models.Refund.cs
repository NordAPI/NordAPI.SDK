namespace NordAPI.Swish;

public sealed record CreateRefundRequest(
    string OriginalPaymentReference,   // id/token från betalningen du vill återbetala
    string Amount,                     // "100.00"
    string Currency,                   // "SEK"
    string Message,                    // valfritt meddelande
    string CallbackUrl                 // webhook för refund-notifiering
);

public sealed record CreateRefundResponse(
    string Id,                         // refund id
    string Status,                     // CREATED/PENDING/PAID/DECLINED...
    string? ErrorCode = null,
    string? ErrorMessage = null
);
