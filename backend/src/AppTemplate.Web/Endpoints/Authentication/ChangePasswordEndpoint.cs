using AppTemplate.UseCases.Authentication.ChangePassword;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Authentication;

public sealed class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}

/// <summary>
/// Changes the caller's password. The account's security stamp changes, so every other session
/// ends at its next refresh; this one gets new tokens, like a sign-in, so it carries on.
/// </summary>
public class ChangePasswordEndpoint(
    IMediator _mediator,
    ICurrentUser _currentUser,
    RefreshTokenCookie _cookie
) : Endpoint<ChangePasswordRequest, Results<Ok<AccessTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/change-password");

        Summary(s =>
        {
            s.Summary = "Change the password";
            s.Description =
                "Checks the current password and sets the new one. Other sessions end at their "
                + "next refresh; this one gets a new access token and refresh token cookie.";
            s.ExampleRequest = new ChangePasswordRequest
            {
                CurrentPassword = "correct horse battery staple",
                NewPassword = "a new and longer passphrase",
            };
            s.Responses[200] = "Changed; a new session for this device";
            s.Responses[400] = "Invalid request data, a wrong current password, or a weak one";
            s.Responses[401] = "Missing or invalid access token";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<ChangePasswordRequest>()
                .Produces<AccessTokenResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(401)
        );
    }

    public override async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (_currentUser.Id is not { } userId)
        {
            return Result.Unauthorized().ToProblem();
        }

        var result = await _mediator.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        // Issued after the change, so the new refresh token records the new security stamp.
        var tokens = await CreateTokenWith<UserTokenService, IssuedTokens>(
            result.Value.Id.Value.ToString(),
            privileges => privileges.AddUser(result.Value)
        );
        _cookie.Append(HttpContext, tokens);

        return TypedResults.Ok(AccessTokenResponse.From(tokens));
    }
}

public sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).ProvidedPassword();
        RuleFor(x => x.NewPassword).NewPassword();
    }
}
