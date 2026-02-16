#!/bin/bash
# Deploy based on git diff analysis
# Usage: ./scripts/smart-deploy.sh [-v|--verbose] [-f|--force] [-b|--base REF]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
VERBOSE=""
FORCE=""
BASE=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--verbose)
            VERBOSE="--verbose"
            shift
            ;;
        -f|--force)
            FORCE="--force"
            shift
            ;;
        -b|--base)
            BASE="--base $2"
            shift 2
            ;;
        -h|--help)
            echo "Deploy based on git diff analysis"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -v, --verbose    Enable verbose output"
            echo "  -f, --force      Force full deployment regardless of changes"
            echo "  -b, --base REF   Base git ref for comparison (default: last deployed commit)"
            echo "  -h, --help       Show this help message"
            echo ""
            echo "Analyzes changes since the last deployment and deploys only what changed:"
            echo "  - Content changes     → Pages + R2 sync + search index"
            echo "  - Worker changes      → Rebuild & deploy affected workers + Pages"
            echo "  - Hugo/theme changes  → Pages only"
            echo "  - --force             → Full migration (provision + all workers + sync + index + pages)"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

# --- Environment checks ---
missing=""
command -v dotnet >/dev/null 2>&1 || missing="$missing dotnet"
command -v node >/dev/null 2>&1   || missing="$missing node"
command -v npm >/dev/null 2>&1    || missing="$missing npm"
command -v git >/dev/null 2>&1    || missing="$missing git"

if [[ -n "$missing" ]]; then
    echo "Error: Missing required tools:$missing"
    echo "Install them before deploying."
    exit 1
fi

# Warn if no deployment state (first deploy should use provision + deploy directly)
if [[ ! -f ".clef-deploy-state.json" ]]; then
    if [[ -z "$FORCE" ]]; then
        echo "Warning: No deployment state found."
        echo "  For first-time setup, run:  ./scripts/provision.sh && ./scripts/deploy.sh -n all"
        echo "  Or use --force for a full migration:  ./scripts/smart-deploy.sh --force"
        exit 1
    fi
fi

# Ensure we're in a git repo
if ! git rev-parse --git-dir >/dev/null 2>&1; then
    echo "Error: Not a git repository. smart-deploy requires git history for diff analysis."
    exit 1
fi

dotnet run --project cli/ClefLang.CLI.fsproj -- smart-deploy $BASE $FORCE $VERBOSE
