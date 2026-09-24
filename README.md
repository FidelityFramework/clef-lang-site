# clef-lang-site

Source for [clef-lang.com](https://clef-lang.com), the official site for the Clef programming language.

Clef is a concurrent systems language targeting CPU, GPU, NPU, FPGA, and other accelerators with proof-carrying capabilities for safe realtime systems. It is developed as part of the [Fidelity Framework](https://github.com/FidelityFramework).

## Repository Structure

```
clef-lang-site/
  hugo/         Hugo site (Hextra theme)
  cli/          F# CLI for deployment (Cloudflare Pages API)
  workers/      Cloudflare Workers (F# via Fable)
  scripts/      Shell wrappers for CLI commands
```

## Content Model

| Tier | Path | Description |
|------|------|-------------|
| Specification | `content/spec/` | Mounted from [clef-lang-spec](https://github.com/FidelityFramework/clef-lang-spec) via Hugo Modules |
| Design Docs | `content/docs/design/` | Informative companion articles on language design |
| Documentation | `content/docs/` | Guides, reference, compiler internals |
| Blog | `content/blog/` | Announcements, releases, analysis |

Blog entries belong to the published set. Keep `draft: false` (or omit the field),
use a publication date that is not in the future, and verify the production build
contains the post. The blog archetype uses `draft: false` so an inherited draft
flag does not hide a completed entry.

Specification refresh and change detection follow `clef-lang-spec`'s `main`
branch. `hugo/go.mod` and `hugo/go.sum` pin the resolved revision. The deployment
CLI re-vendors it under `hugo/_vendor/` before building. The vendor directory is
generated and ignored by Git. The specification repository no longer publishes
through a separate `gh-pages` branch.

## Prerequisites

- [Hugo](https://gohugo.io/) (extended) v0.128.0+
- [Go](https://go.dev/) 1.21+ (for Hugo Modules)
- [.NET SDK](https://dotnet.microsoft.com/) 8.0+ (for CLI and Workers)

## Local Development

```bash
cd hugo
hugo server
```

## Deployment

```bash
# Build and deploy to Cloudflare Pages
./scripts/deploy-pages.sh

# Smart deploy (analyzes git diff for minimal scope)
./scripts/smart-deploy.sh
```

See `./scripts/*.sh --help` for all available commands.

### Updating Atlas

```bash
# Apply the delta between D1 and the current extracted graph
./scripts/graph.sh --verbose

# Delete and reinsert every graph row, even if content is unchanged
./scripts/graph.sh --force --verbose

# Equivalent direct F# CLI command, from the repository root
dotnet run --project cli/ClefLang.CLI.fsproj -- graph --verbose
```

Atlas and search are separate indexes. `graph.sh` extracts Atlas's nodes and
connections from the local content and pinned, vendored specification, then
compares them with the actual D1 rows. It inserts new rows, updates changed
fields, deletes stale rows, and preserves unchanged rows and their timestamps.
`index.sh` updates the full-text and vector search indexes. Neither command
deploys site pages. Deploy new pages before linking to them from the live graph.

AI summary guidance lives in `workers/shared/Synthesis.fs`, compiled into both
`search` and `smart-search`. Changes there require redeploying both workers;
smart deploy maps shared-source changes to both. Content corrections also need
`index.sh` to update full-text snippets, embeddings, and removed sections. Deploy
the site assets to deliver changes to the search UI's saved-session version,
which invalidates summaries persisted under an older prompt or content model.

Run the compiled prompt checks with `npm test` in each search worker directory.

`smart-deploy.sh` reconciles Atlas after each actual deployment, but skips it when
it decides no deployment is needed. `deploy-pages.sh` alone does not rebuild
Atlas. Use `graph.sh` to refresh Atlas independently. `smart-deploy.sh --force`
also forces a complete Atlas replacement. The `--local --port PORT` option
targets a local search worker instead. A rebuild failure during smart deploy
is currently reported as `Skipped`, so check its Atlas output when diagnosing
a stale graph.

Both modes compare and write in one D1 transaction, so a failed update preserves
the previous graph. Output reports actual database counts before and after the
transaction, plus additions, updates, deletions, and unchanged rows. Deploy the
updated search worker before using the new CLI: normal updates use the new
authenticated `/graph/sync` route; forced rebuilds use `/graph/rebuild`. The
extractor includes relative documentation links and tooling pages, and excludes
draft pages.

F# regression checks reside in `cli/tests/`: `GraphTests.fsx` checks extraction
through a local capture endpoint. `GraphStorageTests.fsx` checks a local worker's
full replacement, differential updates, no-op behavior, and rollback. Pass its
optional local SQLite path to audit actual row writes and timestamps. Each file
documents its invocation.

## Related Repositories

- [clef-lang-spec](https://github.com/FidelityFramework/clef-lang-spec): Language specification
- [composer](https://github.com/FidelityFramework/composer): Compiler
- [clefpak](https://github.com/FidelityFramework/clefpak): Package manager

Our language specification defines core types and evaluation primitives,
including `Observable` and `Incremental`, as Clef intrinsics for Composer to
lower. Projects use them without a base-library package dependency. See the
[language specification](https://clef-lang.com/spec/draft/).

## License

Code (templates, scripts, CLI, workers) is licensed under the
[Apache License 2.0 with LLVM Exception](LICENSE).

Documentation, design docs, blog posts, and other content are licensed under
[Creative Commons Attribution 4.0 International (CC BY 4.0)](LICENSE-CONTENT).

See [NOTICE](NOTICE) for acknowledgments and patent information.

Copyright 2025-2026 SpeakEZ Technologies, LLC
