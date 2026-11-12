using AppTemplate.UseCases.Authentication.Commands.Logout;

namespace AppTemplate.Web.Endpoints.Authentication;

/// <summary>Signs out: Identity deletes the session cookie (ADR 015).</summary>
public class LogoutEndpoint(IMediator _mediator) : EndpointWithoutRequest<NoContent>
{
    public override void Configure()
    {
        Post("/auth/logout");

        Summary(s =>
        {
            s.Summary = "Sign out";
            s.Description = "Ends this session by deleting its cookie.";
            s.Responses[204] = "Signed out";
            s.Responses[401] = "Not signed in";
        });

        Tags("Auth");

        Description(builder => builder.Produces(204).ProducesProblem(401));
    }

    public override async Task<NoContent> ExecuteAsync(CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(), cancellationToken);
        return TypedResults.NoContent();
    }
}
