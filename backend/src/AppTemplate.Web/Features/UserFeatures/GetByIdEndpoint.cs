using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Get;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class GetUserByIdRequest
{
    public int Id { get; set; }
}

public class GetByIdEndpoint(IMediator mediator)
    : Endpoint<GetUserByIdRequest, Results<Ok<UserRecord>, ProblemHttpResult>, GetUserByIdMapper>
{
    public override void Configure()
    {
        Get("/users/{id}");
        Policies(Permission.UsersRead);

        Summary(s =>
        {
            s.Summary = "Get a user by Id";
            s.Description = "Get a user with the specified Id.";
            s.ExampleRequest = new GetUserByIdRequest { Id = 1 };
            s.ResponseExamples[200] = new UserRecord(1, "Sample User", null);

            s.Responses[200] = "User obtained successfully";
            s.Responses[403] = $"Requires the {Permission.UsersRead} permission";
            s.Responses[400] = "Invalid request data";
            s.Responses[404] = "User not found";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<GetUserByIdRequest>()
                .Produces<UserRecord>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(404)
        );
    }

    public override async Task<Results<Ok<UserRecord>, ProblemHttpResult>> ExecuteAsync(
        GetUserByIdRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await mediator.Send(
            new GetUserQuery(UserId.From(request.Id)),
            cancellationToken
        );
        return result.ToOkResult(Map.FromEntity);
    }
}

public sealed class GetByIdUserValidator : Validator<GetUserByIdRequest>
{
    public GetByIdUserValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be greater than zero");
    }
}

public sealed class GetUserByIdMapper : Mapper<GetUserByIdRequest, UserRecord, UserDto>
{
    public override UserRecord FromEntity(UserDto e) =>
        new(e.Id.Value, e.Name.Value, e.PhoneNumber?.ToString());
}
