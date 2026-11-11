using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Commands.ForgotPassword;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; set; }
}

/// <summary>
/// Emails a password reset link to a confirmed account. Always 202, whether or not the email has
/// an account, so the answer reveals nothing.
/// </summary>
public class ForgotPasswordEndpoint(IMediator _mediator)
    : Endpoint<ForgotPasswordRequest, Results<Accepted, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/forgot-password");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Ask for a password reset";
            s.Description =
                "Emails a link to POST /auth/reset-password if the address has a confirmed "
                + "account. Answers 202 either way.";
            s.ExampleRequest = new ForgotPasswordRequest { Email = "ada@example.com" };
            s.Responses[202] = "If the account exists, the email is on its way";
            s.Responses[400] = "Invalid request data";
        });

        Tags("Auth");

        Description(builder =>
            builder.Accepts<ForgotPasswordRequest>().Produces(202).ProducesProblem(400)
        );
    }

    public override async Task<Results<Accepted, ProblemHttpResult>> ExecuteAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ForgotPasswordCommand(new EmailAddress(request.Email)),
            cancellationToken
        );
        return result.ToAcceptedResult();
    }
}

public sealed class ForgotPasswordValidator : Validator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).EmailAddress().WithMessage("The email address is invalid.");
    }
}
