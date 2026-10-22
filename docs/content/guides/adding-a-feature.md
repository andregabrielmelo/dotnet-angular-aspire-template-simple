---
title: "Adding a feature, step by step"
weight: 20
---

# Adding a feature, step by step

This guide builds a complete feature, **projects**, through every layer: domain model, database, use cases, endpoints, permissions, the Angular page, and tests. Every code block was compiled and tested against this template before it was written down. Follow the same steps for your own feature, replacing `Project` with your name.

Read [How the project fits together]({{< relref "project-map" >}}) first if the words *command*, *handler* or *query service* aren't familiar yet. Each step names the `User` file it mirrors, so you can compare with working code in the repository.

## 1. Design the slice

Write down the operations and who may call each before you write code:

| Operation | Route | Success | Who may call it |
|---|---|---|---|
| Create a project | `POST /projects` | `201` with the project and a `Location` header | any signed-in user, who becomes the owner |
| List my projects | `GET /projects?page=1&per_page=10` | `200` with a page | any signed-in user; only their own projects |
| Get a project | `GET /projects/{id}` | `200` | the owner, or anyone with the new `projects:read` permission |
| Rename a project | `PUT /projects/{id}` | `200` | the owner |
| Delete a project | `DELETE /projects/{id}` | `204` | the owner |

The browser calls these as `/api/projects/...`. The backend for frontend strips `/api` and adds the access token.

Two kinds of authorization show up here, and both are normal:
- **Permission policies** decide by *who you are*. Declare them on the endpoint with `Policies(Permission.X)`.
- **Resource rules** decide by *what you're touching*, such as "only the owner". They need the loaded data, so they live in the handler, using `ICurrentUser`, and return `Result.Forbidden()`.

## 2. Core: the domain model

Everything here lives in `backend/src/AppTemplate.Core/Aggregates/ProjectAggregate/`. It mirrors `Aggregates/UserAggregate/`.

