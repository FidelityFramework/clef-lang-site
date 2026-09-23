#!/bin/bash
# Update Atlas only. All options and reconciliation logic belong to the F# CLI.
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/.."
exec dotnet run --project cli/ClefLang.CLI.fsproj -- graph "$@"
