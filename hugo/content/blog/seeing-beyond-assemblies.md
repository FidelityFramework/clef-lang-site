---
title: "Seeing Beyond Assemblies"
linkTitle: "Beyond Assemblies"
description: "How .NET and other hosted runtime environments artificially constrain modern systems options"
date: 2021-09-25T16:59:54+06:00
authors: ["Houston Haynes"]
tags: ["Systems", "Compilation"]
params:
  originally_published: 2021-09-25
  original_url: "https://speakez.tech/blog/seeing-beyond-assemblies/"
  migration_date: 2026-02-15
---

The .NET platform introduced the concept of assemblies over two decades ago, a fundamental building block that has served as the cornerstone of .NET development since its inception in 2002. Assemblies, typically manifested as DLL or EXE files, represented a significant advancement in software componentization at the time. They combined compiled Intermediate Language (IL) code with rich metadata about types, references, and versions into a cohesive deployment unit that could be shared across applications.

## The Legacy of .NET Assemblies

This assembly model offered several advantages that have sustained the .NET ecosystem through multiple generations of development:

- **Deployment simplicity**: Assemblies encapsulated all necessary components in binary form, simplifying distribution
- **Version control**: The strong-naming system and version metadata allowed for side-by-side execution of different versions
- **Type safety across boundaries**: The rich metadata enabled type checking across assembly boundaries
- **Language interoperability**: Assemblies compiled from different .NET languages could interact seamlessly
- **Performance**: Pre-compilation to IL reduced startup time compared to interpreting source code

However, this binary-centric approach also introduced limitations that have become increasingly apparent as computing environments diversify:

```mermaid
flowchart LR
    subgraph DotNetAssembly["Binary Assembly Model"]
        direction TB
        Source["Source<br>Code"] --> Compiler["C#/F#<br>Compiler"]
        Compiler --> Assembly["Binary<br>Assembly"]
        Assembly --> Runtime["CLR<br>Runtime"]
        Runtime --> JIT["JIT<br>Compilation"]
        JIT --> NativeExecution["Platform-specific<br>Execution"]
    end
```

The assembly model manifests several limitations in today's diverse computing landscape:

1. **Runtime dependency**: Assemblies require a virtual machine (.NET runtime), increasing the deployment footprint
2. **Limited cross-compilation options**: Targeting new platforms requires runtime support rather than native compilation
3. **Opaque optimizations**: Binary distribution prevents certain cross-cutting compiler optimizations
4. **Resource constraints**: The runtime model isn't always appropriate for resource-constrained environments
5. **Compilation boundary**: Pre-compiled binaries create optimization boundaries that prevent whole-program optimization

These limitations have become increasingly relevant as computing has expanded beyond traditional servers and desktops to encompass embedded devices, specialized accelerators, and heterogeneous computing environments.

## Rust's Return to Source-Based Packaging

When Rust emerged in the 2010s, rather than following the prevalent binary distribution models of platforms like .NET, Java, and others, it took a deliberate step in a different direction, one that ironically echoed older C/C++ practices while modernizing them for contemporary challenges.

Rust's Cargo package manager re-embraced source-based distribution as a foundational principle. This wasn't a regression but rather a thoughtful reconsideration of how modern languages should approach packaging:

```mermaid
flowchart LR
    subgraph RustCargo["Source-Based Package Model"]
        direction TB
        Dependency1["Dependency<br>Source"] --> |"Fetched by<br>Cargo"| BuildSystem
        Dependency2["Dependency<br>Source"] --> |"Fetched by<br>Cargo"| BuildSystem
        ApplicationSource["Application<br>Source"] --> BuildSystem["Cargo<br>Build System"]
        BuildSystem --> LLVM["LLVM<br>Pipeline"]
        LLVM --> Optimization["Cross-cutting<br>Optimization"]
        Optimization --> NativeExecutable["Native<br>Executable"]
    end
```

By distributing packages as source code rather than binaries, Cargo enabled several distinct advantages:

1. **Zero-cost abstractions**: Compiling all code together allows optimizations across package boundaries
2. **Native compilation**: Direct compilation to machine code eliminates the need for a runtime
3. **Cross-platform targeting**: The same source can target multiple platforms without runtime support
4. **Resource efficiency**: Removing the runtime dependency reduces the deployment footprint
5. **Whole-program optimization**: The compiler can analyze and optimize across all package boundaries

