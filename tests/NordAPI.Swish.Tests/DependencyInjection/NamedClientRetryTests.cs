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
    /// <summary>
    /// Verifierar att den namngivna klienten "Swish" gör retry på transienta fel.
    /// Testet injicerar en "sequence handler" som returnerar 500 (första gången)
    /// och 200 (andra gången). Vi förväntar oss att slutresultatet blir 200
    /// och att minst 2 försök har gjorts.
    /// </summary>
    public class NamedClientRetryTests
    {
        [Fact]
        public async Task SwishClient_Retries_On_Transient_5xx_Then_Succeeds()
        {
            // Säkerställ att mTLS inte triggas i test, så att pipen byggs utan cert.
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH",    null);
            Environment.SetEnvironmentVariable("SWISH_PFX_BASE64",  null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASS",     null);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddDebug().AddConsole());

            // Registrera namngiven klient "Swish" via SDK:t
            services.AddSwishHttpClient();

            // Lägg till en ytterligare handler överst i pipen som simulerar svar:
            // 1) 500  →  2) 200
            var seq = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") },
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }
            );

            // OBS: Att anropa AddHttpClient("Swish") igen kompletterar existerande pipeline
            // för samma namn i HttpClientFactory (lägger handlers överst/ytterst).
            services.AddHttpClient("Swish")
                    .ConfigurePrimaryHttpMessageHandler(_ => seq);


            using var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client  = factory.CreateClient("Swish");

            // Absolut URL så vi slipper BaseAddress-konfig
            var res = await client.GetAsync("http://unit.test/ping");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True(seq.Attempts >= 2, $"Expected at least 2 attempts, got {seq.Attempts}");
        }

        /// <summary>
        /// En enkel delegating handler som returnerar en förbestämd sekvens av svar.
        /// När sekvensen är slut returneras sista svaret för resterande anrop.
        /// </summary>
        private sealed class SequenceHandler : DelegatingHandler
        {
            private readonly HttpResponseMessage[] _responses;
            private int _index = -1;

            public int Attempts => Math.Max(0, _index + 1);

            public SequenceHandler(params HttpResponseMessage[] responses)
            {
                if (responses is null || responses.Length == 0)
                    throw new ArgumentException("At least one response is required.", nameof(responses));

                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var next = Interlocked.Increment(ref _index);

                // Om vi har fler definierade svar: använd nästa, annars återanvänd sista.
                var i = next < _responses.Length ? next : _responses.Length - 1;

                // Viktigt: kopiera inte responsen; låt testet vara enkelt.
                return Task.FromResult(CloneIfConsumed(_responses[i]));
            }

            private static HttpResponseMessage CloneIfConsumed(HttpResponseMessage original)
            {
                // För enkelhet: skapa en ny response som speglar status + enkel text.
                // (Att återanvända samma HttpResponseMessage flera gånger är inte säkert.)
                var text = original.Content is null ? "" : original.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
