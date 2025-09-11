using System.Text.Json;

namespace NordAPI.Swish.Errors;

public sealed class SwishApiError
{
    public string? Code { get; init; }
    public string? Message { get; init; }

    public static SwishApiError? TryParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            return JsonSerializer.Deserialize<SwishApiError>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    public override string ToString() => $"{Code}: {Message}";
}