Rust's packaging approach appears well-aligned with its compilation model and optimization goals. By compiling packages from source, Rust potentially enables optimizations across package boundaries that support its zero-cost abstraction philosophy. While binary distribution models have their own advantages, source-based distribution provides the compiler with greater visibility and optimization opportunities across component boundaries.

The TOML-based manifest format provided a clean, human-readable alternative to XML-based project files, while features like workspaces, conditional compilation, and semantic versioning created a cohesive ecosystem for package development and consumption.

## ClefPak: Extending the Source-Based Paradigm

At SpeakEZ, we recognize the years and even decades of work that have gone into the platforms that gave rise to Clef. The .NET ecosystem has provided a robust foundation for functional and concurrent programming in the enterprise, with assemblies playing a crucial role in that success. At the same time, we believe the computing landscape has evolved in ways that call for new approaches.

The Fidelity Framework represents our vision for bringing Clef beyond its .NET origins into a broader computing spectrum, from deeply embedded systems to high-performance servers and AI accelerators. To accomplish this, we needed a package management approach adapted to this diverse landscape.

ClefPak, our package management system, draws inspiration from Rust's Cargo while extending it with concepts from Clef's functional and concurrent paradigm and adapting it to Fidelity's unique compilation model:

```mermaid
flowchart TD
    subgraph FidelityFramework["Fidelity Compilation Pipeline"]
        direction TB
        ClefSource["Clef Source<br>Code"] --> CCS["Clef Compiler<br>Services"]
        CCS --> PSG["Program Semantic<br>Graph"]
        PSG --> MLIRGen["Graph-to-MLIR<br>Lowering"]
        MLIRGen --> MLIR["High-level<br>MLIR"]
        MLIR --> LoweringSteps["Dialect<br>Lowering"]
        LoweringSteps --> LLVMIR["LLVM<br>IR"]
        LLVMIR --> NativeCode["Native<br>Executable"]
    end
```

ClefPak extends beyond Cargo's foundation in several key ways:

### 1. Functional Composition for Platform Adaptation

Where Cargo uses conditional compilation directives for platform-specific code, ClefPak takes a more functional approach through composition:

```fsharp
// Platform configuration through functional composition
let embeddedConfig =
    PlatformConfig.compose
        [withPlatform PlatformType.Embedded;
         withMemoryModel MemoryModelType.Constrained;
         withHeapStrategy HeapStrategyType.Static]
        PlatformConfig.base'
```

This approach creates a clean separation between code and its platform-specific compilation strategy, allowing the same source to adapt to different environments without conditional compilation.

### 2. Spanning the Computing Spectrum

Fidelity and ClefPak are designed from the ground up to target a diverse range of computing environments, from microcontrollers with kilobytes of memory to server clusters with vast resources. This requires more sophisticated platform adaptation than simply targeting different operating systems with relatively similar compute and memory profiles:

```mermaid
flowchart LR

    subgraph AdaptationStrategies["Platform Adaptation"]
        direction TB
        StaticAlloc["Static<br>Allocation"] --- ActorIsolation["Actor-based<br>Isolation"]
        ActorIsolation --- Clustering["Distributed<br>Clustering"]

        HardwareVectors["Hardware<br>Vector Operations"] --- ARMNeon["ARM<br>NEON"]
        ARMNeon --- AVX["Intel<br>AVX"]
        AVX --- TensorCore["NVIDIA<br>Tensor Cores"]
    end
    subgraph ComputingSpectrum["Fidelity Target Spectrum"]
        direction TB
        Embedded["Embedded<br>Microcontrollers"] --- Mobile["Mobile<br>Devices"]
        Mobile --- Desktop["Desktop<br>Applications"]
        Desktop --- Server["Server<br>Systems"]
        Server --- AI["AI<br>Accelerators"]
    end

    ComputingSpectrum -.- AdaptationStrategies
```

### 3. Progressive Dialect Lowering via MLIR

