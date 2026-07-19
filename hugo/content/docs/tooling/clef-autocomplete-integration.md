---
title: "Bridging Clef AutoComplete To The Fidelity Ecosystem"
linkTitle: "Clef AutoComplete Integration"
description: "Extending Clef Language Services to Support Native Compilation Workflows"
date: 2025-12-06
authors: ["Houston Haynes"]
tags: ["Design", "Innovation", "Architecture"]
weight: 10
params:
  originally_published: 2025-12-06
  migration_date: 2026-03-12
---

Editor tooling is what turns a compiler into a usable development platform. As the Fidelity Framework matures from experimental compiler to practical development platform, we face a critical question: how do we provide the developer experience that [the Clef language](https://clef-lang.com) programmers expect while building something distinct from the .NET and Fable ecosystems? We extend F# language services to support the Fidelity compilation model, preserving developer productivity while making clear that the compilation model underneath is new.

## The Innovation Budget

Every developer has a limited capacity for absorbing new concepts. When introducing a novel compilation target like Composer, we must be thoughtful about where we ask developers to spend their "innovation budget." As explored in [ClefPak: Native Clef Source-Based Package Management](/docs/tooling/clefpak-source-based-package-management/), the TOML-based `.fidproj` format, the `clefpak` package manager, and the clefpak.dev registry all represent necessary departures from .NET conventions.

These changes signal a fundamentally different compilation model and enable capabilities that MSBuild's XML format cannot express. However, requiring developers to also abandon their familiar editor experience would be a step too far.

> Our goal is to preserve the tooling developers know while enabling the new capabilities they need.

A developer should be able to open a Fidelity project in their preferred editor, whether VS Code with Ionide or nvim with LSP support. IntelliSense, error highlighting, and go-to-definition should work as expected.

## The F# Tooling Stack

Our integration approach rests on how F# language services actually work. They involve several layers, each with distinct responsibilities:

```mermaid
graph TD
    subgraph "Editor Layer"
        VSCODE[VS Code]
        NVIM[nvim]
        RIDER[JetBrains Rider]
    end

    subgraph "Extension Layer"
        IONIDE[Ionide Extension]
        LSPCONFIG[nvim-lspconfig]
    end

    subgraph "Language Server"
        FSAC[F# Autocomplete<br/>FSAC]
    end

    subgraph "Compiler Services"
        FCS[F# Compiler Services<br/>FCS]
    end

    VSCODE --> IONIDE
    NVIM --> LSPCONFIG
    RIDER --> FCS
    IONIDE --> FSAC
    LSPCONFIG --> FSAC
    FSAC --> FCS
```

F# Compiler Services (FCS), the engine that powers all F# language intelligence, is entirely agnostic about project file formats. FCS never reads `.fsproj` files. It simply receives a data structure called `FSharpProjectOptions` containing source file paths, compiler flags, and reference locations. However the options were assembled, FCS parses, type checks, and provides semantic analysis identically.

> FSAC (F# Autocomplete) assembles those options.

FSAC is a language server that speaks the Language Server Protocol (LSP), a standard that allows any editor to communicate with any language service. FSAC's job is to accept project files, transform them into `FSharpProjectOptions`, and broker communication between editors and FCS.

Currently, FSAC knows how to "crack" `.fsproj` files by invoking MSBuild to resolve references and determine source file ordering. But this is a pluggable architecture. FSAC supports multiple project loaders, and adding a new one for `.fidproj` files is entirely feasible.

## The TOML Parsing Foundation

Before we can extend FSAC to understand `.fidproj` files, we need a robust TOML parser. The F# ecosystem lacks an actively maintained, pure F# TOML parser that supports the current TOML 1.0.0 specification. The existing options are either unmaintained, dependent on FParsec (adding unnecessary complexity), or wrappers around C# libraries.

We're building `Fidelity.Toml`, a pure F# TOML parser using XParsec, the same parser combinator library that powers pattern matching in Composer's code generation layer. This approach offers several advantages:

- **Zero external dependencies**: XParsec is already an integral part of the Fidelity ecosystem
- **Full TOML 1.0.0 compliance**: Including inline tables, arrays of tables, and datetime types
- **Idiomatic F# API**: Returns discriminated unions and immutable maps, not C# interop types
- **Reusable across the ecosystem**: The same parser serves `clefpak`, Composer, and FSAC

The parser consolidates and extends our existing TOML handling code from Composer's `ProjectConfig.clef` and `TemplateLoader.clef` modules into a unified, well-tested library.

```fsharp
// The core TOML value type
type TomlValue =
    | String of string
    | Integer of int64
    | Float of float
    | Boolean of bool
    | DateTime of DateTimeOffset
    | Array of TomlValue list
    | InlineTable of Map<string, TomlValue>
    | Table of Map<string, TomlValue>

// Clean API for parsing
module Toml =
    let parseFile (path: string) : Result<Map<string, TomlValue>, ParseError> = ...
    let parseString (content: string) : Result<Map<string, TomlValue>, ParseError> = ...

    // Typed accessors with path navigation
    let getString (path: string) (toml: Map<string, TomlValue>) : string option = ...
    let getInt (path: string) (toml: Map<string, TomlValue>) : int64 option = ...
    let getArray (path: string) (toml: Map<string, TomlValue>) : TomlValue list option = ...
```

## Extending FSAC for Fidelity Projects

With TOML parsing in place, extending FSAC to load `.fidproj` files becomes straightforward. The implementation follows FSAC's existing project loader pattern:

```fsharp
type FidprojLoader() =
    interface IProjectLoader with
        member _.LoadProject(projectPath: string) =
            // Parse the TOML manifest
            let manifest = Fidelity.Toml.parseFile projectPath

            // Resolve dependencies (local paths, cache, or clefpak.dev)
            let resolved = PackageResolver.resolve manifest

            // Build FSharpProjectOptions - the only thing FCS needs
            let options = {
                ProjectFileName = projectPath
                SourceFiles = resolved.AllSourcesInOrder |> Array.ofList
                OtherOptions = [|
                    "--target:exe"
                    "--define:FIDELITY"
                    yield! resolved.CompilerFlags
                    yield! resolved.References |> Array.map (sprintf "-r:%s")
                |]
                ReferencedProjects = [||]
                IsIncompleteTypeCheckEnvironment = false
                UseScriptResolutionRules = false
                LoadTime = DateTime.Now
                UnresolvedReferences = None
                OriginalLoadReferences = []
                Stamp = None
            }

            options
```

Composer already produces `FSharpProjectOptions` from `.fidproj` files for the compilation pipeline, through its `FidprojLoader.createProjectOptions` function. Integrating with FSAC reuses that code in a new context.

FSAC ignores the Composer-specific sections of the `.fidproj` file: memory models, target triples, and MLIR optimization passes concern only the compilation pipeline. One `.fidproj` file therefore serves both consumers, IDE support through FSAC and native compilation through Composer.

## The Package Resolution Challenge

The "resolve dependencies" step named in the previous section does substantial work. For a Fidelity project, it is more than reading paths from a file. The `.fidproj` format specifies dependencies with version constraints, and those dependencies may need to be fetched from clefpak.dev, extracted from the local cache, or resolved from workspace paths.

```toml
[dependencies]
alloy = "^0.5.0"
barewire = "1.2.0"
my-local-lib = { path = "../lib" }
```

When a developer opens a project and some dependencies aren't locally available, the tooling needs to fetch them. This is analogous to what happens with NuGet packages, but for source-based distribution.

Our approach separates concerns between `clefpak` (the package manager) and FSAC:

1. **FSAC delegates to `clefpak`**: When FSAC encounters a `.fidproj` file, it invokes `clefpak resolve` to handle dependency resolution
2. **`clefpak` manages the cache**: Downloaded packages live in `~/.fidelity/packages/`, organized by name and version
3. **Resolution is transparent**: Once dependencies are resolved, FSAC receives a flat list of source file paths

This architecture keeps FSAC simple by delegating resolution, fetching, and caching to `clefpak`. Developers can also work offline once dependencies are cached, exactly as they'd expect from any modern package manager.

## Editor Integration

With the project loader extended to read `.fidproj` files, editor integration becomes a matter of configuration rather than code. At least in the initial stages for those using VS Code with Ionide, developers would point to the CAC build they would have produced locally and placed in a convenient path:

```json
{
    "FSharp.fsac.netCoreDllPath": "~/.fidelity/tools/fsac/fsautocomplete.dll",
    "files.associations": {
        "*.fidproj": "toml"
    }
}
```

For those seeking nvim with LSP support, the configuration is similarly straightforward:

```lua
local lspconfig = require('lspconfig')

lspconfig.fsautocomplete.setup {
    cmd = {
        'dotnet',
        vim.fn.expand('~/.fidelity/tools/fsac/fsautocomplete.dll')
    },
    filetypes = { 'fsharp' },
    root_dir = lspconfig.util.root_pattern('*.fidproj', '*.fsproj'),
}

-- Associate .fidproj files with TOML syntax highlighting
vim.filetype.add({
    extension = {
        fidproj = 'toml',
    },
})
```

The `clefpak init` command is expected to generate these configuration files automatically, reducing setup friction for new projects:

```bash
$ clefpak init my-project (optional nvim params TBD)
Creating Fidelity project...
  Created my-project.fidproj
  Created src/Program.clef
  Created .vscode/settings.json
  Created .nvim.lua (optional nvim config)
```

## Multi-Pane Development with nvim

For compiler development work, nvim offers multiple synchronized windows showing different stages of the compilation pipeline. A typical Fidelity development session might display Clef source, MLIR intermediate representation, and LLVM IR side by side, each with appropriate language intelligence:

```mermaid
graph LR
    subgraph "nvim Development Environment"
        FS["Clef Source<br/>(.clef)<br/><br/>CAC"]
        MLIR["MLIR<br/>(.mlir)<br/><br/>mlir-lsp-server"]
        LLVM["LLVM IR<br/>(.ll)<br/><br/>clangd"]
    end

    FS --> MLIR
    MLIR --> LLVM
```

Each pane runs its own LSP:
- **Clef source**: CAC (Clef AutoComplete) provides IntelliSense and type information
- **MLIR**: The `mlir-lsp-server` from LLVM provides dialect-aware completions
- **LLVM IR**: clangd offers syntax awareness and navigation

When developing C/C++ bindings through Farscape, clangd serves double duty: it supplies intelligence for the C headers being bound and verifies the generated binding code.

## Farscape and clangd Integration

Our upcoming [STM32L5 unikernel demonstration](/blog/getting-to-the-heart-of-unikernels/) requires bindings to both the CMSIS HAL (for hardware abstraction) and post-quantum cryptography libraries (for secure communication). Farscape generates these bindings by parsing C headers with `clang` and producing idiomatic Clef interfaces that bind to the C library at compile time. Binding a vendor HAL this way is one of the two microcontroller paths [Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/) lays out.

During binding development, clangd surfaces IntelliSense for the C headers being analyzed:

```bash
farscape generate \
    --header stm32l5xx_hal.h \
    --library __cmsis \
    --include-paths ~/STM32CubeL5/Drivers/CMSIS/Include \
    --defines STM32L552xx,USE_HAL_DRIVER \
    --namespace Fidelity.CMSIS.STM32L5
```

Farscape would then optionally emit a `compile_commands.json` file that supplies clangd with the include paths and preprocessor definitions. This enables full C language intelligence when reviewing the headers that Farscape will process.

For the post-quantum cryptography bindings, we're initially targeting liboqs with its permissively-licensed (MIT/Apache-2.0) implementations of ML-KEM (FIPS 203) and ML-DSA (FIPS 204). That clangd integration exposes the liboqs API surface for review before we generate a memory map and API bindings:

```bash
farscape generate \
    --header oqs/oqs.h \
    --library oqs \
    --include-paths ~/liboqs/build/include \
    --namespace Fidelity.Cryptography.PQC
```

## The Upstream Path

While we're initially building a fork of FSAC with Fidelity support, the long-term goal is contributing this work upstream. The F# community benefits from broader tooling support, and the Fidelity ecosystem benefits from community maintenance and scrutiny.

The contribution path follows established community processes:

1. **RFC for `.fidproj` format**: Document the TOML structure and its relationship to existing F# project files
2. **Reference implementation**: The `Fidelity.Toml` parser and `FidprojLoader` components
3. **FSAC pull request**: Add the new project loader alongside existing MSBuild support
4. **Ionide integration**: Update project detection to recognize `.fidproj` files

With supportive voices in the F# community, we're optimistic about this path. The changes are additive and don't affect existing workflows, which we hope will reduce any barriers to acceptance.

## Deliberate Extension

Our design extends existing tools rather than replacing them. We are not trying to replace the .NET toolchain or force developers into an unfamiliar environment. Instead, we extend existing tools to read a new project format, preserving the editor experience while enabling new capabilities.

The `.fidproj` format signals to developers that they're working with something different from traditional .NET. The TOML syntax is cleaner and better fitted to Fidelity's purposes than MSBuild's XML. MSBuild's platform targeting covers .NET's supported runtimes. LLVM and the other MLIR back ends reach a far broader set of targets. The goal is to ensure when developers open these files in their editor, IntelliSense works, errors appear inline, and go-to-definition navigates correctly.

The innovation spend goes to the project format, and the editor experience stays familiar. The result is a development experience that respects both the novelty of native Clef compilation and the productivity developers expect from the modern language tools F# has offered for years.

Meeting developers in the editors they already use is one half of the approach. The other is [Atelier](/docs/tooling/atelier-the-fidelity-workshop/), the environment we are building for developers who want the framework's full design-time surface gathered into one place. The two share a philosophy: extend the tools developers already use, and build our own where direct control matters.

## See also

- [Leveling Up With Lattice](/docs/tooling/leveling-up-with-lattice/): the connected entry this one extends or complements within the same argument family
