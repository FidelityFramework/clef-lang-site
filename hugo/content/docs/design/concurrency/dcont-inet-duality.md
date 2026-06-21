---
title: "The DCont/Inet Duality"
linkTitle: "DCont/Inet Duality"
description: "How computation expressions split across three lowering lanes: delimited continuations for sequential effects, the tensor path for dense data parallelism, and interaction nets for irregular reduction"
date: 2025-09-25T00:00:00-04:00
weight: 40
authors: ["Houston Haynes"]
tags: ["Architecture", "Performance", "Innovation"]
params:
  originally_published: 2025-09-25
  original_url: "https://speakez.tech/blog/dcont-inet-duality/"
  migration_date: 2026-02-15
---

Clef's computation expressions give a unified syntax for control flow that would otherwise be written as explicit branching. Beneath that syntax sits a mathematical structure most compilers do not exploit. A region that threads each step through the result of the last must sequence, and a region whose steps carry no such dependency can run them together. That partition between dependency-carrying and independent work is what our Fidelity framework reads off the source to choose a compilation strategy, and we are designing a compilation path around it to reach zero-cost computation graphs. The dependent side lowers through delimited continuations. The independent side lowers to a parallel target, and which target depends on the shape of the work.

The spine of this is the monad/applicative axis. A monad sequences because the second effect can depend on the first value, which is the DCont case; an applicative composes independent effects and is therefore parallelizable, which is the Inet and tensor case. McBride and Paterson named that distinction in [*Applicative programming with effects*](https://www.staff.city.ac.uk/~ross/papers/Applicative.html), and it is the precise account of what the compiler is partitioning.

This builds on the architectural foundations we've established across the Fidelity framework - from our [coeffect analysis for context-aware compilation](https://speakez.tech/blog/context-aware-compilation/) to our [exploration of continuation preservation](https://speakez.tech/blog/the-continuation-preservation-paradox/), and from our [reactive programming model](https://speakez.tech/blog/alloyrx-native-reactivity-in-fidelity/) to our [approach to referential transparency](https://speakez.tech/blog/seeking-referential-transparency/).

## A Principled Start

To understand how computation expressions compile to optimal machine code, we need to first understand what they actually are beneath the syntax. Let's begin with a problem every developer faces: repetitive code patterns that obscure the essential logic.

Consider processing data with error handling. Without computation expressions, we're forced to write explicit branching at every step:

```fsharp
// Explicit error handling - the pattern dominates the logic
let divideNumbers init x y z =
    let a = init |> divideBy x
    match a with
    | None -> None      // Bail out on error
    | Some a' ->        // Continue with success
        let b = a' |> divideBy y
        match b with
        | None -> None  // Bail out again
        | Some b' ->    // Continue again
            let c = b' |> divideBy z
            match c with
            | None -> None
            | Some c' -> Some c'  // Finally!
```

The actual logic - dividing numbers in sequence - is buried under error-handling boilerplate. Computation expressions let us avoid this pattern:

```fsharp
// Same logic, but the pattern is hidden
let divideNumbers init x y z = maybe {
    let! a = init |> divideBy x
    let! b = a |> divideBy y
    let! c = b |> divideBy z
    return c
}
```

The `let!` is syntactic sugar for a specific pattern of function composition. When we write `let! x = expr in body`, the compiler transforms it into `builder.Bind(expr, fun x -> body)`. That transformation is the point: computation expressions thread continuations through a computation.

## The Continuation Connection

To see why this matters for compilation, let's make the continuation pattern explicit. Every `let` binding can be rewritten as a function application:

```fsharp
// A normal let binding
let x = 42 in
let y = 43 in
x + y

// Can be rewritten using continuations
42 |> (fun x ->
    43 |> (fun y ->
        x + y))
```

This transformation from `let` to continuation-passing is the foundation for how computation expressions work. The `let!` in a computation expression makes the pattern explicit and interceptable:

```fsharp
// The maybe builder intercepts at each continuation point
type MaybeBuilder() =
    member _.Bind(m, continuation) =
        match m with
        | None -> None                // Short-circuit
        | Some x -> continuation x    // Continue

// Each let! becomes a Bind call
maybe {
    let! x = someOption    // Bind(someOption, fun x -> ...)
    let! y = otherOption   // Bind(otherOption, fun y -> ...)
    return x + y           // Return(x + y)
}
```

Once computation expressions are read as continuations, the question becomes what happens when we compile these patterns. This is where our Fidelity framework's approach diverges from a conventional compiler.

## The Fork: Sequential vs Parallel

Once we recognize computation expressions as continuation structures, we face a compilation decision. Some computations require sequential threading of continuations, where each step depends on the previous one. Others carry no such dependencies, so all operations could in principle execute at once.

This distinction determines the compilation strategy for the whole expression. As we explored in our [coeffects and codata analysis](/docs/internals/concepts/coeffects-and-codata/), different computational patterns call for different execution strategies. The independent side splits once more by the shape of the work, which gives three lowering lanes rather than two.

### Three Lowering Lanes

The unit of analysis is the region, classified by its effect and data dependency rather than by the computation expression keyword. Our [Program Hypergraph](https://speakez.tech/blog/coupling-and-cohesion/) carries that classification as a coloring pass over the graph, the same pass that licenses the interaction-net breakout. Three lanes come out of it.

- **Sequential effects lower through DCont.** A region whose steps depend on prior results, or that touches the world, captures its continuation at each suspension point and resumes when the result arrives. This is the dependency-carrying lane.
- **Regular data-parallel work lowers to the tensor path.** Dense, statically shaped, rectangular work, map and filter and reduce and scan over arrays, is SIMD and SIMT data parallelism. It lowers through the standard arithmetic and tensor dialects and the GPU dialect to NVVM for NVIDIA targets and the AMDGPU backend for AMD, with MLIR-AIE carrying NPU targets. Its iterations are independent by construction, so there is no graph rewriting to coordinate.
- **Irregular higher-order reduction lowers to interaction nets.** Recursive functions over trees and graphs, symbolic reduction, and sharing-sensitive computation whose shape is data-dependent are the genuine interaction-net workload. The Inet graph lowers through MLIR to LLVM, and the LLVM NVPTX and AMDGPU backends generate the GPU code. The actor layer orchestrates the CPU-to-GPU boundary with zero-copy BAREWire transport and re-bootstraps the computation graph as the work demands.

The flat map-and-filter query is the case that least needs interaction nets. Dense data parallelism falls out as the rectangular, degenerate case and is handled by the tensor path. Interaction nets earn their keep on the irregular reductions where the rewrite shape is not known until the data arrives.

### Sequential Patterns: The DCont Path

When operations must happen in sequence, because each depends on the previous result or involves effects, we need delimited continuations. These capture "the rest of the computation" at specific points:

```fsharp
// Sequential: each step depends on the previous
let processOrder order = async {
    let! inventory = checkInventory order.Items    // Must complete first
    let! pricing = calculatePricing order          // Needs inventory
    let! shipping = estimateShipping order         // Needs pricing
    return combinedResult inventory pricing shipping
}
```

Our Composer compiler is designed to recognize this pattern and lower it to MLIR's DCont dialect:

```mlir
// DCont: explicit continuation capture and resumption
dcont.func @processOrder(%order: !order) -> !result {
    %k1 = dcont.shift {
        %inventory = call @checkInventory(%order.items)
        dcont.resume %k1 %inventory
    }

    %k2 = dcont.shift {
        %pricing = call @calculatePricing(%order, %inventory)
        dcont.resume %k2 %pricing
    }

    %k3 = dcont.shift {
        %shipping = call @estimateShipping(%order, %pricing)
        dcont.resume %k3 %shipping
    }

    %result = call @combine(%inventory, %pricing, %shipping)
    dcont.reset %result
}
```

Each `dcont.shift` captures the continuation at that point, "the rest of the computation," so the operation can suspend and later resume. In this design async operations run without allocating Tasks or using thread pools. Where these effectful regions cross actor boundaries through synchronous reply, their liveness is a separate obligation, the acyclicity check we develop in [deadlock freedom as an obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/).

### Regular Data-Parallel: The Tensor Path

The simplest independent case is a query over dense, rectangular data:

```fsharp
// Pure query: every row checked and transformed independently
let analysis = query {
    for sale in sales do
    where (sale.Amount > 1000.0)
    select (sale.Region, sale.Amount * 1.1)
}
```

Here there is no need for sequential continuations and no need for interaction nets. Every filter check and every projection is a lane in a SIMD or SIMT computation over a statically shaped array. This is the tensor path, lowering through the standard arithmetic and tensor dialects:

```mlir
// Tensor path: dense data parallelism, independent lanes
func.func @analysis(%sales: tensor<?x!sale>) -> tensor<?x!proj> {
  %mask = tosa.greater %amounts, %threshold : tensor<?xf64>
  %proj = linalg.map { extract_region_amount } ins(%sales) outs(%init)
  %out  = tensor.gather %proj, %mask
  return %out : tensor<?x!proj>
}
```

The loop and lane structure comes from the `affine`, `scf`, and `vector` dialects, and the GPU dialect carries it to NVVM or the AMDGPU backend. Independence here is trivial: the iterations do not interact, so confluence is immediate and there is no rewrite to schedule.

### Irregular Reduction: The Inet Path

Interaction nets earn their place on a different kind of work. A recursive reduction over a tree, where the shape of the rewrite is not known until the data arrives, has parallelism that is real but not rectangular. Two redexes in different subtrees can fire at once, and a third opens up only after one of them completes.

```fsharp
// Irregular: the reduction shape depends on the data
let rec eval expr =
    match expr with
    | Lit n        -> n
    | Add (a, b)   -> eval a + eval b      // both subtrees reduce independently
    | Let (x, e, b) -> eval (subst x (eval e) b)   // sharing through subst
```

This lowers to an interaction net, the model where computation is local graph rewriting and the agents make copying and discarding explicit. Lafont's symmetric interaction combinators give the vocabulary: a constructor agent builds structure, a duplicator agent copies it, and an eraser agent discards it. Coll's Inet dialect carries these agents and their rewrite rules in MLIR:

```mlir
// Inet: an active pair is two agents on the same wire; reducing it is one rule
inet.rule @add_lit {
  // (Add ⋈ Lit) annihilates and rewrites to the summed cell
  %a = inet.agent "Add" : !inet.cell
  %l = inet.agent "Lit" : !inet.cell
  inet.active_pair %a, %l           // the redex
  // ... rewrite to the reduced net
}
```

An active pair is two agents connected at their principal ports, and reducing it is the application of one interaction rule. The eraser realizes weakening, and same-symbol annihilation realizes the trace, which places the dialect at the compact-closed level rather than at plain graph rewriting. The model carries no continuation capture, no sequencing, and no central scheduler.

What licenses running every active pair at once is strong confluence: Lafont's one-step diamond. Any two distinct active pairs are independent, and reducing them in either order yields the same net. That is the theorem behind "all at once," and it holds with no coordination because the rules are local. Confluence is established once over the rule system, in the same character as the abstraction theorem that makes our Tier 1 dimensional content free by parametricity. It is a property of the logic, not an obligation a program carries.

## The Pattern Behind the Fork

This is the monad/applicative axis read through the compiler. The classifier is the region's dependency structure, and the computation expression keyword is only a hint at it.

**Effectful computations** sequence operations through time. They interact with the world, maintain state, or depend on previous results, which is the monadic case where the second effect can depend on the first value. These map to delimited continuations (DCont). As we explored in [continuation preservation](https://speakez.tech/blog/the-continuation-preservation-paradox/), these patterns can survive surprisingly deep into the compilation pipeline.

**Independent computations** carry no effects and no sequential dependencies, which is the applicative case where independent effects compose. These parallelize, and the parallel target depends on shape: dense rectangular work to the tensor path, irregular reduction to interaction nets. Our work on [referential transparency](https://speakez.tech/blog/seeking-referential-transparency/) shows how the compiler identifies these pure regions.

Computation expressions encode a default classification in their structure, and the coloring pass refines it from the region's actual dependencies:

```fsharp
// Async CE → sequential effects → DCont
let fetchData() = async {
    let! response = Http.get url
    let! parsed = parseResponse response
    return parsed
}

// Query CE → dense, rectangular → tensor path
let queryData() = query {
    for item in items do
    where (predicate item)
    select (transform item)
}

// State CE → sequential state threading → DCont
let stateful() = state {
    let! current = getState
    do! setState (current + 1)
    return current
}

// Recursive reduction over a tree → irregular → Inet
let rec size tree =
    match tree with
    | Leaf _      -> 1
    | Node (l, r) -> size l + size r
```

## Making It Concrete: Compilation Strategies

Let's trace how these different patterns compile to see the performance implications:

### DCont Compilation: Stack-Based Async

Traditional async compilation creates numerous heap allocations:

```fsharp
// Traditional: AsyncBuilder, Task objects, closures
let traditional() = async {
    let! data = fetchData()
    let! result = process data
    return result
}
// Allocates: AsyncBuilder (~64 bytes)
//           Task per operation (~128 bytes each)
//           Closure per continuation (~48 bytes each)
// Total: ~350+ bytes of heap allocation
```

Our DCont compilation is designed to eliminate these heap allocations, as we detailed in our exploration of [deterministic memory patterns](/docs/design/beyond-zero-allocation/) and [the full Frosty experience](https://speakez.tech/blog/the-full-frosty-experience/):

```fsharp
// Fidelity: Stack-based continuations
let optimized() = async {
    let! data = fetchData()
    let! result = process data
    return result
}
// Compiles to:
// - Stack frame for continuation state (0 heap bytes)
// - Direct function calls (no indirection)
// - Inline continuation code (no closures)
// Total: 0 bytes of heap allocation
```

The DCont dialect is meant to preserve the continuation structure in MLIR, which leaves room for optimization while keeping execution stack-based with no heap allocations.

### Tensor-Path Compilation: Data Parallelism

Traditional query compilation creates sequential operations:

```fsharp
// Traditional: Sequential iteration with allocations
let traditional = query {
    for x in data do
    where (x.Value > 100)
    select (x.Value * 2)
}
// Executes: One element at a time
//          Iterator objects allocated
//          Delegate allocations for predicates
```

The dense, rectangular query is the tensor-path case, and the design aims to compile it to independent lanes:

```fsharp
// Fidelity: data-parallel lanes
let optimized = query {
    for x in data do
    where (x.Value > 100)
    select (x.Value * 2)
}
// Compiles to:
// - Filtering and projection across independent lanes
// - tensor/linalg algebra, vector/scf loop structure
// - GPU dialect to NVVM or AMDGPU; MLIR-AIE for NPU
// - Zero intermediate allocations
```

On a GPU, the target is thousands of elements processed in a single cycle. On a CPU, it is full SIMD utilization. The irregular reductions that need interaction nets are a separate lane, covered above.

## Hybrid Patterns

Real applications often mix sequential and parallel patterns. Our Composer compiler is designed to handle these together, using our [Program Hypergraph architecture](https://speakez.tech/blog/coupling-and-cohesion/) to identify boundaries between pure and effectful regions:

```fsharp
let hybridWorkflow data = async {
    // Sequential: async I/O (DCont)
    let! rawData = fetchFromDatabase()

    // Parallel: pure transformation (tensor path)
    let processed = query {
        for row in rawData do
        where (isValid row)
        select (transform row)
    }

    // Sequential: async I/O (DCont)
    do! saveToDatabase processed
}
```

The compiler is designed to identify the boundaries and generate code suited to each region:

```mermaid
graph TD
    subgraph "Compilation Analysis"
        CE[Hybrid CE] --> ANALYZE[Region Coloring]
        ANALYZE --> IO1[Async I/O]
        ANALYZE --> PURE[Dense Query]
        ANALYZE --> IO2[Async I/O]
    end

    subgraph "Code Generation"
        IO1 --> DCONT1[DCont Dialect]
        PURE --> TENSOR[Tensor Path]
        IO2 --> DCONT2[DCont Dialect]
    end

    subgraph "Runtime Execution"
        DCONT1 --> SEQ1[Sequential Fetch]
        TENSOR --> PAR[Data-Parallel Process]
        DCONT2 --> SEQ2[Sequential Save]
    end
```

## The Mathematical Foundation

The spine is the monad/applicative axis, and the mathematics on each side is what tells us when these transformations preserve semantics and where they pay off. The sequential side is monadic. The independent side is applicative, and its operational guarantee on the interaction-net lane is a confluence theorem.

### Monads and Sequential Composition

Delimited continuations form a monad, the mathematical structure for sequential composition. The monadic laws guarantee that our transformations preserve program semantics:

The **left identity** law \(\text{return } a \text{ >>= } f \equiv f(a)\) allows us to eliminate unnecessary continuation frames:

```fsharp
// Before: Unnecessary frame
async.Return(42) |> async.Bind(processValue)

// After: Direct call
processValue(42)
```

The **associativity** law \((m \text{ >>= } f) \text{ >>= } g \equiv m \text{ >>= } (\lambda x. f(x) \text{ >>= } g)\) enables continuation fusion:

```fsharp
// Before: Separate continuations
let! temp = operation1()
let! result = operation2(temp)

// After: Fused continuation
let! result = operation1() >>= operation2
```

### Compact-Closed Structure and Parallel Composition

Interaction nets sit at the compact-closed level, where the symmetric monoidal structure carries a duality: every object has a dual, with the unit and counit morphisms connecting them. In Lafont's combinators the eraser realizes weakening and same-symbol annihilation realizes the trace, which is the Geometry-of-Interaction reading. Our [negative and fractional type work](https://arxiv.org/abs/2606.04352) develops the same compact-closed promotion of the graph semantics from the type side, so the algebra the runtime rewrites and the algebra the types track are the same structure.

The monoidal laws are bookkeeping that lets the compiler regroup and reorder. Associativity \((a \otimes b) \otimes c \equiv a \otimes (b \otimes c)\) redistributes work across cores, and braiding \(a \otimes b \equiv b \otimes a\) reorders independent operations for locality. They say work can be rearranged; they do not say it can run at once.

The property that licenses running it at once is strong confluence, Lafont's one-step diamond. If a net contains two distinct active pairs, reducing either one leaves the other available, and the two orders converge on the same net:

```
        N
       / \
   r₁ /   \ r₂
     /     \
    N₁      N₂
     \     /
   r₂ \   / r₁
       \ /
        N'
```

Independence is structural, a consequence of the rules being local to a pair of agents, so no scheduler and no coordination is needed to exploit it. This is the operational content of "the pure side parallelizes for free," and the free-ness is the confluence theorem, proved once over the rule system rather than per program.

## Two Axes, Not One

Lowering and soundness are separate questions, and the post is sharper for keeping them apart. Producing the Inet graph is unconditional codegen on the compilation axis: the region is colored independent, and the rewrite emits. Whether running that graph is correct is verification-axis work, and it divides. Confluence is discharged once at the rule-system level, a property of the logic rather than of any program. The per-program supervision obligations, placement, resource bounds, and non-interference for regions that touch shared state, are Tier 2 and above.

For pure regions the "parallel for free" claim lives on the proof axis at Tier 1: the iterations are independent by referential transparency, and parametricity makes that independence a property of the type and not of the particular function, so the rewrite is correct by construction. For regions that touch shared state the independence is not free, and the obligation is a non-interference property to discharge at Tier 2 or to leave sequential. Naming the axis each claim sits on is what keeps the free case and the discharged case from blurring together.

## Performance Impact

The compilation strategy is projected to change the cost profile of these patterns. The table mixes two kinds of claim, and the right-hand column says which: a lowering claim is a property of the emitted code, and a parallelism claim is a discharged property of the transformation.

| Pattern | Traditional F# | Fidelity | Kind of claim |
|---------|---------------|----------|---------------|
| Async operations | ~350 bytes/operation | 0 heap bytes | lowering: stack-based continuations |
| Dense query | Sequential, allocating | Data-parallel lanes, zero-alloc | lowering + parallelism discharged at Tier 1 |
| State computations | Object allocations | Stack-based | lowering: stack-based state |
| List comprehensions | Iterator objects | Direct generation | lowering: no iterator objects |

These improvements compound. A workflow that mixes async I/O with data processing stands to gain on both sides at once: the async lowering removes the heap traffic, and the pure region parallelizes under a Tier 1 guarantee.

## Custom Computation Expressions

The classification extends to custom computation expressions. Library authors can hint at the intended compilation strategy. This carries over to domain-specific languages like our [Rx reactive framework](https://speakez.tech/blog/alloyrx-native-reactivity-in-fidelity/), where multicast observables map to the parallel side and unicast observables map to delimited continuations:

```fsharp
[<CompileTo(ComputationPattern.Parallel)>]
type QueryBuilder() =
    member _.For(source, body) = ...      // Dense: tensor path
    member _.Where(source, pred) = ...    // Parallel filtering
    member _.Select(source, proj) = ...   // Parallel mapping

[<CompileTo(ComputationPattern.Sequential)>]
type AsyncBuilder() =
    member _.Bind(m, f) = ...            // Will compile to DCont
    member _.Return(x) = ...             // Continuation reset
```

The compiler is designed to respect these hints and to check that they match the actual computation patterns.

## Future Directions: Algebraic Effects and Beyond

As the programming language community embraces algebraic effects, this partition becomes even more relevant. Algebraic effects are essentially typed delimited continuations, making the DCont path natural for effectful computations. Pure computations remain effect-free, taking the tensor path when dense and the interaction-net path when their reduction is irregular.

Future computation expressions could explicitly declare their effect signatures:

```fsharp
// Explicit effect declarations guide compilation
let workflow = async<Effects = {IO; State}> {
    // Compiler knows: needs DCont for effects
    let! data = readFile "input.txt"
    return data
}

let pureWorkflow = query<Effects = Pure> {
    // Compiler guarantees: parallel target
    // Compile error if effects detected
    for x in data do
    select (transform x)
}
```

The independent side stays deterministic in its operational lowering. Where a computation is genuinely nondeterministic, that outcome is modeled in the verification layer rather than the runtime dynamics. Our [tier architecture](/docs/internals/verification/) carries it as a restricted probabilistic fragment at Tier 3, with the distributions living over the same abelian structure the lower tiers already handle, and as relational judgments at Tier 4 when the property relates two runs. Keeping the lowering deterministic is what lets cut elimination stay execution: the net reduces, and the modeling of chance sits in the proof obligation, not in the reduction.

## Structure Is Compilation Strategy

The path from computation expressions to machine code rests on recognizing the structure already present in the code. Sequential patterns that thread continuations map to delimited continuations. Independent patterns parallelize, dense rectangular work through the tensor path and irregular reduction through interaction nets.

This approach treats computation expressions as compile-time specifications rather than runtime abstractions that carry overhead. The builder pattern becomes a source of compilation guidance instead of a runtime object model. For the developer, this means writing ordinary Clef code and getting the performance the structure allows. For the library author, it opens up domain-specific languages without runtime penalties.

On the interaction-net side, HVM and [HVM2](https://github.com/HigherOrderCO/HVM) are the prior art for parallel interaction-net runtimes, including on GPU. The difference is where the cost of soundness is paid. HVM pays it dynamically: the scheduling and the bookkeeping that keep parallel reduction correct run in a runtime host. Our design discharges that cost at compile time as a tier obligation and emits lean kernels through the LLVM GPU backends, with the actor layer orchestrating the boundary. The interaction-net evaluation model is shared; the placement of the soundness work is the difference.

The patterns we work with in expressive programming, monads for sequencing and applicatives for parallelism, are the same structures that tell the compiler how to lower the code. We are early in building this compilation path. The three-lane split, which routes sequential effects, dense data parallelism, and irregular reduction to different targets off one classification, is one we have found no other representative implementation of in the standing literature we have reviewed. It is the direction we will keep developing as the rest of the Composer compiler comes into place.
