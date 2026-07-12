---
title: "Leveling Up With Lattice"
linkTitle: "Leveling Up With Lattice"
description: "How Clef Tooling Evolved From Ionide"
date: 2026-02-01
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
weight: 50
params:
  originally_published: 2026-02-01
  migration_date: 2026-03-12
---

In chemistry, ions are individual charged particles, independent and reactive. When these ions bond together in organized three-dimensional structures, they form **crystal lattices**: a new phase of matter with emergent properties like conductivity and strength that no individual ion possesses. That progression frames our toolchain evolution from Ionide to **Lattice**.

[Ionide](https://ionide.io/), created by Krzysztof Cieślak and maintained by the Ionide community, provides comprehensive F# development tooling for .NET, a polished IDE experience in the functional programming ecosystem. Like ions that combine to form something greater, we needed to reach beyond .NET's boundaries into native, freestanding systems programming. Ionide is a stalwar tool for .NET F# development. We will springboard from that to create Lattice to support Clef's toolchain needs and provide features extending well past of that foundation.

---

## The Genesis: Why Fork?

Clef reconceives F#'s type semantics and execution model rather than serving as one more compile target:

### Native Type Semantics

**.NET F#** assumes the Base Class Library:
- `string` is UTF-16, heap-allocated, reference-counted
- `option<'T>` is a discriminated union wrapping `Some` or `None`
- Integer arithmetic is unchecked by default
- Everything inherits from `System.Object`

**Clef** operates in a freestanding environment:
- `string` is UTF-8, stack-or-heap at the developer's discretion
- `option<'T>` is a value type (like Rust's `Option<T>`)
- Platform words (`nativeint`) are first-class citizens
- No .NET runtime, no garbage collector, no BCL

> For a detailed exploration of these type system differences, see [From BCL to NTU](/docs/design/types/bcl-to-ntu/) and [Doubling Down](/blog/doubling-down-dmm-dts/) for more information on our dimensional type system.

These aren't superficial differences. They're architectural. An IDE that understands `.fsproj` project files, NuGet packages, and BCL types fundamentally cannot understand `.fidproj` manifests (TOML), native linking, and bare-metal semantics without becoming two tools forced into one codebase.

### Extensive Tooling Coverage

Ionide spans a full ecosystem beyond syntax highlighting and autocomplete:
- **Language Server** (Clef Compiler Services)
- **Project Explorer** (MSBuild integration)
- **Debugger** (CoreCLR protocol)
- **REPL** (Clef Interactive)
- **Analyzers** (FSharp.Analyzers.SDK)
- **Package Management** (NuGet, Paket)

Clef requires parallel infrastructure:
- **CCS** (Clef Compiler Services) - pure compiler, no analyzers
- **CAC** (Clef AutoComplete) - LSP server that consumes CCS output
- **Composer** - AOT compiler (Clef → MLIR → LLVM → native binary)
- **`.fidproj`** - TOML-based project manifests
- **Lattice Analyzers** - NuGet package with analyzers that plug into CAC
- **Native Bindings** - MLIR dialect integration, not BCL

Merging these concerns into Ionide would carry a heavy maintenance cost: feature flags everywhere, dual type systems, and bifurcated build paths.

> The Ionide team shouldn't have to reason about MLIR dialects. The Lattice toolchain shouldn't have to preserve MSBuild compatibility.

With a more clearly deliniated tool set, the two can stand alone, and perhaps in the future interoperate with one another.

---

## The Lattice Restructuring: Four Repositories

To avoid potential confusion, we re-labeled the extant Clef tooling ecosystem from "Ionide.FsNative" to "Lattice":

### 1. [lattice-vscode](https://github.com/FidelityFramework/lattice-vscode)
**The VSCode Extension**

- **Extension ID**: `lattice-fsharp`
- **Display Name**: "Lattice for Clef"
- **Activates On**: `.fidproj`, `.fsnx`, `.fsproj`, `.fsni`, `.fidsln`
- **Config Namespace**: `lattice.fsharp.*`

Built with Fable (F# → JavaScript), just like Ionide. Understands both .NET F# (via FsAutoComplete) and Clef (via CAC). Ionide and Lattice run side-by-side: they have different extension IDs and don't conflict.

### 2. [lattice-analyzers](https://github.com/FidelityFramework/lattice-analyzers)
**Custom Analyzers for Clef**

- **NuGet Package**: `Lattice.Analyzers`
- **Framework**: Uses `FSharp.Analyzers.SDK` (community standard)
- **Target**: .NET 10

Analyzers that understand native type semantics. They warn on `System.String` methods that assume UTF-16 encoding, detect unnecessary heap allocations in stack-only memory models, prevent `null` usage (Clef is null-safe by design), and block `obj` downcasting operations that assume .NET's type hierarchy.

### 3. [lattice-vim](https://github.com/FidelityFramework/lattice-vim)
**Vim/Neovim Plugin**

- **Lua Module**: `require('lattice')`
- **Install Path**: `FidelityFramework/lattice-vim` (via vim-plug, packer, lazy.nvim)

For developers who prefer modal editing. Connects to CAC via LSP protocol, provides syntax highlighting for `.fidproj` and `.fsnx` files.

### 4. [lattice-vscode-helpers](https://github.com/FidelityFramework/lattice-vscode-helpers)
**Fable Bindings for VSCode API**

- **Internal Dependency**: Used by `lattice-vscode`
- **Namespace**: `Lattice.VSCode.Helpers`

Type-safe F# bindings for the VSCode extension API. Compiled to JavaScript via Fable. Keeps the extension codebase in idiomatic F#.



---

## Heritage

Every file in the Lattice repositories begins with the same acknowledgment:

> This project is a hard fork of [Ionide](https://ionide.io/), created by Krzysztof Cieślak and maintained by the Ionide community.

We preserve the original MIT License with Ionide copyright holders. We maintain `IONIDE_HERITAGE.md` in each repository explaining the fork rationale, and we link to Ionide in every README.

The two toolchains serve different, occasionally adjacent use cases:

| Aspect | Ionide | Lattice |
|--------|--------|---------|
| **Target** | .NET F# | Clef |
| **Projects** | `.fsproj` (MSBuild XML) | `.fidproj` (TOML) |
| **Type Semantics** | BCL (UTF-16 strings, `System.Object`) | Native (UTF-8 strings, value types) |
| **Execution** | CoreCLR, .NET runtime | LLVM (for now), bare metal |
| **Package Manager** | NuGet, Paket | [ClefPak](/docs/tooling/clefpak-source-based-package-management/) (clefpak.dev) |
| **Compiler Service** | FCS (F# Compiler Services) | CCS (Clef Compiler Services) |

If you're building web apps with Giraffe, microservices with Saturn, or data pipelines with .NET, **use Ionide**. That role will not change.

If you're building operating system kernels, [embedded unikernels](/blog/getting-to-the-heart-of-unikernels/), high-performance native applications, or related tooling, **use Lattice**.

---

## A Unified Vision

Lattice is a framework for a cohesive editing experience that brings Clef together with MLIR's dialect system and LLVM's optimization infrastructure. By integrating Clang and related LLVM tooling alongside native Clef semantics, it provides one environment for systems application development, where type-safe functional programming meets bare-metal performance. Its components, the compiler services, language servers, analyzers, and build tools, work as one toolchain across the Composer pipeline.

---

## Acknowledgments

**To Krzysztof Cieślak and the Ionide community**: Thank you for creating the gold standard of F# IDE tooling. Lattice is a hard fork that leans heavily on Ionide, and it exists because Ionide showed us what great F# developer experience looks like.

**To the F# community**: Your feedback on Clef has been invaluable. Every GitHub issue, every discussion thread, every "why would you do this?" question has sharpened our thinking.

**To the MLIR and LLVM communities**: Your compiler infrastructure enables everything we're building. Clef wouldn't exist without MLIR's extensible dialect system, and LLVM's maturity.

---

**Repository Links**:
- [Lattice VSCode](https://github.com/FidelityFramework/lattice-vscode)
- [Lattice Analyzers](https://github.com/FidelityFramework/lattice-analyzers)
- [Lattice Vim](https://github.com/FidelityFramework/lattice-vim)
- [Ionide](https://ionide.io/) (for .NET F# development)
- [Composer Compiler](https://github.com/FidelityFramework/Composer)
- [CCS](https://github.com/FidelityFramework/clef-lang) (Clef Compiler Services)

## See also

- [Bridging Clef AutoComplete To The Fidelity Ecosystem](/docs/tooling/clef-autocomplete-integration/): how CAC extends the FSAC project-loader architecture to crack `.fidproj` TOML manifests and deliver IntelliSense for native Clef projects in VSCode and nvim.
- [Opining Upon Reflection](/blog/opining-upon-reflection/): the case for why a PSG-backed language server is not a shadow model beside the sources, told for readers arriving from the .NET reflection mindset.
