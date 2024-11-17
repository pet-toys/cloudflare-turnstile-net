using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet
{
    // see: https://developers.cloudflare.com/turnstile/get-started/server-side-validation/
    internal sealed class TurnstileService(
        HttpClient client,
        IOptionsSnapshot<CloudflareTurnstileOptions> optionsSnapshot,
        ILogger<TurnstileService> logger)
        : ITurnstileService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public async Task<bool> VerifyAsync(string token, IPAddress? remoteIp = null, Guid? idempotencyKey = null)
        {
            var message = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Content = JsonContent.Create(
                    new RequestMessage
                    {
                        SecretKey = optionsSnapshot.Value.SecretKey,
                        Token = token,
                        RemoteIp = remoteIp?.ToString(),
                        IdempotencyKey = idempotencyKey,
                    },
                    MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Json),
                    JsonOptions),
            };

            var response = await client.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Returned non successful status code '{Code}'. {Reason}", response.StatusCode, response.ReasonPhrase);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            logger.LogTrace("Verification response: {Response}", json);

            var result = JsonSerializer.Deserialize<ValidationResponse>(json);
            if (result?.Success == true) return true;
            logger.LogWarning("Unsuccessful result: {Response}", json);
            return false;
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
}
