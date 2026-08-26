using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PetToys.CloudflareTurnstileNet;

/// <summary>
/// Verifies the submitted Turnstile token before an MVC action or Razor Page
/// handler runs, turning a failed challenge into a model-state error.
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class ValidateCloudflareTurnstileAttribute : Attribute, IFilterFactory
{
    private const string DefaultFormField = "cf-turnstile-response";
    private const string DefaultFormErrorMessage = "Your request cannot be completed because you failed Cloudflare Turnstile verification.";

    /// <summary>
    /// Gets a value indicating whether the filter instance can be reused across
    /// requests. It always can: the filter holds no request-scoped state.
    /// </summary>
    public bool IsReusable => true;

    /// <summary>
    /// Gets or sets the message added to the model-level error summary when
    /// verification fails. With localization configured it is used as the
    /// lookup key.
    /// </summary>
    public string FormErrorMessage { get; set; } = DefaultFormErrorMessage;

    /// <summary>
    /// Gets or sets the message attached to the widget field itself, for
    /// <c>asp-validation-for</c>. Left <see langword="null"/>, no field-level
    /// error is added. With localization configured it is used as the lookup key.
    /// </summary>
    public string? FieldErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the name of the form field carrying the token. Defaults to
    /// the widget's own <c>cf-turnstile-response</c>.
    /// </summary>
    public string FormField { get; set; } = DefaultFormField;

    /// <summary>
    /// Gets or sets a value indicating whether the caller's IP address is
    /// forwarded to Cloudflare.
    /// </summary>
    public bool UseRemoteIp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an idempotency key is sent, so
    /// that the same token can be verified again safely.
    /// </summary>
    public bool UseIdempotencyKey { get; set; }

    /// <summary>
    /// Creates the filter that performs the verification.
    /// </summary>
    /// <param name="serviceProvider">Unused; the filter resolves what it needs per request.</param>
    /// <returns>The filter configured from this attribute's properties.</returns>
    // The filter carries no request-scoped state, so a single reusable instance is safe.
    // ITurnstileService is resolved per request from HttpContext.RequestServices inside the
    // filter, never captured here.
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new ValidateTurnstileFilter(FormField, FormErrorMessage, FieldErrorMessage, UseRemoteIp, UseIdempotencyKey);
    }
}
