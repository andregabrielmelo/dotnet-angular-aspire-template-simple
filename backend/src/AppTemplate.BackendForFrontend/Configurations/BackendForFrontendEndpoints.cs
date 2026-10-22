using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AppTemplate.BackendForFrontend.Configurations;

public sealed record BackendForFrontendUser(string? Name, string? Email, string LogoutUrl);

/// <summary>
/// Session endpoints the Angular app uses: it redirects the browser to login/register/logout
/// and asks <c>/user</c> whether a session exists. None of them ever return a token.
/// </summary>
public static class BackendForFrontendEndpoints
{
    public const string BasePath = "/backend-for-frontend";
    public const string UserPath = BasePath + "/user";

    public static IEndpointRouteBuilder MapBackendForFrontendEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(BasePath);

        group.MapGet(
            "/login",
            (string? returnUrl) =>
                TypedResults.Challenge(
                    new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                    [OpenIdConnectDefaults.AuthenticationScheme]
                )
        );

        // Same flow as login, but "prompt=create" (OpenID Connect "Initiating User Registration")
        // makes Keycloak open its registration form instead of the sign-in form.
        group.MapGet(
            "/register",
            (string? returnUrl) =>
                TypedResults.Challenge(
                    new OpenIdConnectChallengeProperties
                    {
                        RedirectUri = SafeReturnUrl(returnUrl),
                        Prompt = AuthenticationConfigurations.RegistrationPrompt,
                    },
                    [OpenIdConnectDefaults.AuthenticationScheme]
                )
        );

        group
            .MapGet(
                "/user",
                (HttpContext context) =>
                {
                    var user = context.User;
                    var sessionId = user.FindFirst("sid")?.Value ?? string.Empty;

                    return TypedResults.Ok(
                        new BackendForFrontendUser(
                            user.FindFirst("name")?.Value
                                ?? user.FindFirst("preferred_username")?.Value,
                            user.FindFirst("email")?.Value,
                            $"{BasePath}/logout?sid={Uri.EscapeDataString(sessionId)}"
                        )
                    );
                }
            )
            .RequireAuthorization(CookieOnlyPolicy);

        // A GET (so the SPA can simply navigate to it), protected against CSRF by requiring the
        // session's own "sid" - a cross-site page can't know it, so it can't log the user out.
        group.MapGet(
            "/logout",
            async Task<IResult> (HttpContext context, string? sid) =>
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    return TypedResults.LocalRedirect("/");
                }

                var sessionId = context.User.FindFirst("sid")?.Value ?? string.Empty;
                if (!string.Equals(sid, sessionId, StringComparison.Ordinal))
                {
                    return TypedResults.BadRequest();
                }

                // Best-effort: the Keycloak session is ended below either way.
                await context.RevokeRefreshTokenAsync();

                return TypedResults.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        OpenIdConnectDefaults.AuthenticationScheme,
                    ]
                );
            }
        );

        return endpoints;
    }

    /// <summary>
    /// Challenges with the cookie scheme only, so unauthenticated fetch/XHR calls get a 401
    /// (see the cookie's OnRedirectToLogin) instead of a redirect to Keycloak.
    /// </summary>
    public static readonly Microsoft.AspNetCore.Authorization.AuthorizationPolicy CookieOnlyPolicy =
        new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            CookieAuthenticationDefaults.AuthenticationScheme
        )
            .RequireAuthenticatedUser()
            .Build();

    /// <summary>Only same-origin paths - never an absolute URL (open redirect).</summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//")
        && !returnUrl.StartsWith("/\\")
            ? returnUrl
            : "/";
}
