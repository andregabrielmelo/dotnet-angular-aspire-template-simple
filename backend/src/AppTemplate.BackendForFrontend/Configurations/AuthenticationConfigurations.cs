using AppTemplate.ServiceDefaults;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AppTemplate.BackendForFrontend.Configurations;

public static class AuthenticationConfigurations
{
    public const string CookieName = "__Host-apptemplate";

    /// <summary>OpenID Connect "Initiating User Registration": open the registration form.</summary>
    public const string RegistrationPrompt = "create";

    /// <summary>
    /// OpenID Connect (authorization code + PKCE) against Keycloak for login, and an HTTP-only
    /// cookie for the browser session. The tokens Keycloak issues are kept inside that encrypted
    /// cookie ticket - never readable by JavaScript - and the access token is attached to proxied
    /// API calls by <see cref="ReverseProxyConfigurations"/>.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfigurations(
        this IServiceCollection services,
        WebApplicationBuilder builder
    )
    {
        var keycloak = builder.Configuration.GetSection("Keycloak");
        // Required: read here so a missing value stops startup, not the first sign-in.
        var realm = builder.Configuration.GetRequiredValue("Keycloak:Realm");
        var clientId = builder.Configuration.GetRequiredValue("Keycloak:ClientId");

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    options.Cookie.Name = CookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;

                    // The SPA talks to this host with fetch/XHR - answer with status codes
                    // instead of redirecting API calls to a login page.
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                }
            )
            .AddKeycloakOpenIdConnect(
                serviceName: "keycloak",
                realm: realm,
                options =>
                {
                    options.ClientId = clientId;
                    options.ClientSecret = keycloak["ClientSecret"];
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "name";
                    // Keycloak runs over plain HTTP inside Aspire locally.
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    // The Aspire service-discovery address ("https+http://keycloak/...") is for
                    // local development. Elsewhere, set Keycloak:Authority to the realm's public
                    // HTTPS URL, which is what issued tokens name as their issuer.
                    if (keycloak["Authority"] is { Length: > 0 } authority)
                    {
                        options.Authority = authority;
                    }

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    // Refresh tokens, so the access token can be renewed without a new login.
                    options.Scope.Add("offline_access");

                    // Keycloak advertises pushed authorization requests (PAR), so the handler
                    // sends every parameter in a back-channel POST. Keycloak ignores
                    // prompt=create there and shows its sign-in form, so "Create account"
                    // would open the wrong page. That one challenge goes in the query string.
                    var pushAuthorization = options.Events.OnPushAuthorization;
                    options.Events.OnPushAuthorization = async context =>
                    {
                        if (context.ProtocolMessage.Prompt == RegistrationPrompt)
                        {
                            context.SkipPush();
                        }

                        await pushAuthorization(context);
                    };
                }
            );

        // Refreshes the access token from the stored refresh token shortly before it expires.
        services.AddOpenIdConnectAccessTokenManagement();

        services.AddAuthorization();

        return services;
    }
}
