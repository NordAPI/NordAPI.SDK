namespace NordAPI.Swish.Security.Http;

/// <summary>
/// Limits number of concurrent calls and/or minimum time between calls.
/// </summary>
public sealed class RateLimitingHandler : DelegatingHandler
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _minDelay;
    private DateTime _last = DateTime.MinValue;

    public RateLimitingHandler(int maxConcurrency = 4, TimeSpan? minDelayBetweenCalls = null)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _minDelay = minDelayBetweenCalls ?? TimeSpan.Zero;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var elapsed = DateTime.UtcNow - _last;
            var wait = _minDelay - elapsed;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken);

            var resp = await base.SendAsync(request, cancellationToken);
            _last = DateTime.UtcNow;
            return resp;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
