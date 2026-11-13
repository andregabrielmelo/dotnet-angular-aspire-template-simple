using AppTemplate.UseCases.Authentication.ChangePassword;
using AppTemplate.UseCases.Constants;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}

/// <summary>
/// Changes the caller's password. The account's security stamp changes, so Identity ends every
/// other session at its next stamp check; this one gets a new cookie, so it carries on.
/// </summary>
public class ChangePasswordEndpoint(IMediator _mediator, ICurrentUser _currentUser)
    : Endpoint<ChangePasswordRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/change-password");

        Summary(s =>
        {
            s.Summary = "Change the password";
            s.Description =
                "Checks the current password and sets the new one. Other sessions end within "
                + "minutes; this one stays signed in.";
            s.ExampleRequest = new ChangePasswordRequest
            {
                CurrentPassword = "correct horse battery staple",
                NewPassword = "a new and longer passphrase",
            };
            s.Responses[204] = "Changed; this session stays signed in";
            s.Responses[400] = "Invalid request data, a wrong current password, or a weak one";
            s.Responses[401] = "Not signed in";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<ChangePasswordRequest>()
                .Produces(204)
                .ProducesProblem(400)
                .ProducesProblem(401)
        );
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
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
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }
}

public sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("The current password is required.")
            .MinimumLength(PasswordConstants.MIN_LENGTH)
            .WithMessage(
                $"The current password must be at least {PasswordConstants.MIN_LENGTH} characters long."
            )
            .MaximumLength(PasswordConstants.MAX_LENGTH)
            .WithMessage(
                $"The current password must be at most {PasswordConstants.MAX_LENGTH} characters long."
            );
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("The new password is required.")
            .MinimumLength(PasswordConstants.MIN_LENGTH)
            .WithMessage(
                $"The new password must be at least {PasswordConstants.MIN_LENGTH} characters long."
            )
            .MaximumLength(PasswordConstants.MAX_LENGTH)
            .WithMessage(
                $"The new password must be at most {PasswordConstants.MAX_LENGTH} characters long."
            );
        ;
    }
}
