using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication.Login;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Authentication;

public sealed class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

/// <summary>
/// Signs in, as in FastEndpoints' refresh token docs: the credentials are checked, then
/// <c>CreateTokenWith&lt;UserTokenService&gt;</c> creates the token pair. The access token goes in
/// the body; the refresh token only in its HttpOnly cookie.
/// </summary>
public class LoginEndpoint(IMediator _mediator, RefreshTokenCookie _cookie)
    : Endpoint<LoginRequest, Results<Ok<AccessTokenResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Sign in";
            s.Description =
                "Returns an access token, to send as Authorization: Bearer, and sets the refresh "
                + "token cookie used by POST /auth/refresh-token.";
            s.ExampleRequest = new LoginRequest
            {
                Email = "ada@example.com",
                Password = "correct horse battery staple",
            };
            s.Responses[200] = "Signed in";
            s.Responses[400] = "Invalid request data";
            s.Responses[401] = "Wrong email or password";
        });

        Tags("Auth");

        Description(builder =>
            builder
                .Accepts<LoginRequest>()
                .Produces<AccessTokenResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(401)
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
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var tokens = await CreateTokenWith<UserTokenService, IssuedTokens>(
            result.Value.Id.Value.ToString(),
            privileges => privileges.AddUser(result.Value)
        );
        _cookie.Append(HttpContext, tokens);

        return TypedResults.Ok(AccessTokenResponse.From(tokens));
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
