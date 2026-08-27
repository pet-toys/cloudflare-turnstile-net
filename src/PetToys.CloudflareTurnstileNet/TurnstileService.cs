using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet;

// see: https://developers.cloudflare.com/turnstile/get-started/server-side-validation/
internal sealed class TurnstileService(
    HttpClient client,
    IOptionsMonitor<CloudflareTurnstileOptions> optionsMonitor)
    : ITurnstileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<bool> VerifyAsync(string token, IPAddress? remoteIp = null, bool useIdempotencyKey = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var message = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            Content = JsonContent.Create(
                new RequestMessage
                {
                    SecretKey = optionsMonitor.CurrentValue.SecretKey,
                    Token = token,
                    RemoteIp = remoteIp?.ToString(),
                    IdempotencyKey = useIdempotencyKey ? DeriveIdempotencyKey(token) : null,
                },
                mediaType: null,
                JsonOptions),
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            // An HttpClient timeout (not caller-initiated) surfaces as a cancellation; fail closed.
            return false;
        }

        if (!response.IsSuccessStatusCode) return false;

        try
        {
            var result = await response.Content
                .ReadFromJsonAsync<ValidationResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return result?.Success == true;
        }
        catch (JsonException)
        {
            // A malformed or empty body proves nothing about the visitor; fail closed rather
            // than throwing a parse error out of a filter the caller never sees.
            return false;
        }
        catch (NotSupportedException)
        {
            // The answer was not JSON at all -- a proxy or captive portal error page.
            return false;
        }
    }

    // Cloudflare's idempotency key lets the same token be re-verified and return the original
    // verdict. Deriving it deterministically from the token keeps a resubmission of the same
    // token idempotent, while distinct tokens map to distinct keys.
    private static Guid DeriveIdempotencyKey(string token)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(token), hash);
        return new Guid(hash[..16]);
    }

    private sealed class RequestMessage
    {
        [JsonPropertyName("secret")]
        public string SecretKey { get; init; } = string.Empty;

        [JsonPropertyName("response")]
        public string Token { get; init; } = string.Empty;

        [JsonPropertyName("remoteip")]
        public string? RemoteIp { get; init; }

        [JsonPropertyName("idempotency_key")]
        public Guid? IdempotencyKey { get; init; }
    }
}
