---
title: "Dimensional Type Safety Across Execution Models"
linkTitle: "Dimensional Type Safety"
description: "How Intrinsic Units of Measure Puts Clef in a New Orbit"
date: 2026-01-10T00:00:00+00:00
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design"]
params:
  originally_published: 2026-01-10
  original_url: "https://speakez.tech/blog/dimensional-type-safety/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

When the Mars Climate Orbiter burned up in the Martian atmosphere on September 23, 1999, it wasn't because of faulty sensors or software bugs in the traditional sense. The spacecraft's navigation software expected metric units while Lockheed Martin's ground software provided imperial[^1]. A simple dimensional mismatch, caught by neither compiler nor testing, destroyed a $327 million mission. This wasn't an isolated incident but a recurring pattern across safety-critical systems that the Ada programming language community had been working to prevent since the early 1980s[^2].

Thirty years later, we're building the Fidelity framework with a bold claim: **the same Clef source code can target CPUs, GPUs, NPUs, FPGAs, and CGRAs**. This isn't aspirational marketing - it's a design consequence of choices we made when we hard-forked the F# compiler to create **Clef**. Central to this capability is something that might seem modest: the integration of units of measure as a compiler intrinsic rather than a library feature. But as Ada and VHDL demonstrated decades ago, dimensional type safety isn't just about catching unit conversion errors - it's the foundation for expressing programs in ways that naturally translate between control-flow and dataflow execution models.

This article examines why we made this architectural decision, how it relates to proven techniques in hardware synthesis, and what it means for the future of the Fidelity framework.

## A Note for .NET Developers: Why Clef Exists

Before diving into the technical details, we should address a question that F# developers familiar with the .NET ecosystem will naturally ask: **Why fork the F# compiler?**

The answer is not that we wanted to abandon .NET or create a competing ecosystem. The .NET runtime is excellent for what it does - managed execution with garbage collection, cross-platform deployment, and a rich standard library. For many F# applications, .NET is the right target. We here at SpeakEZ Technologies use it *every day*. It's the basis of our bootstrapping for the Composer compiler.

But Clef serves a different purpose. Our goal with the Fidelity framework is to compile Clef to native code for scenarios where:

- **No runtime dependencies** are acceptable (embedded systems, CPU and GPU "on the metal")
- **Deterministic memory behavior** is required (real-time systems, AI, HPC)
- **Non-Von Neumann architectures** are the target (FPGAs, CGRAs, spatial accelerators)

These requirements fundamentally conflict with the assumptions baked into IL (Intermediate Language) and the CLR. IL assumes garbage collection. It assumes a sequential, Von Neumann execution model. It assumes reference semantics with managed heap allocation.

To target the full spectrum of modern compute architectures, we needed a compilation path that preserves semantic information all the way down to hardware - information that IL necessarily erases. This isn't a criticism of .NET; it's recognition that different targets require different compilation strategies.

**Clef is not a replacement for .NET**. It's a transformation of Clef's semantic richness into a form that can flow through MLIR to diverse backends. The same Clef language, the same developer experience, but with a compilation path designed for native and hardware targets from the ground up. This means that the normal idioms that a .NET developer might expect will diminish with native Clef patterns. We don't have nulls (we use voption everywhere). We don't have BCL norms. And most salient here, we don't have FSharp.UMX as a separate library. It's built right into the Clef core.

With that context established, let's examine why dimensional type safety is central to this vision.

## The Dimensional Safety Imperative

Type systems prevent bugs. This is well understood. What's less appreciated is that **different type system features prevent different classes of bugs in different execution contexts**.

Consider three execution models:

1. **Sequential (Von Neumann)**: Instructions execute in order, with branching and loops providing control flow. Memory is a flat address space accessed through load/store operations.

2. **Parallel (GPU/SIMD)**: Multiple execution units process data simultaneously. Control flow becomes expensive (divergent warps), while uniform data operations are cheap.

3. **Dataflow (FPGA/CGRA)**: Computation is spatially organized. Data flows through configured logic blocks or reconfigurable processing elements. There is no "instruction pointer" - operations fire when their inputs are available.

