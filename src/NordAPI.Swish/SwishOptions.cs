namespace NordAPI.Swish;

public sealed class SwishOptions
{
    public Uri BaseAddress { get; set; } = new Uri("https://api.example.com/");
    public string PaymentsPath { get; set; } = "/paymentrequests";
    public string RefundsPath  { get; set; } = "/refunds";
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}
