---
title: "Leveling Up With Lattice"
linkTitle: "Leveling Up With Lattice"
description: "How Clef Tooling Evolved From Ionide"
date: 2026-02-01
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2026-02-01
  original_url: "https://speakez.tech/blog/leveling-up-with-lattice/"
  migration_date: 2026-03-12
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

In chemistry, ions are individual charged particles, independent, reactive, fundamental. When these ions bond together in organized three-dimensional structures, they form **crystal lattices**. The lattice isn't just a collection of ions; it's a new phase of matter with emergent properties: conductivity, strength, characteristics that no individual ion possesses.

> This metaphor perfectly captures our toolchain evolution from Ionide to **Lattice**.

[Ionide](https://ionide.io/), created by Krzysztof Cieślak and maintained by the Ionide community, provides exceptional F# development tooling for .NET. It's one of the finest IDE experiences in the functional programming ecosystem, comprehensive and polished. But like ions that combine to form something greater, we needed to extend beyond .NET's boundaries into native, freestanding systems programming. This is not to replace Ionide, but to support a new, extended structure and the elements *it* must embrace in order to serve its remit.

---

## The Genesis: Why Fork?

Clef isn't just another compile target. It's a fundamental reconception of F#'s type semantics and execution model:

### Native Type Semantics

**.NET F#** assumes the Base Class Library:
- `string` is UTF-16, heap-allocated, reference-counted
- `option<'T>` is a discriminated union wrapping `Some` or `None`
- Integer arithmetic is unchecked by default
- Everything inherits from `System.Object`

**Clef** operates in a freestanding environment:
- `string` is UTF-8, stack-or-heap at your discretion
- `option<'T>` is a value type (like Rust's `Option<T>`)
- Platform words (`nativeint`) are first-class citizens
- No .NET runtime, no garbage collector, no BCL

> For a detailed exploration of these type system differences, see [FSharp.Native: From IL to NTU](/blog/fsharp-native-from-il-to-ntu/) and [Doubling Down](/blog/doubling-down/) for more information on our dimentional type system.

These aren't superficial differences. They're architectural. An IDE that understands `.fsproj` project files, NuGet packages, and BCL types fundamentally cannot understand `.fidproj` manifests (TOML), native linking, and bare-metal semantics without becoming two tools forced into one codebase.

### Extensive Tooling Coverage

Ionide doesn't just provide syntax highlighting and autocomplete. It's an ecosystem:
- **Language Server** (F# Compiler Services)
- **Project Explorer** (MSBuild integration)
- **Debugger** (CoreCLR protocol)
- **REPL** (F# Interactive)
- **Analyzers** (FSharp.Analyzers.SDK)
- **Package Management** (NuGet, Paket)

Clef requires parallel infrastructure:
- **CCS** (Clef Compiler Services) - pure compiler, no analyzers
- **FSNAC** (FsNativeAutoComplete) - LSP server that consumes CCS output
- **Composer** - AOT compiler (F# → MLIR → LLVM → native binary)
- **`.fidproj`** - TOML-based project manifests
- **Lattice Analyzers** - NuGet package with analyzers that plug into FSNAC
- **Native Bindings** - MLIR dialect integration, not BCL

Attempting to merge these concerns into Ionide would create a maintenance nightmare: feature flags everywhere, dual type systems, bifurcated build paths. 

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

Built with Fable (F# → JavaScript), just like Ionide. Understands both .NET F# (via FsAutoComplete) and Clef (via FSNAC). You can use Ionide and Lattice side-by-side, they have different extension IDs and don't conflict.

### 2. [lattice-analyzers](https://github.com/FidelityFramework/lattice-analyzers)
**Custom Analyzers for Clef**

- **NuGet Package**: `Lattice.Analyzers`
- **Framework**: Uses `FSharp.Analyzers.SDK` (community standard)
- **Target**: .NET 10

Analyzers that understand native type semantics. For example, warning when you try to use `System.String` methods that assume UTF-16 encoding, detecting unnecessary heap allocations in stack-only memory models, preventing `null` usage (Clef is null-safe by design), and blocking `obj` downcasting operations that assume .NET's type hierarchy.

### 3. [lattice-vim](https://github.com/FidelityFramework/lattice-vim)
**Vim/Neovim Plugin**

- **Lua Module**: `require('lattice')`
- **Install Path**: `FidelityFramework/lattice-vim` (via vim-plug, packer, lazy.nvim)

For developers who prefer modal editing. Connects to FSNAC via LSP protocol, provides syntax highlighting for `.fidproj` and `.fsnx` files.

### 4. [lattice-vscode-helpers](https://github.com/FidelityFramework/lattice-vscode-helpers)
**Fable Bindings for VSCode API**

- **Internal Dependency**: Used by `lattice-vscode`
- **Namespace**: `Lattice.VSCode.Helpers`

Type-safe F# bindings for the VSCode extension API. Compiled to JavaScript via Fable. Keeps the extension codebase in idiomatic F#.

---

## Why "Lattice"?

The name progression is intentional:

**Ionide** → Individual ions, reactive and independent
**Lattice** → Organized crystal structure with emergent properties

Ionide provides excellent tooling for F# in the .NET ecosystem, a single, well-defined domain. Lattice extends into **polyglot systems programming**:

- **Clef** (via Composer)
- **MLIR** (via Alex lowering)
- **LLVM** (final backend)
- **F\*** (verification, future integration)
- **Lua** (scripting, configuration)
- **C** (FFI, platform bindings)

Just as a crystal lattice exhibits properties, electrical conductivity, optical refraction, mechanical strength, that individual ions lack, Lattice provides **cross-language integration** that pure F# tooling cannot.

---

## Heritage and Gratitude

Every file in the Lattice repositories begins with the same acknowledgment:

> This project is a hard fork of [Ionide](https://ionide.io/), created by Krzysztof Cieślak and maintained by the Ionide community. We are deeply grateful for the exceptional foundation we build upon.

We preserve the original MIT License with Ionide copyright holders. We maintain `IONIDE_HERITAGE.md` in each repository explaining the fork rationale. We link to Ionide in every README.

**Lattice does not interfere with Ionide.** We serve different, occasionaly adjacent use cases:

| Aspect | Ionide | Lattice |
|--------|--------|---------|
| **Target** | .NET F# | Clef |
| **Projects** | `.fsproj` (MSBuild XML) | `.fidproj` (TOML) |
| **Type Semantics** | BCL (UTF-16 strings, `System.Object`) | Native (UTF-8 strings, value types) |
| **Execution** | CoreCLR, .NET runtime | LLVM (for now), bare metal |
| **Package Manager** | NuGet, Paket | [ClefPak](/blog/native-fsharp-source-based-package-mgmt/) (clefpak.dev) |
| **Compiler Service** | FCS (F# Compiler Services) | CCS (Clef Compiler Services) |

If you're building web apps with Giraffe, microservices with Saturn, or data pipelines with .NET, **use Ionide**. That role will not change.

If you're building operating system kernels, embedded unikernels, high-performance native applications, or related tooling, **use Lattice**.

---

## The Road Ahead

1. **v0.1.0 Releases** - First stable alpha releases 
2. **VSCode Marketplace** - Publish `lattice-fsharp` extension
3. **NuGet** - Publish `Lattice.Analyzers` package
4. **Documentation** - Comprehensive guides for `.fidproj` authoring
5. **WREN Stack templates** - Provide `dotnet new` templates for trying out Composer and build a native application with the WREN Stack


## A Unified Vision

Lattice isn't just an IDE plugin or a collection of tools. It's a framework for creating a **cohesive editing experience** that seamlessly brings together Clef with MLIR's dialect system, LLVM's optimization infrastructure, and eventually F\* proof interactions. By integrating Clang and related LLVM tooling alongside native F# semantics, Lattice provides a unified environment for **systems application development** where type-safe functional programming meets bare-metal performance and formal verification. The lattice structure ensures each component, compiler services, language servers, analyzers, build tools, works in concert to deliver the developer experience that modern systems programming demands.

---

## Acknowledgments

**To Krzysztof Cieślak and the Ionide community**: Thank you for creating the gold standard of F# IDE tooling. Lattice exists because Ionide showed us what great F# developer experience looks like.

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
