---
title: "Clef on Metal Extended"
linkTitle: "On Metal Extended"
description: "Metal beyond the microcontroller: the substrate spectrum from reset vector to container, the Farscape toolchain in Clef, and graduated memory across the range"
weight: 20
date: 2025-12-28T11:00:00-05:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
aliases:
  - /docs/internals/hardware/on-metal-revisited/
params:
  originally_published: 2025-12-28
  migration_date: 2026-02-15
---

[Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/) treats one class of target at full depth: two paths onto a microcontroller, and the trusted-computing-base decision that selects between them. This document covers the wider territory. A sealed Clef image can enter at a reset vector, boot as a microVM guest, or run as a static binary against a host kernel's syscall surface, and what changes between those forms is the substrate declaration, never the program text.

Farscape can parse vendor C/C++ headers in Clef and hand the compiler a coherent typed memory model. The memory strategies graduate from stack-only allocation to actor-owned arenas. Two companion entries carry the parts of the range with machinery of their own: [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/) for the dispatch layer, and [Bring-Up Beyond the CPU](/docs/internals/hardware/spatial-bring-up/) for targets where the registers themselves are compilation outputs. The proposition has held since our earliest bare-metal work: expressive, idiomatic Clef compiling to machine code indistinguishable from hand-written assembly, with the toolchain that delivers machine bring-up and low-level process automation now written in Clef itself.

---

## The Substrate Spectrum

The [Platform Descriptor](/spec/draft/platform-bindings/) carries the target's machine characteristics, and the [scheduler contract](/spec/draft/scheduler-contract/) partitions the instruction-stream targets into four profiles:

| Substrate | Provided by | The image assumes | C linkage |
| --- | --- | --- | --- |
| Freestanding | the silicon itself | entry at the reset vector; a hardware timer and interrupt delivery | none: no libc, no C ABI in the path |
| MicroVM guest | a hypervisor's virtual hardware | virtual devices; vCPU progress against a declared quota; the image brings its own service layer | musl sealed in by static coupling |
| Container | the host kernel's syscall surface | syscalls within the granted policy; carrier-thread progress against an observable budget; empty userland | musl sealed in by static coupling |
| Hosted process | a full OS and its userland | the OS scheduler and dynamic linker | Farscape bindings resolved against shared libraries |

