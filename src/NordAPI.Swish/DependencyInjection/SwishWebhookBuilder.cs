using Microsoft.Extensions.DependencyInjection;

namespace NordAPI.Swish.Webhooks
{
    public interface ISwishWebhookBuilder
    {
        IServiceCollection Services { get; }
    }

    internal sealed class SwishWebhookBuilder : ISwishWebhookBuilder
    {
        public SwishWebhookBuilder(IServiceCollection services) => Services = services;
        public IServiceCollection Services { get; }
    }
}

