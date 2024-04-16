#nullable enable

using System;
using System.Text.Json.Serialization;

namespace PetToys.CloudflareTurnstileNet
{
    internal sealed class ValidationResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[] Errors { get; init; } = [];

        [JsonPropertyName("challenge_ts")]
        public DateTimeOffset? Challenge { get; init; }

        [JsonPropertyName("messages")]
        public string[]? Messages { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("cdata")]
        public string? Cdata { get; init; }
    }
}
