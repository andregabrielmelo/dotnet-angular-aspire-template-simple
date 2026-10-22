using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.BackendForFrontend.Configurations;
using Xunit;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// The backend for frontend's own error responses follow the same problem details contract as
/// the Web API (ADR 014), and never reveal anything about the identity provider.
/// </summary>
public class ProblemDetailsTests(BackendForFrontendFactory factory)
    : IClassFixture<BackendForFrontendFactory>
{
    private readonly HttpClient _client = factory.CreateBrowserClient();

    private static async Task AssertUnauthorizedProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("keycloak", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("realm", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingCsrfHeader_IsA401Problem() =>
        await AssertUnauthorizedProblemAsync(await _client.GetAsync("/api/users/me"));

    [Fact]
    public async Task SessionEndpointWithoutSession_IsA401Problem()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/backend-for-frontend/user");
        request.Headers.Add(AntiforgeryHeaderMiddleware.HeaderName, "1");

        await AssertUnauthorizedProblemAsync(await _client.SendAsync(request));
    }
}
