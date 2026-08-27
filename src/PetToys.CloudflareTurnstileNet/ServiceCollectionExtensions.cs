using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PetToys.CloudflareTurnstileNet;

/// <summary>
/// Registers Cloudflare Turnstile verification with the dependency injection
/// container.
/// </summary>
/// <remarks>
/// Every overload validates <see cref="CloudflareTurnstileOptions"/> when the
/// host starts, so a missing <see cref="CloudflareTurnstileOptions.SecretKey"/>
/// fails the application rather than every verification. The verification
/// <c>HttpClient</c> is registered with a 10-second timeout, which the
/// <c>configureHttpClient</c> overload can override.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITurnstileService"/> and binds
    /// <see cref="CloudflareTurnstileOptions"/> to a configuration section.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configurationSection">
    /// The configuration section holding the site key, secret key and enabled flag.
    /// </param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddCloudflareTurnstile(this IServiceCollection services, IConfigurationSection configurationSection)
    {
        services.Configure<CloudflareTurnstileOptions>(configurationSection);
        services.AddTurnstileInternal();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ITurnstileService"/> and configures
    /// <see cref="CloudflareTurnstileOptions"/> inline.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureOptions">Sets the options.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddCloudflareTurnstile(this IServiceCollection services, Action<CloudflareTurnstileOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddTurnstileInternal();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ITurnstileService"/>, configures
    /// <see cref="CloudflareTurnstileOptions"/> inline, and hands the typed
    /// <c>HttpClient</c> builder over for further configuration.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureOptions">Sets the options.</param>
    /// <param name="configureHttpClient">
    /// Configures the <c>HttpClient</c> the verification call is made through --
    /// its timeout, resilience handlers, and anything else the caller needs.
    /// </param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddCloudflareTurnstile(this IServiceCollection services, Action<CloudflareTurnstileOptions> configureOptions, Action<IHttpClientBuilder> configureHttpClient)
    {
        services.Configure(configureOptions);
        var httpClientBuilder = services.AddTurnstileInternal();
        configureHttpClient(httpClientBuilder);
        return services;
    }

    private static IHttpClientBuilder AddTurnstileInternal(this IServiceCollection services)
    {
        // Data annotations on the options are only enforced once something asks for them to
        // be; validating on start turns a missing secret key into a startup failure naming
        // the property, instead of every verification quietly failing against Cloudflare.
        services
            .AddOptions<CloudflareTurnstileOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The timeout is applied before the caller's configureHttpClient runs, so an
        // override there wins rather than being reapplied over.
        return services.AddHttpClient<ITurnstileService, TurnstileService>((_, client) =>
        {
            client.BaseAddress = CloudflareTurnstileOptions.ValidationBaseUri;
            client.Timeout = CloudflareTurnstileOptions.DefaultHttpClientTimeout;
        });
    }
}
