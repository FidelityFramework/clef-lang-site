---
title: Tooling
weight: 5
---

The instruments that surround the Clef compiler: the language server and editor integration worked through day to day, and the package manager that puts source on the build path without a runtime in the way. The set widens as the toolchain does, toward verification and the evidence a certification lab re-checks.

## Start with the editor path

[Leveling Up With Lattice](leveling-up-with-lattice/) describes the current integration direction and the first working editor loop. The [shared implementation design](https://github.com/FidelityFramework/Composer/blob/main/docs/Lattice_Integration.md) assigns the work across repositories and defines the regression gates. The initial server is hosted in .NET and presents Clef semantics from CCS; the forks' inherited F# services still need that connection.

| Part of the toolchain | Start here |
| --- | --- |
| Language rules and compiler facts | [Clef specification](https://github.com/FidelityFramework/clef-lang-spec), [CCS](https://github.com/FidelityFramework/clef), [Composer](https://github.com/FidelityFramework/Composer) |
| VSCode client and its bindings | [lattice-vscode](https://github.com/FidelityFramework/lattice-vscode), [lattice-vscode-helpers](https://github.com/FidelityFramework/lattice-vscode-helpers) |
| Neovim/Vim and lexical highlighting | [lattice-vim](https://github.com/FidelityFramework/lattice-vim), [clef-grammar](https://github.com/FidelityFramework/clef-grammar) |
| Earlier language-service implementation | [ClefAutoComplete](https://github.com/FidelityFramework/ClefAutoComplete), retained as protocol and migration reference |
| Analysis slots and regression cases | [lattice-analyzers](https://github.com/FidelityFramework/lattice-analyzers), with an active slot inventory alongside its inherited rule corpus |
| Memory, IPC and network contracts | [BAREWire](https://github.com/FidelityFramework/BAREWire), with target declarations from [Fidelity.Platform](https://github.com/FidelityFramework/Fidelity.Platform) |
| Data formats and native binding generation | [Fidelity.Data](https://github.com/FidelityFramework/Fidelity.Data), [Farscape](https://github.com/FidelityFramework/Farscape) |
| SDK and OpenAPI binding generation for F#/Fable | [Xantham](https://github.com/shayanhabibi/Xantham), [Hawaii](https://github.com/FidelityFramework/Hawaii); the near-term bridge for work such as FSharp.CloudEdge |
| Richer compiler and proof views | [Atelier](atelier-the-fidelity-workshop/), with the [WrenHello](https://github.com/FidelityFramework/WRENHello) WebView example and [Partas.Solid](https://github.com/speakeztech/Partas.Solid) frontend bindings |

The first Lattice gate needs the compiler and editor path. The other projects provide inputs or later consumers; they are not all dependencies of a basic editing session. Conclave is the platform for intelligent distributed systems in Cloudflare; BAREWire is the glue connecting its components.

The [earlier AutoComplete proposal](clef-autocomplete-integration/) is retained as historical background. Its FSAC setup examples are superseded by the current CCS-backed integration.
