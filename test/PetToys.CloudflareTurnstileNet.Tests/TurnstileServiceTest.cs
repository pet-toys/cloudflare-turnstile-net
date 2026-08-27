using System;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("""{"success":""")]
    public async Task VerifyAsync_UnreadableJsonBody_ReturnsFalse(string responseJson)
    {
        // A body the package cannot read proves nothing about the visitor, so it counts as a
        // failed challenge rather than an unhandled error on the protected form post.
        var handler = new StubHttpMessageHandler(responseJson: responseJson);
        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_NonJsonContentType_ReturnsFalse()
    {
        // A proxy or captive portal answering 200 with an HTML error page.
        var handler = new StubHttpMessageHandler(
            responseJson: "<html><body>Sign in to continue</body></html>",
            mediaType: MediaTypeNames.Text.Html);
        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_JsonNullBody_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler(responseJson: "null");
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

    [Fact]
    public async Task VerifyAsync_TransportFailure_ReturnsFalse()
    {
        var sut = CreateService(new ThrowingHttpMessageHandler(new HttpRequestException("connection refused")));

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_HttpClientTimeout_ReturnsFalse()
    {
        // An HttpClient timeout (not caller-initiated) surfaces as a TaskCanceledException;
        // it must fail closed rather than propagate.
        var sut = CreateService(new ThrowingHttpMessageHandler(new TaskCanceledException("timeout")));

        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_SameToken_ProducesStableIdempotencyKey()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("same-token", useIdempotencyKey: true, cancellationToken: TestContext.Current.CancellationToken);
        var first = Payload(handler).GetProperty("idempotency_key").GetString();

        await sut.VerifyAsync("same-token", useIdempotencyKey: true, cancellationToken: TestContext.Current.CancellationToken);
        var second = Payload(handler).GetProperty("idempotency_key").GetString();

        second.Should().Be(first);
    }

    [Fact]
    public async Task VerifyAsync_DifferentTokens_ProduceDifferentIdempotencyKeys()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var sut = CreateService(handler);

        await sut.VerifyAsync("token-a", useIdempotencyKey: true, cancellationToken: TestContext.Current.CancellationToken);
        var keyA = Payload(handler).GetProperty("idempotency_key").GetString();

        await sut.VerifyAsync("token-b", useIdempotencyKey: true, cancellationToken: TestContext.Current.CancellationToken);
        var keyB = Payload(handler).GetProperty("idempotency_key").GetString();

        keyB.Should().NotBe(keyA);
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
