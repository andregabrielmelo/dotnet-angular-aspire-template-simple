using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication;

/// <param name="Value">The signed JWT, sent by the client as <c>Authorization: Bearer</c>.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Creates the tokens of a session. Implemented in Web, which owns the JWT format.</summary>
public interface ITokenIssuer
{
    AccessToken IssueAccessToken(User user, IReadOnlyCollection<string> permissions);

    /// <summary>A new random refresh token. Only its hash is ever stored.</summary>
    string CreateRefreshTokenValue();
}
