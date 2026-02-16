#!/bin/bash
# Analyze git diff to determine deployment scope
# Usage: ./scripts/analyze-diff.sh [-j|--json] [-b|--base REF] [-h|--head REF]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
JSON=""
BASE=""
HEAD=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -j|--json)
            JSON="--json"
            shift
            ;;
        -b|--base)
            BASE="--base $2"
            shift 2
            ;;
        --head)
            HEAD="--head $2"
            shift 2
            ;;
        -h|--help)
            echo "Analyze git diff to determine deployment scope"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -j, --json       Output as JSON"
            echo "  -b, --base REF   Base git ref (default: from state or HEAD~1)"
            echo "  --head REF       Head git ref (default: HEAD)"
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
dotnet run --project cli/ClefLang.CLI.fsproj -- analyze-diff $BASE $HEAD $JSON
