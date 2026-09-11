# clef-lang-site

Source for [clef-lang.com](https://clef-lang.com) — the official site for the Clef programming language.

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

Specification refresh and change detection follow `clef-lang-spec`'s `main`
branch. `hugo/go.mod` and `hugo/go.sum` pin the resolved revision; the deployment
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

### Rebuilding Atlas

```bash
# Rebuild the Atlas graph in the configured search worker's D1 database
./scripts/graph.sh --verbose

# Equivalent direct F# CLI command, from the repository root
dotnet run --project cli/ClefLang.CLI.fsproj -- graph --verbose
```

Atlas and search are separate indexes. `graph.sh` replaces Atlas's nodes and
connections from the local content and pinned, vendored specification;
`index.sh` updates the full-text and vector search indexes. Neither command
deploys site pages. Deploy new pages before linking to them from the live graph.

`smart-deploy.sh` rebuilds Atlas after each actual deployment, but skips it when
it decides no deployment is needed. `deploy-pages.sh` alone does not rebuild
Atlas. Use `graph.sh` to refresh Atlas independently; `--local --port PORT`
targets a local search worker instead. A rebuild failure during smart deploy
is currently reported as `Skipped`, so check its Atlas output when diagnosing
a stale graph.

Graph replacement uses one D1 transaction for both nodes and connections, so a
failed rebuild preserves the previous graph. The extractor includes relative
documentation links and tooling pages, and excludes draft pages.

F# regression checks live in `cli/tests/`: `GraphTests.fsx` checks extraction
through a local capture endpoint; `GraphStorageTests.fsx` checks a local worker's
full replacement and rollback behavior. Each file documents its invocation.

## Related Repositories

- [clef-lang-spec](https://github.com/FidelityFramework/clef-lang-spec) — Language specification
- [composer](https://github.com/FidelityFramework/composer) — Compiler
- [clefpak](https://github.com/FidelityFramework/clefpak) — Package manager
- [alloy](https://github.com/FidelityFramework/alloy) — Base libraries

## License

Code (templates, scripts, CLI, workers) is licensed under the
[Apache License 2.0 with LLVM Exception](LICENSE).

Documentation, design docs, blog posts, and other content are licensed under
[Creative Commons Attribution 4.0 International (CC BY 4.0)](LICENSE-CONTENT).

See [NOTICE](NOTICE) for acknowledgments and patent information.

Copyright 2025-2026 SpeakEZ Technologies, LLC
