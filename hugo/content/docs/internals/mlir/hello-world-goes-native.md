---
title: "Hello World Goes Native"
linkTitle: "Hello World Goes Native"
description: "Taking Clef to the Metal with MLIR and LLVM - A Case Study"
date: 2025-08-16
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation"]
params:
  originally_published: 2025-08-16
  migration_date: 2026-02-15
---

Compiler development differs from application development. Where application code flows in one direction from input to output, compiler pipelines are multi-stage transformations where decisions at each layer cascade through everything downstream. The Composer compiler now generates native code from Clef source through a nanopass architecture that preserves semantic fidelity at every stage.

## The Sample Under Test

The `03_HelloWorldHalfCurried` sample represents a middle ground in our progressive complexity series. Its name is an arity distinction: the call is partially applied, so the compiler must decide whether that partial application resolves to a direct call or becomes a closure, a decision covered in [Arity On The Side of Caution](/docs/design/structure-and-performance/arity-on-the-side-of-caution/). It exercises Clef's pipe operators, string interpolation, pattern matching on Result types, and console I/O through compiler intrinsics:

```fsharp
module Examples.HelloWorldHalfCurried

/// Demonstrates HALF-CURRIED patterns:
/// - Pipe operator: `x |> f`
/// - Function composition with pipes
/// Uses a helper function to format the greeting with NativeStr
let greet (name: NativeStr) : unit =
    Console.WriteLine $"Hello, {name}!"

let hello() =
    Console.Write "Enter your name: "

    Console.ReadLine()
    |> greet

[<EntryPoint>]
let main argv =
    hello()
    0
```

Several Clef constructs are at work here: statically resolved type parameters (SRTP) in the `readInto` function, discriminated union construction for the Result type, stack-allocated buffers with lifetime management, and the transformation of pipe operators into proper function application sequences. With Composer's nanopass architecture now operational, each of these constructs compiles cleanly to native code.

## The Nanopass Pipeline Architecture

Composer's compilation proceeds through distinct phases, each building on the previous through [small, single-purpose transformations](/docs/internals/concepts/nanopass-navigation/) inspired by the nanopass framework pioneered at Indiana University:

```
Clef Source → FCS → PSG Nanopasses → Alex/Emission → MLIR → LLVM → Native Binary
```

**F# Compiler Services (FCS)** provides parsing, type checking, and semantic analysis. It serves as the canonical source of [Clef language](https://clef-lang.com) semantics, resolving types, inferring constraints, and building the typed abstract syntax tree.

**The Program Semantic Graph (PSG)** correlates FCS's semantic information with syntactic structure through a series of nanopasses. Each pass does exactly one thing: structural construction, type integration, def-use edge creation, and finalization. This separation enables inspection and validation at every stage, with labeled intermediates emitted for debugging.

**Alex** handles the transformation from PSG to MLIR. It implements a fan-out architecture where a single CCS abstraction can emit different code patterns based on target architecture, operating system, and hardware capabilities.

Each phase is self-contained and composable. Once FCS finishes, every later phase reads from the enriched PSG alone, which keeps the bidirectional zipper central to our [coeffect and codata analysis](/docs/internals/concepts/coeffects-and-codata/).

## PSG as Single Source of Truth

The nanopass architecture enforces a critical invariant: once FCS completes its work and the PSG is constructed, all subsequent phases read from the PSG exclusively. This "single source of truth" principle enables the composable transformations that make the compiler tractable.

Consider how type information flows through the pipeline. A function like:

```fsharp
let formatInt (value: int) (buffer: nativeptr<byte>) (maxLength: int) : int = ...
```

Gets its types resolved once during PSG construction, then that resolved information is available to every downstream pass:

```
FUNCTION: Console.Format.formatInt
└── Binding [formatInt] : int -> nativeptr<byte> -> int -> int
```

The emission layer reads from the PSG's `Type` field and produces MLIR. For .NET developers familiar with pointer types, MLIR uses `memref` (memory reference) to represent buffers and arrays. The Clef type `nativeptr<byte>` becomes `memref<?xi8>` (a dynamic-length buffer of bytes):

```mlir
func.func @formatInt(%arg0: i32, %arg1: memref<?xi8>, %arg2: i32) -> i32 {
```

Where .NET might use `Span<byte>` or unsafe pointers, MLIR `memref` provides a typed, bounds-aware abstraction that preserves safety properties through the compilation pipeline.

This separation of concerns supports several downstream consumers over one stable, validated intermediate representation. Coeffect analysis makes optimization decisions, def-use analysis tracks variables, and the bidirectional zipper drives context-aware transformations.

## Def-Use Edges: Making Data Flow Explicit