**The id.** A [Vogen](https://github.com/SteveDunn/Vogen) value object, so a `ProjectId` can't be mixed up with a `UserId` or a plain `int`:

```csharp
// ProjectId.cs
namespace AppTemplate.Core.Aggregates.ProjectAggregate;

[ValueObject<int>]
public readonly partial struct ProjectId
{
    private static Validation Validate(int value) =>
        value > 0 ? Validation.Ok : Validation.Invalid("ProjectId must be positive.");
}
```

**A value object with a rule.** The name can never be empty or too long, wherever it comes from:

```csharp
// ProjectName.cs
namespace AppTemplate.Core.Aggregates.ProjectAggregate;

[ValueObject<string>(conversions: Conversions.SystemTextJson)]
public partial struct ProjectName
{
    public const int MaxLength = 100;

    private static Validation Validate(in string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Validation.Invalid("Name cannot be empty");
        }

        if (name.Length > MaxLength)
        {
            return Validation.Invalid($"Name cannot be longer than {MaxLength} characters");
        }

        return Validation.Ok;
    }
}
```

**The aggregate.** It has private setters, a factory that checks its inputs, and methods for every change:

```csharp
// Project.cs
namespace AppTemplate.Core.Aggregates.ProjectAggregate;

/// <summary>
/// A project belongs to the user who created it, identified by their identity-provider
/// subject (<c>sub</c>), the same value as <c>User.ExternalId</c>. Only the owner may change it.
/// </summary>
public class Project(string ownerExternalId, ProjectName name)
    : EntityBase<Project, ProjectId>,
        IAggregateRoot
{
    public const int OwnerExternalIdMaxLength = 64;

    public string OwnerExternalId { get; private set; } = ownerExternalId;
    public ProjectName Name { get; private set; } = name;

    /// <summary>When the row was inserted; set by the database, so it's only known once saved.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Project Create(string ownerExternalId, ProjectName name)
    {
        if (string.IsNullOrWhiteSpace(ownerExternalId))
            throw new ArgumentException("Owner cannot be empty", nameof(ownerExternalId));

        if (ownerExternalId.Length > OwnerExternalIdMaxLength)
            throw new ArgumentException(
                $"Owner must not exceed {OwnerExternalIdMaxLength} characters",
                nameof(ownerExternalId)
            );

        return new Project(ownerExternalId, name);
    }

    public bool IsOwnedBy(string? externalId) =>
        externalId is not null && externalId == OwnerExternalId;

    public Project Rename(ProjectName newName)
    {
        Name = newName;
        return this;
    }
}
```

The owner is stored as the Keycloak `sub` (what `ICurrentUser.ExternalId` returns), not as a foreign key to `users`. The handler can then authorize without loading the user, and a project doesn't depend on the `User` slice you'll eventually delete. Use a real foreign key when the relationship matters to queries.

**A specification** for the read-only lookup:

```csharp
// Specifications/ProjectByIdSpecification.cs
namespace AppTemplate.Core.Aggregates.ProjectAggregate.Specifications;

/// <summary>
/// Read-only lookup (used by GetProjectHandler), so AsNoTracking. Writes load the project
/// with IRepository.GetByIdAsync instead, which tracks it.
/// </summary>
public class ProjectByIdSpecification : Specification<Project>
{
    public ProjectByIdSpecification(ProjectId projectId) =>
        Query.Where(project => project.Id == projectId).AsNoTracking();
}
```

Add `.AsNoTracking()` when nothing will change what's loaded. Leave it off when the handler changes the entity and saves it.

If something elsewhere should react when a project is created, raise a domain event in `Create` with `RegisterDomainEvent`, as `User.Create` does with `UserProvisioned`. See the [cookbook]({{< relref "cookbook#react-to-a-domain-event" >}}).

## 3. Infrastructure: the database

**Map the entity** in `Infrastructure/Data/Configurations/ProjectConfiguration.cs` (mirrors `UserConfiguration.cs`):

```csharp
using AppTemplate.Core.Aggregates.ProjectAggregate;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder
            .Property(entity => entity.Id)
            .HasValueGenerator<
                VogenIdValueGenerator<ApplicationDatabaseContext, Project, ProjectId>
            >()
            .HasVogenConversion()
            .IsRequired();

        builder
            .Property(entity => entity.Name)
            .HasVogenConversion()
            .HasMaxLength(ProjectName.MaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.OwnerExternalId)
            .HasMaxLength(Project.OwnerExternalIdMaxLength)
            .IsRequired();

        // The list endpoint filters by owner on every call.
        builder.HasIndex(entity => entity.OwnerExternalId);

        builder.Property(entity => entity.CreatedAtUtc).HasDefaultValueSql("now()");
    }
}
```

**Register the Vogen converters** once, centrally, in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs`. `HasVogenConversion()` doesn't compile without them:

```csharp
[EfCoreConverter<UserId>]
[EfCoreConverter<UserName>]
[EfCoreConverter<ProjectId>]
[EfCoreConverter<ProjectName>]
internal partial class VogenEfCoreConverters;
```

**Add the set** to `Infrastructure/Data/ApplicationDatabaseContext.cs`. `ApplyConfigurationsFromAssembly` picks up `ProjectConfiguration` by itself:

```csharp
public DbSet<Project> Projects => Set<Project>();
```

**Create the migration** from `backend/`, then format it:

```bash
dotnet ef migrations add AddProjects \
  --project src/AppTemplate.Infrastructure \
  --startup-project src/AppTemplate.Web \
  -o Data/Migrations
dotnet csharpier format .
```

Read the generated `Data/Migrations/*_AddProjects.cs`: a `projects` table with snake_case columns (`owner_external_id`, `created_at_utc`), the primary key, and the index. It's applied automatically when the app starts in Development. `MigrationDriftTests` fails if you change the model and forget this step.

**The list query service**, `Infrastructure/Data/Queries/ListProjectsQueryService.cs`. Its interface comes in step 4. It reads straight into DTOs, without loading aggregates:

```csharp
using AppTemplate.UseCases.Projects;
using AppTemplate.UseCases.Projects.List;

namespace AppTemplate.Infrastructure.Data.Queries;

public class ListProjectsQueryService(ApplicationDatabaseContext _db) : IListProjectsQueryService
{
    public async Task<PagedResult<ProjectDto>> ListAsync(
        string ownerExternalId,
        int page,
        int perPage,
        CancellationToken cancellationToken
    )
    {
        var owned = _db.Projects.AsNoTracking().Where(p => p.OwnerExternalId == ownerExternalId);

        var totalCount = await owned.CountAsync(cancellationToken);
        var items = await owned
            .OrderBy(p => p.Id) // a stable order, so pages don't overlap or skip rows
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(p => new ProjectDto(p.Id, p.Name, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)perPage);
        return new PagedResult<ProjectDto>(items, page, perPage, totalCount, totalPages);
    }
}
```

`ListUsersQueryService` shows the alternative: raw SQL with `FromSqlRaw`. Use that only when LINQ can't express the query or is measurably slow. If you do, column names are snake_case and input is always a parameter.

**Register it** in `Infrastructure/InfrastructureServiceExtensions.cs`, next to the users one:

```csharp
services
    .AddScoped(typeof(IRepository<>), typeof(EntityFrameworkRepository<>))
    .AddScoped<IListUsersQueryService, ListUsersQueryService>()
    .AddScoped<IListProjectsQueryService, ListProjectsQueryService>();
```

`IRepository<Project>` needs no registration: the open generic covers every aggregate.

## 4. UseCases: commands, queries and handlers

Everything here lives in `backend/src/AppTemplate.UseCases/Projects/`, one folder per use case. It mirrors `UseCases/Users/`.

**Repository or query service?**
- **Changing something** (load it, call a domain method, save it): use `IRepository<T>`, plus a specification for anything beyond lookup by id.
- **Reading a list, a projection or a join**: use a query service that returns DTOs.
- **Reading one item to authorize and return it**: a repository with an `AsNoTracking` specification is fine, as in `GetProjectHandler` below.

More detail in [When To Abstract]({{< relref "design-decisions#when-to-abstract" >}}).

**The DTO** that handlers return. Plain data, never the entity:

```csharp
// ProjectDto.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;

namespace AppTemplate.UseCases.Projects;

public record ProjectDto(ProjectId Id, ProjectName Name, DateTimeOffset CreatedAtUtc)
{
    public static ProjectDto FromEntity(Project project) =>
        new(project.Id, project.Name, project.CreatedAtUtc);
}
```

### Create: a command

```csharp
// Create/CreateProjectCommand.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;

namespace AppTemplate.UseCases.Projects.Create;

public record CreateProjectCommand(ProjectName Name) : ICommand<Result<ProjectDto>>;
```

```csharp
// Create/CreateProjectHandler.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Projects.Create;

/// <summary>The caller becomes the project's owner.</summary>
public class CreateProjectHandler(IRepository<Project> _repository, ICurrentUser _currentUser)
    : ICommandHandler<CreateProjectCommand, Result<ProjectDto>>
{
    public async ValueTask<Result<ProjectDto>> Handle(
        CreateProjectCommand command,
        CancellationToken cancellationToken
    )
    {
        if (_currentUser.ExternalId is not { } owner)
            return Result<ProjectDto>.Unauthorized();

        var project = await _repository.AddAsync(
            Project.Create(owner, command.Name),
            cancellationToken
        );

        return ProjectDto.FromEntity(project);
    }
}
```

`AddAsync` saves immediately, in its own transaction, and EF reads back the database-generated `CreatedAtUtc`. A handler that makes several changes which must succeed together needs an explicit transaction ([Transactions And Side Effects]({{< relref "design-decisions#transactions-and-side-effects" >}})).

### Get: a query with a resource rule

```csharp
// Get/GetProjectQuery.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;

namespace AppTemplate.UseCases.Projects.Get;

public record GetProjectQuery(ProjectId ProjectId) : IQuery<Result<ProjectDto>>;
```

```csharp
// Get/GetProjectHandler.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;
using AppTemplate.Core.Aggregates.ProjectAggregate.Specifications;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Projects.Get;

/// <summary>
/// Resource-based authorization: the owner may read their project, and anyone with
/// <see cref="Permission.ProjectsRead"/> may read every project. It needs the loaded
/// project, so it's decided here rather than by an endpoint policy.
/// </summary>
public class GetProjectHandler(IRepository<Project> _repository, ICurrentUser _currentUser)
    : IQueryHandler<GetProjectQuery, Result<ProjectDto>>
{
    public async ValueTask<Result<ProjectDto>> Handle(
        GetProjectQuery query,
        CancellationToken cancellationToken
    )
    {
        var project = await _repository.FirstOrDefaultAsync(
            new ProjectByIdSpecification(query.ProjectId),
            cancellationToken
        );
        if (project is null)
            return Result<ProjectDto>.NotFound();

        if (
            !project.IsOwnedBy(_currentUser.ExternalId)
            && !_currentUser.HasPermission(Permission.ProjectsRead)
        )
            return Result<ProjectDto>.Forbidden();

        return ProjectDto.FromEntity(project);
    }
}
```

Returning `Forbidden` tells a stranger that the project exists. When that matters, return `NotFound()` for other people's resources instead.

### List: a paged query through a query service

```csharp
// List/IListProjectsQueryService.cs
namespace AppTemplate.UseCases.Projects.List;

/// <summary>
/// Reads a page of projects straight into DTOs, without loading aggregates.
/// Implemented by <c>ListProjectsQueryService</c> (Infrastructure). Used by <see cref="ListProjectsHandler"/>.
/// </summary>
public interface IListProjectsQueryService
{
    Task<PagedResult<ProjectDto>> ListAsync(
        string ownerExternalId,
        int page,
        int perPage,
        CancellationToken cancellationToken
    );
}
```

```csharp
// List/ListProjectsQuery.cs
namespace AppTemplate.UseCases.Projects.List;

/// <summary>The caller's own projects, a page at a time.</summary>
public record ListProjectsQuery(int Page = 1, int PerPage = Constants.DEFAULT_PAGE_SIZE)
    : IQuery<Result<PagedResult<ProjectDto>>>;
```

```csharp
// List/ListProjectsHandler.cs
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Projects.List;

public class ListProjectsHandler(IListProjectsQueryService _query, ICurrentUser _currentUser)
    : IQueryHandler<ListProjectsQuery, Result<PagedResult<ProjectDto>>>
{
    public async ValueTask<Result<PagedResult<ProjectDto>>> Handle(
        ListProjectsQuery query,
        CancellationToken cancellationToken
    )
    {
        if (_currentUser.ExternalId is not { } owner)
            return Result<PagedResult<ProjectDto>>.Unauthorized();

        return await _query.ListAsync(owner, query.Page, query.PerPage, cancellationToken);
    }
}
```

The query-service interface lives in UseCases, not Core, because it returns a use case DTO. Ports to things outside the process, such as `IEmailSender`, live in `Core/Interfaces`.

### Rename and Delete: commands that change an aggregate

```csharp
// Rename/RenameProjectCommand.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;

namespace AppTemplate.UseCases.Projects.Rename;

public record RenameProjectCommand(ProjectId ProjectId, ProjectName Name)
    : ICommand<Result<ProjectDto>>;
```

```csharp
// Rename/RenameProjectHandler.cs
using AppTemplate.Core.Aggregates.ProjectAggregate;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Projects.Rename;

/// <summary>Only the owner may rename a project.</summary>
public class RenameProjectHandler(IRepository<Project> _repository, ICurrentUser _currentUser)
    : ICommandHandler<RenameProjectCommand, Result<ProjectDto>>
{
    public async ValueTask<Result<ProjectDto>> Handle(
        RenameProjectCommand command,
        CancellationToken cancellationToken
    )
    {
        var project = await _repository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
            return Result<ProjectDto>.NotFound();

        if (!project.IsOwnedBy(_currentUser.ExternalId))
            return Result<ProjectDto>.Forbidden();

        project.Rename(command.Name);
        await _repository.UpdateAsync(project, cancellationToken);

        return ProjectDto.FromEntity(project);
    }
}
```

`DeleteProjectCommand(ProjectId ProjectId) : ICommand<Result>` and its handler follow the same shape: load with `GetByIdAsync`, return `NotFound` or `Forbidden`, then `DeleteAsync` and `Result.Success()`.

Handlers never touch HTTP, EF Core or the `DbContext`. They return a `Result` for every expected outcome and throw only for the unexpected.

## 5. A new permission

`projects:read` lets an admin read anyone's project. A permission is a Keycloak client role, so it's added in three places:

1. **`UseCases/Authorization/Permission.cs`**: the constant, and add it to `All`. Every name in `All` automatically becomes an ASP.NET Core policy.
   ```csharp
   /// <summary>Read anyone's project (everyone may read their own).</summary>
   public const string ProjectsRead = "projects:read";

   public static IReadOnlyList<string> All { get; } =
   [UsersRead, UsersWrite, UsersDelete, ProjectsRead];
   ```
2. **`AppHost/Realms/apptemplate-realm.json`**: a client role on `apptemplate-api`, added to the `admin` realm role's composites:
   ```json
   { "name": "projects:read", "description": "Read anyone's project" }
   ```
   Keycloak imports the realm only into an empty data volume. Delete the Keycloak volume, or add the role in the admin console (`http://localhost:8080`).
3. **`frontend/src/app/core/auth/permissions.ts`**: `ProjectsRead: 'projects:read'`, so the UI can show or hide things.

## 6. Web: the endpoints

One class per operation, in `backend/src/AppTemplate.Web/Features/ProjectFeatures/`. Each file holds the endpoint, its request, and its validator. It mirrors `Web/Features/UserFeatures/`.

**The response shape**, shared by the endpoints:

```csharp
// ProjectRecord.cs
using AppTemplate.UseCases.Projects;

namespace AppTemplate.Web.Features.ProjectFeatures;

/// <summary>A project as the API returns it: plain values, never the aggregate.</summary>
public record ProjectRecord(int Id, string Name, DateTimeOffset CreatedAtUtc)
{
    public static ProjectRecord FromDto(ProjectDto dto) =>
        new(dto.Id.Value, dto.Name.Value, dto.CreatedAtUtc);
}
```

**A complete endpoint**, `CreateEndpoint.cs`:

```csharp
using AppTemplate.Core.Aggregates.ProjectAggregate;
using AppTemplate.UseCases.Projects.Create;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.ProjectFeatures;

public sealed class CreateProjectRequest
{
    public required string Name { get; set; }
}

public class CreateEndpoint(IMediator _mediator)
    : Endpoint<CreateProjectRequest, Results<Created<ProjectRecord>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/projects");

        Summary(s =>
        {
            s.Summary = "Create a project";
            s.Description = "Creates a project owned by the caller.";
            s.ExampleRequest = new CreateProjectRequest { Name = "Website redesign" };
            s.ResponseExamples[201] = new ProjectRecord(
                1,
                "Website redesign",
                DateTimeOffset.UnixEpoch
            );

            s.Responses[201] = "Project created; Location points to it";
            s.Responses[400] = "Invalid request data";
        });

        Tags("Projects");

        Description(builder =>
            builder
                .Accepts<CreateProjectRequest>()
                .Produces<ProjectRecord>(201, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task<Results<Created<ProjectRecord>, ProblemHttpResult>> ExecuteAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new CreateProjectCommand(ProjectName.From(request.Name)),
            cancellationToken
        );

        return result.ToCreatedResult(
            project => $"/projects/{project.Id.Value}",
            ProjectRecord.FromDto
        );
    }
}

public sealed class CreateProjectValidator : Validator<CreateProjectRequest>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(ProjectName.MaxLength)
            .WithMessage($"Name must not exceed {ProjectName.MaxLength} characters");
    }
}
```

**Each part of the endpoint:**
- **No `Policies(...)`:** any signed-in user may create. The global authorization fallback already rejects anonymous callers. An endpoint for admins only adds `Policies(Permission.X)`; a public one calls `AllowAnonymous()` and is added to `EndpointAuthorizationTests`.
- **The validator** checks the request's shape, so bad input gets a 400 with an `errors` object keyed by field name. `ProjectName.From` would throw on the same input, but that would be a 500. Validators turn expected bad input into a proper answer.
- **`ResultExtensions`** is the only place a `Result` becomes HTTP: `ToOkResult` (200), `ToCreatedResult` (201 with `Location`), `ToNoContentResult` (204). `Invalid` becomes 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409, and anything else 500, all as problem details ([ADR 009]({{< relref "architecture-decisions/adr-009-problem-details-error-contract" >}})).
- **`Summary`, `Tags` and `Description`** feed the OpenAPI document, which in turn generates the frontend's types.
- **`Location`** is the API's path (`/projects/1`). A browser client reaches it as `/api/projects/1`.

**The other endpoints** follow the same shape. These are the parts that differ:

```csharp
// GetByIdEndpoint.cs: the id comes from the route
public sealed class GetProjectByIdRequest
{
    public int Id { get; set; }
}
// Configure(): Get("/projects/{id}");
// ExecuteAsync():
var result = await _mediator.Send(
    new GetProjectQuery(ProjectId.From(request.Id)),
    cancellationToken
);
return result.ToOkResult(ProjectRecord.FromDto);
```

```csharp
// RenameEndpoint.cs: route value and body together
public sealed class RenameProjectRequest
{
    /// <summary>Bound from the route - FastEndpoints lets route values override the body.</summary>
    public int Id { get; set; }

    public required string Name { get; set; }
}
// Configure(): Put("/projects/{id}");
```

```csharp
// DeleteEndpoint.cs: no response body
public class DeleteEndpoint(IMediator _mediator)
    : Endpoint<DeleteProjectRequest, Results<NoContent, ProblemHttpResult>>
// Configure(): Delete("/projects/{id}");
// ExecuteAsync(): return result.ToNoContentResult();
```

**The list endpoint**, `ListEndpoint.cs`, binds the paging query parameters and maps a page of DTOs to a page of records:

```csharp
using AppTemplate.UseCases.Projects;
using AppTemplate.UseCases.Projects.List;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.ProjectFeatures;

public sealed class ListProjectsRequest
{
    [BindFrom("page")]
    public int Page { get; init; } = 1;

    [BindFrom("per_page")]
    public int PerPage { get; init; } = Constants.DEFAULT_PAGE_SIZE;
}

public record ProjectListResponse : UseCases.PagedResult<ProjectRecord>
{
    public ProjectListResponse(
        IReadOnlyList<ProjectRecord> Items,
        int Page,
        int PerPage,
        int TotalCount,
        int TotalPages
    )
        : base(Items, Page, PerPage, TotalCount, TotalPages) { }

    public static ProjectListResponse FromDto(UseCases.PagedResult<ProjectDto> page) =>
        new(
            page.Items.Select(ProjectRecord.FromDto).ToList(),
            page.Page,
            page.PerPage,
            page.TotalCount,
            page.TotalPages
        );
}

public class ListEndpoint(IMediator _mediator)
    : Endpoint<ListProjectsRequest, Results<Ok<ProjectListResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/projects");
        // Summary(...), Tags("Projects"), Description(...) as in CreateEndpoint
    }

    public override async Task<Results<Ok<ProjectListResponse>, ProblemHttpResult>> ExecuteAsync(
        ListProjectsRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ListProjectsQuery(request.Page, request.PerPage),
            cancellationToken
        );

        return result.ToOkResult(ProjectListResponse.FromDto);
    }
}

public sealed class ListProjectsValidator : Validator<ListProjectsRequest>
{
    public ListProjectsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page must be >= 1");

        RuleFor(x => x.PerPage)
            .InclusiveBetween(1, Constants.MAX_PAGE_SIZE)
            .WithMessage($"per_page must be between 1 and {Constants.MAX_PAGE_SIZE}");
    }
}
```

In Web, write `UseCases.PagedResult<T>` in full: `Ardalis.Result` has a `PagedResult<T>` too, and both namespaces are global usings there, so the short name doesn't compile. The users list also adds a `Link` header with first, previous, next and last URLs; copy `AddLinkHeader` from `Web/Features/UserFeatures/ListEndpoint.cs` if your clients want it.

## 7. Update the API contract

The frontend's types are generated from the API. After any endpoint, request or response change, run these from the repository root:

```bash
scripts/export-openapi.sh
```

```bash
cd frontend && npm run api:generate
```

`backend/openapi/v1.json` gains the `/projects` paths, and `frontend/src/app/core/api/api-types.ts` gains `ProjectRecord`, `CreateProjectRequest`, `RenameProjectRequest` and `PagedResultOfProjectRecord`. Commit both files. CI fails when either is stale.

## 8. Frontend: the Angular feature

Everything here lives in `frontend/src/app/features/projects/`. It mirrors `features/users/`.

**Types**, `projects.model.ts`. Aliases of the generated schemas, so a renamed field fails the build:

```ts
import { ApiSchema } from '../../core/api/api-schema';

// Shapes returned by the Web API's /projects endpoints, generated from its OpenAPI document.
export type Project = ApiSchema<'ProjectRecord'>;
export type ProjectPage = ApiSchema<'PagedResultOfProjectRecord'>;
export type CreateProjectRequest = ApiSchema<'CreateProjectRequest'>;
export type RenameProjectRequest = ApiSchema<'RenameProjectRequest'>;
```

**HTTP calls**, `projects.service.ts`. Components never inject `HttpClient`, and ESLint enforces it:

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateProjectRequest, Project, ProjectPage, RenameProjectRequest } from './projects.model';
import { API_BASE } from '../../core/api/api-paths';

const BASE = `${API_BASE}/projects`;

/** The projects API (reached through the backend for frontend). */
@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);

  list(page: number, perPage: number): Observable<ProjectPage> {
    return this.http.get<ProjectPage>(BASE, { params: { page, per_page: perPage } });
  }

  create(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(BASE, request);
  }

  rename(id: number, request: RenameProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${BASE}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`);
  }
}
```

The session cookie and the `X-CSRF` header are added for you by the browser and `auth-interceptor.ts`.

**The page**, `projects-page.ts`. It uses signals for state and a reactive form, and reads validation messages from the problem details `errors` object:

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Project } from './projects.model';
import { ProjectsService } from './projects.service';

