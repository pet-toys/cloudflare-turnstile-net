#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet
{
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class ValidateCloudflareTurnstileAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => true;

        public string ErrorMessage { get; set; } = "Your request cannot be completed because you failed Captcha verification.";

        public string FormField { get; set; } = "cf-turnstile-response";

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var service = serviceProvider.GetRequiredService<ITurnstileService>();
            var snapshot = serviceProvider.GetRequiredService<IOptionsSnapshot<CloudflareTurnstileOptions>>();
            return new ValidateTurnstileFilter(service, FormField, ErrorMessage, snapshot);
        }
    }
}