The nanopass architecture includes a dedicated pass for constructing def-use edges: explicit connections from variable uses to their definitions. This makes data flow a first-class property of the PSG rather than something reconstructed during emission.

```fsharp
// Nanopass 3a: Build symbol definition index
// Nanopass 3b: Create SymbolUse edges from uses to definitions
type FunctionCallInfo = {
    FunctionNode: PSGNode        // The function node (has Type field)
    FunctionName: string         // Display name for MLIR emission
    Arguments: PSGNode list      // Argument nodes
}
```

With explicit def-use edges, the emitter simply follows edges rather than tracking scope imperatively:

```fsharp
// At use site - follow edge to definition
let defNode = followSymbolUseEdge node
let ssaValue = nodeSSAMap[defNode.Id]
emit (Value(ssaValue, ...))
```

This approach mirrors how SSA/MLIR represents data flow and enables the XParsec-based pattern matching that makes Alex's transformations composable. Each nanopass enriches the PSG, and each subsequent pass operates on a richer structure.

## Type Integration and the Bidirectional Zipper

The PSG construction phase includes a crucial type integration step. After building the structural graph from FCS's parsed AST, Composer runs FCS's constraint resolution and correlates the resolved types back to PSG nodes:

```
[TYPE INTEGRATION] CANONICAL resolved type index built: 3302 range mappings, 451 symbol mappings
[TYPE INTEGRATION] Correlating RESOLVED type information with 5898 PSG nodes
[TYPE INTEGRATION] CANONICAL correlation complete: 5844/5898 nodes updated with type information
```

This step captures the results of FCS's sophisticated constraint solving, including [SRTP resolution](/docs/design/types/structural-polymorphism/). When FCS determines that a generic parameter `^T` in `readInto` is actually `StackBuffer<byte>`, that resolved type is stored in the PSG node.

The bidirectional zipper implementation is the forward-looking piece of this architecture. The zipper provides context-aware navigation through the PSG, enabling sophisticated transformations while preserving graph invariants:

```fsharp
module PSGZipper =
    let moveDown: NodeId -> PSGZipper -> PSGZipper option
    let moveUp: PSGZipper -> PSGZipper option
    let followDataFlow: PSGZipper -> PSGZipper option      // Forward flow
    let followDemandFlow: PSGZipper -> PSGZipper option    // Backward flow
 
```

The zipper tracks accumulated coeffects along the path, determining whether a computation is pure, requires async machinery, or accesses external resources. This feeds directly into the compilation strategy selection that Alex uses for hardware-aware code generation.

## MLIR Emission Patterns

With the type information properly flowing through the PSG, MLIR emission becomes more straightforward. The emitter recognizes patterns in the semantic graph and generates appropriate MLIR operations, and critically, the zipper provides the context needed for optimization decisions.

### Discriminated Unions and Pattern Matching

For .NET developers, discriminated unions (DUs) are similar to algebraic data types or tagged unions in other languages. The `Result` type in Clef can be either `Ok` with a success value or `Error` with an error value.

Composer [represents discriminated unions](/spec/draft/discriminated-union-representation/) using MLIR's type system. A Result type compiles to a tagged representation where the tag indicates which case is active:

```mlir
// Result DU layout: tag (i32) + payload space
%c0 = arith.constant 0 : i32  // Tag for Ok case
%c1 = arith.constant 1 : i32  // Tag for Error case
%tag_ref = memref.alloca() : memref<1xi32>
memref.store %c0, %tag_ref[%c0] : memref<1xi32>
```

Pattern matching on Result types loads the tag and branches accordingly:

```mlir
// Match on Result: check which case is active
%tag = memref.load %tag_ref[%c0] : memref<1xi32>
%is_ok = arith.cmpi eq, %tag, %c0 : i32
cf.cond_br %is_ok, ^ok_case, ^error_case
```

This is analogous to how you might pattern match in C# with `switch` expressions, but MLIR makes the control flow explicit through basic blocks and conditional branches.

The emitter uses zipper navigation to traverse pattern match arms, ensuring each path receives proper SSA handling through MLIR's block arguments.

### SSA and Control Flow

MLIR uses Static Single Assignment (SSA) form, where each value is defined exactly once. This is familiar territory for .NET developers who work with compiler optimizations, but MLIR makes it explicit at the IR level.

When control flow merges from multiple paths (like an if/else expression that returns a value), the `cf` dialect uses block arguments to phi-merge values:

