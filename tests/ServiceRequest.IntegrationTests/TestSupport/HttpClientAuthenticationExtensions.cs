using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ServiceRequest.IntegrationTests.TestSupport;

public static class HttpClientAuthenticationExtensions
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<string> LoginAndGetTokenAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password },
            JsonOptions);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("accessToken").GetString()!;
    }

    public static async Task AuthenticateAsync(this HttpClient client, string username, string password)
    {
        var token = await client.LoginAndGetTokenAsync(username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
