---
title: "Clef on Metal Revisited"
linkTitle: "On Metal Revisited"
description: "From Stack-Only to Graduated Memory: A Year Of Fidelity Framework Evolution"
date: 2025-12-28T11:00:00-05:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2025-12-28
  original_url: "https://speakez.tech/blog/fsharp-on-metal-revisited/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

Nearly two years ago, [Clef Goes Metal](https://speakez.tech/blog/fsharp-on-metal---fidelity-lowered-to-stm32/) laid out a vision for running Clef on bare-metal microcontrollers with zero runtime cost. The core proposition remains unchanged: developers should be able to write expressive, idiomatic Clef code that compiles to machine code indistinguishable from hand-written assembly. What has changed, or more accurately has *grown*, is how we achieve that goal.

The original article described Farscape parsing CMSIS headers through CppSharp, a .NET binding to libclang. That approach worked to a certain degree, but it carried baggage that conflicted with our broader philosophy. In specific, this article traces the evolution from wrapper-based parsing to a direct Clef approach. It details how this progression was always seen as inevitable, and describes how the introduction of CCS (Clef Compiler Services) enabled that more principled, more precise architecture. This entry also serves as a survey of the much broader architectural vision which started as an idea two years ago, and now has become manifest in the Fidelity framework's continued development today.

---

## The Problem with Wrappers

CppSharp is a capable tool. It wraps libclang to parse C and C++ code, exposing the AST through .NET types. For the project, borrowing from the .NET ecosystem was always understood as temporary scaffolding. During early prototyping, while we were already working around BCL type machinery and building shadow APIs to test native compilation concepts, CppSharp served its purpose. But as the architecture matured, those borrowed tools introduced contradictions that had to be resolved.

### Wrapper Indirection

The issue was never C++ tooling itself. MLIR is C++. One of Fidelity's strengths is direct binding to C and C++ libraries, whether statically through LTO or dynamically at runtime. That capability is a significant part of what makes native Clef compilation attractive for developers accustomed to the .NET library ecosystem.

The problem with CppSharp was the indirection: C headers flowed through libclang, through .NET bindings, through additional transformation logic, before finally arriving as Clef types. Each layer added complexity. Each layer was a potential source of bugs. And running CppSharp required a .NET runtime during the toolchain's own execution, which felt incongruous even if it didn't affect the compiled output.

### Semantic Mismatch

CppSharp produces an AST designed for C++ semantics. Transforming that AST into Clef abstractions required mapping between fundamentally different type systems. C++ templates, macros, and preprocessor directives do not map cleanly to Clef constructs. Every edge case required special handling, and the edge cases in vendor-provided headers are numerous.

More critically, the transformation lost information that mattered. CMSIS headers use `__I`, `__O`, and `__IO` qualifiers to indicate register access semantics: read-only, write-only, and read-write. These qualifiers are critical for correct code generation. They inform the compiler when volatile semantics are required, when writes trigger hardware actions, when reads return undefined values. CppSharp treated these as type qualifiers to be preserved, but the semantic intent had to be recovered downstream through additional heuristics.

## XParsec: A Typed Parser for Clef

The solution was to parse headers directly, using XParsec. This parser combinator library powers other parts of the Fidelity toolchain, including PSG traversal in the Composer compiler. Using XParsec for header parsing means:

- No external dependencies. The parser is written in Clef, compiled alongside everything else.
- Type-safe parsing. Parse failures produce structured error messages with precise source locations.
- Semantic preservation. The parser captures `__I`, `__O`, and `__IO` qualifiers as first-class constructs, mapping them directly to `AccessKind` values that flow through the compilation pipeline.

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

The parser is compositional. Simple parsers combine into complex ones. Each combinator preserves types, so the compiler catches errors before the parser runs. This is a significant improvement over working with an external tool's output and hoping the transformation logic handles all cases.

## Quotation-Based Output

The larger architectural shift involved what Farscape produces. The original vision described generating P/Invoke-style bindings. The current architecture generates Clef quotations and active patterns.

### Why Quotations?

Quotations are Clef code represented as data. They can be inspected, transformed, and consumed by other parts of the compilation pipeline. When Farscape generates a `PeripheralDescriptor` quotation, it creates a structured representation of hardware memory layout that the CCS nanopass pipeline can decompose:

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

This quotation encodes everything the compiler needs to generate correct memory-mapped access: base addresses, register offsets, access semantics. The CCS pipeline can pattern-match on this structure to apply constraints, validate access patterns, and emit appropriate MLIR.

### Active Patterns for Recognition

Alongside quotations, Farscape generates active patterns that recognize hardware operations in the PSG (Program Semantic Graph):

```fsharp
let (|GpioWritePin|_|) (node: PSGNode) : (string * int * uint32) option =
    match node with
    | CallToExtern "HAL_GPIO_WritePin" [gpio; pin; state] ->
        Some (extractGpioInstance gpio, extractPinNum pin, extractState state)
    | _ -> None
```

These patterns compose. A higher-level `(|PeripheralAccess|_|)` pattern can match against multiple hardware operations, providing a unified recognition surface for the code generator. This compositionality comes naturally from Clef's pattern matching; it would be awkward to achieve through wrapper-based code generation.

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

When CCS compiles code that uses Farscape-generated bindings, it consumes this `MemoryModel` during nanopass execution. The quotations are decomposed via pattern matching. Access constraints are enforced. Memory regions inform volatile semantics. All of this happens at compile time; nothing remains at runtime except the optimal machine code for the target hardware.

## Multiple Microcontroller Families

The original article focused on STM32 as the target platform, reflecting our experience with the Wilderness Labs Meadow and the STM32F7 family. That experience continues: STM32L5 and STM32H7 remain active targets, and the depth of tooling around STM32 makes it valuable for development and validation. The architecture now supports multiple microcontroller families, with the same Farscape pipeline producing target-specific output.

For ARM-based microcontrollers (STM32, NXP, Nordic, Renesas), CMSIS headers provide the hardware definitions. For other architectures, vendor-specific header formats require different parsers, but the output converges to the same quotation-based representation. The downstream pipeline does not change; only the header parsing varies per vendor.

This generalization matters for our current work on hardware security modules. Different microcontroller families offer different security features, and some features are more critical than others for specific applications. The Renesas RA family, particularly the RA6M5, has become a focus for our hardware security work. The RA6M5 provides a Hardware Unique Key (HUK) that enables per-device cryptographic identity without requiring external key injection during manufacturing. For applications like QuantumCredential where device authenticity is foundational, the HUK capability moves security guarantees from software policy into silicon. Renesas also provides TrustZone support, secure boot, and tamper detection, making the RA family well-suited for security-focused embedded applications.

The Fidelity architecture accommodates these variations without becoming fragmented. Whether targeting STM32 for its ecosystem maturity, Renesas for its security features, or other families for their specific strengths, the same Clef source compiles through the same pipeline to optimal native code.

## Beyond Stack Allocation: The Memory Model Matures

The original article emphasized stack-only allocation as the target memory model. This was intentional. Proving that Clef could compile to native code with purely stack-based memory management served two purposes: it validated that our approach could work in the most constrained environments (microcontrollers with kilobytes of RAM), and it demonstrated a clean break from managed runtime ideology. If we could compile Clef without any dynamic memory allocation, we had truly escaped the assumptions that garbage collection embeds in language design.

That constraint was a starting point, not an endpoint. The Fidelity architecture has since developed graduated memory management strategies that extend far beyond simple stack allocation, while preserving the deterministic characteristics that make bare-metal deployment possible.

### RAII and Actor-Aware Arenas

The [Olivier actor model](https://speakez.tech/blog/raii-in-olivier-and-prospero/) provides natural boundaries for resource ownership. Each actor owns an arena that lives exactly as long as the actor does. When an actor terminates, its entire memory arena is reclaimed immediately. No scanning, no heuristics, no unpredictable pauses. This is RAII (Resource Acquisition Is Initialization) applied to concurrent actor systems, and it scales from microcontrollers to distributed systems.

The [Prospero orchestration layer](/docs/design/cache-aware-compilation-cpu/) extends this by configuring arenas based on actor behavior. A high-frequency message processor receives a different arena configuration than a batch data handler. These decisions are made at compile time based on static analysis of access patterns. The result is memory management that adapts to workload characteristics without runtime overhead.

### Context-Aware Compilation

The Composer compiler performs [coeffect analysis](/docs/design/context-aware-compilation/) to understand what code needs from its environment. Pure computations with no external dependencies compile differently than code that accesses resources or maintains temporal state. This analysis guides optimization strategies across the entire pipeline, from type resolution through MLIR generation.

For memory-mapped hardware access, coeffects capture access patterns that inform volatile semantics and cache behavior. The same analysis that determines parallelization strategy also determines whether a peripheral register read requires memory barriers or can be reordered for efficiency.

### Cache-Conscious Memory Management

Modern processors present a fundamental challenge: the performance gap between L1 cache hits and main memory access can be fifty-fold or more. [Cache-aware compilation](/docs/design/cache-aware-compilation-cpu/) addresses this by making cache behavior a first-class concern throughout the compilation pipeline.

BAREWire's deterministic memory layouts enable precise cache analysis that would be impossible with dynamic allocation. When the compiler knows exactly where every field resides in memory, it can predict which cache lines each access will touch. This transforms cache optimization from speculation to calculation.

### Delimited Continuations as Connective Tissue

The realization that [delimited continuations](/docs/design/delimited-continuations/) form the connective tissue between async expressions, actors, and native compilation transformed our entire approach. Async expressions are delimited continuations with I/O-triggered resumption. Actors are delimited continuations with message-triggered resumption. All of them compile through the same DCont dialect, share the same optimization passes, and benefit from the same continuation-based memory management.

This unification means that improvements to continuation handling propagate across all features that use them. Stack-allocated continuations when scope is bounded. Deterministic cleanup at well-defined points. The async syntax that developers write compiles to native code with the same memory characteristics as hand-written state machines.

### The Spectrum of Control

[Memory Management by Choice](https://speakez.tech/blog/memory-management-by-choice/) captures the philosophy: developers should be able to choose when and where to think about memory. Most code uses sensible defaults. Performance-critical paths can take explicit control. Library authors can leverage detailed annotations while application developers consume clean APIs.

This spectrum extends to hardware access. The quotation-based peripheral descriptors that Farscape generates provide high-level abstractions for typical use. Developers who need precise control over register timing or DMA configuration can reach through to the underlying memory model. The framework provides defaults without imposing ceilings.

## Principled Efficiency

Moving from CppSharp to XParsec was not merely a technical preference. It reflects a principle: the tools should embody the same constraints as the output. Fidelity compiles Clef to native code without runtime dependencies. The toolchain that produces Fidelity bindings should itself be comprised of Clef without external dependencies. Our eventual goal is for the entire Fidelity framework toolchain to be self-hosted. And while we thought .NET tool interop would persist longer, we're pleased to find all of these natural "shared edges" that promote a native Clef approach that easily extends to self-hosting.

This principle also has practical near-term benefits. Build times are faster without the CppSharp interop layer. Error messages can provide greater clarity because the parser understands the domain. Modifications are safer because the entire pipeline is type-checked. But the principle also has philosophical weight. When the toolchain follows the same rules as the generated code, the mental model stays coherent throughout.

## What Comes Next

The Farscape and CCS integration is functional, but work continues. Current efforts focus on expanding microcontroller family support, improving error diagnostics for malformed headers, and optimizing the quotation decomposition in CCS nanopasses.

Longer term, the same architecture that handles microcontroller peripherals will handle other hardware interfaces. ADCs, DACs, DMA controllers, and communication peripherals all have memory-mapped registers with access constraints. The quotation-based approach scales to these without architectural changes.

The vision from the original article remains: Clef on bare metal with zero runtime cost. The path has changed. It is now a path built entirely in Clef, without wrappers, without external dependencies, without compromises to the principles that define the framework.

---

## Further Reading

### The Foundation

- [Clef Goes Metal](https://speakez.tech/blog/fsharp-on-metal---fidelity-lowered-to-stm32/): The original vision for bare-metal Clef (updated December 2025)
- [Fidelity Framework: A Primer](https://speakez.tech/blog/fidelity-framework-a-primer/): Overview of the native Clef compilation approach

### Memory Architecture

- [Memory Management by Choice](https://speakez.tech/blog/memory-management-by-choice/): The spectrum from automatic to explicit memory control
- [RAII in Olivier and Prospero](https://speakez.tech/blog/raii-in-olivier-and-prospero/): Actor-aware memory management through deterministic lifetimes
- [Cache-Conscious Memory Management](/docs/design/cache-aware-compilation-cpu/): From memory-aware to cache-aware compilation
- [Next-Generation Memory Coherence](https://speakez.tech/blog/next-generation-memory-coherence/): Leveraging CXL, NUMA, and PCIe for zero-copy computing

### Compiler Architecture

- [Context-Aware Compilation](/docs/design/context-aware-compilation/): How coeffects guide optimization across heterogeneous hardware
- [Delimited Continuations: Fidelity's Turning Point](/docs/design/delimited-continuations/): The unifying abstraction for async, actors, and native compilation
- [Baker: A Key Ingredient to Composer](/docs/design/baker-saturation-engine/): Type resolution and the zipper-based correlation pipeline

### Reactive Systems

- [Getting the Signal with BAREWire](https://speakez.tech/blog/getting-the-signal-with-barewire/): Subscription-free reactive programming across native, web, and edge targets
- [AlloyRx: Native Reactivity in Fidelity](https://speakez.tech/blog/alloyrx-native-reactivity-in-fidelity/): Push-based observables with zero allocation overhead