Traditional type systems work well for sequential execution. Rust's ownership model, for instance, provides memory safety by tracking when values can be read or written. But ownership assumes a Von Neumann model with linear memory access patterns. As we noted in [The Abstract Machine Model Paradox](/docs/design/abstract-machine-model-paradox/):

> "Rust's ownership model fundamentally assumes von Neumann architecture with linear memory. There's no 'owner' of a signal propagating through configured logic blocks."

This is where dimensional type safety becomes essential. **Units of measure express constraints that are orthogonal to execution model**. Whether a value is processed sequentially, in parallel, or through spatial dataflow, its dimensional properties remain invariant. A `float<meters>` is still a length measurement regardless of how it's computed.

## Historical Foundations: Ada's Lesson

The U.S. Department of Defense mandated Ada in 1983 after decades of dealing with software reliability problems in military systems[^2]. One of Ada's distinctive features was its derived type mechanism:

```ada
type Meters is new Float;
type Feet is new Float;
type Kilograms is new Float;
type Pounds is new Float;

-- These are incompatible types despite both being Float internally
distance_m : Meters := 100.0;
distance_f : Feet := distance_m;  -- COMPILE ERROR: type mismatch
```

This approach caught real bugs in real systems. The Boeing 777, developed in the 1990s with Ada as its primary avionics language, achieved unprecedented software reliability. The derived type system meant that mixing metric and imperial units - the exact bug that doomed Mars Climate Orbiter - would be reliably caught at compile time.

But Ada's approach had limitations. The types are distinct but not parameterized - you can't write generic code that works with "any length unit" while preserving dimensional correctness. And critically for our purposes, Ada's type system was designed for sequential execution on conventional processors.

## VHDL: When Hardware Demanded Type Safety

VHDL (VHSIC Hardware Description Language) emerged from the same DoD initiative as Ada, standardized as IEEE 1076 in 1987 for hardware description[^3]. It inherited Ada's type safety philosophy but extended it for a fundamentally different execution model: **concurrent dataflow**.

In VHDL, the basic unit of computation is the signal assignment, which models physical signal propagation through hardware:

```vhdl
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity dimensional_example is
    port(
        clk : in STD_LOGIC;
        distance_meters : in REAL;    -- Physical type
        time_seconds : in REAL;       -- Physical type
        velocity_result : out REAL    -- Must be meters/second
    );
end dimensional_example;

architecture behavioral of dimensional_example is
    -- VHDL supports physical types with dimensional checking
    type distance is range 0 to 1000000
        units
            nm;           -- nanometers (base unit)
            um = 1000 nm;
            mm = 1000 um;
            m = 1000 mm;
            km = 1000 m;
        end units;

    type time_unit is range 0 to 1000000
        units
            fs;           -- femtoseconds (base unit)
            ps = 1000 fs;
            ns = 1000 ps;
            us = 1000 ns;
            ms = 1000 us;
            s = 1000 ms;
        end units;
begin
    -- Concurrent signal assignment (dataflow semantics)
    velocity_result <= distance_meters / time_seconds;
end behavioral;
```

The key insight is that VHDL's type system serves **two purposes simultaneously**:

1. **Correctness**: Catching dimensional errors at "compile" time (synthesis)
2. **Synthesis guidance**: Informing the hardware synthesizer about signal properties

When VHDL code is synthesized to an FPGA, the physical types don't just prevent bugs - they guide the synthesizer in generating efficient hardware. The dimensional information flows through synthesis to affect routing, timing, and resource allocation.

This is precisely the pattern we're implementing in the Fidelity framework: **dimensional types that serve both correctness and code generation purposes**.

## FSharp.UMX: Bringing Dimensional Safety to Functional Programming

Before discussing our intrinsic implementation, we must acknowledge the work that made it possible. **FSharp.UMX**, created by Eirik Tsarpalis in 2019[^4], demonstrated that F#'s units of measure could be extended beyond numeric types through clever use of the type system.

