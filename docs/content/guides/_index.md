---
title: "Guides"
weight: 15
---

# Guides

How to work in this codebase, step by step. They're written for people and coding agents alike.

- **[How the project fits together]({{< relref "project-map" >}})**: read it first. Where everything lives, what happens to a request on its way through, the vocabulary (endpoint, command, query, handler, specification, query service...) and where to look when you need to change something.
- **[Adding a feature, step by step]({{< relref "adding-a-feature" >}})**: builds a complete `Project` feature, from the domain model to the Angular page and the tests. Follow it whenever you add something that has its own data.
- **[Common tasks]({{< relref "cookbook" >}})**: short recipes. Add a permission, make an endpoint public, send an email, react to a domain event, add a setting, filter a list, and more.

The guides show *how*. The rules they follow live in [`AGENTS.md`](https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/blob/main/AGENTS.md), and the reasons behind them in [Goals & Design Decisions]({{< relref "design-decisions" >}}) and the [Architecture Decisions]({{< relref "architecture-decisions" >}}). When a guide and a rule disagree, the rule wins, and the guide is the one to fix.
