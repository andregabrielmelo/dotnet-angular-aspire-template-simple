namespace AppTemplate.UseCases.Authentication.ConsumeRefreshToken;

public record ConsumeRefreshTokenCommand(string Token) : ICommand<Result<AuthenticatedUserDto>>;
