#!/usr/bin/env bash
# Renames every "AppTemplate" (and "apptemplate") occurrence in this repo to your
# project's name - project/namespace names, file names, folder names, the Postgres
# database name, and the Keycloak realm/client names.
#
# Usage: scripts/rename-template.sh YourProjectName
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "Usage: $0 YourProjectName" >&2
  exit 1
fi

NEW_PASCAL="$1"
if ! [[ "$NEW_PASCAL" =~ ^[A-Za-z][A-Za-z0-9]*$ ]]; then
  echo "Project name must be a single PascalCase word (letters/digits, no spaces, dots or hyphens)." >&2
  exit 1
fi
NEW_LOWER=$(echo "$NEW_PASCAL" | tr '[:upper:]' '[:lower:]')

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "Renaming AppTemplate -> $NEW_PASCAL (and apptemplate -> $NEW_LOWER)..."

# 1. Replace file contents
grep -rIl "AppTemplate\|apptemplate" . --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj --exclude-dir=.angular 2>/dev/null | while read -r f; do
  sed -i.bak "s/AppTemplate/${NEW_PASCAL}/g; s/apptemplate/${NEW_LOWER}/g" "$f"
  rm -f "$f.bak"
done

# 2. Rename directories (deepest first)
find . -depth \( -iname '*apptemplate*' \) -type d \
  -not -path '*/node_modules/*' -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' | while read -r d; do
  newd=$(echo "$d" | sed "s/AppTemplate/${NEW_PASCAL}/g; s/apptemplate/${NEW_LOWER}/g")
  [ "$d" != "$newd" ] && mv "$d" "$newd"
done

# 3. Rename files
find . -iname '*apptemplate*' -type f \
  -not -path '*/node_modules/*' -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' | while read -r f; do
  newf=$(echo "$f" | sed "s/AppTemplate/${NEW_PASCAL}/g; s/apptemplate/${NEW_LOWER}/g")
  [ "$f" != "$newf" ] && mv "$f" "$newf"
done

echo "Done. Review the diff, then:"
echo "  cd backend && dotnet build ${NEW_PASCAL}.slnx"
echo "  cd frontend && npm install"
