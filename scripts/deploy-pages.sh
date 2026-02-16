#!/bin/bash
# Deploy Hugo site to Cloudflare Pages
# Usage: ./scripts/deploy-pages.sh [-v|--verbose] [-d|--hugo-dir PATH] [-n|--project-name NAME]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
VERBOSE=""
HUGO_DIR=""
PROJECT_NAME=""
REFRESH_SPEC=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--verbose)
            VERBOSE="--verbose"
            shift
            ;;
        -d|--hugo-dir)
            HUGO_DIR="--hugo-dir $2"
            shift 2
            ;;
        -n|--project-name)
            PROJECT_NAME="--project-name $2"
            shift 2
            ;;
        -r|--refresh-spec)
            REFRESH_SPEC="--refresh-spec"
            shift
            ;;
        -h|--help)
            echo "Deploy Hugo site to Cloudflare Pages"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -v, --verbose         Enable verbose output"
            echo "  -d, --hugo-dir PATH   Hugo site directory (default: ./hugo)"
            echo "  -n, --project-name    Pages project name (default: clef-lang)"
            echo "  -r, --refresh-spec    Pull latest spec from clef-lang-spec before building"
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
dotnet run --project cli/ClefLang.CLI.fsproj -- deploy-pages $HUGO_DIR $PROJECT_NAME $REFRESH_SPEC $VERBOSE
