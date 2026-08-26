using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet;

/// <summary>
/// Verifies a Turnstile token against Cloudflare's siteverify API.
/// </summary>
public interface ITurnstileService
{
    /// <summary>
    /// Verifies a token the browser widget produced.
    /// </summary>
    /// <param name="token">The token submitted by the widget.</param>
    /// <param name="remoteIp">
    /// The visitor's IP address, forwarded to Cloudflare when supplied.
    /// </param>
    /// <param name="useIdempotencyKey">
    /// <see langword="true"/> to send an idempotency key derived from the token,
    /// so that the same token can be verified again safely.
    /// </param>
    /// <param name="cancellationToken">Cancels the in-flight HTTP call.</param>
    /// <returns>
    /// <see langword="true"/> only when Cloudflare confirms the token. Verification
    /// fails closed: an empty token, a transport error and an unsuccessful
    /// response all yield <see langword="false"/> rather than throwing.
    /// </returns>
    /// <exception cref="System.OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    Task<bool> VerifyAsync(string token, IPAddress? remoteIp = null, bool useIdempotencyKey = false, CancellationToken cancellationToken = default);
}
