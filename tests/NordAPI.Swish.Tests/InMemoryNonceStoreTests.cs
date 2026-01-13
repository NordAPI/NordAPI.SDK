using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests;

public sealed class InMemoryNonceStoreTests
{
    [Fact]
    public async Task TryRememberAsync_IsAtomic_UnderConcurrency()
    {
        using var store = new InMemoryNonceStore(TimeSpan.FromMinutes(10));

        var nonce = "same-nonce";
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

        var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 500)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await store.TryRememberAsync(nonce, expiresAtUtc);
            }))
            .ToArray();

        start.Set();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(results.Length - 1, results.Count(r => !r));
    }
}
