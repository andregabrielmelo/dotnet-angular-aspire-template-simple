using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Update;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;
using AppTemplate.Web.Validation;

namespace AppTemplate.Web.Features.UserFeatures;

public sealed class UpdateUserRequest
{
    /// <summary>Bound from the route - FastEndpoints lets route values override the body.</summary>
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Leave empty to keep the current phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>E.164 country calling code, e.g. "+55". Required with <see cref="PhoneNumber"/>.</summary>
    public string? PhoneCountryCode { get; set; }

    public string? PhoneExtension { get; set; }
}

public sealed record UpdateUserResponse(UserRecord User);

public class UpdateEndpoint(IMediator _mediator)
    : Endpoint<
        UpdateUserRequest,
        Results<Ok<UpdateUserResponse>, ProblemHttpResult>,
        UpdateUserMapper
    >
{
    public override void Configure()
    {
        Put("/users/{id}");
        Version(ApiVersions.V1);

        Summary(s =>
        {
            s.Summary = "Update a user";
            s.Description =
                "Updates a user's name and, optionally, phone number (which needs a country code). "
                + "Users may update their own profile; "
                + "updating anyone else's requires the users:write permission.";
            s.ExampleRequest = new UpdateUserRequest
            {
                Id = 1,
                Name = "Sample User",
                PhoneNumber = "555 0100",
                PhoneCountryCode = "+1",
            };
            s.ResponseExamples[200] = new UpdateUserResponse(
                new UserRecord(1, "Sample User", "+1 555 0100")
            );

            s.Responses[200] = "User updated successfully";
            s.Responses[400] = "Invalid request data";
            s.Responses[403] = "Not your profile, and no users:write permission";
            s.Responses[404] = "User not found";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Accepts<UpdateUserRequest>()
                .Produces<UpdateUserResponse>(200, "application/json")
                .ProducesProblem(400)
                .ProducesProblem(403)
                .ProducesProblem(404)
        );
    }

    public override async Task<Results<Ok<UpdateUserResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateUserCommand(
            UserId.From(request.Id),
            UserName.From(request.Name),
            request.PhoneNumber,
            request.PhoneCountryCode,
            request.PhoneExtension
        );
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToOkResult<UserDto, UpdateUserResponse>(Map.FromEntity);
    }
}

public sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(UserName.MaxLength)
            .WithMessage($"User name must not exceed {UserName.MaxLength} characters");
        // Id comes from the route (route values override the body), so there is no separate
        // body Id to compare it against.
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be greater than zero");
        RuleFor(x => x.PhoneCountryCode)
            .NotEmpty()
            .WithMessage("Phone country code is required with a phone number")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.PhoneCountryCode).PhoneCountryCode();
        RuleFor(x => x.PhoneNumber).PhoneNumber();
        RuleFor(x => x.PhoneExtension).PhoneExtension();
    }
}

public sealed class UpdateUserMapper : Mapper<UpdateUserRequest, UpdateUserResponse, UserDto>
{
    public override UpdateUserResponse FromEntity(UserDto e) =>
        new(new UserRecord(e.Id.Value, e.Name.Value, e.PhoneNumber?.ToString()));
}
