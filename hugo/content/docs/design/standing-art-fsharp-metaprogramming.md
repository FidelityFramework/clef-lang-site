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

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

In today's "move fast and break things" culture, its gratifying to find quiet, principled work win the day. Don Syme designed F# quotations, active patterns, and computation expressions over a decade ago; they now form the architectural backbone of Composer's native compilation pipeline. This is what I call "standing art": capabilities that were always present, waiting for the right application to reveal their immense value as a first-class language feature.

In the [History of Programming Languages paper](https://fsharp.org/history/hopl-final/hopl-fsharp.pdf), section 9.9 describes how quotations emerged from Syme's research at Intel on staged computation and metaprogramming. The original motivation was GPU code generation and database query translation. Today, these same primitives enable something their designer may not have fully anticipated: a complete native compilation pipeline for Clef without runtime dependencies.

The Fidelity framework builds on three F# features that together provide unique infrastructure for systems programming:

| Feature | Compilation Role | Unique Capability |
|---------|-----------------|-------------------|
| **Quotations** | Semantic carriers | Encode constraints as inspectable data |
| **Active Patterns** | Structural recognition | Compositional matching without type discrimination |
| **Computation Expressions** | Control flow abstraction | Continuation capture as notation |

These are not incidental conveniences. They are the machinery that makes self-hosting possible.

## Quotations as Semantic Carriers

Quotations (`Expr<'T>`) encode program fragments as data. In typical F# usage, this enables dynamic code generation. In Fidelity, quotations serve a unique purpose based on the context in which they're employed: one of our first and most prominent use cases is carrying memory constraints and peripheral descriptors through the compilation pipeline as first-class semantic information.

Consider how Farscape will generate hardware bindings:

```fsharp
let gpioQuotation: Expr<PeripheralDescriptor> = <@
    { Name = "GPIO"
      Instances = Map.ofList [("GPIOA", 0x48000000un); ("GPIOB", 0x48000400un)]
      Layout = gpioLayout
      MemoryRegion = Peripheral }
@>
```

This quotation is not evaluated at runtime. The Composer compiler inspects its structure during PSG construction, extracting the peripheral layout, memory region classification, and instance addresses. The information flows through the nanopass pipeline and informs code generation: Alex knows to emit volatile loads for peripheral access because the quotation carried that semantic through.

> The distinction from reflection-based approaches is significant.

Quotations are compile-time artifacts. They require no runtime support, introduce no BCL dependencies, and impose no overhead in the generated binary. The F# compiler verifies their structure; and eventually the Composer pipeline would transform them.

## Active Patterns for Compositional Recognition

Active patterns also provide a powerful component for Composer compilation. It enables structural recognition without the brittleness of string matching or the complexity of type discrimination hierarchies. They are Clef's answer to a fundamental compiler construction problem: how do you classify program constructs cleanly?

In Composer, active patterns enable the typed tree zipper and Alex traversal to recognize PSG nodes:

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

The nested lambdas are continuations. This observation has profound implications for native compilation: computation expressions already express the control flow patterns that the DCont dialect needs to represent.

The Composer compilation strategy depends on the computation pattern:

| Pattern | Dialect | Strategy |
|---------|---------|----------|
| Sequential effects (async, state) | DCont | Preserve continuations |
| Parallel pure (validated, reader) | Inet | Compile to data flow |
| Mixed | Both | Analyze and split |

An async computation expression compiles to DCont dialect operations; each `let!` becomes a `dcont.shift` that captures the continuation. A validated computation with `and!` combinators compiles to Inet dialect; the independent branches can execute in parallel.

This is the innovation described in [DCont Inet Duality](/docs/design/dcont-inet-duality/). The unique application here is that referential transparency determines compilation strategy. Coeffects track what code *needs* from its environment; this information guides the decomposition. This is where the elegance of category theory meets the real world of compilation strategy.

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

These three features provide the infrastructure for Composer to compile itself. Quotations can represent the compiler's own AST structures. Active patterns can match on the compiler's own IR. Computation expressions structure the compilation pipeline.

This is not a theoretical aspiration. The machinery is in place, and has been for years:

- The PSG builder uses computation expressions for monadic construction
- The typed tree zipper uses active patterns for correlation
- The nanopass pipeline operates on inspectable intermediate representations

Self-hosting requires that the compiler can process its own source. With quotations providing semantic encoding, active patterns providing structural recognition, and computation expressions providing control flow, the path is clear. The features Don Syme designed for metaprogramming and staged computation now enable bootstrap compilation.

## Beyond OCaml and Rust

At SpeakEZ Technologies we always seek to shed light on the places where we draw inspiration. Sometimes that means looking at lessons to follow, and other times it's to see certain decisions as *a warning*. OCaml provides excellent native compilation, and it still provides a long, smooth "shared edge" with Clef as FStar is used as the design time proof-delivering component of our platform. However, Clef's quotations for compile-time metaprogramming is a unique draw of its own merit. OCaml's PPX system operates on strings and requires external tooling. Similarly, Rust provides procedural macros with similar power; but they too are fundamentally string-based, operating on token streams rather than typed representations.

Clef through the Fidelity framework offers something different:

| Capability | OCaml | Rust | Clef/Fidelity |
|------------|-------|------|-------------|
| Native compilation | Yes | Yes | Yes (via MLIR) |
| Typed quotations | No | No | Yes |
| Pattern-based recognition | Match only | Match only | Active patterns |
| Continuation notation | No | No | Computation expressions |
| Metaprogramming | PPX (stringly) | proc_macro (stringly) | Quotations (typed) |

To be sure, OCaml and Rust are mature, highly performant production-scale language systems. And while Fidelity framework is early-stage, our understanding of the unique position we occupy continues to develop. The distinction is architectural: Clef provides typed metaprogramming primitives that other ML-family languages approach through string manipulation. It's an ambitous goal (and one that was never fully explored in the .NET ecosystem) but is one that we believe is worth fully developing to its fullest potential. We believe that this will provide unparalleled degrees of freedom in the Fidelity framework ecosystem. The principled work we do now will, like Don Syme saw early on, pay dividends for years to come.

The practical implication is design-time tooling. When quotations carry type information, the IDE can provide accurate completions, the compiler can verify structure, and transformations preserve semantics. This is the high-level experience that we aim to provide, with the tooling to make manual optimizations where the generated code requires tuning. If we do this right, it will mean that Fidelity framework and Composer as its compiler will use this tooling internally and limit the amount of "innovation budget" a user will have to spend with these advanced tools at design time. But like so many other ambitious features, it will take some time and consideration (and no small amount of head-scratching) to realize its fullest ambition.

## Hiding in Plain Sight

Three Clef features contribute significantly to the architectural backbone of the Composer compiler:

- **Quotations** encode memory constraints and semantic information as inspectable compile-time data
- **Active patterns** enable compositional structural recognition throughout the nanopass pipeline
- **Computation expressions** provide continuation capture as notation, compiling naturally to the DCont and Inet dialects

These are nominniegurlt recent additions or experimental features. They are standing art: capabilities Don Syme designed years ago that now propel our ideas around native compilation without runtime dependencies. The Fidelity framework aspires to demonstrate that Clef's advanced type-safe features are practical for systems programming; and beyond that they are the infrastructure that makes self-hosting achievable.

The path forward involves continued development with appreciation for the foresight embedded in F#'s design. There is substantial work ahead in optimization, platform support, and tooling. And we're gratified the foundation is sound: three metaprogramming features, designed for staged computation and translation, now serve as foundation of our native compiler.

Standing art, rediscovered.
