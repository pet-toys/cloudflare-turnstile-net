#nullable enable

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
            var options = provider.GetService<CloudflareTurnstileOptions>();

            service.Should().NotBeNull();
            options?.SiteKey.Should().Be(SiteKeys.AlwaysPassesInvisible);
            options?.SecretKey.Should().Be(SecretKeys.AlwaysPasses);
        }
    }
}
