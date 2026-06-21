using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Notisight.Api.Features.Auth.Contracts;

namespace Notisight.Api.Tests.Infrastructure;

public static class HttpTestExtensions
{
    public static async Task<AuthResponse> RegisterAsync(
        this HttpClient client,
        string email,
        string password,
        string username)
    {
        var response = await client.PostAsJsonAsync("/auth/register", new RegisterRequest(ToUsername(username, email), email, password));
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Register failed with {(int)response.StatusCode}: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static void SetBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static string ToUsername(string value, string email)
    {
        var baseName = $"{value}-{email.Split('@')[0]}";
        var normalized = Regex.Replace(baseName.Trim().ToLowerInvariant(), @"[^a-z0-9._-]+", "-");
        normalized = normalized.Trim('-', '.', '_');
        return normalized.Length >= 3 ? normalized : $"{normalized}user";
    }
}
