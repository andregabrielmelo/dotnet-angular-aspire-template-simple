namespace AppTemplate.UseCases.Authentication;

/// <summary>What a sign-in, registration or refresh hands back to the client.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt
);
