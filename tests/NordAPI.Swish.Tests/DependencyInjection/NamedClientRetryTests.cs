using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using NordAPI.Swish.DependencyInjection;

namespace NordAPI.Swish.Tests.DependencyInjection
{
    // Tests that the named HTTP client "Swish" retries on transient errors
    public class NamedClientRetryTests
    {
        [Fact]
        public async Task SwishClient_Retries_On_Transient_5xx_Then_Succeeds()
        {
            // Disable mTLS settings for test environment
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_BASE64", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASS", null);

            // Set up services with logging and Swish transport pipeline
            var services = new ServiceCollection();
            services.AddLogging(config => config.AddDebug().AddConsole());
            services.AddSwishMtlsTransport();

            // Prepare a handler sequence: first 500, then 200
            var sequenceHandler = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") },
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }
            );

            // Register the sequence handler as the primary HTTP handler
            services.AddHttpClient("Swish")
                    .ConfigurePrimaryHttpMessageHandler(_ => sequenceHandler);

            // Build provider and create the named client
            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("Swish");

            // Act: perform a GET request
            var response = await client.GetAsync("http://unit.test/ping");

            // Assert: request eventually succeeds and at least two attempts occurred
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(sequenceHandler.Attempts >= 2, $"Expected at least 2 attempts, got {sequenceHandler.Attempts}");
        }

        // A handler that returns a predefined sequence of HTTP responses
        private sealed class SequenceHandler : DelegatingHandler
        {
            private readonly HttpResponseMessage[] _responses;
            private int _currentIndex = -1;

            // Number of times SendAsync has been invoked
            public int Attempts => Math.Max(0, _currentIndex + 1);

            // Requires at least one response in the sequence
            public SequenceHandler(params HttpResponseMessage[] responses)
            {
                if (responses == null || responses.Length == 0)
                    throw new ArgumentException("At least one response must be provided", nameof(responses));

                _responses = responses;
            }

            // Returns the next response in the sequence, cloning it each time
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var index = Interlocked.Increment(ref _currentIndex);
                var chosen = index < _responses.Length ? _responses[index] : _responses[^1];
                return Task.FromResult(CloneResponse(chosen));
            }

            // Clone status code, headers, and textual content to avoid reuse issues
            private static HttpResponseMessage CloneResponse(HttpResponseMessage original)
            {
                var text = original.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                var clone = new HttpResponseMessage(original.StatusCode)
                {
                    ReasonPhrase = original.ReasonPhrase,
                    Content = new StringContent(text)
                };
                foreach (var header in original.Headers)
                {
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                return clone;
            }
        }
    }
}



