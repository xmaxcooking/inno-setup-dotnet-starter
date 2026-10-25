namespace App.Updates.Tests;

internal sealed class ThrowingHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        throw new InvalidOperationException("CreateClient should not have been called.");
}