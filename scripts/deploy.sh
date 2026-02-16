#!/bin/bash
# Deploy Ask AI worker
# Usage: ./scripts/deploy.sh [-v|--verbose] [-s|--skip-build] [-f|--force] [-w|--worker-dir PATH]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
VERBOSE=""
SKIP_BUILD=""
FORCE=""
WORKER_DIR=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
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
            echo "Deploy Ask AI worker"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -v, --verbose         Enable verbose output"
            echo "  -s, --skip-build      Skip worker build step"
            echo "  -f, --force           Force deployment even if source unchanged"
            echo "  -w, --worker-dir PATH Worker source directory (default: ./workers/ask-ai)"
            echo "  -h, --help            Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"
dotnet run --project cli/ClefLang.CLI.fsproj -- deploy $WORKER_DIR $SKIP_BUILD $FORCE $VERBOSE
