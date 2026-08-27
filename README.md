# Cloudflare Turnstile for .NET

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url] [![Target frameworks][dotnet-badge]][nuget-url] [![License][license-badge]][license-url]

> Stop bots at the door. Drop Cloudflare Turnstile verification into any
> ASP.NET Core app — an attribute for the happy path, a service for everything
> else, and a single switch to turn it all off.

Server-side validation for [Cloudflare Turnstile][turnstile], the privacy-first
CAPTCHA alternative, built for ASP.NET Core. The widget runs in the browser and
hands you a token; this package checks that token against Cloudflare's
[siteverify][siteverify] API and feeds the result straight into MVC and Razor
Pages model validation.

## Why

A Turnstile widget on its own proves nothing — the token it produces is only
worth as much as the server-side check behind it. Wiring that check up by hand
means an `HttpClient`, the right request shape, response parsing, and a way to
turn a failed challenge into a validation error your views already understand.

This package owns that server half so you don't have to:

- **One attribute, done.** Annotate a controller action or page model with
  `[ValidateCloudflareTurnstile]` and a failed challenge becomes a model-state
  error automatically — your existing `ModelState.IsValid` check does the rest.
- **Or stay in control.** Inject `ITurnstileService` and call `VerifyAsync`
  yourself when you need the raw boolean and nothing else.
- **Flip it off where it gets in the way.** A single `Enabled` flag
  short-circuits verification, so local runs and tests don't need a live widget.
- **Production details, already handled.** Forward the visitor's IP, send an
  idempotency key for safe retries, localize the error messages, and cancel
  in-flight checks with a `CancellationToken`.

It plugs into the standard hosting pipeline: the verification `HttpClient` is
registered through `IHttpClientFactory`, and options bind from configuration
like everything else.

## Features

- **Attribute-based validation** for MVC controllers and Razor Pages
  (`[ValidateCloudflareTurnstile]`); safe methods (`GET`, `HEAD`, `OPTIONS`) are
  skipped automatically, so the attribute is safe to apply at controller or
  page-model scope.
- **Manual validation** through `ITurnstileService.VerifyAsync`.
- **Form- and field-level errors** — a summary message plus an optional inline
  message attached to the widget field, ready for `asp-validation-for`.
- **Localized messages** via `IStringLocalizer` when localization is configured.
- **Remote IP forwarding** (`UseRemoteIp`) and **idempotency keys**
  (`UseIdempotencyKey`) for Cloudflare's optional checks.
- **Kill switch** — toggle verification with `Enabled` without touching any call
  site.
- **Cancellation** — `VerifyAsync` honors a `CancellationToken` through the
  HTTP call.
- **Fail-fast configuration** — a missing secret key stops the host at startup
  instead of failing every challenge at request time.
- **Typed `HttpClient`** registered through `IHttpClientFactory`, with a
  10-second timeout out of the box.

## Installation

```sh
dotnet add package PetToys.CloudflareTurnstileNet
```

## Getting started

You need a [Cloudflare][cloudflare] account with a Turnstile widget; from its
dashboard you get a **site key** (public, used by the widget) and a **secret
key** (private, used for server-side validation).

Register the services and bind the options from configuration:

```csharp
using PetToys.CloudflareTurnstileNet;

builder.Services.AddCloudflareTurnstile(
    builder.Configuration.GetSection(nameof(CloudflareTurnstileOptions)));
```

`appsettings.json`:

```json
{
  "CloudflareTurnstileOptions": {
    "SiteKey": "<your site key>",
    "SecretKey": "<your secret key>",
    "Enabled": true
  }
}
```

Prefer to configure inline? Pass an action instead:

```csharp
builder.Services.AddCloudflareTurnstile(options =>
{
    options.SiteKey = "<your site key>";
    options.SecretKey = "<your secret key>";
});
```

Only `SecretKey` is required, and it is checked when the host starts — a missing
one fails the application with a message naming the property rather than turning
every visitor away at request time. `SiteKey` is the widget's half of the pair
and the server never reads it, so leave it out if you render the widget
somewhere else.