Where Rust compiles to LLVM IR directly, our Fidelity pipeline is designed to lower Clef through MLIR's multi-level representation, descending from a high-level domain form toward machine code one dialect at a time. That extra layer is meant to give us room to target diverse hardware that a direct LLVM path would not:

```mermaid
flowchart TD
    subgraph MLIRDialectLowering["MLIR Dialect Lowering"]
        direction TB
        HighLevel["High-level<br>Domain Dialect"] --> Mid["Mid-level<br>Structured Dialect"]
        Mid --> Target["Target<br>Dialect"]
        Target --> LLVM["LLVM<br>Dialect"]
    end
```

Lowering one dialect at a time is meant to let analysis and optimization happen at the level where each is cheapest to state, rather than reconstructing high-level intent from LLVM IR after the fact. The multi-level approach is also what lets the pipeline target non-LLVM back ends where the hardware calls for it, such as DSPs, FPGAs, or accelerators that carry their own toolchains.

### 4. BAREWire: Binary Interlock Memory Management

Our approach to memory at this stage centers on BAREWire, which we called "binary interlock" at the time. We were after a different starting point for memory safety than Rust's borrow checker takes:

```fsharp
// Example of BAREWire's flexible memory management
let embeddedMemory =
    MemoryConfig.compose
        [withStrategy MemoryStrategy.StaticAllocation;
         withSafetyChecks SafetyChecks.CompileTime;
         withLifetimeModel LifetimeModel.RegionBased]
        MemoryConfig.base'

let serverMemory =
    MemoryConfig.compose
        [withStrategy MemoryStrategy.ActorIsolated;
         withSafetyChecks SafetyChecks.Hybrid;
         withLifetimeModel LifetimeModel.MessagePassing]
        MemoryConfig.base'
```

Rust's borrow checker settles on one ownership model and makes it the organizing principle of program design, a deliberate choice that gives Rust its guarantees. Our aim with BAREWire was a spectrum of memory strategies chosen by context instead:

1. **Resource-Constrained Environments**: Static allocation with zero-copy operations for predictable memory usage
2. **Mid-Range Devices**: Region-based memory with isolated heaps, providing safety without global tracking
3. **Server Systems**: Actor model with message-passing semantics for scalable concurrent systems

The premise is that different computing environments carry different memory requirements, and a developer should be able to lead with the problem domain and choose a strategy to fit it. The intent was for memory safety to fall out of the architecture rather than become the thing every program is organized around, so a developer writes idiomatic Clef and the safety model meets the target instead of the reverse.

The design also means to carry its weight inside the compilation pipeline. By stating memory layout and safety constraints at the Clef level, the pipeline is meant to receive that information directly rather than reconstruct it from LLVM IR after the source has already lost it. The aim is a model that keeps the type-safety guarantees while leaving the compiler free to optimize for each target's characteristics, so safety and performance are settled together rather than traded against each other.

## An Architecture Built for Purpose

This evolution over decades, from .NET's assembly model through Rust's rekindling of source-managed packaging to Fidelity's ClefPak, illustrates a broader trend in software development: a language's compilation strategy is as pivotal to its longevity as the language itself. .NET's assembly model served its purpose admirably for two decades within the context of a managed runtime environment targeting primarily general-purpose computing platforms. Cargo's source-based approach recognized the limitations of binary distribution for languages targeting native compilation without a runtime. And now it's time for Clef to move forward with the Fidelity framework. Our ClefPak package manager extends this foundation with concepts specific to Clef's functional and concurrent paradigm and Fidelity's broad target spectrum.

This evolution isn't about rejection but about purpose-built design. Each system was crafted for its specific context, assemblies for .NET's managed environment, Cargo for Rust's focus on systems programming without a runtime, and ClefPak for Fidelity's mission to bring concurrent programming to the entire computing spectrum. As computing continues to diversify beyond traditional platforms to encompass specialized accelerators, edge devices, and heterogeneous systems, we believe this source-centered, compilation-focused approach to package management represents not just the present but the future of software development.

The Fidelity Framework, with ClefPak as its package management system, aims to bring the expressiveness and safety of Clef to this sophisticated computing landscape without the limitations of legacy approaches. In all facets, SpeakEZ's method is to honor what came before while boldly embracing what's next. We're eager to share this new foundation that embraces the entire computing spectrum.
