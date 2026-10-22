#!/usr/bin/env bash
# Writes the Web API's OpenAPI document to backend/openapi/v1.json - the committed contract the
# Angular types are generated from (frontend: npm run api:generate). Run it after changing an
# endpoint, request or response; CI fails when the committed file is stale.
#
# Usage: scripts/export-openapi.sh [--no-build]
#
# The API starts briefly on a loopback port to build the document (see OpenApiExport.cs). It
# never connects to Postgres, Redis or Keycloak, so the values below only satisfy startup
# validation and must never be real credentials.
set -euo pipefail

cd "$(dirname "$0")/../backend"

dotnet run --project src/AppTemplate.Web --configuration Release --no-launch-profile "$@" -- \
  --OpenApi:ExportPath=../../openapi/v1.json \
  --urls=http://127.0.0.1:0 \
  "--ConnectionStrings:apptemplate=Host=openapi-export-unused"
