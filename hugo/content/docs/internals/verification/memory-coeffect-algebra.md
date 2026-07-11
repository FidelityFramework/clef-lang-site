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
  migration_date: 2026-02-25
---

The transparent verification model extends beyond dimensional constraints to encompass memory safety. Deterministic Memory Management in Clef is designed to be formalized as a **coeffect discipline** within the same PSG that enforces dimensional consistency.

Effects describe what a computation *does* to its environment (mutation, I/O, exceptions). Coeffects describe what a computation *requires from* its environment (capabilities, resources, contextual assumptions). Memory allocation strategy is a coeffect: a function that allocates from an arena requires that an arena exists in its calling context; a function that places values on the stack requires that the stack frame outlives those values.

## Escape Classification

Classical escape analysis determines whether a value outlives its creating scope. In most compilers, this is a binary classification (escapes or doesn't) used to decide between stack and heap allocation. The analysis runs during optimization, is opaque to the developer, and produces no design-time feedback. The classification also prices hard target budgets: on the [eBPF target](/blog/building-bulletproof-ebpf-programs/), stack bytes across a probe's call chain must sum within the kernel's 512-byte ceiling, an obligation assembled directly from these classifications.

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

## The Coeffect Check as a Tier 2 Hoare Assertion

The lifetime promotion check has a precise reading in Hoare logic, and naming it that way clarifies how the coeffect system fits the broader verification stack.

The lifetime ordering on coeffect classifications (stack < arena < heap < static) is a finite lattice. For each value \(v\) in the PSG, the framework derives a tentative classification from the binding site (typically StackScoped if the binding is local), and for each use site \(\text{use}_i\) it computes the lifetime the use requires. The promotion rule states that if \(\lambda_{\text{required}}(v, \text{use}_i) > \lambda_{\text{tentative}}(v)\) for any use \(i\), the value is promoted. Read as a Hoare triple, the check is \(\{P\}\, C\, \{Q\}\) where \(P\) is "the value is at lifetime \(\lambda_{\text{tentative}}\)," \(C\) is the use sequence in the function body, and \(Q\) is "every use's required lifetime is \(\le\) the value's actual lifetime." The promotion is what makes the postcondition true. The check that the resulting program satisfies the triple is a QF_LIA obligation over the lattice ordering: a single linear inequality per use site, decided by Z3.

This places memory coeffect verification one tier above dimensional consistency in the framework's verification stack. Dimensional types are Tier 1 (free, parametricity, abelian-group equality). The coeffect lattice check is Tier 2 (QF_LIA over a finite lattice, decided by Z3 in microseconds). Both run during PSG elaboration; both are zero-annotation in the common case; both are sound and complete on their respective fragments. The [decidability sweet spot](../decidability-sweet-spot) document treats the design-time pass as weakest-precondition computation and the compile-time re-discharge as the consequence rule, and the same framing applies here: the elaboration computes the weakest coeffect that satisfies all use sites, and each MLIR lowering re-verifies that the lowered representation preserves the coeffect annotation.

## A Sister Sheaf: Access as Coeffect

The coeffect framework as described above tracks *what context the computation requires from its environment*: a stack frame, an arena, a closure environment. A natural extension of the same algebra tracks *who is permitted to execute the computation*: a capability, an access right, an authorization token. The two questions are formally similar (both ask "what does the environment supply?") and both fit the same lattice-ordered Hoare-triple discharge.

Beckmann and Setzer's recent access Hoare logic [(arXiv:2511.01754)](https://arxiv.org/abs/2511.01754) develops the assertional layer for the access-discipline question. Where memory coeffects ensure that a buffer's lifetime fits the scope of every use, access coeffects would ensure that a key share's authorization fits the privilege of every operation. The two systems share a base poset (the compilation pipeline) and a discharge mechanism (Z3 over a finite lattice), and they differ in their stalk category: memory coeffects carry lifetime ordering, access coeffects carry capability ordering. In the [compilation sheaf design document](/docs/design/categorical-foundations/the-compilation-sheaf/), this is the precise statement that memory and access verification are *different sheaves over the same compilation poset*, checked by the same dual-pass machinery without interfering with one another.

The framework currently implements memory coeffects, with capability tracking through the same mechanism for hardware-target capabilities (does the target support exact accumulation, posit arithmetic, the relevant memory regions). Access discipline as a first-class verification target is an adjacent extension that the existing coeffect machinery already supports structurally. The engineering work would lie in choosing the right capability vocabulary and writing the lemma library that gives the discharge actionable diagnostics, rather than in extending the underlying decision procedure.

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

## Arena Hoisting Across the Actor Hierarchy

The escape cases above resolve within a single call chain. The actor model introduces a case the function-local taxonomy does not cover, because an actor owns an arena and that arena's lifetime was modeled as the actor's own. When a value allocated in one actor escapes to another, the actor's termination is no longer the arena's end, and binding the arena to the actor's lifetime is the wrong model. The corrected model is that the arena lives as long as the longest-lived value that escapes it, and the place that value goes is an ancestor in the actor hierarchy.

This is a fifth escape classification, the actor-hierarchy form of `ReturnEscape`:

| Escape Classification | Allocation Strategy | Lifetime Bound | Default Treatment |
|---|---|---|---|
| ActorHierarchyEscape(*a*) | Sentinel-guarded in the actor's own arena, or hoisted to ancestor *a* | Lifetime of ancestor actor *a* | Sentinel guard at the access boundary |

The classification asks the same lifetime question the lattice already encodes, with the escape target resolved to an actor rather than a lexical scope. The answer divides into the same three cases the [deadlock-freedom design](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) draws for liveness, because arena lifetime across the actor hierarchy is a liveness property and is resolved the same way deadlock is, by proving it at build time where the structure is visible and guarding it where it is not.

**Nothing escapes the actor.** The arena's lifetime equals the actor's. Prospero frees it on termination, deterministically. This is the common path and it is unchanged.

**Something escapes to a statically resolvable ancestor.** The compiler knows at build time which ancestor receives the value, so the value's required lifetime is the ancestor's, computed by the same promotion rule the lattice already applies. The escaping allocation is placed in the ancestor's arena at allocation time, which is arena hoisting. The actor's own arena stays a pure no-escape arena, and the actor terminating does not free the hoisted value, because the value never lived in the actor's arena. The ancestor's lifetime bounds it, and the release stays deterministic with no scan.

**Something escapes to an ancestor resolved at runtime.** The receiving actor is chosen by live data, so the compiler cannot place the allocation statically. This is the genuinely dynamic case, the memory analogue of the dynamic deadlock fragment, and it takes the same treatment: the value stays in the actor's own arena and a sentinel guards the escaped reference at the access boundary. The sentinel is not a collector. It is the `Valid | ActorTerminated` check the [Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/) model already carries, an O(1) validity test at exactly the one boundary the compiler could not prove safe. Prospero holds the arena against a counted set of outstanding sentinels and frees it when the set empties, which is a known finite obligation rather than a reachability search.

The distinction that keeps this from being garbage collection is that the set of things that can hold an arena past its actor's death is always known: statically in the first two cases, and as an explicit counted set of outstanding sentinels in the third. A collector frees when a search proves nothing reaches an object. The arena frees when a known obligation set empties. Same outcome, and the mechanism is bounded and deterministic rather than a scan with unpredictable timing.

### The Default and the Hoisting Choice

The default in the statically resolvable case is to leave the escaping allocation in the actor's own arena under a sentinel guard, and to suggest the hoist rather than perform it silently. This is the pit-of-success default because it is the maximally safe posture. The program is correct whether or not the developer ever acts on the suggestion, since the escaped reference is guarded either way. Accepting the suggestion is an optimization: it trades the runtime sentinel check for a compile-time placement in the ancestor's arena, paying with a declaration that makes the lifetime explicit. A developer who ignores the suggestion ships a correct program that carries one runtime guard it could have shed.

The hoist, once accepted, is a normative change to lowering with guaranteed semantics. The allocation moves to the ancestor's arena, the sentinel guard is removed, and the value's lifetime becomes static. The mechanism is fixed. What is configurable is the posture: whether the compiler holds to guard-by-default or elevates aggressively, and how an accepted hoist is recorded, are a declared preference with a pit-of-success default, carried in the project configuration where a second reader of the project can find it. A real-time or [unikernel target](/blog/getting-to-the-heart-of-unikernels/) that wants the thinnest artifact would declare aggressive elevation, shedding guards wherever a static ancestor is provable. The escape analysis produces a correct program under either posture, so the preference moves only where the program sits on the guard-versus-static-placement tradeoff, never whether it is safe. The [Arena Hoisting analyzer](/docs/design/memory/arena-hoisting/) is the surface that presents the suggestion and honors the configured posture.
