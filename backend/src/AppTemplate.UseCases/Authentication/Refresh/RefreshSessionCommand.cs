namespace AppTemplate.UseCases.Authentication.Refresh;

public record RefreshSessionCommand(string RefreshToken) : ICommand<Result<AuthSession>>;
