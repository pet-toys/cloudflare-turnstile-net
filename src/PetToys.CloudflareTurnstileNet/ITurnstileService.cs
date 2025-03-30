using System.Net;
using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet;

public interface ITurnstileService
{
    Task<bool> VerifyAsync(string token, bool useIdempotencyKey, IPAddress? remoteIp = null);
}