/** The signed-in user's own projects: list, create and delete. */
@Component({
  selector: 'app-projects-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './projects-page.html',
})
export class ProjectsPage implements OnInit {
  private readonly projectsService = inject(ProjectsService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly projects = signal<Project[]>([]);
  protected readonly errorMessage = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    this.projectsService.list(1, 50).subscribe({
      next: (page) => this.projects.set(page.items),
      error: () => this.errorMessage.set('Could not load your projects.'),
    });
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.projectsService.create({ name: this.form.getRawValue().name.trim() }).subscribe({
      next: (project) => {
        this.projects.update((projects) => [...projects, project]);
        this.form.reset();
      },
      error: (error: HttpErrorResponse) =>
        this.errorMessage.set(
          // A 400 is problem details with an `errors` object keyed by field name.
          error.status === 400
            ? (error.error?.errors?.name?.[0] ?? 'Check the name and try again.')
            : 'The project could not be created. Please try again later.',
        ),
    });
  }

  delete(project: Project): void {
    this.errorMessage.set(null);
    this.projectsService.delete(project.id).subscribe({
      next: () => this.projects.update((projects) => projects.filter((p) => p.id !== project.id)),
      error: () => this.errorMessage.set(`Could not delete ${project.name}.`),
    });
  }
}
```

**The template**, `projects-page.html`, follows `features/profile/profile-page.html`:
- a form with a labelled input
- an error tied to the input with `aria-describedby`
- `role="alert"` on the error message
- an `@for` list with an `@empty` row

Use no inline scripts or event-handler attributes: the content security policy blocks them, and `npm run csp:check` fails the build.

**The route**, in `frontend/src/app/app.routes.ts`:

```ts
{
  path: 'projects',
  canActivate: [authGuard],
  loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
},
```

For a page only some users may open, add `permissionGuard(Permission.X)` next to `authGuard`, as the `users` route does. Guards only decide what to show; the API enforces every rule itself.

**A link to it** on `features/home/home-page.html`: `<a routerLink="/projects">Projects</a>`, styled like its neighbours.

## 9. Tests

**Unit tests** cover domain rules and handler decisions, without a database. `backend/tests/AppTemplate.UnitTests/`, mirroring `Core/UserAggregate/` and `UseCases/Users/`:

```csharp
// Core/ProjectAggregate/ProjectTests.cs (excerpt)
[Theory]
[InlineData("owner-sub", true)]
[InlineData("someone-else", false)]
[InlineData(null, false)]
public void IsOwnedBy_ComparesTheSubject(string? caller, bool expected)
{
    var project = Project.Create("owner-sub", ProjectName.From("Website"));

    Assert.Equal(expected, project.IsOwnedBy(caller));
}
```

```csharp
// UseCases/Projects/RenameProjectHandlerTests.cs (excerpt)
private readonly IRepository<Project> _repository = Substitute.For<IRepository<Project>>();
private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
private readonly Project _project = Project.Create("owner-sub", ProjectName.From("Website"));

