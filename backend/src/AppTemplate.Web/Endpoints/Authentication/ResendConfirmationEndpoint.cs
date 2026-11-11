using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Commands.ResendConfirmation;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class ResendConfirmationRequest
{
    public required string Email { get; set; }
}

/// <summary>
/// Sends another confirmation link. Always 202, whether or not the email has an account, so the
/// answer reveals nothing.
/// </summary>
public class ResendConfirmationEndpoint(IMediator _mediator)
    : Endpoint<ResendConfirmationRequest, Results<Accepted, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/resend-confirmation");
        AllowAnonymous();
        Options(builder =>
            builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy)
        );

        Summary(s =>
        {
            s.Summary = "Resend the confirmation email";
            s.Description =
                "Emails a new confirmation link if the address has an unconfirmed account. "
                + "Answers 202 either way.";
            s.ExampleRequest = new ResendConfirmationRequest { Email = "ada@example.com" };
            s.Responses[202] = "If the account exists and needs it, the email is on its way";
            s.Responses[429] = "Too many attempts from this address";
            s.Responses[400] = "Invalid request data";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<ResendConfirmationRequest>()
                .Produces(202)
                .ProducesProblem(400)
                .ProducesProblem(429)
        );
    }

    public override async Task<Results<Accepted, ProblemHttpResult>> ExecuteAsync(
        ResendConfirmationRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ResendConfirmationCommand(new EmailAddress(request.Email)),
            cancellationToken
        );
        return result.ToAcceptedResult();
    }
}

public sealed class ResendConfirmationValidator : Validator<ResendConfirmationRequest>
{
    public ResendConfirmationValidator()
    {
        RuleFor(x => x.Email).EmailAddress();
    }
}
