using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace PetToys.CloudflareTurnstileNet.Tests;

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

    [Fact]
    public void AddCloudflareTurnstile_ResolvesServiceFromRootProvider()
    {
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(opt =>
        {
            opt.SiteKey = SiteKeys.AlwaysPassesInvisible;
            opt.SecretKey = SecretKeys.AlwaysPasses;
        });

        // validateScopes:true rejects captive scoped state: the service must depend on
        // IOptionsMonitor (singleton), not IOptionsSnapshot (scoped), to resolve here.
        var provider = services.BuildServiceProvider(validateScopes: true);
        var resolve = () => provider.GetRequiredService<ITurnstileService>();

        resolve.Should().NotThrow();
    }

    [Fact]
    public async Task AddCloudflareTurnstile_WithHttpClientConfiguration_UsesProvidedHandler()
    {
        var handler = new StubHttpMessageHandler(responseJson: """{"success":true}""");
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(
            opt =>
            {
                opt.SiteKey = SiteKeys.AlwaysPassesInvisible;
                opt.SecretKey = SecretKeys.AlwaysPasses;
            },
            builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        var sut = services.BuildServiceProvider().GetRequiredService<ITurnstileService>();
        var result = await sut.VerifyAsync("token", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddCloudflareTurnstile_MissingSecretKey_FailsStartupValidation(string? secretKey)
    {
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(opt =>
        {
            opt.SiteKey = SiteKeys.AlwaysPassesInvisible;
            opt.SecretKey = secretKey!;
        });

        var validate = () => Validate(services);

        validate.Should()
            .Throw<OptionsValidationException>()
            .WithMessage($"*{nameof(CloudflareTurnstileOptions.SecretKey)}*");
    }

    [Fact]
    public void AddCloudflareTurnstile_MissingSecretKey_FailsStartupValidation_WithConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{nameof(CloudflareTurnstileOptions)}:{nameof(CloudflareTurnstileOptions.SiteKey)}"] = SiteKeys.AlwaysPassesInvisible,
        }).Build();

        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(configuration.GetSection(nameof(CloudflareTurnstileOptions)));

        var validate = () => Validate(services);

        validate.Should()
            .Throw<OptionsValidationException>()
            .WithMessage($"*{nameof(CloudflareTurnstileOptions.SecretKey)}*");
    }

    [Fact]
    public void AddCloudflareTurnstile_MissingSecretKey_FailsStartupValidation_WhenDisabled()
    {
        // The requirement deliberately does not follow Enabled: the flag is re-read per
        // request while this check runs once, and it gates the filter alone.
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(opt => opt.Enabled = false);

        var validate = () => Validate(services);

        validate.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddCloudflareTurnstile_MissingSecretKey_ExplainsHowToSatisfyIt()
    {
        // The message carries the whole fix, because the alternative is a maintainer
        // wondering why a build that ran yesterday no longer starts.
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(opt => opt.Enabled = false);

        var validate = () => Validate(services);

        validate.Should()
            .Throw<OptionsValidationException>()
            .WithMessage($"*{nameof(CloudflareTurnstileOptions.Enabled)}*")
            .WithMessage($"*{SecretKeys.AlwaysPasses}*");
    }

    [Fact]
    public void AddCloudflareTurnstile_WithoutSiteKey_PassesStartupValidation()
    {
        // The site key belongs to the browser widget; server-only consumers never render it
        // and must not have to invent a value to start.
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(opt => opt.SecretKey = SecretKeys.AlwaysPasses);

        var validate = () => Validate(services);

        validate.Should().NotThrow();
    }

    [Fact]
    public void AddCloudflareTurnstile_RegistersClientWithPackageDefaultTimeout()
    {
        var client = CreateHttpClient(builder => { });

        client.Timeout.Should().Be(CloudflareTurnstileOptions.DefaultHttpClientTimeout);
        client.Timeout.Should().NotBe(TimeSpan.FromSeconds(100));
    }

    [Fact]
    public void AddCloudflareTurnstile_WithHttpClientConfiguration_OverridesTheDefaultTimeout()
    {
        var timeout = TimeSpan.FromSeconds(3);

        var client = CreateHttpClient(builder => builder.ConfigureHttpClient(c => c.Timeout = timeout));

        client.Timeout.Should().Be(timeout);
    }

    private static void Validate(IServiceCollection services)
        => services.BuildServiceProvider().GetRequiredService<IStartupValidator>().Validate();

    private static HttpClient CreateHttpClient(Action<IHttpClientBuilder> configureHttpClient)
    {
        var name = string.Empty;
        var services = new ServiceCollection();
        services.AddCloudflareTurnstile(
            opt => opt.SecretKey = SecretKeys.AlwaysPasses,
            builder =>
            {
                name = builder.Name;
                configureHttpClient(builder);
            });

        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient(name);
    }
}