### Tuning the HTTP client

The verification client is registered with a 10-second timeout, short enough
that an unresponsive endpoint cannot hold a request thread for the framework's
default of 100 seconds. Pass a second delegate to reach the `IHttpClientBuilder`
and change it, or to add a resilience handler:

```csharp
builder.Services.AddCloudflareTurnstile(
    options => options.SecretKey = "<your secret key>",
    http =>
    {
        http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(5));
        http.AddStandardResilienceHandler();
    });
```

Whatever you set there wins: the package default is applied first and never
reapplied over your value. (`AddStandardResilienceHandler` ships in the separate
[`Microsoft.Extensions.Http.Resilience`][resilience] package — retries and a
circuit breaker are worth it if a Turnstile outage would otherwise lock your
forms.)

## Usage

### Automatic validation

Apply `[ValidateCloudflareTurnstile]` to a Razor Page model or an MVC action.
The filter reads the token from the submitted form, verifies it, and adds a
model error when the challenge fails — so the only thing left to do is the
`ModelState.IsValid` check you already write.

Razor Pages:

```csharp
using PetToys.CloudflareTurnstileNet;

[ValidateCloudflareTurnstile(FormErrorMessage = "Verification failed. Please try again.")]
public class ContactModel : PageModel
{
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            // Turnstile verification (or other validation) failed.
            return Page();
        }

        // Turnstile verification succeeded…
        return RedirectToPage("Thanks");
    }
}
```

MVC controllers — put it on the action that handles the POST:

```csharp
[HttpPost]
[ValidateCloudflareTurnstile(FormErrorMessage = "Verification failed. Please try again.")]
public IActionResult Submit(ContactViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View("Index");
    }

    // Turnstile verification succeeded…
    return RedirectToAction("Thanks");
}
```

The attribute exposes everything you might want to tune:

| Property            | Default                    | Purpose                                                                    |
| ------------------- | -------------------------- | -------------------------------------------------------------------------- |
| `FormField`         | `cf-turnstile-response`    | Name of the form field carrying the token.                                 |
| `FormErrorMessage`  | A generic failure sentence | Message added to the model-level (`ModelOnly`) error summary.              |
| `FieldErrorMessage` | `null`                     | Optional message attached to the widget field (for `asp-validation-for`).  |
| `UseRemoteIp`       | `false`                    | Forward the caller's IP address to Cloudflare.                             |
| `UseIdempotencyKey` | `false`                    | Send an idempotency key so a token can be safely re-verified.              |

### The client-side widget

Add the Turnstile script and the widget `div` to your page. Gating both on
`Enabled` keeps the markup quiet when verification is switched off, and the
`asp-validation-summary` is what surfaces the `FormErrorMessage`.

```razor
@page
@using Microsoft.Extensions.Options
@using PetToys.CloudflareTurnstileNet
@inject IOptions<CloudflareTurnstileOptions> TurnstileOptions
@model ContactModel
<!DOCTYPE html>
<html lang="en">
<head>
  @if (TurnstileOptions.Value.Enabled)
  {
    <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" defer></script>
  }
</head>
<body>
  <form method="post">
    <h1>Contact us</h1>
    <div asp-validation-summary="ModelOnly"></div>
    <textarea asp-for="Message"></textarea>
    <span asp-validation-for="Message"></span>
    @if (TurnstileOptions.Value.Enabled)
    {
      <div class="cf-turnstile" data-sitekey="@TurnstileOptions.Value.SiteKey"></div>
    }
    <button type="submit">Send</button>
  </form>
</body>
</html>
```

### Manual validation

When the attribute doesn't fit, inject `ITurnstileService` and verify the token
yourself. `VerifyAsync` returns `true` only when Cloudflare confirms the token;
an empty token, a transport error, an unsuccessful response, or a body that
can't be read as Cloudflare's JSON all return `false` without throwing.

One thing the attribute does for you and the service cannot: honor `Enabled`.
The service has no way to tell "verification is switched off" from "verify this
token", so it always verifies — check the flag yourself, as below.

