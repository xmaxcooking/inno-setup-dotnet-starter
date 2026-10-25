using System.Net;

namespace App.Updates.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;

    public StubHttpMessageHandler(string responseBody)
    {
        _responseBody = responseBody;
    }

    public string? RequestedUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUri = request.RequestUri?.ToString();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody)
        };
        return Task.FromResult(response);
    }
}