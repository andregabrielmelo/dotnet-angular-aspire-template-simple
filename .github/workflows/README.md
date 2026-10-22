# GitHub Actions Workflows

## `backend-build.yml`

Restores (`--locked-mode`, against the committed `packages.lock.json` files), format-checks (`dotnet csharpier check`), builds, and tests (`dotnet test`) the backend solution on Windows, Linux, and macOS. On Linux it also re-exports the OpenAPI document (`scripts/export-openapi.sh`) and fails if `backend/openapi/v1.json` is stale. NuGet packages are cached by `setup-dotnet`, keyed on the lock files' hash. Triggers on pushes to `main`/`develop` that touch `backend/**` and on every PR. On a PR, the build only runs if it touches `backend/**`, but the `Backend CI` gate check always reports (see [Required checks](#required-checks-and-the-gate-jobs)). `AppTemplate.UnitTests` runs against Core/UseCases with no external dependencies; `AppTemplate.FunctionalTests` exercises the full HTTP -> FastEndpoints -> Mediator -> EF Core pipeline against a real Postgres started with [Testcontainers](https://dotnet.testcontainers.org/). Hosted Windows and macOS runners can't run Linux containers, so only the Linux job runs the whole suite; the others skip tests marked `[Trait("Category", "RequiresDocker")]` (`--filter "Category!=RequiresDocker"`).

## `frontend-build.yml`

Installs, regenerates the API types from `backend/openapi/v1.json` and fails if `src/app/core/api/api-types.ts` is stale (so it also runs when only `backend/openapi/` changed), format-checks (`npm run format:check`, prettier), lints (`eslint`, via `@angular-eslint`), tests (`vitest`, via `npm run test`), builds the Angular app, and checks the built `index.html` has no inline script or event handler the backend for frontend's Content-Security-Policy would block (`npm run csp:check`). Triggers on pushes to `main`/`develop` that touch `frontend/**` and on every PR. On a PR, the build only runs if it touches `frontend/**`, but the `Frontend CI` gate check always reports (see [Required checks](#required-checks-and-the-gate-jobs)).

## `pr-conventions.yml`

Runs on every PR and enforces the [Git workflow rules](../CONTRIBUTING.md#branching-gitflow) in plain shell (no third-party actions):
- **Gitflow branch:** `feature/*` may only target `develop`. Only `release/*` and `hotfix/*` may target `main`.
- **Conventional Commits:** every non-merge commit in the PR must match `<type>[(scope)][!]: <description>`.

## `secret-scan.yml`

Runs [gitleaks](https://github.com/gitleaks/gitleaks) over the **whole git history** on every PR and on pushes to `main`/`develop`, and fails on anything that looks like a credential: cloud keys, private keys, tokens, connection strings with passwords. It scans every commit, not only the PR's, because a secret removed in a later commit is still leaked.
- **CLI, not `gitleaks-action`.** It downloads the gitleaks release archive pinned by version and SHA-256 (`GITLEAKS_VERSION`/`GITLEAKS_SHA256`, from the release's `checksums.txt`). `gitleaks-action` needs a paid license for organization-owned repositories, which a project created from this template may well be. Dependabot can't bump a downloaded binary, so update both values together by hand.
- **No allowlist.** The repository's history has no findings: the dev-only Keycloak and Postgres values are AppHost parameters or user secrets, never committed. If a finding is a genuine false positive, add its fingerprint (printed in the log) to a `.gitleaksignore` file. Never exempt whole paths or file types.
- **If it catches a real secret, rotate it first.** Removing it from the branch doesn't un-leak it. Then rewrite the branch's history before it merges.
- Run it locally with `gitleaks git .` ([install](https://github.com/gitleaks/gitleaks#installing)).

The job is named `Secret scan` and has no path filter, so it always reports and is safe to require. It is not yet in the `main`/`develop` ruleset; add it there to block merges on findings.

## `hugo-docs.yml`

Builds the `docs/` Hugo site (using the `hugo-book` theme, fetched fresh each run) and deploys it to GitHub Pages on pushes to `main` that touch `docs/**`. Requires Pages to be enabled for the repo with the source set to "GitHub Actions" (Settings → Pages).

## Conventions shared by every workflow

- **Triggers:** build workflows run on `push` to the two long-lived [gitflow](../CONTRIBUTING.md#branching-gitflow) branches (`main`, `develop`) and on every `pull_request`, not on pushes to every branch. That way a `feature/*` branch with an open PR runs once, not twice. Use `workflow_dispatch` (the "Run workflow" button) to build a branch that has no PR yet.
- **Concurrency:** build workflows cancel an in-progress run when a newer commit lands on the same PR. Runs on `main` and `develop` are never cancelled, so their status history stays complete.
- **Least-privilege token:** every workflow declares `permissions:` explicitly. Build workflows only get `contents: read`.
- **Pinned actions:** every `uses:` points at a full commit SHA with the version as a trailing comment (e.g. `actions/checkout@<sha> # v7.0.1`). Tags are mutable and SHAs aren't. Dependabot (`.github/dependabot.yml`, `github-actions` ecosystem, grouped into one PR against `develop`) keeps both the SHA and the comment current, so don't hand-edit one without the other.
- **`persist-credentials: false`** on every checkout, since no workflow pushes back to the repo. Without it, the token stays in `.git/config` for later steps.
- **`timeout-minutes`** on every job, so a hung job doesn't burn the 6-hour default.

## Required checks and the gate jobs

The `main` and `develop` ruleset requires exactly four checks: `Frontend CI`, `Backend CI`, `Gitflow branch`, and `Conventional Commits`. Don't add the matrix build jobs (`Build & Test (Node 22.x)`, `Build (.NET 10, ubuntu-latest)`, ...) to the ruleset, for two reasons:

- **Path filters and required checks don't mix.** A workflow skipped by an `on.pull_request.paths` filter never reports a status. A required check from it stays at "Expected — Waiting for status to be reported" forever, so every backend-only PR would be blocked on the frontend check, and vice versa.
- **Skipped matrix jobs report the wrong name.** When a matrix job is skipped by `if:`, GitHub reports it under its unexpanded name (`Build & Test (Node ${{ matrix.node-version }})`), which never matches the required name.

Each build workflow therefore has three jobs:

1. **`changes`** (`Detect changes`) lists the PR's files through the API and outputs whether anything relevant changed. On `push` (already path-filtered) and `workflow_dispatch` it always says yes.
2. **`build`** is the real (matrix) build. It runs only when `changes` says so.
3. **`gate`** (`Frontend CI` / `Backend CI`) always runs. It passes when the build succeeded or was skipped, and fails when the build failed or was cancelled. This is the one check the ruleset requires.

A new build workflow that should gate PRs must follow the same pattern and add its gate job's name to the ruleset. Changing a matrix (adding Node 24, another OS) needs no ruleset change.

## NuGet lock files

`backend/Directory.Build.props` turns on `RestorePackagesWithLockFile`, so each project has a committed `packages.lock.json`. After changing a package version in `Directory.Packages.props`, run `dotnet restore` and commit the updated lock files. Otherwise CI's `--locked-mode` restore fails. `AppTemplate.AppHost` opts out because the Aspire SDK pulls OS-specific packages (`*.linux-x64`, `*.win-x64`, ...), so its lock file would only ever match the OS that generated it.

## Adding a status badge

```markdown
![Backend Build](https://github.com/<owner>/<repo>/actions/workflows/backend-build.yml/badge.svg)
![Frontend Build](https://github.com/<owner>/<repo>/actions/workflows/frontend-build.yml/badge.svg)
```
