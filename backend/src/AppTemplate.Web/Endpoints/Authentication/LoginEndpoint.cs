using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Commands.Login;
using AppTemplate.UseCases.Constants;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

/// <summary>
/// Signs in: Identity checks the credentials (with lockout) and sets its session cookie, which the
/// browser then sends with every request (ADR 015).
/// </summary>
public class LoginEndpoint(IMediator _mediator)
    : Endpoint<LoginRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Sign in";
            s.Description =
                "Sets the session cookie (HttpOnly, SameSite=Strict), which signs in every later "
                + "request from this browser.";
            s.ExampleRequest = new LoginRequest
            {
                Email = "ada@example.com",
                Password = "correct horse battery staple",
            };
            s.Responses[204] = "Signed in; the response sets the session cookie";
            s.Responses[400] = "Invalid request data";
            s.Responses[401] = "Wrong email or password, or too many failed attempts";
            s.Responses[403] = "The password is right, but the email isn't confirmed yet";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<LoginRequest>()
                .Produces(204)
                .ProducesProblem(400)
                .ProducesProblem(401)
                .ProducesProblem(403)
        );
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new LoginCommand(new EmailAddress(request.Email), request.Password),
            cancellationToken
        );
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).EmailAddress().WithMessage("The email address is invalid.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("The password is required.")
            .MinimumLength(PasswordConstants.MIN_LENGTH)
            .WithMessage(
                $"The password must be at least {PasswordConstants.MIN_LENGTH} characters long."
            )
            .MaximumLength(PasswordConstants.MAX_LENGTH)
            .WithMessage(
                $"The current password must be at most {PasswordConstants.MAX_LENGTH} characters long."
            );
    }
}
