using System;
using System.ComponentModel.DataAnnotations;

namespace PetToys.CloudflareTurnstileNet;

/// <summary>
/// Options for Cloudflare Turnstile verification, bound from configuration or
/// set inline when the services are registered.
/// </summary>
public sealed class CloudflareTurnstileOptions
{
    internal static readonly Uri ValidationBaseUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    /// <summary>
    /// Gets or sets the widget's site key, taken from the Cloudflare dashboard.
    /// It is public, and the browser widget renders with it.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the widget's secret key, taken from the Cloudflare
    /// dashboard. It never leaves the server and authenticates the verification
    /// call.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether verification is performed.
    /// Setting it to <see langword="false"/> short-circuits the filter, which is
    /// what local runs and tests use instead of a live widget.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
