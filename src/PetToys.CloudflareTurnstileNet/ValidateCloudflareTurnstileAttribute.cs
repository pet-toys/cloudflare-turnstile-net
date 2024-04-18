#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace PetToys.CloudflareTurnstileNet
{
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class ValidateCloudflareTurnstileAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => true;

        public string FormErrorMessage { get; set; } = "Your request cannot be completed because you failed Cloudflare Turnstile verification.";

        public string? FieldErrorMessage { get; set; }

        [Obsolete($"Use {nameof(FormErrorMessage)} instead.", false)]
        public string ErrorMessage
        {
            get => FormErrorMessage;
            set => FormErrorMessage = value;
        }

        public string FormField { get; set; } = "cf-turnstile-response";

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var service = serviceProvider.GetRequiredService<ITurnstileService>();
            return new ValidateTurnstileFilter(service, FormField, FormErrorMessage, FieldErrorMessage);
        }
    }
}
