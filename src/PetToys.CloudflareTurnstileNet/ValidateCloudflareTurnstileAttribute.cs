using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PetToys.CloudflareTurnstileNet;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class ValidateCloudflareTurnstileAttribute : Attribute, IFilterFactory
{
    private const string DefaultFormField = "cf-turnstile-response";
    private const string DefaultFormErrorMessage = "Your request cannot be completed because you failed Cloudflare Turnstile verification.";

    public bool IsReusable => true;

    public string FormErrorMessage { get; set; } = DefaultFormErrorMessage;

    public string? FieldErrorMessage { get; set; }

    public string FormField { get; set; } = DefaultFormField;

    public bool UseRemoteIp { get; set; }

    public bool UseIdempotencyKey { get; set; }

    // The filter carries no request-scoped state, so a single reusable instance is safe.
    // ITurnstileService is resolved per request from HttpContext.RequestServices inside the
    // filter, never captured here.
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new ValidateTurnstileFilter(FormField, FormErrorMessage, FieldErrorMessage, UseRemoteIp, UseIdempotencyKey);
    }
}
