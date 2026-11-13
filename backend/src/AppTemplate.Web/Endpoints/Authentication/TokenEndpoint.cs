using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Commands.CreateToken;
using AppTemplate.UseCases.Constants;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Authentication;

public sealed class TokenRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

/// <summary>A bearer token: send it as <c>Authorization: Bearer {AccessToken}</c>.</summary>
public sealed class TokenResponse
{
    /// <summary>The signed JWT.</summary>
    public required string AccessToken { get; set; }

    /// <summary>Always <c>Bearer</c>.</summary>
    public required string TokenType { get; set; }

    /// <summary>When the token stops being accepted; sign in again for a new one.</summary>
    public required DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Issues a bearer token for clients that can't use the session cookie: scripts, mobile and
/// desktop apps, other services (ADR 018). Browsers keep using <c>POST /auth/login</c>.
/// </summary>
public class TokenEndpoint(IMediator _mediator)
    : Endpoint<TokenRequest, Results<Ok<TokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/token");
        AllowAnonymous();
        Options(builder =>
            builder.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy)
        );

        Summary(s =>
        {
            s.Summary = "Get a bearer token";
            s.Description =
                "Checks the credentials as signing in does and returns a short-lived JWT, for "
                + "clients that can't hold the session cookie. Send it as "
                + "`Authorization: Bearer <token>`; nothing revokes it before it expires.";
            s.ExampleRequest = new TokenRequest
            {
                Email = "ada@example.com",
                Password = "correct horse battery staple",
            };
            s.Responses[200] = "The token and when it expires";
            s.Responses[400] = "Invalid request data";
            s.Responses[401] = "Wrong email or password, or too many failed attempts";
            s.Responses[403] = "The password is right, but the email isn't confirmed yet";
            s.Responses[429] = "Too many attempts from this address";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<TokenRequest>()
                .Produces<TokenResponse>(200)
                .ProducesProblem(400)
                .ProducesProblem(401)
                .ProducesProblem(403)
                .ProducesProblem(429)
        );
    }

    public override async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> ExecuteAsync(
        TokenRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new CreateTokenCommand(new EmailAddress(request.Email), request.Password),
            cancellationToken
        );
        return result.IsSuccess
            ? TypedResults.Ok(
                new TokenResponse
                {
                    AccessToken = result.Value.Token,
                    TokenType = "Bearer",
                    ExpiresAt = result.Value.ExpiresAt,
                }
            )
            : result.ToProblem();
    }
}

public sealed class TokenValidator : Validator<TokenRequest>
{
    public TokenValidator()
    {
        RuleFor(x => x.Email).EmailAddress().WithMessage("The email address is invalid.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("The password is required.")
            .MaximumLength(PasswordConstants.MAX_LENGTH)
            .WithMessage(
                $"The password must be at most {PasswordConstants.MAX_LENGTH} characters long."
            );
    }
}
