using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users.Register;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Authentication;

public sealed class RegisterRequest
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

/// <summary>Creates an account and signs it in, like <see cref="LoginEndpoint"/>.</summary>
public class RegisterEndpoint(IMediator _mediator, RefreshTokenCookie _cookie)
    : Endpoint<RegisterRequest, Results<Created<AccessTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Register";
            s.Description =
                "Creates an account and signs it in: returns an access token and sets the refresh "
                + "token cookie. A welcome email is sent to the address.";
            s.ExampleRequest = new RegisterRequest
            {
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                Password = "correct horse battery staple",
            };
            s.Responses[201] = "Account created and signed in";
            s.Responses[400] = "Invalid request data";
            s.Responses[409] = "An account with this email already exists";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<RegisterRequest>()
                .Produces<AccessTokenResponse>(201, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(409)
        );
    }

    public override async Task<
        Results<Created<AccessTokenResponse>, ProblemHttpResult>
    > ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RegisterUserCommand(
                UserName.From(request.Name),
                new EmailAddress(request.Email),
                request.Password
            ),
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var tokens = await CreateTokenWith<UserTokenService, IssuedTokens>(
            result.Value.Id.Value.ToString(),
            privileges => privileges.AddUser(result.Value)
        );
        _cookie.Append(HttpContext, tokens);

        return TypedResults.Created("/users/me", AccessTokenResponse.From(tokens));
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
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(AuthValidation.PasswordMinLength)
            .WithMessage($"Password must be at least {AuthValidation.PasswordMinLength} characters")
            .MaximumLength(AuthValidation.PasswordMaxLength);
    }
}
