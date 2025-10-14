#nullable enable
namespace NordAPI.Security;

/// <summary>Default production clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