The middle rows are the sealed-image territory in the [MirageOS](https://mirage.io) tradition: the build specialized to the application, only what the application uses linked in, the result booted as a guest or run as a process behind a narrow syscall filter. [Getting to the Heart of Unikernels](/blog/getting-to-the-heart-of-unikernels/) reads that history and the cold-start economics in full. Each tier carries its own C-linkage discipline, with the static and dynamic mechanisms in [library binding](/docs/design/interop/library-binding/):

- hosted: Farscape bindings resolved dynamically against the system's shared libraries
- container and microVM guest: the same bindings statically coupled, musl sealed into the image
- freestanding: [no C runtime at all](/spec/draft/ffi-boundary/), no foreign-function boundary, interior memory on the compiler's own lifetime lattice

A platform declaration that describes a 4-byte word with no heap region and one that grants eight cores and a gigabyte under a syscall policy are the same kind of object, and the program text between them is untouched. The build that enters at a Cortex-M33 reset vector and the build that runs multi-threaded Olivier actors inside a container grant are one programming model at two points on the declared range.

[Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/) treats the scheduler beneath the image: the dispatch contract our actor system holds constant across these same rows, and the per-substrate manifest that separates what an implementation discharges itself from what it assumes from below.

On spatial silicon the image is a configuration rather than a program, and the registers a driver would poke are artifacts the compiler synthesizes. [Bring-Up Beyond the CPU](/docs/internals/hardware/spatial-bring-up/) follows the backend legs onto those targets.

## The Problem with Wrappers

Farscape's first implementation parsed CMSIS headers through CppSharp, a .NET binding to libclang. That approach worked to a certain degree, but its layered indirection and .NET runtime requirement conflicted with our broader philosophy. Farscape now parses headers directly in Clef, an architecture the introduction of CCS (Clef Compiler Services) made possible. That change belongs to a direction we first sketched in early 2024 and are still developing in the Fidelity framework today.

CppSharp is a capable tool. It wraps libclang to parse C and C++ code, exposing the AST through .NET types. For the project, borrowing from the .NET ecosystem was always understood as temporary scaffolding. During early prototyping, while we were already working around BCL type machinery and building shadow APIs to test native compilation concepts, CppSharp served its purpose.

### Wrapper Indirection

The issue was never C++ tooling itself. MLIR is C++. Fidelity binds directly to C and C++ libraries, whether statically through LTO or dynamically at runtime, a binding style familiar to developers arriving from the .NET library ecosystem.

The problem with CppSharp was the indirection: C headers flowed through libclang, then through .NET bindings, then through additional transformation logic before finally arriving as Clef types. Each layer added complexity and was a potential source of bugs. And running CppSharp required a .NET runtime during the toolchain's own execution, which we found incongruous even if it didn't affect the compiled output.

### Semantic Mismatch

CppSharp produces an AST designed for C++ semantics. Transforming that AST into Clef abstractions required mapping between different type systems. C++ templates, macros, and preprocessor directives do not map cleanly to Clef constructs. Every edge case required special handling, and the edge cases in vendor-provided headers are numerous.

The transformation also lost register-access semantics. CMSIS headers use `__I`, `__O`, and `__IO` qualifiers to indicate register access semantics: read-only, write-only, and read-write. These qualifiers inform the compiler when volatile semantics are required, when writes trigger hardware actions, and when reads return undefined values. CppSharp treated these as type qualifiers to be preserved, but the semantic intent had to be recovered downstream through additional heuristics.

## XParsec: A Typed Parser for Clef

The solution was to parse headers directly, using XParsec. This parser combinator library is used in other parts of the Fidelity toolchain, including PSG traversal in the Composer compiler. Using XParsec for header parsing means:

- No external dependencies. The parser is written in Clef, compiled alongside everything else.
- Type-safe parsing. Parse failures produce structured error messages with precise source locations.
- Semantic preservation. The parser captures `__I`, `__O`, and `__IO` qualifiers as first-class constructs, mapping them directly to [`AccessKind` values](/spec/draft/access-kinds/) that flow through the compilation pipeline.

```fsharp
// CMSIS qualifiers become first-class parse results
let cmsis_I = keyword "__I" >>% AccessKind.ReadOnly
let cmsis_O = keyword "__O" >>% AccessKind.WriteOnly
let cmsis_IO = keyword "__IO" >>% AccessKind.ReadWrite

// Field declarations capture access semantics
let fieldDecl =
    parse {
        let! access = optional (cmsis_I <|> cmsis_O <|> cmsis_IO)
        let! fieldType = typeSpecifier
        let! name = identifier
        let! arraySize = optional arrayBrackets
        do! symbol ";"
        return {
            Name = name
            Type = fieldType
            Access = access |> Option.defaultValue AccessKind.ReadWrite
            ArraySize = arraySize
        }
    }
```

The parser is compositional: simple parsers combine into complex ones. Because each combinator preserves types, the compiler catches errors before the parser runs. An external tool's output offers no such check: correctness there rests on the transformation logic covering every case.

## Quotation-Based Output

Farscape's early output was P/Invoke-style bindings. The current architecture generates Clef quotations and active patterns.

### Why Quotations?

Quotations are Clef code represented as data. They can be inspected, transformed, and consumed by other parts of the compilation pipeline. When Farscape generates a `PeripheralDescriptor` quotation, it creates a structured representation of hardware memory layout that the CCS [nanopass pipeline](/docs/internals/concepts/nanopass-navigation/) can decompose:

```fsharp
let gpioPeripheralQuotation: Expr<PeripheralDescriptor> = <@
    { Name = "GPIO"
      Instances = Map.ofList [
          ("GPIOA", 0x48000000un)
          ("GPIOB", 0x48000400un)
      ]
      Layout = {
          Size = 0x400
          Alignment = 4
          Fields = [
              { Name = "MODER"; Offset = 0x00; Type = U32; Access = ReadWrite }
              { Name = "IDR"; Offset = 0x10; Type = U32; Access = ReadOnly }
              { Name = "BSRR"; Offset = 0x18; Type = U32; Access = WriteOnly }
          ]
      }
      MemoryRegion = Peripheral }
@>
```

This quotation encodes everything the compiler needs to generate correct memory-mapped access: base addresses, register offsets, and access semantics. The CCS pipeline can pattern-match on this structure to apply constraints, validate access patterns, and emit appropriate MLIR.

### Active Patterns for Recognition

Alongside quotations, Farscape generates active patterns that recognize hardware operations in the PSG (Program Semantic Graph):

```fsharp
let (|GpioWritePin|_|) (node: PSGNode) : (string * int * uint32) option =
    match node with
    | CallToExtern "HAL_GPIO_WritePin" [gpio; pin; state] ->
        Some (extractGpioInstance gpio, extractPinNum pin, extractState state)
    | _ -> None
```

These patterns compose. A higher-level `(|PeripheralAccess|_|)` pattern can match against multiple hardware operations, providing a unified recognition surface for the code generator. This compositionality comes naturally from Clef's pattern matching. Achieving it through wrapper-based code generation would be awkward.

## CCS Integration

The introduction of CCS (Clef Compiler Services) enabled this architecture. CCS provides native type resolution at the source level, allowing the compiler to understand Fidelity-specific types without BCL dependencies. The integration surface between Farscape and CCS is the `MemoryModel` record:

```fsharp
type MemoryModel = {
    TargetFamily: string
    PeripheralDescriptors: Expr<PeripheralDescriptor> list
    RegisterConstraints: Expr<RegisterConstraint> list
    Regions: Expr<RegionDescriptor list>
    Recognize: PSGNode -> MemoryOperation option
    CacheTopology: Expr<CacheLevel list> option
    CoherencyModel: Expr<CoherencyPolicy> option
}
```

When CCS compiles code that uses Farscape-generated bindings, it consumes this `MemoryModel` during nanopass execution. It decomposes the quotations through pattern matching, enforces the access constraints, and derives volatile semantics from the memory regions. All of this happens at compile time. Nothing remains at runtime except machine code specialized for the target hardware.

## Multiple Microcontroller Families

Our early bare-metal work centered on STM32, reflecting our experience with the Wilderness Labs Meadow and the STM32F7 family. That experience continues: STM32L5 and STM32H7 remain active targets, and the depth of tooling around STM32 makes it valuable for development and validation. The architecture now supports multiple microcontroller families, with a single Farscape pipeline producing target-specific output.

For ARM-based microcontrollers (STM32, NXP, Nordic, Renesas), CMSIS headers provide the hardware definitions. For other architectures, vendor-specific header formats require different parsers, but the output converges to the same quotation-based representation. The downstream pipeline does not change. Only the header parsing varies per vendor.

Our current work on hardware security modules depends on this generalization. Different microcontroller families offer different security features. The Renesas RA family, particularly the RA6M5, has become a focus for our hardware security work. The RA6M5 provides a Hardware Unique Key (HUK) that enables per-device cryptographic identity without requiring external key injection during manufacturing. For applications like Post-Quantum Credential where device authenticity is foundational, the HUK makes device identity a property of the silicon rather than of software policy. Renesas also provides TrustZone support, secure boot, and tamper detection.

Whether targeting STM32 for its ecosystem maturity, Renesas for its security features, or other families for their specific strengths, the Clef source stays unchanged and compiles through a single pipeline to target-specific native code.

The header-parsing route described here is one of two ways onto a microcontroller, and it is the right one when a vendor HAL already exists and belongs in the trusted computing base. A security case can also demand the opposite: no vendor runtime at all, with Clef compiled straight to the reset vector so the audited source is the trusted computing base end to end. The credential work is built in that second direction on the RA6M5, reading the Renesas FSP for register knowledge while running nothing of it. [Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/) sets the two paths side by side and gives the pure-Clef bring-up idiom.

## Beyond Stack Allocation

Stack-only allocation was our first target memory model. Proving that Clef could compile to native code with purely stack-based memory management served two purposes: it validated that our approach could work in the most constrained environments (microcontrollers with kilobytes of RAM), and it demonstrated a clean break from managed runtime ideology. If we could compile Clef without any dynamic memory allocation, we had truly escaped the assumptions that garbage collection embeds in language design.

That constraint was a starting point. The Fidelity architecture has since developed graduated memory management strategies that extend far beyond simple stack allocation, while preserving the deterministic characteristics that make bare-metal deployment possible.

### RAII and Actor-Aware Arenas

The [Olivier actor model](/docs/design/memory/raii-in-olivier-and-prospero/) provides natural boundaries for resource ownership. Each actor owns an arena that lives exactly as long as the actor does. When an actor terminates, its entire memory arena is reclaimed immediately in a single bounded operation, with no scanning phase, no collection heuristics, and no unpredictable pauses. This is RAII (Resource Acquisition Is Initialization) applied to concurrent actor systems, and it scales from microcontrollers to distributed systems.

The [Prospero orchestration layer](/docs/internals/hardware/cache-aware-compilation-cpu/) extends this by configuring arenas based on actor behavior. A high-frequency message processor receives a different arena configuration than a batch data handler. These decisions are made at compile time based on static analysis of access patterns. The result is memory management that adapts to workload characteristics without runtime overhead.

### Context-Aware Compilation

The Composer compiler performs [coeffect analysis](/docs/internals/mlir/context-aware-compilation/) to understand what code needs from its environment. Pure computations with no external dependencies compile differently than code that accesses resources or maintains temporal state. This analysis guides optimization strategies across the pipeline, from type resolution through MLIR generation.

For memory-mapped hardware access, coeffects capture access patterns that inform volatile semantics and cache behavior. The same analysis that determines parallelization strategy also determines whether a peripheral register read requires memory barriers or can be reordered for efficiency.

### Cache-Conscious Memory Management

On modern processors, the performance gap between an L1 cache hit and a main memory access can be fifty-fold or more. [Cache-aware compilation](/docs/internals/hardware/cache-aware-compilation-cpu/) addresses this by making cache behavior a first-class concern throughout the compilation pipeline.

BAREWire's deterministic memory layouts enable precise cache analysis that would be impossible with dynamic allocation. When the compiler knows exactly where every field resides in memory, it can predict which cache lines each access will touch.

### Delimited Continuations as the Unifying Abstraction

Recognizing that [delimited continuations](/docs/design/concurrency/delimited-continuations/) are the common mechanism behind async expressions, actors, and native compilation changed our approach. Async expressions are delimited continuations with I/O-triggered resumption. Actors are delimited continuations with message-triggered resumption. All of them compile through a single DCont dialect, share one set of optimization passes, and draw on common continuation-based memory management.

This unification means that improvements to continuation handling propagate across all features that use them. Continuations are stack-allocated when scope is bounded, and cleanup is deterministic at well-defined points. The async syntax that developers write compiles to native code with the same memory characteristics as hand-written state machines.

### The Spectrum of Control

[Memory Management by Choice](/docs/design/memory/memory-management-by-choice/) captures the philosophy: developers should be able to choose when and where to think about memory. Most code uses sensible defaults. Developers can take explicit control on performance-critical paths. Library authors can apply detailed annotations while application developers consume clean APIs.

This spectrum extends to hardware access. The quotation-based peripheral descriptors that Farscape generates provide high-level abstractions for typical use. Developers who need precise control over register timing or DMA configuration can reach through to the underlying memory model.

## Principled Efficiency

The move from CppSharp to XParsec follows a principle: the tools should embody the same constraints as the output. Fidelity compiles Clef to native code without runtime dependencies. The toolchain that produces Fidelity bindings should itself be written in Clef, free of external dependencies. Our eventual goal is a self-hosted Fidelity toolchain. And while we thought .NET tool interop would persist longer, we're pleased to find that so much of the toolchain already shares native Clef infrastructure, and that self-hosting is closer than we expected.

Build times drop without the CppSharp interop layer. Error messages can gain clarity because the parser understands the domain, and modifications are safer because the pipeline is type-checked end to end.

## Continued Evolution

We are expanding microcontroller family support, improving error diagnostics for malformed headers, and continuing the quotation decomposition work in CCS nanopasses.

Longer term, the architecture that handles microcontroller peripherals extends to other hardware interfaces. ADCs, DACs, DMA controllers, and communication peripherals all have memory-mapped registers with access constraints. The quotation-based approach is designed to reach them without architectural change.

The goal we set at the start holds: Clef on bare metal with zero runtime cost. The path is now built entirely in Clef, with no wrappers and no external dependencies, and the substrate range it serves keeps widening.

---

## Further Reading

### The Foundation

- [Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/): both paths onto a microcontroller, and the trusted-computing-base choice that selects between them
- [Getting to the Heart of Unikernels](/blog/getting-to-the-heart-of-unikernels/): the sealed-image artifact class, its prior art, and the cold-start economics
- [Fidelity Framework: A Primer](/blog/fidelity-framework-primer/): Overview of the native Clef compilation approach
- [Where Native Goes, Mobile Follows]({{< ref "where-native-goes-mobile-follows" >}}): the cross-platform native-compilation thesis these bare-metal targets sit inside
- [RDNA Unified Memory on the Desktop](/docs/internals/memory-fabrics/rdna-unified-memory-desktop/): the desktop-GPU target on the same heterogeneous matrix

### The Companion Entries

- [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/): the dispatch contract across the substrate spectrum, down to Thread and Handler mode
- [Bring-Up Beyond the CPU](/docs/internals/hardware/spatial-bring-up/): the backend legs onto spatial silicon, where registers are compilation outputs

### Memory Architecture

- [Memory Management by Choice](/docs/design/memory/memory-management-by-choice/): The spectrum from automatic to explicit memory control
- [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/): Actor-aware memory management through deterministic lifetimes
- [Cache-Conscious Memory Management](/docs/internals/hardware/cache-aware-compilation-cpu/): From memory-aware to cache-aware compilation
- [Next-Generation Memory Coherence](/docs/internals/memory-fabrics/next-generation-memory-coherence/): Leveraging CXL, NUMA, and PCIe for zero-copy computing

### Compiler Architecture

- [Context-Aware Compilation](/docs/internals/mlir/context-aware-compilation/): How coeffects guide optimization across heterogeneous hardware
- [Delimited Continuations: Fidelity's Turning Point](/docs/design/concurrency/delimited-continuations/): The unifying abstraction for async, actors, and native compilation
- [Baker: A Key Ingredient to Composer](/docs/internals/pipeline/baker-saturation-engine/): Type resolution and the zipper-based correlation pipeline

### Reactive Systems

- [Getting the Signal with BAREWire](/blog/getting-the-signal-with-barewire/): Subscription-free reactive programming across native, web, and edge targets
- [Fidelity.Rx: Native Reactivity in Clef](/blog/fidelityrx-native-reactivity/): Push-based observables with zero allocation overhead
