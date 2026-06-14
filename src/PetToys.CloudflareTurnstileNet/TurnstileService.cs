using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet;

// see: https://developers.cloudflare.com/turnstile/get-started/server-side-validation/
internal sealed class TurnstileService(
    HttpClient client,
    ScopeWrapper scopeWrapper,
    IOptionsSnapshot<CloudflareTurnstileOptions> optionsSnapshot)
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
                    SecretKey = optionsSnapshot.Value.SecretKey,
                    Token = token,
                    RemoteIp = remoteIp?.ToString(),
                    IdempotencyKey = useIdempotencyKey ? scopeWrapper.Uid : null,
                },
                MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Json),
                JsonOptions),
        };

        var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var result = JsonSerializer.Deserialize<ValidationResponse>(json);
        return result?.Success == true;
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
