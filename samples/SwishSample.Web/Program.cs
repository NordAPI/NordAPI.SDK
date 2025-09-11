using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Registrera SwishClient i DI (placeholder-värden tills vi kör riktiga tester)
builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL") ?? "https://example.invalid");
    opts.ApiKey      = Environment.GetEnvironmentVariable("SWISH_API_KEY") ?? "dev-key";
    opts.Secret      = Environment.GetEnvironmentVariable("SWISH_SECRET") ?? "dev-secret";
});

// Bygg appen
var app = builder.Build();

// Root endpoint (så vi slipper 404 på /)
app.MapGet("/", () => "Swish sample is running. Try /health or /ping");

// Enkel health-check
app.MapGet("/health", () => "ok");

// Kontrollera att ISwishClient registrerats i DI
app.MapGet("/di-check", (ISwishClient swish) =>
    swish is not null ? "ISwishClient is registered" : "not found"
);

// Mockad ping (ingen riktig HTTP-request)
app.MapGet("/ping", () => Results.Ok("pong (mocked)"));

// Starta appen
app.Run();
