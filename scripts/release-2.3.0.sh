#!/usr/bin/env bash
# release-2.3.0.sh — finalise the v2.3.0 release of yp (YitPush).
#
# What this script does (in order):
#   1. Pre-flight: verify the repo is ready to ship v2.3.0 (csproj, CHANGELOG,
#      llms.txt, llms-full.txt, SKILL.md, skills/yp/SKILL.md).
#   2. dotnet pack -c Release     → writes ./nupkg/YitPush.2.3.0.nupkg
#   3. dotnet nuget push ...     → publishes to nuget.org
#   4. git tag -a v2.3.0 ...     → annotated tag at HEAD
#   5. git push origin v2.3.0    → tag goes to the remote
#   6. npx skills add ...        → re-publishes the yp agent skill (non-fatal)
#
# Pre-requisites:
#   - dotnet SDK 10
#   - git configured with push access to origin
#   - NUGET_API_KEY in the environment (only needed for the first push of a
#     given package+version; subsequent pushes with the same key work via
#     --skip-duplicate)
#   - npx available (only used for the final non-fatal step)
#
# Usage:
#   ./scripts/release-2.3.0.sh           # full release
#   ./scripts/release-2.3.0.sh --dry-run # pre-flight only, no mutations
#
# The testable design lives in YitPush.Commands.ReleasePlan (see
# tests/YitPush.Tests/ReleasePlanTests.cs). The steps below are intentionally
# in lock-step with the assertions in those tests — if you change a command
# here, update the tests; if you change a test, update this script.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

DRY_RUN=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=1 ;;
    -h|--help)
      sed -n '2,30p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown argument: $arg" >&2
      echo "Usage: $0 [--dry-run]" >&2
      exit 64
      ;;
  esac
done

VERSION="${VERSION:-$(grep -o '<Version>[^<]*' YitPush.csproj | sed 's/<Version>//')}"
if [ -z "$VERSION" ]; then
  echo "FAIL: could not extract <Version> from YitPush.csproj" >&2
  exit 1
fi

NUPKG_FILE="YitPush.${VERSION}.nupkg"
GIT_TAG="v${VERSION}"

# ─── Pre-flight ───────────────────────────────────────────────────────────────
echo "Pre-flight for yp v${VERSION}..."

FAIL=0

check_file_contains() {
  local label="$1" file="$2" needle="$3"
  if [ ! -f "$file" ]; then
    echo "  FAIL [${label}]: ${file} does not exist" >&2
    FAIL=1
    return
  fi
  if ! grep -qF -- "$needle" "$file"; then
    echo "  FAIL [${label}]: ${file} is missing '${needle}'" >&2
    FAIL=1
    return
  fi
  echo "  OK   [${label}]: ${file} mentions '${needle}'"
}

check_file_contains "CHANGELOG"      CHANGELOG.md     "## [${VERSION}]"
check_file_contains "LLMS_TXT"       llms.txt         "${VERSION}"
check_file_contains "LLMS_FULL_TXT"  llms-full.txt    "${VERSION}"
check_file_contains "SKILL_ROOT"     SKILL.md         "${VERSION}"
check_file_contains "SKILL_INSTALL"  skills/yp/SKILL.md "${VERSION}"

if ! diff -q SKILL.md skills/yp/SKILL.md >/dev/null 2>&1; then
  echo "  FAIL [SKILL_DRIFT]: SKILL.md and skills/yp/SKILL.md have drifted." >&2
  FAIL=1
else
  echo "  OK   [SKILL_DRIFT]: SKILL.md and skills/yp/SKILL.md are byte-identical."
fi

if [ -z "${NUGET_API_KEY:-}" ]; then
  echo "  FAIL [NUGET_API_KEY]: NUGET_API_KEY is not set in the environment. Add 'export NUGET_API_KEY=...' to ~/.bashrc and retry." >&2
  FAIL=1
else
  echo "  OK   [NUGET_API_KEY]: present (prefix ${NUGET_API_KEY:0:4}...)"
fi