```mlir
^then_block:
    %then_result = memref.alloca() : memref<?xi8>
    cf.br ^merge(%then_result : memref<?xi8>)
^else_block:
    %else_result = memref.alloca() : memref<?xi8>
    cf.br ^merge(%else_result : memref<?xi8>)
^merge(%final: memref<?xi8>):
    // %final is the SSA value from whichever branch was taken
    // This is similar to the phi nodes in LLVM IR that .NET JIT generates
 
```

For .NET developers: this is analogous to how the JIT compiler handles branching with PHI nodes, but MLIR keeps it explicit and type-safe at the intermediate representation level.

### Stack Allocation and Resource Safety

The `stackBuffer<byte> 256` expression in Clef generates stack allocation using `memref.alloca`:

```mlir
%buffer = memref.alloca() : memref<256xi8>
// Allocates 256 bytes on the stack, similar to stackalloc in C#
 
```

This is directly analogous to C#'s `stackalloc byte[256]` or using `Span<byte>` backed by stack memory. The memref type tracks both the element type (`i8` for byte) and the size (256 elements).

Resource lifetimes align with continuation boundaries, a principle central to the broader Fidelity vision. Stack-allocated resources release automatically when continuations terminate, while the coeffect system tracks whether computations require more sophisticated resource management.

## Architectural Principles for Native Functional Compilation

The nanopass architecture codifies several principles that enable functional programming to compile efficiently to native code:

**Small, composable passes.** Each nanopass does one thing: add def-use edges, propagate coeffects, resolve generics. This separation enables inspection, testing, and validation at every stage. The output of each pass becomes training data for future optimization learning.

**The intermediate representation is the contract.** The PSG serves as the single source of truth between FCS ingestion and code generation. Everything the emitter needs is present in the PSG. If it is not, the fix belongs in PSG construction, not the emitter.

**Bidirectional traversal for context-aware decisions.** The zipper enables both producer-driven (data flow) and consumer-driven (demand flow) navigation. This duality directly supports the codata patterns that enable efficient streaming without intermediate allocations.

**Explicit data flow.** Making def-use relationships explicit in the PSG mirrors how SSA/MLIR represents data flow. This alignment minimizes transformation complexity during MLIR generation and opens the path toward the Program Hypergraph (PHG) that will enable multi-architecture targeting.

## What This Enables

With the nanopass architecture operational, the Composer compiler now produces native executables from Clef source. The `03_HelloWorldHalfCurried` sample compiles through the full pipeline:

```
FUNCTION: Console.Format.formatInt
└── Binding [formatInt] : int -> nativeptr<byte> -> int -> int
```

Generates:

```mlir
func.func @formatInt(%arg0: i32, %arg1: memref<?xi8>, %arg2: i32) -> i32 {
```

This MLIR then lowers through mlir-opt and LLVM to native machine code. The emission layer reads from the PSG's `Type` field and produces correct MLIR, with the bidirectional zipper enabling context-aware optimization decisions throughout.

On this foundation, Alex's fan-out architecture can now target multiple platforms from the same PSG. A single CCS intrinsic like `Time.currentTicks()` will fan out to Linux syscalls, Windows kernel calls, macOS mach APIs, or ARM register access depending on the target triple. The coeffect analysis determines whether pure computation allows aggressive optimization or whether async boundaries require continuation machinery.

## Looking Forward: From PSG to PHG

The nanopass architecture builds toward the Program Hypergraph (PHG): a temporal hypergraph that captures multi-way relationships and learns from compilation history. Where the current PSG uses binary edges, the PHG will use hyperedges that preserve semantic relationships naturally:

| Current PSG | Future PHG |
|-------------|-----------|
| Binary def-use edges | Variable flow hyperedges |
| Sequential control flow | Async join hyperedges |
| Simple capture lists | Closure context hyperedges |

Each nanopass intermediate emitted today becomes training data for the learning compiler of tomorrow. The labeled `psg_phase_*.json` files enable both debugging and future machine learning on compilation patterns.

The same PHG structure will enable unified compilation across traditional and novel architectures: from LLVM-targeted CPUs to spatial computing and dataflow accelerators. Different "gradients" through the hypergraph emphasize different architectural concerns:

- **Control-flow emphasis** → LLVM IR, traditional CPU optimization
- **Dataflow emphasis** → Spatial kernels, streaming pipelines
- **Hybrid** → Heterogeneous CPU+GPU execution

Native compilation of functional languages has historically required significant compromises, either in the expressiveness of the source language or in the efficiency of the generated code. The Fidelity framework's approach (preserving Clef's full type information through progressive lowering via MLIR to LLVM) aims to avoid these compromises.

The nanopass pipeline, the bidirectional zipper, and the coeffect tracking together compile functional code to native binaries. The temporal PHG we are building toward would carry the hardware model through the full lowering.