F# has supported units of measure since 2008, based on Andrew Kennedy's academic work on dimensional analysis in ML-family languages[^5]:

```fsharp
[<Measure>] type meters
[<Measure>] type seconds

let distance : float<meters> = 100.0<meters>
let time : float<seconds> = 9.58<seconds>
let velocity = distance / time  // float<meters/seconds>
```

This is powerful but limited to numeric types (`float`, `decimal`, `int`, etc.). FSharp.UMX extended this through the `[<MeasureAnnotatedAbbreviation>]` attribute:

```fsharp
// FSharp.UMX approach (library-based)
[<Measure>] type customerId
[<MeasureAnnotatedAbbreviation>] type CustomerId = string<customerId>

let processCustomer (id: CustomerId) =
    // id is typed distinctly from other strings
    ...
```

This approach works within standard F# and .NET. It provides non-numeric dimensional safety through the existing type system. For .NET applications, FSharp.UMX is an excellent solution.

**The Fidelity framework owes a conceptual debt to FSharp.UMX** for demonstrating the power of extending units of measure beyond numerics.

> Our approach differs from .NET implementation but shares the same fundamental insight: dimensional constraints are too valuable to limit to numbers.

## Beyond Library to Intrinsic: The Clef Decision

In Clef and the Fidelity framework, we've integrated non-numeric units of measure as a **compiler intrinsic** rather than a library feature. This distinction matters enormously for code generation.

When units of measure are a library feature (as in FSharp.UMX on .NET), the compiler treats them as phantom types that are erased before code generation. The runtime sees no trace of the dimensional information - it exists purely for type checking.

When units of measure are a compiler intrinsic (as in Clef), the dimensional information flows through the **entire compilation pipeline**:

```
Clef Source → Clef → PSG → Alex → MLIR → LLVM/Hardware Backend
                ↑                    ↑
          Units preserved      Units inform
          in type checking     code generation
```

This means dimensional constraints can influence:

- **Memory layout decisions**: A `Ptr<byte, Peripheral, ReadOnly>` has different allocation semantics than `Ptr<byte, Stack, ReadWrite>`
- **MLIR dialect selection**: Tensor operations with dimensional constraints can target different dialects (linalg, vector, gpu)
- **Hardware synthesis**: When targeting FPGAs, dimensional information can guide resource allocation and pipelining

Consider how this works for memory regions:

```fsharp
// In Clef, this is intrinsic to the type universe
type Ptr<'T, 'Region, 'Access>

[<Measure>] type Peripheral
[<Measure>] type Flash
[<Measure>] type Stack
[<Measure>] type ReadOnly
[<Measure>] type ReadWrite

// These constraints flow through compilation
let gpioReg : Ptr<uint32, Peripheral, ReadWrite> = ...
let flashData : Ptr<byte, Flash, ReadOnly> = ...
let localBuffer : Ptr<float32, Stack, ReadWrite> = ...

// The compiler can verify at every stage:
// - Peripheral access uses appropriate memory barriers
// - Flash reads don't attempt writes
// - Stack allocations have appropriate lifetime
```

The critical point is **when** these constraints are erased. In .NET, phantom types disappear before code generation - they exist only for type checking. In Clef, dimensional constraints are preserved through the Program Semantic Graph (PSG), carried through MLIR generation, and available for target-specific optimization. They're only erased at the final lowering stage, **after** all compilation decisions that can benefit from them have been made. This is what we mean by "intrinsic" - the dimensional information is woven into the compiler's representation at every level where it can inform code generation. It's ***also*** among the reasons why we gave our framework the name "Fidelity".

## The SSA Bridge: Why Control-Flow and Dataflow Are Equivalent

Here we arrive at the key technical insight that enables the Fidelity framework's multi-architecture targeting. It's not new - Andrew Appel demonstrated it in 1998[^6] - but its implications for dimensional types have been underappreciated:

> **Static Single Assignment (SSA) form is mathematically equivalent to functional programming.**

This equivalence is well-known in compiler theory. What's less appreciated is its consequence: **any program in SSA form can be viewed as either control-flow or dataflow**.

Consider a simple loop:

