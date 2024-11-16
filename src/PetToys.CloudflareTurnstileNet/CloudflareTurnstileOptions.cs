using System;
using System.ComponentModel.DataAnnotations;

namespace PetToys.CloudflareTurnstileNet
{
    public sealed class CloudflareTurnstileOptions
    {
        internal static readonly Uri ValidationBaseUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

        [Required(AllowEmptyStrings = false)]
        public string SiteKey { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string SecretKey { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
    }
}
