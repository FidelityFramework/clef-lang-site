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
