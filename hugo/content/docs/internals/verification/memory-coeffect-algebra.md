---
title: "Memory Safety as Coeffect Algebra"
linkTitle: "Memory Coeffect Algebra"
description: "How Deterministic Memory Management uses escape classification and coeffect propagation for zero-annotation memory safety"
weight: 30
date: 2026-02-25
authors: ["Houston Haynes"]
tags: ["Memory Management", "Formal Methods", "Coeffects"]
params:
  originally_published: 2026-02-25
  original_url: "https://speakez.tech/blog/automatic-verification-in-clef-lang/"
  migration_date: 2026-02-25
---

> This article is part of the [Transparent Verification](..) series. It builds
> on the [Decidability Sweet Spot](../decidability-sweet-spot) to show how the
> same transparent SMT mechanism is designed to extend from dimensional constraints to memory safety.

The transparent verification model extends beyond dimensional constraints to encompass memory safety. Deterministic Memory Management in Clef is designed to be formalized as a **coeffect discipline** within the same PSG that enforces dimensional consistency.

Effects describe what a computation *does* to its environment (mutation, I/O, exceptions). Coeffects describe what a computation *requires from* its environment (capabilities, resources, contextual assumptions). Memory allocation strategy is a coeffect: a function that allocates from an arena requires that an arena exists in its calling context; a function that places values on the stack requires that the stack frame outlives those values.

## Escape Classification

