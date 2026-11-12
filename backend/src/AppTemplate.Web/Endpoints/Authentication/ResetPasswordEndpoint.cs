using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Commands.ResetPassword;
using AppTemplate.UseCases.Constants;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class ResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Code { get; set; }
    public required string NewPassword { get; set; }
}

/// <summary>
/// Sets a new password with the code from a reset link. Every existing session ends at its next
/// refresh (the account's security stamp changes); sign in again with the new password.
/// </summary>
public class ResetPasswordEndpoint(IMediator _mediator)
    : Endpoint<ResetPasswordRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/reset-password");
        AllowAnonymous();
        Options(builder =>
            builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy)
        );

        Summary(s =>
        {
            s.Summary = "Reset the password";
            s.Description =
                "Takes the email and code from the reset link and the new password. Signs out "
                + "every session at its next refresh, and lifts a lockout.";
            s.ExampleRequest = new ResetPasswordRequest
            {
                Email = "ada@example.com",
                Code = "Q2ZESjhB...",
                NewPassword = "correct horse battery staple",
            };
            s.Responses[204] = "The password is changed";
            s.Responses[429] = "Too many attempts from this address";
            s.Responses[400] = "Invalid request data, a wrong or expired code, or a weak password";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<ResetPasswordRequest>()
                .Produces(204)
                .ProducesProblem(400)
                .ProducesProblem(429)
        );
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ResetPasswordCommand(
                new EmailAddress(request.Email),
                request.Code,
                request.NewPassword
            ),
            cancellationToken
        );
        return result.ToNoContentResult();
    }
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).EmailAddress().WithMessage("The email address is invalid.");

        RuleFor(x => x.Code).NotEmpty().WithMessage("The code is required.");

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
    }
}
