namespace AppTemplate.UseCases.Authentication.Logout;

/// <summary>Ends the caller's session: Identity deletes its cookie.</summary>
public record LogoutCommand : ICommand<Result>;
