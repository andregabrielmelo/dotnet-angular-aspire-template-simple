using System.Net.Http.Headers;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace AppTemplate.BackendForFrontend.Configurations;

public static class ReverseProxyConfigurations
{
    private const string ApiRouteId = "api";
    private const string AccessTokenItemKey = "AppTemplate.AccessToken";
    private const string CookieOnlyPolicyName = "cookie-only";

    /// <summary>
    /// <c>/api/**</c> goes to the Web API with the user's access token as a Bearer header. In
    /// Development everything else goes to the Angular dev server, so the browser only ever
    /// talks to this one origin (cookie, OIDC redirect URIs and SameSite all line up).
    /// Destinations are Aspire service-discovery names, resolved at runtime.
    /// </summary>
    public static IServiceCollection AddReverseProxyConfigurations(
        this IServiceCollection services,
        WebApplicationBuilder builder
    )
    {
        services
            .AddAuthorizationBuilder()
            .AddPolicy(CookieOnlyPolicyName, BackendForFrontendEndpoints.CookieOnlyPolicy);

        var routes = new List<RouteConfig>
        {
            new()
            {
                RouteId = ApiRouteId,
                ClusterId = "web",
                AuthorizationPolicy = CookieOnlyPolicyName,
                Match = new RouteMatch { Path = "/api/{**rest}" },
            },
        };
        var clusters = new List<ClusterConfig>
        {
            new()
            {
                ClusterId = "web",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["web"] = new() { Address = "https+http://web" },
                },
            },
        };

        if (builder.Environment.IsDevelopment())
        {
            routes.Add(
                new RouteConfig
                {
                    RouteId = "spa",
                    ClusterId = "angular",
                    // Matches last, after the API route and this host's own endpoints.
                    Order = int.MaxValue,
                    Match = new RouteMatch { Path = "/{**rest}" },
                }
            );
            clusters.Add(
                new ClusterConfig
                {
                    ClusterId = "angular",
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        ["angular"] = new() { Address = "http://angular" },
                    },
                }
            );
        }

        services
            .AddReverseProxy()
            .LoadFromMemory(routes, clusters)
            .AddServiceDiscoveryDestinationResolver()
            .AddTransforms(context =>
            {
                if (context.Route.RouteId is not ApiRouteId)
                {
                    return;
                }

                context.AddPathRemovePrefix("/api");
                // The browser's cookie is for this host only - never forward it to the API.
                context.AddRequestHeaderRemove("Cookie");

                if (context.Route.RouteId != ApiRouteId)
                {
                    return;
                }

                context.AddRequestTransform(transformContext =>
                {
                    if (
                        transformContext.HttpContext.Items[AccessTokenItemKey] is string accessToken
                    )
                    {
                        transformContext.ProxyRequest.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", accessToken);
                    }

                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }

    public static IEndpointRouteBuilder MapReverseProxyWithAccessTokens(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapReverseProxy(pipeline =>
        {
            // Resolve (and refresh if needed) the access token before proxying an API call. If
            // it can't be obtained - e.g. the refresh token expired or was revoked - the session
            // is over: drop the cookie and tell the SPA to log in again.
            pipeline.Use(
                async (context, next) =>
                {
                    if (context.GetReverseProxyFeature().Route.Config.RouteId == ApiRouteId)
                    {
                        var tokenResult = await context.GetUserAccessTokenAsync();
                        if (!tokenResult.WasSuccessful(out var userToken))
                        {
                            await context.SignOutAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme
                            );
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        context.Items[AccessTokenItemKey] = userToken.AccessToken.ToString();
                    }

                    await next();
                }
            );
        });

        return endpoints;
    }
}
