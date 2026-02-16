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
            echo "  -b, --base REF   Base git ref for comparison"
            echo "  -h, --help       Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"
dotnet run --project cli/ClefLang.CLI.fsproj -- smart-deploy $BASE $FORCE $VERBOSE
