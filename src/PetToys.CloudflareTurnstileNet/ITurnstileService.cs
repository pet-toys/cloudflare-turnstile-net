#nullable enable

using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet
{
    public interface ITurnstileService
    {
        Task<bool> VerifyAsync(string token, string? remoteIp = null, string? uuid = null);
    }
}
