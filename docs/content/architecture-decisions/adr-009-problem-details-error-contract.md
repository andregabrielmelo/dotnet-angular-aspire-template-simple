---
title: "ADR 009: Problem details for use case results"
weight: 90
---

# ADR 009: Problem details for use case results

## Status
Accepted. Revised: the simple template keeps problem details for use case results only. Request validation and the framework's own errors (401, 403, 404, 500) use ASP.NET Core's and FastEndpoints' defaults, without extra configuration, and there is no `traceId` extension.

## Context
Error responses came from five places, and each made its own choice:

| Source | Before |
|---|---|
| FastEndpoints request validation | FastEndpoints' `ErrorResponse` (`statusCode`, `message`, `errors`), not problem details |
| Use case `Invalid` results | ASP.NET Core's `ValidationProblem` |
| Other use case results (`ResultExtensions`) | A catch-all mapped `Conflict`, `Unavailable` and `Error` to **400**, and echoed `result.Errors`, which can describe internals |
| Authentication and authorization | Empty 401 and 403 bodies |
| Unmatched routes and unhandled exceptions | An empty 404; FastEndpoints' own exception JSON |

Several endpoints, `MeEndpoint` among them, each had their own `switch`. A client (the Angular app, or a generated one) couldn't parse errors in one way, and the wrong status codes hid what had happened.

## Decision
Use case results are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details responses (`application/problem+json`) with `type`, `title`, `status` and an optional `detail`. Everything else uses the framework's defaults, configured nowhere:

| Source | Response |
|---|---|
| Use case results (`ResultExtensions`) | Problem details, by the table below |
| FastEndpoints request validation, and `AddError` in the refresh endpoint | FastEndpoints' `ErrorResponse`: `statusCode`, `message`, and `errors`, camelCase field name to messages |
| No or an invalid access token, a missing permission | An empty 401 or 403 |
| A revoked access token (`RevokedTokenChecker`, [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}})) | A 401 problem details |
| Unmatched routes | An empty 404, or 401 for an anonymous caller (the fallback policy runs first) |
| Unhandled exceptions, outside Development | FastEndpoints' `UseDefaultExceptionHandler(useGenericReason: true)`: a 500 JSON body without the exception's message; the exception is logged |

Both shapes a form cares about carry the same `errors` object, so a client shows field messages the same way for request validation and a use case's `Invalid` result:

```json
{ "statusCode": 400, "message": "One or more errors occurred!",
  "errors": { "email": ["Email is required"] } }
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

## Consequences
- A new endpoint only picks a `To…Result` helper; it never builds an error response by hand. A new result status means one new row in `ToProblem`.
- Clients read `errors` for field messages and the status code for everything else; they don't rely on a body for 401, 403 or 404.
- `ResultExtensionsToProblem` (functional tests, no Docker) pins the mapping, and the endpoint tests pin each endpoint's status codes.
- No `traceId` in error bodies. Find a failing request in the Aspire dashboard by its route and time, or add `AddProblemDetails()` and FastEndpoints' `Errors.ResponseBuilder` (as the full template does) when clients need one error shape with a trace id.
- In Development, the developer exception page still shows exception details, on purpose.
