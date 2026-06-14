using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet;

public interface ITurnstileService
{
    Task<bool> VerifyAsync(string token, IPAddress? remoteIp = null, bool useIdempotencyKey = false, CancellationToken cancellationToken = default);
}
