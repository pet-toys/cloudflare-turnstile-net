#nullable enable

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests
{
    public sealed class ServiceCollectionExtensionsTest
    {
        [Fact]
        public void AddCloudflareTurnstile_WorksCorrectly()
        {
            var services = new ServiceCollection();
            services.AddCloudflareTurnstile(opt =>
            {
                opt.SiteKey = SiteKeys.AlwaysPassesInvisible;
                opt.SecretKey = SecretKeys.AlwaysPasses;
            });

            var provider = services.BuildServiceProvider();
            var service = provider.GetService<ITurnstileService>();
            var options = provider.GetService<IOptions<CloudflareTurnstileOptions>>();

            service.Should().NotBeNull();
            options.Should().NotBeNull();
            options?.Value.SiteKey.Should().Be(SiteKeys.AlwaysPassesInvisible);
            options?.Value.SecretKey.Should().Be(SecretKeys.AlwaysPasses);
        }

        [Fact]
        public void AddCloudflareTurnstile_WorksCorrectly_WithConfiguration()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{nameof(CloudflareTurnstileOptions)}:{nameof(CloudflareTurnstileOptions.SiteKey)}"] = SiteKeys.AlwaysPassesInvisible,
                [$"{nameof(CloudflareTurnstileOptions)}:{nameof(CloudflareTurnstileOptions.SecretKey)}"] = SecretKeys.AlwaysPasses,
            }).Build();

            var services = new ServiceCollection();
            services.AddCloudflareTurnstile(configuration.GetSection(nameof(CloudflareTurnstileOptions)));

            var provider = services.BuildServiceProvider();
            var service = provider.GetService<ITurnstileService>();
            var options = provider.GetService<IOptions<CloudflareTurnstileOptions>>();

            service.Should().NotBeNull();
            options.Should().NotBeNull();
            options?.Value.SiteKey.Should().Be(SiteKeys.AlwaysPassesInvisible);
            options?.Value.SecretKey.Should().Be(SecretKeys.AlwaysPasses);
        }
    }
}
