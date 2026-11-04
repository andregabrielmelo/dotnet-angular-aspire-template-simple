using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.ConsumeRefreshToken;
using AppTemplate.UseCases.Authentication.StoreRefreshToken;
using AppTemplate.Web.Configurations;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// FastEndpoints' refresh token service (https://fast-endpoints.com/docs/security#jwt-refresh-tokens),
/// shaped like its Refresh-Tokens-Demo: it creates every access/refresh token pair (also for
/// <c>CreateTokenWith</c> at sign-in), stores the refresh tokens, and serves
/// <c>POST /auth/refresh-token</c>. The refresh token comes from its cookie, is used once, and the
/// new one goes back in the cookie; the body is only the access token (<see cref="IssuedTokens"/>).
/// Key, issuer and audience come from the global JwtCreationOptions.
/// </summary>
public sealed class UserTokenService : RefreshTokenService<TokenRequest, IssuedTokens>
{
    private readonly IMediator _mediator;
    private readonly RefreshTokenCookie _cookie;
    private AuthenticatedUserDto? _user;

    public UserTokenService(
        IOptions<JwtSettings> jwt,
        IMediator mediator,
        RefreshTokenCookie cookie
    )
    {
        _mediator = mediator;
        _cookie = cookie;

        Setup(options =>
        {
            options.AccessTokenValidity = jwt.Value.AccessTokenValidity;
            options.RefreshTokenValidity = jwt.Value.RefreshTokenValidity;
            options.Endpoint(
                "/auth/refresh-token",
                endpoint =>
                {
                    endpoint.Summary(s =>
                    {
                        s.Summary = "Refresh the access token";
                        s.Description =
                            "Uses the refresh token cookie (sent automatically by the browser), "
                            + "returns a new access token and rotates the cookie. Send {}: the "
                            + "body's userId and refreshToken are ignored.";
                        s.Responses[200] = "A new access token";
                        s.Responses[400] = "The refresh token is missing, invalid, used or expired";
                    });
                    endpoint.Description(builder =>
                        builder
                            .ClearDefaultProduces([200])
                            .Produces<AccessTokenResponse>(200, "application/json")
                    );
                }
            );
        });
    }

    /// <summary>
    /// The body's fields are ignored: the token comes only from its HttpOnly cookie, and the user
    /// from the stored token. Web clients post <c>{}</c>.
    /// </summary>
    public override async Task RefreshRequestValidationAsync(TokenRequest req)
    {
        var result = await _mediator.Send(
            new ConsumeRefreshTokenCommand(_cookie.Read(HttpContext) ?? string.Empty)
        );

        if (!result.IsSuccess)
        {
            _cookie.Delete(HttpContext);
            AddError("The refresh token is not valid!");
            return;
        }

        _user = result.Value;
        req.UserId = _user.Id.Value.ToString();
    }

    public override Task SetRenewalPrivilegesAsync(TokenRequest request, UserPrivileges privileges)
    {
        // Validation passed, so the user is known: same claims and current permissions as at sign-in.
        privileges.AddUser(_user!);
        return Task.CompletedTask;
    }

    public override async Task PersistTokenAsync(IssuedTokens response)
    {
        var result = await _mediator.Send(
            new StoreRefreshTokenCommand(
                UserId.From(int.Parse(response.UserId)),
                response.RefreshToken,
                new DateTimeOffset(response.RefreshExpiry, TimeSpan.Zero)
            )
        );
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("The refresh token could not be stored.");
        }
    }

    public override Task OnAfterRenewalTokenCreationAsync(
        TokenRequest? request,
        IssuedTokens response
    )
    {
        _cookie.Append(HttpContext, response);
        return Task.CompletedTask;
    }
}
