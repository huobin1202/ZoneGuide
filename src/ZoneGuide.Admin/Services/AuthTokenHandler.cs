using Blazored.LocalStorage;

namespace ZoneGuide.Admin.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;

    public AuthTokenHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("accessToken", cancellationToken);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch
        {
            // LocalStorage may not be available during prerendering
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