```fsharp
// Control-flow view: sequential loop
let mutable sum = 0.0<meters>
for i in 0 .. n-1 do
    sum <- sum + distances.[i]
```

In SSA form, this becomes:

```
entry:
    sum_0 = 0.0<meters>
    br loop

loop:
    i = phi [0, entry], [i_next, loop]
    sum = phi [sum_0, entry], [sum_next, loop]
    val = load distances[i]
    sum_next = add sum, val
    i_next = add i, 1
    cond = cmp i_next, n
    br cond, loop, exit

exit:
    return sum
```

The phi (phi) functions at loop headers express data dependencies. **This SSA representation is simultaneously**:

1. A control-flow graph (basic blocks connected by branches)
2. A dataflow graph (values flowing through phi functions and operations)

The dimensional constraints (`<meters>`) are preserved throughout. `sum`, `sum_next`, and `val` all carry the `meters` dimension. The phi function's type is `float<meters>`, ensuring dimensional consistency across loop iterations.

**This duality is why the same Clef code can target both Von Neumann and dataflow architectures**. The control-flow view maps naturally to CPUs. The dataflow view maps naturally to FPGAs and CGRAs. The dimensional constraints remain valid in both interpretations.

## The Control-Flow to Dataflow Transform

High-Level Synthesis (HLS) tools have been performing this transform for decades[^7]. Xilinx Vivado HLS, Intel oneAPI, and similar tools take C/C++ code and synthesize hardware:

```c
// Input: Sequential C with loop
void vector_add(float* a, float* b, float* c, int n) {
    #pragma HLS DATAFLOW
    for (int i = 0; i < n; i++) {
        #pragma HLS PIPELINE
        c[i] = a[i] + b[i];
    }
}

// Output: Pipelined dataflow hardware
// - Streaming interfaces for a, b, c
// - Fully pipelined loop body
// - Concurrent read/compute/write stages
```

The DATAFLOW pragma instructs the synthesizer to convert control dependencies into data dependencies. Operations that don't have true data dependencies can execute concurrently.

**This works because SSA exposes the true data dependencies.** The sequential ordering in the source code is largely artificial - a consequence of the Von Neumann programming model, not the algorithm itself.

But look more closely: **existing HLS tools perform this transform without the dimensional type information that Fidelity preserves**. They succeed through sophisticated analysis and pragmas that hint at the programmer's intent.

> They're reconstructing information that may well have been present in the original design but then lost through compilation.

The Fidelity framework's approach is different. By preserving dimensional types through compilation:

1. **Dimensional constraints guide dataflow construction**: Memory regions, access patterns, and data flow properties are explicit in the type system
2. **No pragmas required for common cases**: The type information expresses intent directly
3. **Verification at every stage**: Dimensional consistency is checked during control-flow analysis, SSA construction, and dataflow synthesis

