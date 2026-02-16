#!/bin/bash
# Deploy workers (smart-search, search, content-sync, or all)
# Usage: ./scripts/deploy.sh [-n|--worker-name NAME] [-v|--verbose] [-s|--skip-build] [-f|--force] [-w|--worker-dir PATH]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
VERBOSE=""
SKIP_BUILD=""
FORCE=""
WORKER_DIR=""
WORKER_NAME=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -n|--worker-name)
            WORKER_NAME="--worker-name $2"
            shift 2
            ;;
        -v|--verbose)
            VERBOSE="--verbose"
            shift
            ;;
        -s|--skip-build)
            SKIP_BUILD="--skip-build"
            shift
            ;;
        -f|--force)
            FORCE="--force"
            shift
            ;;
        -w|--worker-dir)
            WORKER_DIR="--worker-dir $2"
            shift 2
            ;;
        -h|--help)
            echo "Deploy workers (smart-search, search, content-sync, or all)"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -n, --worker-name NAME  Worker to deploy: smart-search, search, content-sync, all (default: smart-search)"
            echo "  -v, --verbose           Enable verbose output"
            echo "  -s, --skip-build        Skip worker build step"
            echo "  -f, --force             Force deployment even if source unchanged"
            echo "  -w, --worker-dir PATH   Worker source directory (overrides default for worker)"
            echo "  -h, --help              Show this help message"
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

if [[ -n "$missing" ]]; then
    echo "Error: Missing required tools:$missing"
    echo "Install them before deploying workers."
    exit 1
fi

# Ensure deployment state exists (provision must run first)
if [[ ! -f ".clef-deploy-state.json" ]]; then
    echo "Error: No deployment state found. Run './scripts/provision.sh' first."
    exit 1
fi

dotnet run --project cli/ClefLang.CLI.fsproj -- deploy $WORKER_NAME $WORKER_DIR $SKIP_BUILD $FORCE $VERBOSE
