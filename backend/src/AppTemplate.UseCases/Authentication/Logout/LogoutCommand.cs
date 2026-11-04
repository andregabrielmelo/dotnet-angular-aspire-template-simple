namespace AppTemplate.UseCases.Authentication.Logout;

/// <param name="RefreshToken">The session's refresh token; <c>null</c> when the client has none.</param>
public record LogoutCommand(string? RefreshToken) : ICommand<Result>;