This is what we meant in [Hyping Hypergraphs](https://speakez.tech/blog/hyping-hypergraphs/) when we discussed the Program Hypergraph encoding both control-flow and dataflow views. The dimensional types aren't just for catching bugs - they're semantic information that guides code generation for diverse targets.

## Case Study: NextSilicon GUPS Benchmark

To make this concrete, consider a recent demonstration from NextSilicon's Maverick-2 processor[^8] - a Coarse-Grained Reconfigurable Array (CGRA) designed for irregular, data-intensive workloads.

The GUPS (Giga Updates Per Second) benchmark measures random memory access performance - the worst case for conventional architectures due to unpredictable memory access patterns that defeat caches and prefetchers. A traditional CPU running GUPS spends most of its time waiting for memory.

NextSilicon's demonstration showed **30x performance improvement** over conventional CPUs on GUPS[^8]. How? By transforming the inherently sequential benchmark into dataflow execution. The Maverick architecture uses "mill cores" - software-defined processing units that can be dynamically configured for different computational patterns. Instead of fixed ALUs and memory hierarchies, the processor adapts its structure to match the application's dataflow graph.

**What makes this relevant to Fidelity?** NextSilicon's compiler performs the control-flow to dataflow transformation, but in the case of the GPUS demo it seems to accomplish this feat without dimensional type information. The compiler must infer data dependencies, memory access patterns, and synchronization requirements from (what we imagine to be) untyped LLVM IR.

With Fidelity's approach:

```fsharp
// Hypothetical GUPS in dimensionally-typed Clef
[<Measure>] type tableIndex
[<Measure>] type randomState

let gups (table: Ptr<uint64, MainMemory, ReadWrite>)
         (n: int<tableIndex>)
         (updates: int) =
    let mutable rng : uint64<randomState> = initialSeed
    for _ in 1 .. updates do
        rng <- nextRandom rng
        let idx = (rng % uint64 n) |> int
        table.[idx] <- table.[idx] ^^^ rng
```

The dimensional types (`tableIndex`, `randomState`, memory region constraints) are information that a dataflow synthesizer can use:

- `tableIndex` and `randomState` are distinct domains that don't interact
- The table access pattern depends on the random state
- The random state update is a linear recurrence (can be parallelized with known techniques)

This information exists in the programmer's mental model. Traditional compilation erases it. Fidelity preserves it.

## The Program Hypergraph: Encoding Both Views

In our earlier article [Hyping Hypergraphs](https://speakez.tech/blog/hyping-hypergraphs/), we described the evolution from the Program Semantic Graph (PSG) to the Program Hypergraph (PHG). This remains a future design goal - something "on the board" - but the architectural direction is clear.

The PHG would naturally encode both control-flow and dataflow relationships:

```fsharp
// Conceptual PHG representation
type PHGHyperedge =
    | ControlFlow of {
        Source: BasicBlock
        Target: BasicBlock
        Condition: Option<PHGNode>
      }
    | DataFlow of {
        Producer: PHGNode
        Consumers: Set<PHGNode>
        DimensionalConstraint: DimensionalType
      }
    | Synchronization of {
        Barrier: PHGNode
        Participants: Set<PHGNode>
      }
```

Pivotal to this is recognition that **hyperedges can connect more than two nodes**. A dataflow relationship might have one producer and multiple consumers. A synchronization barrier involves multiple participants. These multi-way relationships are awkward in traditional graphs but natural in hypergraphs.

With dimensional types preserved:

```fsharp
// PHG with dimensional annotations
DataFlow {
    Producer = randomGeneratorNode
    Consumers = { tableAccess1; tableAccess2; ... }
    DimensionalConstraint = uint64<randomState>  // Preserved!
}
```

The dimensional constraint flows through the hyperedge, ensuring that all consumers receive values of the correct type. This is verified during PHG construction and remains available during target-specific code generation.

## Why Rust Can't Do This

We've mentioned Rust several times as a point of comparison. Rust is an excellent language with genuine innovations in memory safety. But its design makes the control-flow/dataflow pivot fundamentally more difficult.

The issue is ownership semantics. Rust's borrow checker assumes:

1. **Linear memory**: Values exist at specific addresses that can be borrowed or moved
2. **Temporal ownership**: At any point in time, a value has exactly one owner
3. **Sequential reasoning**: Lifetimes are defined by program execution order

These assumptions align perfectly with Von Neumann execution. They make it harder to reason about:

- **Spatial dataflow**: Where data exists "everywhere at once" in a configured logic fabric
- **Streaming semantics**: Where values flow through pipelines without discrete ownership transfers
- **CGRA execution**: Where the same "value" may exist in multiple mill cores simultaneously

Rust can target FPGAs through projects like Rust-GPU and various HLS tools. But these tools work by imposing significant restrictions on the Rust subset that can be synthesized, or by treating the FPGA as a coprocessor rather than a first-class target.

**The dimensional type approach is orthogonal to ownership**. A `float<meters>` has the same dimensional constraint whether it's owned, borrowed, or flowing through a dataflow graph. This orthogonality is what enables the execution model flexibility we're building toward.

## Beyond the Abelian Fragment

The dimensional type system does its work in a specific algebraic corner: the free abelian group on the base units, with operations that preserve the group structure. Within that corner, every consistency check reduces to integer linear algebra, every inference is decidable in polynomial time, and parametricity guarantees that the result of the check survives every parametric lowering pass. The engineer pays no annotation cost, because the type structure does the proof. This is the genuinely free fragment.

The corner has a boundary, and it is worth being explicit about where it falls. Properties that involve *non-abelian* group actions (rotor conjugation in the Clifford algebra, gauge transformations in physics-aware models, permutation symmetries in graph neural networks) are equivariance properties rather than equality properties, and parametricity over the abelian dimensional group is silent about them. Mehta and Hsu's recent symmetry Hoare logic [(arXiv:2509.00587, OOPSLA '25)](https://arxiv.org/abs/2509.00587) is the natural assertional layer for this case: it generalizes Hoare's pre/postcondition discipline to group actions, where the precondition is "the input is in this orbit" and the postcondition is "the operation is equivariant under this group." The dimensional fragment is the abelian special case of that framework, where the group is free abelian and the equivariance reduces to vector equality. Equivariant neural networks, conservation-law verification, and gauge-aware physics models live one step further out, in the non-abelian regime where assertions become load-bearing.

The Fidelity framework treats both fragments as compatible *sheaves over the same compilation poset*: the abelian sheaf is checked for free by Tier 1, and the symmetry sheaf would be checked by an assertional layer that reuses the same dual-pass discharge mechanism with a different stalk category. The [compilation sheaf design document](/docs/design/categorical-foundations/the-compilation-sheaf/) makes this categorical view precise, and the [triangle of functors](/blog/a-triangle-of-functors/) post sketches why the abelian and non-abelian cases share enough structure to live in the same framework.

## The Path Forward

The Fidelity framework's multi-architecture targeting is not complete - we're building toward it. The current implementation focuses on native compilation through MLIR to LLVM targets (x86_64, ARM64, RISC-V). But the architectural choices we've made - particularly the intrinsic dimensional type system - are designed with the broader vision in mind.

The path forward includes:

1. **Current**: Native compilation preserving dimensional types through MLIR -> LLVM
2. **Near-term**: GPU targeting via MLIR's gpu and nvvm dialects
3. **Medium-term**: Exploration of HLS-style dataflow synthesis
4. **Long-term**: Direct CGRA/spatial architecture targeting

At each stage, the dimensional types provide semantic information that improves code generation. For LLVM targets, they enable better memory layout and access pattern optimization. For GPU targets, they inform memory hierarchy usage (shared vs. global memory, texture sampling). For dataflow targets, they directly guide the graph construction.

## Conclusion: A Language for All Architectures

Ada proved that dimensional type safety catches real bugs in real systems. VHDL proved that dimensional information can guide hardware synthesis. FSharp.UMX proved that dimensional constraints extend naturally to non-numeric types. Decades of HLS research proved that control-flow programs can be automatically transformed to dataflow execution.

The Fidelity framework combines these proven techniques with Clef's expressive type system and MLIR's flexible compilation infrastructure. By making dimensional types intrinsic to the compiler rather than a library feature, we preserve semantic information that has traditionally been lost during compilation.

This isn't speculative research. The control-flow to dataflow transformation is implemented in production HLS tools. The dimensional type systems are proven in safety-critical Ada and VHDL codebases. The MLIR infrastructure provides a principled path to diverse backends.

What's new is putting these pieces together with a language designed for developer productivity. Clef developers can write expressive, concurrent code with rich type safety. That code can target CPUs today, GPUs soon, and spatial architectures as the ecosystem matures. The dimensional types don't just catch bugs - they carry semantic intent through the entire compilation pipeline.

**This is why we created Clef**. Not to replace .NET, but to transform Clef's semantic richness into a form that can flow to any computational substrate. The same language, the same developer experience, but with a compilation path designed from the ground up for the heterogeneous computing future.

---

### Related Articles

- [Hyping Hypergraphs](https://speakez.tech/blog/hyping-hypergraphs/) - The evolution from PSG to Program Hypergraph and targeting post-Von Neumann architectures
- [Why Clef Fits MLIR](/docs/design/why-clef-fits-mlir/) - The theoretical foundation connecting concurrent programming to modern compilation
- [The Abstract Machine Model Paradox](/docs/design/abstract-machine-model-paradox/) - Why ownership semantics assume Von Neumann architectures
- [Beyond Zero-Allocation](/docs/design/beyond-zero-allocation/) - How async, arenas, and actors complete the Fidelity memory model
- [Context-Aware Compilation](/docs/design/context-aware-compilation/) - Coeffects and their role in optimization decisions
- [Standing Art: Clef Metaprogramming in Composer](/docs/design/standing-art-clef-metaprogramming/) - Computation expressions, active patterns, quotations, and units of measure

---

[^1]: Mars Climate Orbiter Mishap Investigation Board. (1999). [*Phase I Report*](https://llis.nasa.gov/llis_lib/pdf/1009464main1_0641-mr.pdf). NASA. The spacecraft was lost due to a navigation error caused by ground software producing thrust data in pound-force seconds while the spacecraft expected newton-seconds.

[^2]: U.S. Department of Defense. (1983). [*Reference Manual for the Ada Programming Language*](https://swtch.com/ada-mil-std-1815a.pdf) (MIL-STD-1815A). Ada's derived type mechanism was specifically designed to prevent the kind of unit confusion that would later doom Mars Climate Orbiter. See also: [Ada - Wikipedia](https://en.wikipedia.org/wiki/Ada_(programming_language)).

[^3]: IEEE. (1987). [*IEEE Standard VHDL Language Reference Manual*](https://en.wikipedia.org/wiki/VHDL) (IEEE Std 1076-1987). VHDL's physical type system extended Ada's strong typing to hardware description with concurrent dataflow semantics. Current standard: [IEEE 1076-2019](https://standards.ieee.org/ieee/1076/5179/).

[^4]: Tsarpalis, E. (2019). [*FSharp.UMX: F# Units of Measure for primitive non-numeric types*](https://github.com/fsprojects/FSharp.UMX). GitHub. The library uses `[<MeasureAnnotatedAbbreviation>]` to extend F#'s unit of measure system to non-numeric types.

[^5]: Kennedy, A. (1997). [*Relational Parametricity and Units of Measure*](https://dl.acm.org/doi/10.1145/263699.263761). Proceedings of the 24th ACM SIGPLAN-SIGACT Symposium on Principles of Programming Languages (POPL '97), pp. 442-455. This foundational work established the theoretical basis for F#'s units of measure system. Also available via [Microsoft Research](https://www.microsoft.com/en-us/research/publication/relational-parametricity-and-units-of-measure/).

[^6]: Appel, A. W. (1998). [*SSA is Functional Programming*](https://www.cs.princeton.edu/~appel/papers/ssafun.pdf). ACM SIGPLAN Notices, 33(4), 17-20. This paper demonstrates the mathematical equivalence between SSA form and functional programming with lexical scope.

[^7]: Cong, J., Liu, B., Neuendorffer, S., Noguera, J., Vissers, K., & Zhang, Z. (2011). [*High-Level Synthesis for FPGAs: From Prototyping to Deployment*](https://www.researchgate.net/publication/224225866_High-Level_Synthesis_for_FPGAs_From_Prototyping_to_Deployment). IEEE Transactions on Computer-Aided Design of Integrated Circuits and Systems, 30(4), 473-491. A landmark paper marking HLS's transition from research to production deployment.

[^8]: NextSilicon. (2025). [*Maverick-2 GUPS Demonstration*](https://www.youtube.com/watch?v=E6qMQQ47sIA). The demonstration showed 30x improvement on the GUPS benchmark through dataflow transformation of inherently sequential memory access patterns. NextSilicon's "mill cores" are software-defined processing units that adapt to application-specific dataflow graphs. See also: [NextSilicon Takes Aim At CPUs And GPUs](https://www.nextplatform.com/2025/10/22/nextsilicon-takes-aim-at-cpus-and-gpus-with-maverick-2-dataflow-engine/).
