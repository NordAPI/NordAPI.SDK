using System;
using Microsoft.Extensions.DependencyInjection;
using NordAPI.Swish.DependencyInjection;
using Xunit;

namespace NordAPI.Swish.Tests.DependencyInjection;

public sealed class SwishWebhookOptionsValidationTests
{
    [Fact]
    public void AddSwishWebhookVerification_Accepts_Zero_Boundary_Values()
    {
        var services = new ServiceCollection();

        var builder = services.AddSwishWebhookVerification(options =>
        {
            options.SharedSecret = "dev_secret";
            options.AllowedClockSkew = TimeSpan.Zero;
            options.MaxMessageAge = TimeSpan.Zero;
        });

        Assert.NotNull(builder);
    }

    [Fact]
    public void AddSwishWebhookVerification_Accepts_FifteenMinute_Boundary_Values()
    {
        var services = new ServiceCollection();

        var builder = services.AddSwishWebhookVerification(options =>
        {
            options.SharedSecret = "dev_secret";
            options.AllowedClockSkew = TimeSpan.FromMinutes(15);
            options.MaxMessageAge = TimeSpan.FromMinutes(15);
        });

        Assert.NotNull(builder);
    }

    [Fact]
    public void AddSwishWebhookVerification_Rejects_Negative_AllowedClockSkew()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSwishWebhookVerification(options =>
            {
                options.SharedSecret = "dev_secret";
                options.AllowedClockSkew = TimeSpan.FromSeconds(-1);
            }));

        Assert.Contains("AllowedClockSkew", ex.Message);
    }

    [Fact]
    public void AddSwishWebhookVerification_Rejects_AllowedClockSkew_Above_Fifteen_Minutes()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSwishWebhookVerification(options =>
            {
                options.SharedSecret = "dev_secret";
                options.AllowedClockSkew = TimeSpan.FromMinutes(15).Add(TimeSpan.FromTicks(1));
            }));

        Assert.Contains("AllowedClockSkew", ex.Message);
    }

    [Fact]
    public void AddSwishWebhookVerification_Rejects_Negative_MaxMessageAge()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSwishWebhookVerification(options =>
            {
                options.SharedSecret = "dev_secret";
                options.MaxMessageAge = TimeSpan.FromSeconds(-1);
            }));

        Assert.Contains("MaxMessageAge", ex.Message);
    }

    [Fact]
    public void AddSwishWebhookVerification_Rejects_MaxMessageAge_Above_Fifteen_Minutes()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSwishWebhookVerification(options =>
            {
                options.SharedSecret = "dev_secret";
                options.MaxMessageAge = TimeSpan.FromMinutes(15).Add(TimeSpan.FromTicks(1));
            }));

        Assert.Contains("MaxMessageAge", ex.Message);
    }
}
