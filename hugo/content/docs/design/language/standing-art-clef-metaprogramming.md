---
title: "Standing Art: Clef Metaprogramming Features in the Composer Compiler"
linkTitle: "Clef Metaprogramming"
description: "How Composer's unique value pivots on Don Syme's principled designs"
date: 2025-12-21T00:00:00+00:00
authors:
  - SpeakEZ
tags: ["architecture", "innovation", "design", "metaprogramming", "clef-lang"]
params:
  originally_published: 2025-12-21T00:00:00+00:00
  original_url: "https://speakez.tech/blog/standing-art-fsharp-metaprogramming-in-firefly/"
  migration_date: 2026-02-15
---

In today's "move fast and break things" culture, it's gratifying to find quiet, principled work win the day. Don Syme designed F# quotations, active patterns, and computation expressions over a decade ago; they now form the architectural backbone of our Composer native compilation pipeline. This is what I call "standing art": capabilities that were always present, waiting for the right application to put them to use as first-class language features.

In the [History of Programming Languages paper](https://fsharp.org/history/hopl-final/hopl-fsharp.pdf), section 9.9 describes how quotations emerged from Syme's research at Intel on staged computation and metaprogramming. The original motivation was GPU code generation and database query translation. Today, these same primitives enable something their designer may not have fully anticipated: a complete native compilation pipeline for Clef without runtime dependencies.

Our Fidelity framework builds on three features Clef inherits from F#, which together provide infrastructure for systems programming:

| Feature | Compilation Role | Unique Capability |
|---------|-----------------|-------------------|
| **Quotations** | Semantic carriers | Encode constraints as inspectable data |
| **Active Patterns** | Structural recognition | Compositional matching without type discrimination |
| **Computation Expressions** | Control flow abstraction | Continuation capture as notation |

Together they are the machinery that makes self-hosting possible.

## Quotations as Semantic Carriers

Quotations (`Expr<'T>`) encode program fragments as data. In typical F# usage, this enables dynamic code generation. In our Fidelity framework, quotations serve a purpose set by the context in which they're employed: one of our first use cases is [carrying memory constraints and peripheral descriptors](/spec/draft/special-attributes-and-types/#memory-region-attributes) through the compilation pipeline as first-class semantic information.

Consider how Farscape will generate hardware bindings:

```fsharp
let gpioQuotation: Expr<PeripheralDescriptor> = <@
    { Name = "GPIO"
      Instances = Map.ofList [("GPIOA", 0x48000000un); ("GPIOB", 0x48000400un)]
      Layout = gpioLayout
      MemoryRegion = Peripheral }
@>
```

This quotation is not evaluated at runtime. Our Composer compiler is designed to inspect its structure during PSG construction, extracting the peripheral layout, memory region classification, and instance addresses. The information flows through the nanopass pipeline and informs code generation: Alex emits volatile loads for peripheral access because the quotation carried that semantic through.

This differs from reflection-based approaches. Quotations are compile-time artifacts. They require no runtime support, introduce no BCL dependencies, and impose no overhead in the generated binary. The F# compiler verifies their structure, and our Composer pipeline is designed to transform them.

## Active Patterns for Compositional Recognition

Active patterns are a second component our Composer compilation draws on. They enable structural recognition without the brittleness of string matching or the complexity of type discrimination hierarchies. They are Clef's answer to a recurring compiler construction problem: how do you classify program constructs cleanly?

In our Composer, active patterns let the typed tree zipper and Alex traversal recognize PSG nodes:

```fsharp
let (|PeripheralAccess|_|) (node: PSGNode) =
    match node with
    | CallToExtern name args when isPeripheralBinding name ->
        Some (extractPeripheralInfo args)
    | _ -> None

let (|SRTPDispatch|_|) (node: PSGNode) =
    match node.TypeCorrelation with
    | Some { SRTPResolution = Some srtp } -> Some srtp
    | _ -> None
```

These patterns compose with `&` and `|`; they can be tested in isolation; they encapsulate recognition logic. The traversal code becomes declarative:

```fsharp
match currentNode with
| PeripheralAccess info -> emitVolatileAccess info
| SRTPDispatch srtp -> emitResolvedCall srtp
| _ -> emitDefault node
```

The alternative would be a nested conditional structure that mixes recognition with action, or a visitor pattern that spreads classification across multiple methods. Active patterns keep the structure visible and the logic local.

## Computation Expressions as Delimited Continuations

As explained in detail in other blog posts, every `let!` in a computation expression is syntactic sugar for continuation capture:

```fsharp
maybe {
    let! x = someOption
    let! y = otherOption
    return x + y
}
```

Desugars to:

```fsharp
builder.Bind(someOption, fun x ->
    builder.Bind(otherOption, fun y ->
        builder.Return(x + y)))
```

The nested lambdas are continuations. This observation carries directly into native compilation: computation expressions already express the control flow patterns that our DCont dialect needs to represent.

The Composer compilation strategy depends on the computation pattern:

| Pattern | Dialect | Strategy |
|---------|---------|----------|
| Sequential effects (async, state) | DCont | Preserve continuations |
| Parallel pure (validated, reader) | Inet | Compile to data flow |
| Mixed | Both | Analyze and split |

An async computation expression is designed to compile to DCont dialect operations, where each `let!` becomes a `dcont.shift` that captures the continuation. A validated computation with `and!` combinators compiles to Inet dialect, where the independent branches can execute in parallel.

We describe this construction in [DCont Inet Duality](/docs/design/concurrency/dcont-inet-duality/). The application here is that referential transparency determines compilation strategy. Our coeffect system tracks what code *needs* from its environment, and this information guides the decomposition.

The MLIR builder is itself a computation expression:

```fsharp
let emitFunction (node: PSGNode) : MLIR<Val> = mlir {
    let! funcType = deriveType node
    let! entry = createBlock "entry"
    do! setInsertionPoint entry
    let! result = emitBody node.Body
    do! emitReturn result
    return result
}
```

The compiler's internal structure mirrors the patterns it compiles.

## The Self-Hosting Path

These three features provide the infrastructure for our Composer to compile itself. Quotations can represent the compiler's own AST structures. Active patterns can match on the compiler's own IR. Computation expressions structure the compilation pipeline.

The underlying language machinery has been in place for years:

- The PSG builder uses computation expressions for monadic construction
- The typed tree zipper uses active patterns for correlation
- The nanopass pipeline operates on inspectable intermediate representations

Self-hosting requires that the compiler can process its own source. Quotations provide the semantic encoding, active patterns provide the structural recognition, and computation expressions provide the control flow. The features Don Syme designed for metaprogramming and staged computation are what we intend to carry into bootstrap compilation.

## Beyond OCaml and Rust

We draw inspiration from several language systems, some as a lesson to follow and some as a decision to read as *a warning*. OCaml provides native compilation, and it shares a long edge with Clef, since we plan to use F* as the design-time proof-delivering component of our platform. Clef's quotations for compile-time metaprogramming have a separate draw. OCaml's PPX system operates on strings and requires external tooling. Rust provides procedural macros of comparable reach, but they too are string-based, operating on token streams rather than typed representations.

Clef through the Fidelity framework offers something different:

| Capability | OCaml | Rust | Clef/Fidelity |
|------------|-------|------|-------------|
| Native compilation | Yes | Yes | Yes (via MLIR) |
| Typed quotations | No | No | Yes |
| Pattern-based recognition | Match only | Match only | Active patterns |
| Continuation notation | No | No | Computation expressions |
| Metaprogramming | PPX (stringly) | proc_macro (stringly) | Quotations (typed) |

OCaml and Rust are mature, performant, production-scale language systems. Our Fidelity framework is early-stage, and our understanding of the position we occupy continues to develop. The distinction is architectural: Clef provides typed metaprogramming primitives that other ML-family languages approach through string manipulation. We have found no other representative implementations of this approach in the .NET-lineage literature we have reviewed, and we think it is worth developing further. The principled work we do now will, as Don Syme saw early on, pay dividends for years to come.

The practical implication is design-time tooling. When quotations carry type information, the IDE can provide accurate completions, the compiler can verify structure, and transformations preserve semantics. This is the experience we aim to provide, with the tooling to make manual optimizations where the generated code requires tuning. We intend for our Fidelity framework and Composer to use this tooling internally, which would limit the "innovation budget" a user spends on these design-time tools. Realizing that intent will take time and consideration, and no small amount of head-scratching.

## Hiding in Plain Sight

Three Clef features form the architectural backbone of our Composer compiler:

- **Quotations** encode memory constraints and semantic information as inspectable compile-time data
- **Active patterns** enable compositional structural recognition throughout the nanopass pipeline
- **Computation expressions** provide continuation capture as notation, compiling naturally to the DCont and Inet dialects

All three predate our framework by years and have been stable in F# the whole time. They are standing art: capabilities Don Syme designed years ago that now carry our ideas around native compilation without runtime dependencies. Our Fidelity framework aims to show that the type-safe features Clef inherits are practical for systems programming, and that they are the infrastructure self-hosting rests on.

The path forward involves continued development with appreciation for the foresight embedded in F#'s design. There is substantial work ahead in optimization, platform support, and tooling. We're gratified the foundation is sound, and we will keep building on these three features, designed for staged computation and translation, as our native compiler takes shape.
