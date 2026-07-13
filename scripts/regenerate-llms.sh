#!/usr/bin/env bash
# Regenerate llms.txt and llms-full.txt from the v2.3.0 source.
#
# This is a manual step. The two files in the repo are committed snapshots of
# the runtime surface; this script is a placeholder for the next release cycle
# that automates the regeneration.
#
# Strategy:
#   1. Print the assembly version from YitPush.csproj as a header.
#   2. Grep every public subcommand from Program.cs's ShowHelp() and the
#      ShowAzureDevOpsHelp() in AzureDevOps/AzureDevOpsCommand.cs.
#   3. Concatenate the SKILL.md (human-readable) into llms-full.txt.
#
# Run from the repo root:
#   ./scripts/regenerate-llms.sh
#
# Requirements: bash, rg (ripgrep), awk, sed.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

VERSION="$(grep -o '<Version>[^<]*' YitPush.csproj | sed 's/<Version>//')"

if [ -z "$VERSION" ]; then
  echo "Could not extract <Version> from YitPush.csproj" >&2
  exit 1
fi

echo "Regenerating llms.txt and llms-full.txt for v${VERSION}..."
echo "  repo root: ${REPO_ROOT}"

# Smoke-check the runtime surface has every subcommand we expect.
EXPECTED_SUBCOMMANDS=(
  "task delete"
  "task attach"
  "resolve-field"
  "refresh-fields"
  "pr list"
  "pr show"
  "pr comments"
  "pr reply"
  "pr create"
)

for needle in "${EXPECTED_SUBCOMMANDS[@]}"; do
  if ! rg -q --fixed-strings "$needle" SKILL.md; then
    echo "  WARN: SKILL.md is missing '${needle}' — fix that first" >&2
  fi
done

# Regenerate llms.txt with the v2.3.0 header line.
sed "s/^Version: 2\.[0-9.]*\$/Version: ${VERSION}/" llms.txt > llms.txt.tmp
mv llms.txt.tmp llms.txt
echo "  llms.txt      -> version stamp updated to v${VERSION}"

# Regenerate llms-full.txt with the v2.3.0 header line.
sed "s/^Version: 2\.[0-9.]* | /Version: ${VERSION} | /" llms-full.txt > llms-full.txt.tmp
mv llms-full.txt.tmp llms-full.txt
echo "  llms-full.txt -> version stamp updated to v${VERSION}"

echo
echo "Done. Review the diff, then commit llms.txt, llms-full.txt, and any"
echo "SKILL.md updates together with the release."