[Fact]
public async Task Handle_SomeoneElse_IsForbiddenAndChangesNothing()
{
    _currentUser.ExternalId.Returns("another-sub");

    var result = await Handle();

    Assert.Equal(ResultStatus.Forbidden, result.Status);
    Assert.Equal("Website", _project.Name.Value);
    await _repository
        .DidNotReceive()
        .UpdateAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
}
```

Cover each `Result` a handler can return: success, `NotFound`, `Forbidden`, and the permission that overrides ownership. In a test namespace such as `AppTemplate.UnitTests.UseCases.Projects`, add `using AppTemplate.UseCases.Projects;` rather than writing `UseCases.Projects.ProjectDto`, which resolves against the test namespace.

**Functional tests** send real HTTP requests through the whole API to a real Postgres, in `backend/tests/AppTemplate.FunctionalTests/ProjectFeatures/ProjectEndpointsTests.cs` (mirrors `UserFeatures/UserEndpointsTests.cs`):

```csharp
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class ProjectEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private static async Task<ProjectRecord> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/projects", new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectRecord>())!;
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithALocationThatResolves()
    {
        var client = factory.CreateAuthenticatedClient(NewSubject());

        var response = await client.PostAsJsonAsync("/projects", new { name = "Website" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProjectRecord>();
        var fetched = await client.GetFromJsonAsync<ProjectRecord>(response.Headers.Location);
        Assert.Equal(created!.Id, fetched!.Id);
    }

    [Fact]
    public async Task Get_SomeoneElsesProject_IsForbiddenWithoutProjectsRead()
    {
        var project = await CreateAsync(factory.CreateAuthenticatedClient(NewSubject()), "Mine");

        var stranger = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetAsync($"/projects/{project.Id}");
        var admin = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.ProjectsRead)
            .GetAsync($"/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }
}
```

**How the functional tests work:**
- `CreateAuthenticatedClient(sub, permissions...)` signs a request in as that subject, with those permissions. A fresh subject per test keeps tests independent, because they share one database per class.
- Cover, for every endpoint:
  - success, including the `Location` header for a create
  - invalid input (400, with the field in `errors`)
  - no session (401)
  - someone else's resource (403)
  - a missing id (404)
  - for lists: paging, and that callers see only their own rows

**Frontend specs** live next to their files: `projects.service.spec.ts` checks URLs, methods and bodies with `HttpTestingController`; `projects-page.spec.ts` checks the rendered list, creating a project, and showing a 400's field message. Mirror `users.service.spec.ts` and `users-page.spec.ts`.

Some tests need no changes:
- The **architecture tests** check that the new endpoints live under `Web.Features`, that the handlers live in UseCases, and that the names end in `Command` or `Query`.
- **`EndpointAuthorizationTests`** still passes, because none of the new routes is anonymous.

## 10. Finish

Run the [done-means checklist](https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/blob/main/AGENTS.md#13-done-means). From `backend/`:

```bash
dotnet csharpier format . && dotnet build AppTemplate.slnx && dotnet test AppTemplate.slnx
```

From `frontend/`:

```bash
npm run format && npm run lint && npm run test && npm run build && npm run csp:check
```

Then run the app (`dotnet run --project src/AppTemplate.AppHost`), sign in at `https://localhost:7100`, and use the new page. Commit as `feat: add projects`, on a `feature/` branch.

## 11. Later: removing the `User` reference

Once your own features exist, `User` is no longer needed as an example. What to do with it:
- **Keep** `GET /users/me` (`MeEndpoint`, `GetOrCreateCurrentUserHandler`) if you want a local row per signed-in person; the frontend's `CurrentUserService` uses it for the user's name and permissions.
- **Delete** what's only an example: the update, delete and list endpoints, their use cases and tests, `features/users/`, and the `users:*` permissions you don't need.
- **Keep** `ICurrentUser`, `Permission` and `KeycloakPermissionsClaimsTransformation`: they are the authorization mechanism, not the example.
- Removing the `users` table needs a migration. Removing `MeEndpoint` means pointing `CurrentUserService` elsewhere, for example at `/backend-for-frontend/user` for the name, plus an endpoint that returns the caller's permissions.
