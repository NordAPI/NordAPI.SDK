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
    /// Verifierar att den namngivna klienten "Swish" gÃ¶r retry pÃ¥ transienta fel.
    /// Testet injicerar en "sequence handler" som returnerar 500 (fÃ¶rsta gÃ¥ngen)
    /// och 200 (andra gÃ¥ngen). Vi fÃ¶rvÃ¤ntar oss att slutresultatet blir 200
    /// och att minst 2 fÃ¶rsÃ¶k har gjorts.
    /// </summary>
    public class NamedClientRetryTests
    {
        [Fact]
        public async Task SwishClient_Retries_On_Transient_5xx_Then_Succeeds()
        {
            // SÃ¤kerstÃ¤ll att mTLS inte triggas i test, sÃ¥ att pipen byggs utan cert.
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH",    null);
            Environment.SetEnvironmentVariable("SWISH_PFX_BASE64",  null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASS",     null);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddDebug().AddConsole());

            // Registrera namngiven klient "Swish" via SDK:t
            services.AddSwishMtlsTransport();

            // LÃ¤gg till en ytterligare handler Ã¶verst i pipen som simulerar svar:
            // 1) 500  â†’  2) 200
            var seq = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") },
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }
            );

            // OBS: Att anropa AddHttpClient("Swish") igen kompletterar existerande pipeline
            // fÃ¶r samma namn i HttpClientFactory (lÃ¤gger handlers Ã¶verst/ytterst).
            services.AddHttpClient("Swish")
                    .ConfigurePrimaryHttpMessageHandler(_ => seq);


            using var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client  = factory.CreateClient("Swish");

            // Absolut URL sÃ¥ vi slipper BaseAddress-konfig
            var res = await client.GetAsync("http://unit.test/ping");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True(seq.Attempts >= 2, $"Expected at least 2 attempts, got {seq.Attempts}");
        }

        /// <summary>
        /// En enkel delegating handler som returnerar en fÃ¶rbestÃ¤md sekvens av svar.
        /// NÃ¤r sekvensen Ã¤r slut returneras sista svaret fÃ¶r resterande anrop.
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

                // Om vi har fler definierade svar: anvÃ¤nd nÃ¤sta, annars Ã¥teranvÃ¤nd sista.
                var i = next < _responses.Length ? next : _responses.Length - 1;

                // Viktigt: kopiera inte responsen; lÃ¥t testet vara enkelt.
                return Task.FromResult(CloneIfConsumed(_responses[i]));
            }

            private static HttpResponseMessage CloneIfConsumed(HttpResponseMessage original)
            {
                // FÃ¶r enkelhet: skapa en ny response som speglar status + enkel text.
                // (Att Ã¥teranvÃ¤nda samma HttpResponseMessage flera gÃ¥nger Ã¤r inte sÃ¤kert.)
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

