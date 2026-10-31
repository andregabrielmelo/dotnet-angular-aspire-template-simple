using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Login;
using AppTemplate.Web.Authentication;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.AuthFeatures;

public sealed class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class LoginEndpoint(IMediator _mediator, AuthCookies _cookies)
    : Endpoint<LoginRequest, Results<Ok<AccessTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy));

        Summary(s =>
        {
            s.Summary = "Sign in";
            s.Description =
                "Returns a 15-minute access token, to send as Authorization: Bearer, and sets the "
                + "refresh token cookie used by /auth/refresh.";
            s.ExampleRequest = new LoginRequest
            {
                Email = "ada@example.com",
                Password = "correct horse battery staple",
            };
            s.ResponseExamples[200] = new AccessTokenResponse("eyJhbGciOiJIUzI1NiJ9...", 900);

            s.Responses[200] = "Signed in";
            s.Responses[400] = "Invalid request data";
            s.Responses[401] = "Wrong email or password";
            s.Responses[429] = "Too many attempts; retry after Retry-After";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<LoginRequest>()
                .Produces<AccessTokenResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(401)
                .ProducesProblem(429)
        );
    }

    public override async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new LoginCommand(new EmailAddress(request.Email), request.Password),
            cancellationToken
        );

        return result.ToOkResult(session => _cookies.SignIn(HttpContext, session));
    }
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MaximumLength(AuthValidation.PasswordMaxLength);
    }
}
