using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// Keycloak advertises pushed authorization requests (PAR), and the handler uses them, but
/// Keycloak ignores <c>prompt=create</c> sent that way and shows its sign-in form. So sign-in
/// goes through PAR while "Create account" keeps its parameters in the query string.
/// </summary>
public class PushedAuthorizationTests : IClassFixture<PushedAuthorizationTests.ParFactory>
{
    private readonly ParFactory _factory;
    private readonly HttpClient _client;

    public PushedAuthorizationTests(ParFactory factory)
    {
        _factory = factory;
        _client = factory.CreateBrowserClient();
        _factory.Keycloak.PushedRequests.Clear();
    }

    private static Dictionary<string, string> RedirectQuery(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return QueryHelpers
            .ParseQuery(response.Headers.Location!.Query)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
    }

    [Fact]
    public async Task SignIn_PushesTheAuthorizationRequest()
    {
        var query = RedirectQuery(await _client.GetAsync("/backend-for-frontend/login"));

        Assert.Equal(ParFactory.RequestUri, query["request_uri"]);
        Assert.Single(_factory.Keycloak.PushedRequests);
    }

    [Fact]
    public async Task Register_SendsPromptCreateInTheQueryString()
    {
        var query = RedirectQuery(await _client.GetAsync("/backend-for-frontend/register"));

        Assert.Equal("create", query["prompt"]);
        Assert.False(query.ContainsKey("request_uri"));
        Assert.Empty(_factory.Keycloak.PushedRequests);
    }

    /// <summary>Answers Keycloak's PAR endpoint and records each pushed request body.</summary>
    public sealed class StubKeycloak : HttpMessageHandler
    {
        public List<string> PushedRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            lock (PushedRequests)
            {
                PushedRequests.Add(request.RequestUri!.ToString());
            }
            await Task.CompletedTask;
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    $$"""{"request_uri":"{{ParFactory.RequestUri}}","expires_in":60}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }

    public sealed class ParFactory : BackendForFrontendFactory
    {
        public const string RequestUri = "urn:ietf:params:oauth:request_uri:test";

        public StubKeycloak Keycloak { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    options =>
                    {
                        var configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = "https://keycloak.test/realms/apptemplate",
                            AuthorizationEndpoint = AuthorizationEndpoint,
                            TokenEndpoint = "https://keycloak.test/realms/apptemplate/token",
                            EndSessionEndpoint = "https://keycloak.test/realms/apptemplate/logout",
                            PushedAuthorizationRequestEndpoint =
                                "https://keycloak.test/realms/apptemplate/par",
                        };
                        options.Configuration = configuration;
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                configuration
                            );
                        options.Backchannel = new HttpClient(Keycloak);
                    }
                )
            );
        }
    }
}
