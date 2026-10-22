# Frontend

The Angular app. Normally you run the whole stack via the Aspire AppHost (see the [root README](../README.md)), which starts this alongside the API and database - you shouldn't need to run these commands directly outside of quick iteration.

```bash
npm ci
npm start      # ng serve - open the app through the backend for frontend, not this port
npm run build  # production build, output in dist/
npm run test   # vitest
npm run lint   # eslint (@angular-eslint)
```

Formatting (prettier) and linting (eslint) are both enforced in CI - run `npm run format` and `npm run lint` before committing.

See the [docs site](https://andregabrielmelo.github.io/dotnet-angular-aspire-template-simple/notes/cors-and-proxy/) for the single-origin (backend for frontend) setup and the rest of the template's architecture.
