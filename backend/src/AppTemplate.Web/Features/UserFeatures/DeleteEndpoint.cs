using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users.Delete;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class DeleteUserRequest
{
    public int UserId { get; init; }
}

public class DeleteEndpoint(IMediator _mediator)
    : Endpoint<DeleteUserRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("/users/{UserId}");
        Policies(Permission.UsersDelete);

        Summary(s =>
        {
            s.Summary = "Delete a user";
            s.Description = "Deletes a user with the specified id.";
            s.ExampleRequest = new DeleteUserRequest { UserId = 1 };

            s.Responses[400] = "Invalid request data";
            s.Responses[403] = $"Requires the {Permission.UsersDelete} permission";
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
        var command = new DeleteUserCommand(UserId.From(request.UserId));
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToNoContentResult();
    }
}

public sealed class DeleteUserValidator : Validator<DeleteUserRequest>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Id is required");
    }
}
