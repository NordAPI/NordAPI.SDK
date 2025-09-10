using System;

namespace NordAPI.Swish.Security.Webhooks;

/// <summary>
/// Lagrar inkommande nonces så att samma meddelande inte kan spelas upp igen (replay).
/// Returnerar true om noncen var ny och registrerades, annars false (replay).
/// </summary>
public interface ISwishNonceStore
{
    bool TryRemember(string nonce, DateTimeOffset expiresAtUtc);
}
