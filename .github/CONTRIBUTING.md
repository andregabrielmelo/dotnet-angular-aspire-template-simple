# Contributing

This is a personal template repository - the primary way to "use" it is clicking **Use this template**, not contributing back to it. That said, fixes and improvements to the template itself are welcome.

## Reporting an issue

- Check whether it's actually a problem with the template (missing piece, broken build, outdated doc) rather than something specific to your project after you've renamed and extended it.
- Include enough to reproduce it: what you ran, what you expected, what happened instead.

## Branches and commits

- Branching follows [Gitflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow). Name work branches `feature/<name>` (from `develop`, merged back into `develop`), `release/<version>` or `hotfix/<name>`. Never commit directly to `main` or `develop`.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/), for example `feat(auth): add logout endpoint` or `fix(frontend): handle expired session`.

## Pull requests

- Keep PRs focused on one change - a bug fix, a doc update, a workflow tweak.
- Run `dotnet csharpier format .` (backend) and `npm run format` (frontend) before committing - CI checks formatting.
- If you're changing something architectural, consider whether it needs an [ADR](../docs/content/architecture-decisions/README.md).
- Follow the conventions in [AGENTS.md](../AGENTS.md). It is written for coding agents, but the rules (layer boundaries, endpoint and use case shape, tests) apply to every change.

## Branching: Gitflow

This repo follows [Gitflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow):

| Branch | Branched from | PR into |
|---|---|---|
| `feature/<short-kebab-name>` | `develop` | `develop` |
| `release/<version>` | `develop` | `main` (tagged), then back into `develop` |
| `hotfix/<short-name>` | `main` | `main` (tagged), then back into `develop` |

`main` holds released history only, and `develop` is the integration branch. Never commit to either directly. Dependabot PRs target `develop`.

## Commits: Conventional Commits

Every commit message follows [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/): `<type>[(scope)][!]: <description>`, for example `feat(frontend): add a settings page` or `ci: pin actions to commit SHAs`.

- Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
- The scope is optional and names the area touched: `backend`, `frontend`, `auth`, `apphost`, `ci`, and so on.
- Write the description in the imperative mood, with no trailing period. Mark breaking changes with `!` and a `BREAKING CHANGE:` footer.

The [PR Conventions](workflows/pr-conventions.yml) workflow checks both rules on every PR: the branch name against its target, and every non-merge commit subject. To fix a rejected commit message, reword it with `git rebase -i` and force-push.

A PR can merge into `main` or `develop` once four checks pass: `Gitflow branch`, `Conventional Commits`, `Frontend CI`, and `Backend CI`. The two build gates pass straight away when the PR doesn't touch their directory. See [Required checks](workflows/README.md#required-checks-and-the-gate-jobs).

Thanks for taking the time to improve it.
