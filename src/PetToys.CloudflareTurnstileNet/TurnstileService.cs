#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PetToys.CloudflareTurnstileNet
{
    internal sealed class TurnstileService(
        HttpClient client,
        IOptionsSnapshot<CloudflareTurnstileOptions> optionsSnapshot,
        ILogger<TurnstileService> logger)
        : ITurnstileService
    {
        private static readonly Uri RequestUri = new("turnstile/v0/siteverify", UriKind.Relative);

        // see: https://developers.cloudflare.com/turnstile/get-started/server-side-validation/
        public async Task<bool> VerifyAsync(string token, string? remoteIp = null, string? uuid = null)
        {
            var requestData = new Dictionary<string, string>()
            {
                ["secret"] = optionsSnapshot.Value.SecretKey,
                ["response"] = token,
            };
            if (remoteIp is not null) requestData.Add("remoteip", remoteIp);
            if (uuid is not null) requestData.Add("idempotency_key", uuid);

            var message = new HttpRequestMessage(HttpMethod.Post, RequestUri)
            {
                Content = new FormUrlEncodedContent(requestData),
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
            logger.LogWarning("Unsuccessful result: {Result}", json);
            return false;
        }
    }
}
