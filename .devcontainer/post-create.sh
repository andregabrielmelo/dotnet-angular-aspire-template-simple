#!/usr/bin/env bash
# Runs once, when the dev container is created (devcontainer.json, postCreateCommand).
set -euo pipefail
cd "$(dirname "$0")/.."

echo "dotnet $(dotnet --version), node $(node --version)"

# Every request is HTTPS, development included (ADR 016). Exit code 4 means only some stores took
# the certificate: OpenSSL does (through SSL_CERT_DIR, set in devcontainer.json), and there's no
# browser store in the container. Anything else is a failure.
dotnet dev-certs https --trust || [ $? -eq 4 ]

# The certificate's public part, for the browser on your machine to trust (Getting Started).
# It holds no private key; .gitignore keeps it out of the repository anyway.
cp "$HOME"/.aspnet/dev-certs/trust/aspnetcore-localhost-*.pem .devcontainer/dev-cert.crt

(cd backend && dotnet tool restore && dotnet restore AppTemplate.slnx)
(cd frontend && npm ci)

echo "Ready: cd backend && dotnet run --project src/AppTemplate.AppHost"
