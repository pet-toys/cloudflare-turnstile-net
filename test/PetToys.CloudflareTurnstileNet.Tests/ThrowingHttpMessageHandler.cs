using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet.Tests;

/// <summary>
/// <see cref="HttpMessageHandler"/> that always faults the request with a fixed
/// exception, used to simulate transport-level failures and client timeouts.
/// </summary>
internal sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromException<HttpResponseMessage>(exception);
}
