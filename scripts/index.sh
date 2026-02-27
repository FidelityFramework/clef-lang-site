#!/bin/bash
# Index content into D1 FTS5 + Vectorize for search
# Usage: ./scripts/index.sh [-v|--verbose] [-l|--local] [-p|--port PORT] [-d|--content-dir PATH]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
VERBOSE=""
LOCAL=""
PORT=""
CONTENT_DIR=""
FORCE=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -f|--force)
            FORCE="--force"
            shift
            ;;
        -v|--verbose)
            VERBOSE="--verbose"
            shift
            ;;
        -l|--local)
            LOCAL="--local"
            shift
            ;;
        -p|--port)
            PORT="--port $2"
            shift 2
            ;;
        -d|--content-dir)
            CONTENT_DIR="--content-dir $2"
            shift 2
            ;;
        -h|--help)
            echo "Index content into D1 FTS5 + Vectorize for search"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -f, --force             Purge existing index before re-indexing"
            echo "  -v, --verbose           Enable verbose output"
            echo "  -l, --local             Use local search worker (localhost:8787)"
            echo "  -p, --port PORT         Local worker port (default: 8787, requires --local)"
            echo "  -d, --content-dir PATH  Hugo content directory (default: ./hugo/content)"
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
dotnet run --project cli/ClefLang.CLI.fsproj -- index $CONTENT_DIR $FORCE $LOCAL $PORT $VERBOSE
