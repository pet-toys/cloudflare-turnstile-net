#nullable enable

using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests
{
    [Trait("Category", "Integration")]
    public sealed class TurnstileServiceTest
    {
        [Theory]
        [InlineData(SecretKeys.AlwaysPasses, true)]
        [InlineData(SecretKeys.AlwaysFails, false)]
        [InlineData(SecretKeys.TokenAlreadySpent, false)]
        public async Task VerifyAsync_WorksCorrectly(string secretKey, bool value)
        {
            var provider = CreateProvider(string.Empty, secretKey);
            var sut = provider.GetRequiredService<ITurnstileService>();

            var result = await sut.VerifyAsync("token", IPAddress.Loopback, Guid.NewGuid());

            result.Should().Be(value);
        }

        private static ServiceProvider CreateProvider(string siteKey, string secretKey)
        {
            var services = new ServiceCollection();
            services.AddCloudflareTurnstile(opt =>
            {
                opt.SiteKey = siteKey;
                opt.SecretKey = secretKey;
            });

            return services.BuildServiceProvider();
        }
    }
}