Classical escape analysis determines whether a value outlives its creating scope. In most compilers, this is a binary classification (escapes or doesn't) used to decide between stack and heap allocation. The analysis runs during optimization, is opaque to the developer, and produces no design-time feedback.

The coeffect model replaces this with a richer taxonomy:

| Escape Classification | Allocation Strategy | Lifetime Bound | Planned Lattice Diagnostic |
|---|---|---|---|
| StackScoped | Stack (`memref.alloca`) | Lexical scope of binding | None (optimal) |
| ClosureCapture(*t*) | Arena (closure environment) | Lifetime of closure *t* | "Captured by closure at line *n*" |
| ReturnEscape | Arena (caller's scope) | Caller's scope | "Escapes via return path" |
| ByRefEscape | Arena (parameter's origin scope) | Origin scope of aliased reference | "Escapes via byref parameter" |

Each classification maps to a specific allocation strategy and lifetime bound. The classification interacts with a lifetime ordering (stack < arena < heap < static), and when any usage of a value demands a lifetime exceeding the value's tentative assignment, the value is promoted:

\[\text{If } \lambda_{\text{required}}(v, \text{use}_i) > \lambda_{\text{tentative}}(v) \text{ for any use } i, \text{ then } \lambda(v) := \max_i(\lambda_{\text{required}}(v, \text{use}_i))\]

The promotion is recorded in the PSG as a coeffect annotation, a visible, navigable property of the graph. Our plans for Lattice include surfacing these promotions directly in the editor, with diagnostics like: "this value was promoted to arena because it is captured by a closure that is returned from the enclosing function."

## Verifying Memory in Practice

This same transparent SMT mechanism is designed to verify concrete memory safety. If a Clef actor allocates a buffer in a Frame Arena to handle high-frequency BAREWire network data, the NTU would derive `QF_LIA` assertions verifying that no references to that buffer escape into a longer-lived scope:

```fsharp
let processNetworkFrame (arena: FrameArena) =
    // Allocated in Arena (Lifetime = 1)
    let payload = Span.zeroCreate<CloseEncounter> 195

    // Z3 verifies that 'payload' never escapes this function's scope
    // or is captured by a closure with a Lifetime >= 2 (Heap).
    BAREWire.Transmit(payload)
```

Z3 proves the lifetime inequality mathematically. The goal is to achieve memory safety without runtime garbage collection, reference counting, or manual lifetime annotations like Rust's tick syntax (`'a`).

## The Push, Bounded, and Poll Models

Developers will interact with the coeffect system through three models that form a spectrum analogous to type annotation in ML-family languages:

**Push model (explicit declaration).** The engineer annotates with explicit coeffect constraints. The PSG reaches saturation quickly.

```fsharp
let processReadings [<Target: x86_64 | xilinx>]
                    [<Memory: arena>]
                    (sensors: Span<float<celsius>>)
                    : ProcessedData =
    // ...
```

**Bounded model (scoped inference).** The engineer provides scope boundaries; the compiler infers within those bounds.

```fsharp
let processReadings () = arena {
    let! readings = readSensors ()
    let summary = summarize readings
    return (readings, summary)
}
```

**Poll model (full inference).** The engineer writes without coeffect annotations. The compiler infers everything from usage context.

```fsharp
let processReadings sensors =
    let readings = sensors |> Span.map (fun s -> s * calibrationFactor)
    summarize readings
```

| Model | Type Analogy | Developer Provides | Compiler Infers | PSG Saturation |
|---|---|---|---|---|
| Push | `let x: int = 5` | Full coeffect constraints | Internal details | Immediate |
| Bounded | `let f (x: int) = ...` | Scope boundaries | Allocation within scope | Fast |
| Poll | `let x = 5` | Nothing | All coeffects from context | Context-dependent |

No model is incorrect. The design-time tooling is planned to exploit these differences to provide "pit of success" guidance. When a function's coeffect resolution varies across call sites, Lattice will display the variation and suggest either a bounded scope or an explicit annotation. The goal is for the tooling to show the *consequences* of each choice, so the engineer can decide how much to specify.

## Escape-Driven Restructuring Guidance

Most compilers treat escape analysis as an optimization detail. A value either escapes or it doesn't, the compiler makes its allocation decision, and the developer never sees the reasoning. If performance is poor, the developer reaches for a profiler, stares at allocation counts, and guesses at which values might be heap-allocated unnecessarily. The feedback loop between "the compiler made a memory decision" and "the developer understands that decision" is broken by design.

The coeffect model is designed to repair this feedback loop. Because escape classification is a first-class property of the PSG, Lattice will be able to present escape paths as navigable, structured information at design time. When the compiler determines that a stack-eligible value must be promoted to arena allocation, the plan is for it to surface the promotion, explain the escape path that caused it, and propose concrete structural alternatives that would change the outcome.

Consider a function that processes sensor readings and returns both the transformed data and a summary:

```fsharp
let processReadings (sensors: Span<float<celsius>>) =
    let readings = sensors |> Span.map (fun s -> s * calibrationFactor)
    let summary = summarize readings
    (readings, summary) // ← readings escapes via return path
```

The `readings` span is allocated within `processReadings`, but it appears in the return tuple. This is a `ReturnEscape` classification: the value's lifetime must extend beyond the creating function's scope, which means it cannot live on the stack. The compiler promotes it to arena allocation. Lattice plans to report the promotion with a diagnostic like: "readings escapes via return path; promoted from stack to arena."

That diagnostic alone would be informative but not actionable. The design goal for Lattice goes further: analyzing the escape path in the PSG and generating concrete restructuring proposals that the developer can evaluate in context. Each proposal would offer a different way to resolve the escape, with different trade-offs in allocation strategy, API shape, and caller responsibility.

The most direct resolution inverts the allocation responsibility. Instead of `processReadings` creating a buffer and trying to return it, the caller provides a pre-allocated destination:

```fsharp
let processReadings (sensors: Span<float<celsius>>)
                    (output: Span<float<celsius>>) =
    sensors |> Span.mapInto output (fun s -> s * calibrationFactor)
    summarize output
```

The escape disappears entirely. The `readings` value no longer exists as a separate allocation; the caller owns the output buffer, controls its lifetime, and can place it on the stack if the calling scope permits. This pattern is familiar in high-performance systems code and maps naturally to the buffer-passing conventions used in embedded and real-time systems. The trade-off is API ergonomics: every caller must now manage an output buffer, which shifts complexity outward.

A second approach preserves the original API's simplicity while keeping the data on the stack. Rather than returning the data, the function accepts a continuation that consumes it while it is still alive:

```fsharp
let processReadings (sensors: Span<float<celsius>>)
                    (k: Span<float<celsius>> -> Summary -> 'a) =
    let readings = sensors |> Span.map (fun s -> s * calibrationFactor)
    k readings (summarize readings)
```

The `readings` span never leaves the scope of `processReadings`. It is created on the stack, passed to the continuation `k`, and deallocated when the function returns. The continuation can do whatever it needs with the data, but it cannot store a reference that outlives the call. This is a well-known functional programming technique for managing resource lifetimes, and the coeffect system would verify that the continuation itself does not re-introduce an escape path. The trade-off here is structural: continuation-passing style changes the control flow of calling code, and deeply nested continuations can reduce readability.

Sometimes, though, arena allocation is genuinely the right answer. The third proposal accepts the promotion and documents the intent explicitly:

```fsharp
let processReadings [<Memory: arena>]
                    (sensors: Span<float<celsius>>) =
    let readings = sensors |> Span.map (fun s -> s * calibrationFactor)
    let summary = summarize readings
    (readings, summary)
```

The `[<Memory: arena>]` annotation tells the compiler and future readers that arena allocation is an intentional design choice, not an accidental promotion. This is appropriate when the function is called within a frame-scoped arena that will be bulk-deallocated at the end of a processing cycle. The PSG reaches saturation immediately because the developer has resolved the ambiguity, and the annotation serves as living documentation of the memory strategy.

### From Escape Paths to Graph Transformations

These proposals would not be templates or heuristics. The escape path that triggers the promotion is a chain of edges in the PSG, from the binding site of `readings` through the tuple construction to the return node. Each restructuring proposal corresponds to a graph transformation. The caller-provided buffer removes the binding node entirely. The continuation-passing style redirects the data flow edge into a callback parameter. The explicit promotion replaces the inferred coeffect with a declared one. Lattice plans to preview these transformations before the developer commits to any of them.

Because these transformations operate on the PSG rather than on surface syntax, they compose with the rest of the verification pipeline. The dimensional constraints on `sensors` (the `float<celsius>` annotation) would propagate correctly through each restructured variant. The memory coeffects would be re-verified after the transformation. The goal is for the developer to see the quantified consequences of each choice, allocation counts, lifetime bounds, API surface changes, and make an informed decision with the compilation graph serving as both the analysis tool and the refactoring engine.

The [next article](../proofs-to-silicon) follows the verified PSG through MLIR lowering, platform-aware representation selection, and the cryptographic release certificate.
