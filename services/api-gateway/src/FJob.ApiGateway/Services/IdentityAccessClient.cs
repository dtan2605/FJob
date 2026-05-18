using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FJob.ApiGateway.Services;

public sealed class IdentityAccessClient(HttpClient httpClient, IOptions<ApiGatewayOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HttpResponseMessage> LoginAsync(object request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            new Uri(new Uri(options.Value.IdentityAccessBaseUrl), "/api/auth/login"),
            request,
            JsonOptions,
            cancellationToken);

        return response;
    }

    public async Task<HttpResponseMessage> RegisterAsync(object request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            new Uri(new Uri(options.Value.IdentityAccessBaseUrl), "/api/auth/register"),
            request,
            JsonOptions,
            cancellationToken);

        return response;
    }

    public async Task<HttpResponseMessage> LogoutAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.Value.IdentityAccessBaseUrl), "/api/auth/logout"));

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }
}
