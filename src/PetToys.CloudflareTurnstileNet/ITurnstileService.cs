#nullable enable

using System;
using System.Net;
using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet
{
    public interface ITurnstileService
    {
        Task<bool> VerifyAsync(string token, IPAddress? remoteIp = null, Guid? idempotencyKey = null);
    }
}
