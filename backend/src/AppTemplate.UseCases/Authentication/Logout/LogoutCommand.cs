namespace AppTemplate.UseCases.Authentication.Logout;

public record LogoutCommand(string RefreshToken) : ICommand<Result>;
