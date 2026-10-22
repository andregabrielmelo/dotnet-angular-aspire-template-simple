using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// Hosts the backend for frontend without Keycloak: the OpenID Connect handler gets a static
/// discovery document, so challenges produce a real redirect URL to assert on without any
/// network call.
/// </summary>
public class BackendForFrontendFactory : WebApplicationFactory<Program>
{
    public const string AuthorizationEndpoint = "https://keycloak.test/realms/apptemplate/auth";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Keycloak:ClientSecret", "tests");
        // Outside Development the authority must be a real HTTPS URL (see ADR 007).
        builder.UseSetting("Keycloak:Authority", "https://keycloak.test/realms/apptemplate");

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
                    };
                    options.Configuration = configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                }
            )
        );
    }

    /// <summary>HTTPS (the app redirects HTTP) and no auto-redirects, so tests see the 302s.</summary>
    public HttpClient CreateBrowserClient() =>
        CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );
}
