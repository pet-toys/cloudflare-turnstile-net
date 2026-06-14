# Security Policy

## Supported versions

The package major version tracks the latest supported .NET major version. Only
the latest major line receives security fixes.

| Version | Supported          |
| ------- | :----------------: |
| 10.x    | :white_check_mark: |
| 8.x     | :x:                |
| < 8.0   | :x:                |

## Reporting a vulnerability

Please do not report security vulnerabilities through public issues, pull
requests, or discussions.

Instead, use GitHub's private vulnerability reporting: open the repository's
**Security** tab and click **Report a vulnerability**. This keeps the report
confidential until a fix is available.

When reporting, please include as much of the following as you can:

- A description of the vulnerability and its impact.
- The affected package version(s) and target framework.
- Steps to reproduce, ideally with a minimal sample.
- Any known workarounds or mitigations.

## What to expect

- We aim to acknowledge a report within a few days.
- We will keep you informed as we investigate and work on a fix.
- Once a fix ships, we will publish a security advisory and credit the
  reporter, unless you prefer to remain anonymous.

## Scope

This library performs server-side validation of Cloudflare Turnstile tokens: it
sends the configured secret key, the submitted token, and optionally the
caller's IP address to Cloudflare's `siteverify` endpoint over HTTPS and reports
whether the challenge passed. Reports about how the library handles the secret
key, builds or transmits the verification request, interprets the response, or
could let an invalid token be treated as valid (a verification bypass) are in
scope.

Out of scope are issues in Cloudflare's own service, weaknesses in how a
consuming application stores or exposes its site and secret keys, and the
client-side widget itself.
