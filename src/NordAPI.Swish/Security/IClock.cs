#nullable enable
namespace NordAPI.Security;

/// <summary>Abstraction for time, to enable testable clock-skew logic.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
