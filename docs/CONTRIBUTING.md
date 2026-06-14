# Contributing

Thanks for taking the time to contribute! This project is a small, focused
Cloudflare Turnstile integration for ASP.NET Core, so contributions of any size
are welcome.

## Ways to contribute

- [Report a bug](https://github.com/pet-toys/cloudflare-turnstile-net/issues/new?template=bug_report.yml).
- [Request a feature](https://github.com/pet-toys/cloudflare-turnstile-net/issues/new?template=feature_request.yml).
- Improve the documentation.
- Open a pull request against the `dev` branch.

For anything beyond a small fix, please open an issue first so the approach can
be discussed before you invest time in a pull request.

## Getting started

The repository uses the .NET SDK version pinned in [`global.json`](../global.json)
and multi-targets `net8.0`, `net9.0`, and `net10.0`.

```bash
git clone https://github.com/pet-toys/cloudflare-turnstile-net.git
cd cloudflare-turnstile-net

dotnet restore
dotnet build -c Release
dotnet test
```

`Release` builds treat warnings as errors and enforce code-style and analyzer
rules, so build with `-c Release` before opening a pull request to catch the
same issues CI will.

Some tests are tagged `Category=Integration` and call the live Cloudflare
endpoint; CI skips them with `--filter Category!=Integration`, and you can do the
same for an offline run.

## Pull requests

- Branch off `dev` and target `dev`.
- Keep each pull request focused on a single change.
- Link the related issue (for example, `Closes #123`).
- Add or update tests for any behavioral change.
- Make sure `dotnet build -c Release` and `dotnet test` both pass locally.

Commit messages and pull request descriptions should be written in English and
describe the change in plain, neutral terms.

## Code style

Most conventions are enforced automatically by the analyzers and
`.editorconfig`, so a clean `Release` build is the source of truth. The
guidelines below capture the conventions that are not fully machine-checked:

- Use `PascalCase` for type, method, property, and constant names.
- Use `camelCase` for parameters and local variables.
- Prefix private fields with an underscore (`_field`).
- Prefix interfaces with `I`.
- Use language keywords (`int`, `string`) rather than framework type names
  (`Int32`, `String`).
- Use boolean-style prefixes (`Is`, `Has`, `Can`, `Any`) for boolean members.
- Always use braces, even for single-statement `if`, `for`, and `foreach`.
- Do not use Hungarian notation.

Nullable reference types are enabled project-wide, so do not add `#nullable`
directives to individual files.

### Tests

Tests use xUnit and follow the `Method_State_ExpectedResult` naming pattern
(for example, `VerifyAsync_EmptyToken_ReturnsFalseWithoutCallingCloudflare`).
Keep test data close to the tests that use it, and prefer deterministic tests
over ones that depend on network access or timing — reach for the in-memory HTTP
handler instead of the live endpoint where you can.
