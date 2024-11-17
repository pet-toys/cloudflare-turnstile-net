# Cloudflare Turnstile for NET

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url]

Based on the following [project](https://github.com/michaelvs97/AspNetCore.ReCaptcha)

Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).

[nuget-v-badge]: https://img.shields.io/nuget/v/PetToys.CloudflareTurnstileNet.svg
[nuget-dt-badge]: https://img.shields.io/nuget/dt/PetToys.CloudflareTurnstileNet.svg
[nuget-url]: https://www.nuget.org/packages/PetToys.CloudflareTurnstileNet/
[test-badge]: https://github.com/pet-toys/cloudflare-turnstile-net/actions/workflows/test.yml/badge.svg?branch=dev
[test-url]: https://github.com/pet-toys/cloudflare-turnstile-net/actions?query=workflow%3Atest+branch%3Adev

## Requirements

You must have a CloudFlare account. Create a Turnstile widget for your form or site. You will need the SiteKey and SecretKey.

## Installation

You can install this package using NuGet.

```powershell
# Package Manager
PM> Install-Package PetToys.CloudflareTurnstileNet

# .NET CLI
dotnet add package PetToys.CloudflareTurnstileNet
```
## Configuration

Create a new configuration section like this in your appsettings.json and update the SiteKey and SecretKey values from CloudFlare.

```json
{
  "CloudflareTurnstileOptions": {
    "SiteKey": "Site key from your CloudFlare dashboard",
    "SecretKey": "Secret key from your CloudFlare dashboard",
    "Enabled": true
  },
}
```

## Usage

To use PetToys.CloudflareTurnstileNet in your project, you must add the CloudflareTurnstileNet services to the service container like so:

```c#
builder.Services.AddCloudflareTurnstile(builder.Configuration.GetSection("CloudflareTurnstileOptions"));
```

## Clientside Code

The cloudflare turnstile javascript must be added as well as the turnstile div. In automatic mode the `asp-validation-summary` is also required to show the validation error.

An example razor page:

```razor
@page
@namespace MyApp.Pages
@using Microsoft.Extensions.Options
@using PetToys.CloudflareTurnstileNet
@inject IOptions<CloudflareTurnstileOptions> turnstileOptions
@model ContactModel
<!DOCTYPE html>
<html lang="en">
<head>
  @if (turnstileOptions.Value.Enabled)
  {
    <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" defer></script>
  }
</head>
<body>
  <form method="POST">
    <h1>Contact Us</h1>
    <div asp-validation-summary="ModelOnly" class=""></div>
    <textarea asp-for="Message"></textarea>
    <span asp-validation-for="Message"></span>
    @if (turnstileOptions.Value.Enabled)
    {
      <div class="cf-turnstile" data-sitekey="@turnstileOptions.Value.SiteKey"></div>
    }
    <input type="submit" name="submit" value="Send" />
  </form>
  <!-- add asp.net validation scripts -->
</body>
</html>
```

## Automatic Validation

Automatic verification can be applied by adding an attribute to your page model or controller. Here is a razor page example that uses `FormErrorMessage` to set a custom error message:

```c#
using PetToys.CloudflareTurnstileNet;

[ValidateCloudflareTurnstile(FormErrorMessage = "Cloudflare Turnstile verification failed")]
public class ContactModel : PageModel
{
```

For an MVC controller apply the attribute to the action method:

```c#
[HttpPost]
[ValidateCloudflareTurnstile(FormErrorMessage = "Cloudflare Turnstile verification failed")]
public IActionResult Submit(ContactViewModel model)
{
	if (!ModelState.IsValid)
	{
		return View("Index");
	}
```

In the handler method `ModelState.IsValid` must be checked as the attribute will add a model error if validation fails. An example:

```c#
public IActionResult OnPost()
{
	if (!ModelState.IsValid)
	{
		// Turnstile verification or other verification failed
		return Page();
	}

	// Turnstile verification succeeded...
}
```

## Manual Validation

If you want to manually validate the Turnstile token inject `ITurnstileService` and `IOptions<CloudflareTurnstileOptions>`.

Example:

```c#
private readonly ITurnstileService _turnstileService;
private readonly IOptions<CloudflareTurnstileOptions> _turnstileOptions;

public ContactModel(ITurnstileService turnstileService, IOptions<CloudflareTurnstileOptions> turnstileOptions)
{
	_turnstileService = turnstileService;
	_turnstileOptions = turnstileOptions;
}
```

In your page handler, get the turnstile token from the form then call the turnstile service method `VerifyAsync` to verify the token with CloudFlare. If the returned value is `true` then the user passed validation.

```c#
public async Task<IActionResult> OnPostAsync([FromForm(Name = "cf-turnstile-response")] string turnstileToken)
{
	bool turnstileSuccess = true;
	if (_turnstileOptions.Value.Enabled)
	{
		turnstileSuccess = await _turnstileService.VerifyAsync(turnstileToken);
	}
	// other custom validation

```