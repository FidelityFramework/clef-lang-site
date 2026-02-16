#!/bin/bash
# Show deployment status
# Usage: ./scripts/status.sh [-j|--json]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
JSON=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -j|--json)
            JSON="--json"
            shift
            ;;
        -h|--help)
            echo "Show deployment status"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -j, --json    Output status as JSON"
            echo "  -h, --help    Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"
dotnet run --project cli/ClefLang.CLI.fsproj -- status $JSON
