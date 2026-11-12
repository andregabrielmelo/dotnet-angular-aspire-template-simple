using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users.Queries.GetCurrent;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Users;

/// <param name="Permissions">What the caller may do - lets clients show or hide actions. The API still enforces every permission itself.</param>
public sealed record CurrentUserResponse(
    int Id,
    string Name,
    string Email,
    IReadOnlyList<string> Permissions
);

/// <summary>The caller's own profile, identified by the session cookie.</summary>
public class MeEndpoint(IMediator _mediator, ICurrentUser _currentUser)
    : EndpointWithoutRequest<Results<Ok<CurrentUserResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/users/me");

        Summary(s =>
        {
            s.Summary = "Get the current user";
            s.Description =
                "Returns the authenticated caller's profile and the permissions their roles grant.";
            s.ResponseExamples[200] = new CurrentUserResponse(
                1,
                "Sample User",
                "sample@example.com",
                [Permission.UsersRead.Value]
            );

            s.Responses[200] = "Current user returned successfully";
            s.Responses[401] = "Not signed in";
            s.Responses[404] = "The user was deleted";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Produces<CurrentUserResponse>(200, "application/json")
                .ProducesProblem(401)
                .ProducesProblem(404)
        );
    }

    public override async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        if (_currentUser.Id is not { } userId)
        {
            return Result.Unauthorized().ToProblem();
        }

        var result = await _mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);

        return result.ToOkResult(user => new CurrentUserResponse(
            user.Id.Value,
            user.Name.Value,
            user.Email.Value,
            _currentUser.Permissions.Order(StringComparer.Ordinal).ToList()
        ));
    }
}
