namespace AppTemplate.Web.Authentication;

/// <summary>
/// What sign-in, registration and refresh return. FastEndpoints' <see cref="TokenResponse"/> also
/// carries the user id and the refresh token: the refresh token only travels in its HttpOnly
/// cookie (<see cref="RefreshTokenCookie"/>), so page script never sees it, and the client reads
/// its profile from <c>GET /users/me</c>.
/// </summary>
/// <param name="AccessToken">The JWT, to send as <c>Authorization: Bearer</c>.</param>
/// <param name="AccessTokenExpiry">When the access token expires (UTC). Refresh shortly before.</param>
public sealed record AccessTokenResponse(string AccessToken, DateTime AccessTokenExpiry)
{
    public static AccessTokenResponse From(TokenResponse tokens) =>
        new(tokens.AccessToken, tokens.AccessExpiry);
}
