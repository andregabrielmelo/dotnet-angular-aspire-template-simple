---
title: "ADR 009: One problem details contract for every error"
weight: 90
---

# ADR 009: One problem details contract for every error

## Status
Accepted.

## Context
Error responses came from five places, and each made its own choice:

| Source | Before |
|---|---|
| FastEndpoints request validation | FastEndpoints' `ErrorResponse` (`statusCode`, `message`, `errors`), not problem details |
| Use case `Invalid` results | ASP.NET Core's `ValidationProblem` |
| Other use case results (`ResultExtensions`) | A catch-all mapped `Conflict`, `Unavailable` and `Error` to **400**, and echoed `result.Errors`, which can describe internals |
| Authentication and authorization | Empty 401 and 403 bodies, from both the API and the backend for frontend |
| Unmatched routes and unhandled exceptions | An empty 404; FastEndpoints' own exception JSON |

Several endpoints, `MeEndpoint` among them, each had their own `switch`. A client (the Angular app, or a generated one) couldn't parse errors in one way, and the wrong status codes hid what had happened.

## Decision
Every error is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details response (`application/problem+json`) with `type`, `title`, `status`, optional `detail`, and a `traceId` extension. The `traceId` is the W3C trace id, which finds the request's logs and trace in the Aspire dashboard.

**Sources** (`Web/Configurations/ProblemDetailsConfigurations.cs`):
- `AddProblemDetails()` adds `traceId` to everything written through `IProblemDetailsService`.
- `UseExceptionHandler()` (outside Development) turns unhandled exceptions into a 500 with no exception details, and logs the exception.
- `UseStatusCodePages()` gives empty 4xx and 5xx responses a body: 401 and 403 from authorization, and 404 for unmatched routes.
- FastEndpoints' `Errors.ResponseBuilder` writes validation failures as an ASP.NET Core `HttpValidationProblemDetails`. Request validation and use case `Invalid` results therefore share one shape, an `errors` object of camelCase field name to messages:

  ```json
  { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.", "status": 400,
    "errors": { "id": ["Id must be greater than zero"] }, "traceId": "00-…" }
  ```

**Mapping use case results.** `Web/Extensions/ResultExtensions.cs` is the only place an `Ardalis.Result` becomes HTTP. Endpoints return `Results<Ok<T> | Created<T> | NoContent | Accepted, ProblemHttpResult>` through `ToOkResult`, `ToCreatedResult`, `ToNoContentResult` and `ToAcceptedResult`.

| Result | Status | `detail` |
|---|---|---|
| `Invalid` | 400 | `errors` by field |
| `Unauthorized` | 401 | none |
| `Forbidden` | 403 | generic |
| `NotFound` | 404 | the use case's message, if any |
| `Conflict` | 409 | the use case's message |
| `Unavailable` | 503 | generic |
| `Error`, `CriticalError`, anything else | 500 | generic, never `result.Errors` |

Messages from `Invalid`, `NotFound` and `Conflict` are written by use cases *for the caller*. The others may describe internals, so a use case logs those itself before returning them.

**Backend for frontend.** It calls `AddProblemDetails()`, `UseExceptionHandler()` and `UseStatusCodePages()` too, so its own 401s (missing `X-CSRF`, no session) and 403s use the same contract. Proxied responses already have a body, so they pass through unchanged.

## Consequences
- **Breaking for API clients:** validation errors no longer use FastEndpoints' `ErrorResponse`; 404s now have a body; 409, 503 and 500 replace former 400s.
- A new endpoint only picks a `To…Result` helper; it never builds an error response by hand. A new result status means one new row in `ToProblem`.
- `ProblemDetailsContractTests` (HTTP, against real Postgres), `ResultMappingTests` (every status, no Docker) and the backend for frontend's `ProblemDetailsTests` pin the contract. They check that the three middleware pieces never overwrite or empty each other's bodies, and that no exception text leaks.
- In Development, the developer exception page still shows exception details, on purpose.
