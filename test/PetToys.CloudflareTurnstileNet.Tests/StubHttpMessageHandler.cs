using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PetToys.CloudflareTurnstileNet.Tests;

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> that records the outgoing request
/// and returns a configurable response without touching the network.
/// </summary>
internal sealed class StubHttpMessageHandler(
    HttpStatusCode statusCode = HttpStatusCode.OK,
    string responseJson = """{"success":false}""") : HttpMessageHandler
{
    public int CallCount { get; private set; }

    public HttpMethod? LastMethod { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastMethod = request.Method;
        LastRequestUri = request.RequestUri;

        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseJson),
        };
    }
}
