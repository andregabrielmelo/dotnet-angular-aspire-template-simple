using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authentication.ConfirmEmail;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class ConfirmEmailRequest
{
    public int UserId { get; set; }
    public required string Code { get; set; }
}

/// <summary>Confirms an email with the code from its confirmation link; the account can then sign in.</summary>
public class ConfirmEmailEndpoint(IMediator _mediator)
    : Endpoint<ConfirmEmailRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/confirm-email");
        AllowAnonymous();
        Options(builder =>
            builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy)
        );

        Summary(s =>
        {
            s.Summary = "Confirm an email address";
            s.Description =
                "Takes the userId and code from the link in the confirmation email. The account "
                + "can sign in afterwards.";
            s.ExampleRequest = new ConfirmEmailRequest { UserId = 1, Code = "Q2ZESjhB..." };
            s.Responses[204] = "Confirmed";
            s.Responses[429] = "Too many attempts from this address";
            s.Responses[400] = "Invalid request data, or a wrong or expired code";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<ConfirmEmailRequest>()
                .Produces(204)
                .ProducesProblem(400)
                .ProducesProblem(429)
        );
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ConfirmEmailCommand(UserId.From(request.UserId), request.Code),
            cancellationToken
        );
        return result.ToNoContentResult();
    }
}

public sealed class ConfirmEmailValidator : Validator<ConfirmEmailRequest>
{
    public ConfirmEmailValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("The link is incomplete. Open it from the email again.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("The code is required.");
    }
}
