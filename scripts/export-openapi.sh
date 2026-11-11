#!/usr/bin/env bash
# Writes the Web API's OpenAPI document to backend/openapi/v1.json - the committed contract the
# Angular types are generated from (frontend: npm run api:generate). Run it after changing an
# endpoint, request or response; CI fails when the committed file is stale.
#
# Usage: scripts/export-openapi.sh [--no-build]
#
# The API starts briefly on a loopback port to build the document, using FastEndpoints'
# ExportSwaggerDocsAndExitAsync (Program.cs; the output folder is SwaggerExportPath in the Web
# csproj). It never connects to Postgres or signs a token, so the values below only satisfy
# startup validation and must never be real credentials.
set -euo pipefail

cd "$(dirname "$0")/../backend"

dotnet run --project src/AppTemplate.Web --configuration Release --no-launch-profile "$@" -- \
  --export-swagger-docs true \
  --urls=http://127.0.0.1:0 \
  "--ConnectionStrings:apptemplate=Host=openapi-export-unused" \
  --Mailserver:FromAddress=openapi-export@example.invalid \
  --AccountLinks:ClientUrl=https://openapi-export.invalid

# FastEndpoints writes the document without a final newline; keep the committed file stable.
[ -n "$(tail -c1 openapi/v1.json)" ] && echo >> openapi/v1.json
