namespace App.Updates.Tests;

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _httpClient;

    public FakeHttpClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public HttpClient CreateClient(string name) => _httpClient;
}