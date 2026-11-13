using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Bearer tokens next to the session cookie (ADR 018). A policy scheme becomes the default for
/// authenticating, challenging and forbidding: a request with <c>Authorization: Bearer ...</c> is
/// authenticated by JwtBearer, any other by Identity's session cookie (ADR 015). Signing in and
/// out, and the security stamp checks, stay on the cookie scheme.
/// </summary>
public static class TokenAuthentication
{
    /// <summary>The bearer scheme, also the authentication type of the identities it creates.</summary>
    public const string BearerScheme = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>The default scheme, which picks one of the two per request.</summary>
    public const string SessionOrBearerScheme = "SessionOrBearer";

    /// <summary>Tolerated clock difference between the token's issuer and this server.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>Call after <c>AddIdentity</c>, whose default schemes this replaces.</summary>
    public static IServiceCollection AddTokenAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SessionOrBearerScheme;
                options.DefaultChallengeScheme = SessionOrBearerScheme;
                options.DefaultForbidScheme = SessionOrBearerScheme;
            })
            .AddPolicyScheme(
                SessionOrBearerScheme,
                "Session cookie or bearer token",
                options =>
                    options.ForwardDefaultSelector = context =>
                        context.Request.Headers.Authorization.ToString() is var header
                        && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? BearerScheme
                            : IdentityConstants.ApplicationScheme
            )
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(BearerScheme)
            .Configure<IOptions<JwtSettings>, TimeProvider>(
                (options, jwtSettings, time) =>
                {
                    var settings = jwtSettings.Value;
                    // Keep the claim types as the claims factory wrote them (user_id, permissions,
                    // and Identity's name and role), so a token's user reads like a session's.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = settings.Issuer,
                        ValidAudience = settings.Audience,
                        IssuerSigningKey = settings.SecurityKey(),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        RequireExpirationTime = true,
                        // Checked against TimeProvider, as the session cookie is.
                        LifetimeValidator = (notBefore, expires, _, _) =>
                        {
                            var now = time.GetUtcNow().UtcDateTime;
                            return (notBefore is null || notBefore <= now + ClockSkew)
                                && expires is not null
                                && expires > now - ClockSkew;
                        },
                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role,
                        AuthenticationType = BearerScheme,
                    };
                }
            );

        return services;
    }

    /// <summary>Whether the request's user was authenticated by a bearer token.</summary>
    public static bool IsBearerAuthenticated(this ClaimsPrincipal user) =>
        user.Identity is { IsAuthenticated: true, AuthenticationType: BearerScheme };
}
