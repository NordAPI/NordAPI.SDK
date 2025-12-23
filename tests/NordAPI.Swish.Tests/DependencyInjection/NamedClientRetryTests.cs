using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using Xunit;

namespace NordAPI.Swish.Tests.DependencyInjection
{
    // Tests that SwishClient retries on transient errors when using the named HTTP client "Swish".
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

            var services = new ServiceCollection();
            services.AddLogging(config => config.AddDebug().AddConsole());

            // SDK transport pipeline (rate limiting, primary preserving, etc.)
            services.AddSwishMtlsTransport();

            // Prepare a handler sequence: first 500, then 200 with valid JSON body
            var sequenceHandler = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") },
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test-id\",\"status\":\"CREATED\"}") }
            );

            // Override the named "Swish" client to use our deterministic handler + base address
            services.AddHttpClient("Swish")
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://unit.test/"))
                .ConfigurePrimaryHttpMessageHandler(_ => sequenceHandler);

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("Swish");
            var swish = new SwishClient(httpClient);

            // Act: SwishClient should retry internally and then succeed
            var result = await swish.GetPaymentStatusAsync("test-payment-id");

            // Assert
            Assert.Equal("test-id", result.Id);
            Assert.Equal("CREATED", result.Status);
            Assert.True(sequenceHandler.Attempts >= 2, $"Expected at least 2 attempts, got {sequenceHandler.Attempts}");
        }

        private sealed class SequenceHandler : DelegatingHandler
        {
            private readonly HttpResponseMessage[] _responses;
            private int _currentIndex = -1;

            public int Attempts => Math.Max(0, _currentIndex + 1);

            public SequenceHandler(params HttpResponseMessage[] responses)
            {
                if (responses is null || responses.Length == 0)
                    throw new ArgumentException("At least one response must be provided", nameof(responses));

                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var index = Interlocked.Increment(ref _currentIndex);
                var chosen = index < _responses.Length ? _responses[index] : _responses[^1];
                return Task.FromResult(CloneResponse(chosen));
            }

            private static HttpResponseMessage CloneResponse(HttpResponseMessage original)
            {
                var text = original.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                var clone = new HttpResponseMessage(original.StatusCode)
                {
                    ReasonPhrase = original.ReasonPhrase,
                    Content = new StringContent(text)
                };

                foreach (var header in original.Headers)
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

                return clone;
            }
        }
    }
}
