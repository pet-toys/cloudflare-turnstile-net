using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests;

public sealed class TurnstileServiceTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task VerifyAsync_EmptyToken_ReturnsFalseWithoutCallingCloudflare(string? token)
    {
        var handler = new StubHttpMessageHandler();
        var sut = CreateService(handler);

        var result = await sut.VerifyAsync(token!, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task VerifyAsync_SuccessResponse_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAsync_UnsuccessfulResponse_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":false}""");
        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_NonSuccessStatusCode_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, """{"success":true}""");
        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_PostsToConfiguredEndpoint()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        handler.LastMethod.Should().Be(HttpMethod.Post);
        handler.LastRequestUri.Should().Be(CloudflareTurnstileOptions.ValidationBaseUri);
    }

    [Fact]
    public async Task VerifyAsync_SendsSecretAndToken()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("the-token", cancellationToken: TestContext.Current.CancellationToken);

        var payload = Payload(handler);
        payload.GetProperty("secret").GetString().Should().Be(SecretKeys.AlwaysPasses);
        payload.GetProperty("response").GetString().Should().Be("the-token");
    }

    [Fact]
    public async Task VerifyAsync_WithRemoteIp_IncludesRemoteIp()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("token", IPAddress.Parse("203.0.113.7"), cancellationToken: TestContext.Current.CancellationToken);

        Payload(handler).GetProperty("remoteip").GetString().Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task VerifyAsync_WithoutRemoteIp_OmitsRemoteIp()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        Payload(handler).TryGetProperty("remoteip", out _).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_WithIdempotencyKey_IncludesParseableGuid()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("token", useIdempotencyKey: true, cancellationToken: TestContext.Current.CancellationToken);

        var key = Payload(handler).GetProperty("idempotency_key").GetString();
        Guid.TryParse(key, out _).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WithoutIdempotencyKey_OmitsIdempotencyKey()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        Payload(handler).TryGetProperty("idempotency_key", out _).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_PropagatesCancellation()
    {
        var handler = new StubHttpMessageHandler();
        var sut = CreateService(handler);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await sut.VerifyAsync("token", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(0);
    }

    private static ITurnstileService CreateService(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(
            opt =>
            {
                opt.SiteKey = SiteKeys.AlwaysPassesInvisible;
                opt.SecretKey = SecretKeys.AlwaysPasses;
            },
            builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        return services.BuildServiceProvider().GetRequiredService<ITurnstileService>();
    }

    private static JsonElement Payload(StubHttpMessageHandler handler)
    {
        handler.LastRequestBody.Should().NotBeNull();
        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        return document.RootElement.Clone();
    }
}