if [ "$FAIL" -ne 0 ]; then
  echo "" >&2
  echo "Pre-flight failed for v${VERSION}. Fix the issues above and retry." >&2
  exit 2
fi

echo "Pre-flight OK — ready to ship v${VERSION}."
echo ""

if [ "$DRY_RUN" -eq 1 ]; then
  echo "Dry-run: skipping all 5 release steps."
  echo "  would: clean nupkg/*.nupkg and nupkg/*.symbols.nupkg"
  echo "  would: dotnet pack -c Release -o ./nupkg"
  echo "  would: dotnet nuget push \"./nupkg/YitPush.${VERSION}.nupkg\" --api-key \"\$NUGET_API_KEY\" --skip-duplicate --source https://api.nuget.org/v3/index.json"
  echo "  would: git tag -a ${GIT_TAG} -m \"Release ${VERSION}\"   (idempotent: skipped if tag already exists)"
  echo "  would: git push origin ${GIT_TAG}"
  echo "  would: npx skills add elvisbrevi/yitpush --skill yp --all -y   (non-fatal)"
  exit 0
fi

# Aggressive pre-build cleanup: remove any stale .nupkg / .symbols.nupkg files
# in nupkg/ before pack. Without this, `dotnet nuget push ./nupkg/YitPush.<v>.nupkg`
# could glob the directory and re-push artifacts from old versions (which then
# 409-conflict but skip the current version). The `nupkg/` dir is not git-tracked
# (build output), so `rm -f` is safe. Only runs on a real (non-dry-run) release.
if [ -d nupkg ]; then
  shopt -s nullglob
  STALE_ARTIFACTS=(nupkg/*.nupkg nupkg/*.symbols.nupkg)
  shopt -u nullglob
  if [ "${#STALE_ARTIFACTS[@]}" -gt 0 ]; then
    echo "Pre-build cleanup: removing ${#STALE_ARTIFACTS[@]} stale artifact(s) from nupkg/:"
    printf '         %s\n' "${STALE_ARTIFACTS[@]}"
    rm -f "${STALE_ARTIFACTS[@]}"
  fi
fi

# ─── 1. Pack ──────────────────────────────────────────────────────────────────
echo "[1/5] Building ${NUPKG_FILE}..."
dotnet pack -c Release -o ./nupkg

# ─── 2. NuGet push ────────────────────────────────────────────────────────────
echo "[2/5] Pushing ${NUPKG_FILE} to nuget.org..."
dotnet nuget push "./nupkg/YitPush.${VERSION}.nupkg" \
  --api-key "$NUGET_API_KEY" \
  --skip-duplicate \
  --source https://api.nuget.org/v3/index.json

# ─── 3. Git tag (local) — idempotent ─────────────────────────────────────────
echo "[3/5] Creating annotated git tag ${GIT_TAG}..."
if git rev-parse "${GIT_TAG}" >/dev/null 2>&1; then
  echo "  tag ${GIT_TAG} already exists locally, skipping (idempotent)"
else
  git tag -a "${GIT_TAG}" -m "Release ${VERSION}"
fi

# ─── 4. Git push tag ──────────────────────────────────────────────────────────
echo "[4/5] Pushing tag ${GIT_TAG} to origin..."
git push origin "${GIT_TAG}"

# ─── 5. Re-publish the yp agent skill (non-fatal) ─────────────────────────────
echo "[5/5] Re-publishing the yp agent skill on skills.sh..."
# Non-fatal: a transient skills.sh outage must not block the NuGet release.
if ! npx skills add elvisbrevi/yitpush --skill yp --all -y; then
  echo "WARN: skill publish failed (non-fatal). Continuing with the rest of the release." >&2
fi

echo ""
echo "✔ yp v${VERSION} released."
echo "  - NuGet:   https://www.nuget.org/packages/YitPush/${VERSION}"
echo "  - GitHub:  https://github.com/elvisbrevi/yitpush/releases/tag/${GIT_TAG}"
echo "  - Skill:   https://skills.sh/elvisbrevi/yitpush"
