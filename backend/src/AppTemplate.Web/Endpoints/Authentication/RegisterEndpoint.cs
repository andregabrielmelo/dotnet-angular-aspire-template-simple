using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Constants;
using AppTemplate.UseCases.Users.Register;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class RegisterRequest
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

/// <summary>
/// Creates an account and emails its confirmation link. It signs in (<see cref="LoginEndpoint"/>)
/// once the email is confirmed, so registration returns no tokens.
/// </summary>
public class RegisterEndpoint(IMediator _mediator)
    : Endpoint<RegisterRequest, Results<Created, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Options(builder =>
            builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy)
        );

        Summary(s =>
        {
            s.Summary = "Register";
            s.Description =
                "Creates an account and emails a link that confirms the address. Sign in "
                + "(POST /auth/login) once it's confirmed.";
            s.ExampleRequest = new RegisterRequest
            {
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                Password = "correct horse battery staple",
            };
            s.Responses[201] = "Account created; the confirmation email is on its way";
            s.Responses[400] = "Invalid request data";
            s.Responses[409] = "An account with this email already exists";
            s.Responses[429] = "Too many attempts from this address";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<RegisterRequest>()
                .Produces(201)
                .ProducesProblem(400)
                .ProducesProblem(409)
                .ProducesProblem(429)
        );
    }

    public override async Task<Results<Created, ProblemHttpResult>> ExecuteAsync(
        RegisterRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new RegisterUserCommand(
                UserName.From(request.Name),
                new EmailAddress(request.Email),
                request.Password
            ),
            cancellationToken
        );
        return result.IsSuccess ? TypedResults.Created("/users/me") : result.ToProblem();
    }
}

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(UserName.MaxLength)
            .WithMessage($"User name must not exceed {UserName.MaxLength} characters");
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
                $"The password must be at most {PasswordConstants.MAX_LENGTH} characters long."
            );
    }
}