```csharp
using PetToys.CloudflareTurnstileNet;

public class ContactModel(
    ITurnstileService turnstileService,
    IOptions<CloudflareTurnstileOptions> options) : PageModel
{
    public async Task<IActionResult> OnPostAsync(
        [FromForm(Name = "cf-turnstile-response")] string turnstileToken,
        CancellationToken cancellationToken)
    {
        if (options.Value.Enabled &&
            !await turnstileService.VerifyAsync(turnstileToken, cancellationToken: cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Verification failed. Please try again.");
            return Page();
        }

        // Turnstile verification succeeded…
        return RedirectToPage("Thanks");
    }
}
```

`VerifyAsync` also accepts the visitor's IP address and an idempotency-key flag,
mirroring the `UseRemoteIp` and `UseIdempotencyKey` options on the attribute:

```csharp
var passed = await turnstileService.VerifyAsync(
    turnstileToken,
    remoteIp: HttpContext.Connection.RemoteIpAddress,
    useIdempotencyKey: true,
    cancellationToken: cancellationToken);
```

### Localized error messages

Register localization and the filter resolves both the form and field messages
through `IStringLocalizer`, keyed on the controller or page type. The strings you
pass to `FormErrorMessage` and `FieldErrorMessage` become the lookup keys, so a
matching resource entry is translated and a missing one falls back to the literal
text.

## Good to know

- **`Enabled` is a hard gate — on the filter.** When it is `false`, the
  validation filter returns immediately: Cloudflare is never called and no model
  errors are added. `ITurnstileService` callers check it themselves.
- **Empty tokens fail fast.** A `null`, empty, or whitespace token resolves to
  `false` without a network round-trip.
- **Only unsafe methods are guarded.** `GET`, `HEAD`, and `OPTIONS` skip
  verification on MVC actions and page handlers alike; everything else is
  checked.
- **Misconfiguration is a startup error.** `SecretKey` is validated when the host
  starts, so you find out before the first visitor does.
- **The verification call times out in 10 seconds** unless you say otherwise.
- **Anything short of a confirmed answer is a failed challenge.** A timeout, a
  transport error, an unsuccessful status code, or a body that isn't Cloudflare's
  documented JSON all resolve to `false` — never an exception out of your form
  post. Only your own `CancellationToken` still throws.
- **You still own the outcome.** The filter only records model errors — check
  `ModelState.IsValid` in your handler and decide what to return.

More runnable examples live in the [unit tests][tests-url].

## License

Provided under the [Apache License, Version 2.0][license-url].

[test-badge]: https://img.shields.io/github/actions/workflow/status/pet-toys/cloudflare-turnstile-net/test.yml?branch=dev&style=flat-square&logo=github&label=test
[test-url]: https://github.com/pet-toys/cloudflare-turnstile-net/actions?query=workflow%3Atest+branch%3Adev
[nuget-v-badge]: https://img.shields.io/nuget/v/PetToys.CloudflareTurnstileNet?style=flat-square&logo=nuget&label=version
[nuget-dt-badge]: https://img.shields.io/nuget/dt/PetToys.CloudflareTurnstileNet?style=flat-square&logo=nuget
[nuget-url]: https://www.nuget.org/packages/PetToys.CloudflareTurnstileNet/
[dotnet-badge]: https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4?style=flat-square&logo=dotnet
[license-badge]: https://img.shields.io/github/license/pet-toys/cloudflare-turnstile-net?style=flat-square&color=blue
[license-url]: https://www.apache.org/licenses/LICENSE-2.0
[turnstile]: https://developers.cloudflare.com/turnstile/
[siteverify]: https://developers.cloudflare.com/turnstile/get-started/server-side-validation/
[cloudflare]: https://dash.cloudflare.com/
[resilience]: https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience
[tests-url]: https://github.com/pet-toys/cloudflare-turnstile-net/tree/dev/test/PetToys.CloudflareTurnstileNet.Tests
