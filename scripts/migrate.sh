#!/bin/bash
# Full migration: provision -> deploy workers -> sync -> index -> deploy pages
# Usage: ./scripts/migrate.sh [-v|--verbose] [--skip-provision] [--skip-sync] [--skip-index] [--skip-deploy]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
VERBOSE=""
SKIP_PROVISION=""
SKIP_SYNC=""
SKIP_INDEX=""
SKIP_DEPLOY=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--verbose)
            VERBOSE="--verbose"
            shift
            ;;
        --skip-provision)
            SKIP_PROVISION="--skip-provision"
            shift
            ;;
        --skip-sync)
            SKIP_SYNC="--skip-sync"
            shift
            ;;
        --skip-index)
            SKIP_INDEX="--skip-index"
            shift
            ;;
        --skip-deploy)
            SKIP_DEPLOY="--skip-deploy"
            shift
            ;;
        -h|--help)
            echo "Full migration: provision -> deploy workers -> sync -> index -> deploy pages"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -v, --verbose      Enable verbose output"
            echo "  --skip-provision   Skip resource provisioning"
            echo "  --skip-sync        Skip content sync"
            echo "  --skip-index       Skip search indexing"
            echo "  --skip-deploy      Skip worker deployment"
            echo "  -h, --help         Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"
dotnet run --project cli/ClefLang.CLI.fsproj -- migrate $SKIP_PROVISION $SKIP_SYNC $SKIP_INDEX $SKIP_DEPLOY $VERBOSE
