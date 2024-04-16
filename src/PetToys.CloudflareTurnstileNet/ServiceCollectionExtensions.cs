#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;

namespace PetToys.CloudflareTurnstileNet
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCloudflareTurnstile(this IServiceCollection services, Action<CloudflareTurnstileOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddTurnstileInternal();
            return services;
        }

        public static IServiceCollection AddCloudflareTurnstile(this IServiceCollection services, Action<CloudflareTurnstileOptions> configureOptions, Action<IHttpClientBuilder> configureHttpClient)
        {
            services.Configure(configureOptions);
            var httpClientBuilder = services.AddTurnstileInternal();
            configureHttpClient(httpClientBuilder);
            return services;
        }

        private static IHttpClientBuilder AddTurnstileInternal(this IServiceCollection services)
        {
            services.AddLogging();

            var httpBuilder = services.AddHttpClient<ITurnstileService, TurnstileService>((sp, client) =>
            {
                client.BaseAddress = CloudflareTurnstileOptions.ValidationBaseUri;
            });

            return httpBuilder;
        }
    }
}
