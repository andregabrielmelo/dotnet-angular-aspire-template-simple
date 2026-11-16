using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users.Delete;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Endpoints.Users;

public sealed class DeleteUserRequest
{
    public int Id { get; init; }
}

public class DeleteEndpoint(IMediator mediator)
    : Endpoint<DeleteUserRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("/users/{id}");
        Permissions(Permission.UsersDelete);

        Summary(s =>
        {
            s.Summary = "Delete a user";
            s.Description = "Deletes a user with the specified id.";
            s.ExampleRequest = new DeleteUserRequest { Id = 1 };

            s.Responses[400] = "Invalid request data";
            s.Responses[403] = $"Requires the {Permission.UsersDelete.Value} permission";
            s.Responses[404] = "User not found";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<DeleteUserRequest>()
                .Produces(statusCode: 204, contentType: "application/json")
                .ProducesProblem(400)
                .ProducesProblem(403)
                .ProducesProblem(404)
        );
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        DeleteUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new DeleteUserCommand(UserId.From(request.Id));
        var result = await mediator.Send(command, cancellationToken);

        return result.ToNoContentResult();
    }
}

public sealed class DeleteUserValidator : Validator<DeleteUserRequest>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be greater than zero");
    }
}
