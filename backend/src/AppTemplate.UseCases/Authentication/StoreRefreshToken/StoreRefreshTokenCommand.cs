using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.StoreRefreshToken;

/// <param name="Token">The refresh token as issued; only its hash is stored.</param>
public record StoreRefreshTokenCommand(UserId UserId, string Token, DateTimeOffset ExpiresAt)
    : ICommand<Result>;
