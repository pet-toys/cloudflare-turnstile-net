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

    // A siteverify call is a single small round-trip to Cloudflare's edge; the framework
    // default of 100 seconds would pin a request thread for far longer than any answer is
    // worth waiting for. Callers who need something else set it through configureHttpClient.
    internal static readonly TimeSpan DefaultHttpClientTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the widget's site key, taken from the Cloudflare dashboard.
    /// It is public, and the browser widget renders with it. Server-side
    /// verification never reads it, so leaving it unset is fine when the widget
    /// is rendered elsewhere.
    /// </summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the widget's secret key, taken from the Cloudflare
    /// dashboard. It never leaves the server and authenticates the verification
    /// call. It is required whatever <see cref="Enabled"/> says: without it the
    /// host fails to start.
    /// </summary>
    // The requirement does not follow Enabled on purpose. Enabled is bound from
    // configuration and re-read per request, while startup validation runs once, so a
    // secret waived at startup could be needed minutes later; and Enabled gates the filter
    // alone, never a caller that resolves ITurnstileService itself.
    [Required(AllowEmptyStrings = false, ErrorMessage =
        "CloudflareTurnstileOptions.SecretKey is required, even when Enabled is false. " +
        "For local runs and CI use a Cloudflare testing key, for example 1x0000000000000000000000000000000AA.")]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether verification is performed.
    /// Setting it to <see langword="false"/> short-circuits the filter, which is
    /// what local runs and tests use instead of a live widget. It gates the
    /// filter only -- code that calls <see cref="ITurnstileService"/> directly is
    /// responsible for checking it.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
