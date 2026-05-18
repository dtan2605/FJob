using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FJob.AdminOperationsService.Contracts;
using FJob.Observability;
using Microsoft.Extensions.Options;

namespace FJob.AdminOperationsService.Services;

public sealed class IdentityAccessClient(
    HttpClient httpClient,
    IOptions<AdminOperationsOptions> options,
    ILogger<IdentityAccessClient> logger)
{
    public async Task<AdminActionResult<AdminLoginResponse>> LoginAsync(
        AdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "identity-login",
            logger,
            async ct =>
            {
                var uri = BuildUri("/api/auth/login");
                return await httpClient.PostAsJsonAsync(uri, request, ct);
            },
            cancellationToken,
            maxAttempts: 2,
            timeoutSeconds: 4);

        if (!response.IsSuccessStatusCode)
        {
            return new AdminActionResult<AdminLoginResponse>
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                Message = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Invalid username or password."
                    : await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }

        return new AdminActionResult<AdminLoginResponse>
        {
            Success = true,
            StatusCode = (int)response.StatusCode,
            Data = await response.Content.ReadFromJsonAsync<AdminLoginResponse>(cancellationToken)
        };
    }

    public async Task<AdminCurrentUser?> GetCurrentUserAsync(string bearerToken, CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "identity-me",
            logger,
            async ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, BuildUri("/api/auth/me"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                return await httpClient.SendAsync(request, ct);
            },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AdminCurrentUser>(cancellationToken);
    }

    public async Task<bool> LogoutAsync(string bearerToken, CancellationToken cancellationToken)
    {
        using var response = await ResilienceExecutor.ExecuteAsync(
            "identity-logout",
            logger,
            async ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/api/auth/logout"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                return await httpClient.SendAsync(request, ct);
            },
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private Uri BuildUri(string relativePath) => new(new Uri(options.Value.IdentityBaseUrl), relativePath);
}
