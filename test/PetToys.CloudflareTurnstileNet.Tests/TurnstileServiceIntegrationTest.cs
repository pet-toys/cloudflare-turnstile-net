using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests;

/// <summary>
/// Smoke tests against the live Cloudflare siteverify endpoint using the
/// documented testing secret keys. Require outbound network access; filter
/// them out with <c>--filter Category!=Integration</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TurnstileServiceIntegrationTest
{
    [Theory]
    [InlineData(SecretKeys.AlwaysPasses, true)]
    [InlineData(SecretKeys.AlwaysFails, false)]
    [InlineData(SecretKeys.TokenAlreadySpent, false)]
    public async Task VerifyAsync_AgainstCloudflareSandbox_ReturnsExpected(string secretKey, bool expected)
    {
        var provider = new ServiceCollection()
            .AddCloudflareTurnstile(opt =>
            {
                opt.SiteKey = string.Empty;
                opt.SecretKey = secretKey;
            })
            .BuildServiceProvider();
        var sut = provider.GetRequiredService<ITurnstileService>();

        var result = await sut.VerifyAsync("token", IPAddress.Loopback, true, TestContext.Current.CancellationToken);

        result.Should().Be(expected);
    }
}
