using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests;

public sealed class TurnstileServiceTest
{
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(SecretKeys.AlwaysPasses, true)]
    [InlineData(SecretKeys.AlwaysFails, false)]
    [InlineData(SecretKeys.TokenAlreadySpent, false)]
    public async Task VerifyAsync_WorksCorrectly(string secretKey, bool value)
    {
        var provider = CreateProvider(string.Empty, secretKey);
        var sut = provider.GetRequiredService<ITurnstileService>();

        var result = await sut.VerifyAsync("token", IPAddress.Loopback, true, TestContext.Current.CancellationToken);

        result.Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task VerifyAsync_EmptyToken_ReturnsFalseWithoutCallingCloudflare(string? token)
    {
        var handler = new RecordingHandler();
        var provider = CreateProvider(handler);
        var sut = provider.GetRequiredService<ITurnstileService>();

        var result = await sut.VerifyAsync(token!, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task VerifyAsync_NonEmptyToken_CallsCloudflare()
    {
        var handler = new RecordingHandler("""{"success":true}""");
        var provider = CreateProvider(handler);
        var sut = provider.GetRequiredService<ITurnstileService>();

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAsync_PropagatesCancellationToken()
    {
        var handler = new RecordingHandler();
        var provider = CreateProvider(handler);
        var sut = provider.GetRequiredService<ITurnstileService>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await sut.VerifyAsync("token", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
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

    private static ServiceProvider CreateProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(
            opt =>
            {
                opt.SiteKey = string.Empty;
                opt.SecretKey = SecretKeys.AlwaysPasses;
            },
            builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        return services.BuildServiceProvider();
    }

    private sealed class RecordingHandler(string responseJson = """{"success":false}""") : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson),
            };
        }
    }
}
