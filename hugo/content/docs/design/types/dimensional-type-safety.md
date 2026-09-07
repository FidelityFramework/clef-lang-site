---
title: "Dimensional Type Safety Across Execution Models"
linkTitle: "Dimensional Type Safety"
weight: 40
description: "How Intrinsic Units of Measure Puts Clef in a New Orbit"
date: 2026-01-10T00:00:00+00:00
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design"]
params:
  originally_published: 2026-01-10
  migration_date: 2026-02-15
---

The Mars Climate Orbiter investigation traced the loss to a mismatch between ground software producing thrust data in pound-force seconds and navigation software expecting newton-seconds[^1]. The numbers crossed the interface, but their intended meaning did not. A dimensional contract must connect the producer and consumer to catch that error. Naming the quantities in comments is insufficient when the interface accepts the same unqualified number on both sides.

We are building our Fidelity framework toward a design goal: **the same Clef source code can target CPUs, GPUs, NPUs, FPGAs, and CGRAs**. This is a design consequence of choices we made when we hard-forked the F# compiler to create **Clef**. We integrate units of measure as a compiler intrinsic rather than a library feature. As Ada and VHDL demonstrated decades ago, dimensional type safety does more than catch unit conversion errors. It preserves quantity identity while the compiler analyzes the control-flow and dataflow requirements of a target.

## Hosting and Native Semantics

Our Clef Compiler Service is hosted in .NET while we develop the native language and its toolchain. That hosting arrangement provides an implementation environment. Clef's source contracts and native representations are specified independently of the host's conventions.

Our Fidelity framework targets programs that need explicit memory placement and hardware execution beyond managed IL. The [Native Type Universe](/spec/draft/ntu-types/) carries source kind and dimension. The PSG retains the facts needed for representation selection and later lowering. This is also why Clef tooling must report Clef dimensions, constraints, and proofs rather than interpret the program through F# defaults.

## The Dimensional Safety Imperative

Type systems prevent bugs. Less appreciated is that **different type system features prevent different classes of bugs in different execution contexts**.

Consider three execution models:

1. **Sequential (Von Neumann)**: Instructions execute in order, with branching and loops providing control flow. Memory is a flat address space accessed through load/store operations.

2. **Parallel (GPU/SIMD)**: Multiple execution units process data simultaneously. Control flow becomes expensive (divergent warps), while uniform data operations are cheap.

3. **Dataflow (FPGA/CGRA)**: Computation is spatially organized. Data flows through configured logic blocks or reconfigurable processing elements. There is no "instruction pointer" - operations fire when their inputs are available. The parallelism a CPU gives up by imposing an instruction pointer on this kind of work is what [flow loss analysis](/docs/design/structure-and-performance/flow-loss-analysis/) measures, reading the memory space and access pattern these same dimensional types make explicit.

Ownership and dimensional identity describe different properties. Ownership tracks permitted uses and sharing of a resource. A dimension identifies the kind of quantity, such as length or duration. Both can matter in a parallel or spatial program.

Clef's [units of measure](/spec/draft/units-of-measure/) retain their algebraic identity across execution models. A `float<meters>` remains a length when a CPU instruction, a GPU kernel, or a synthesized arithmetic unit computes it. The compiler still needs separate evidence for memory access, scheduling, and target capabilities.

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

The derived types make this implicit assignment between `Meters` and `Feet` a type mismatch. Carry those distinct types across a producer-consumer interface, and the compiler can expose the disagreement before execution. The interface then states the units its caller must supply.

Ada's derived types establish distinct nominal identities, and its generics can abstract over types. The derived-type mechanism is distinct from the Abelian-group equations used by Clef for products, quotients, and measure polymorphism: Ada's [derived-type rules](https://www.adaic.org/resources/add_content/standards/05aarm/html/AA-3-4.html) are a precedent for preserving semantic identity, while its generics provide a separate abstraction mechanism.

## VHDL

VHDL (VHSIC Hardware Description Language) emerged from the same DoD initiative as Ada, standardized as IEEE 1076 in 1987 for hardware description[^3]. It inherited Ada's type safety philosophy but extended it for a different execution model: **concurrent hardware semantics**.

VHDL separates physical quantities from unadorned numbers. A physical type declares a base unit, a range in that unit, and optional scaled units. For example, this declaration expresses distance in micrometres:

```vhdl
type distance is range 0 to 1000000
    units
        um;
        mm = 1000 um;
        cm = 10 mm;
    end units;
```

That type-level quantity information is distinct from the representation selected by a particular implementation or foreign interface. For example, [GHDL's foreign-interface documentation](https://ghdl.github.io/ghdl-cosim/vhpidirect/declarations.html#restrictions-on-type-declarations) specifies a 64-bit representation for physical types. That documents one implementation boundary. Clef quantities follow their own target declarations.

The physical type must be used on the quantity being checked. A signal declared as `distance` carries that contract, while one declared merely `REAL` remains an unadorned number even if its name contains “meters.” Clef extends the quantity-based approach with Kennedy-style product and quotient inference under its [units-of-measure algebra](/spec/draft/units-of-measure/). That inference supplies relationships beyond VHDL's physical-type declarations.

Ada and VHDL provide architectural precedents for keeping quantity identity, legal ranges, and implementation constraints explicit. Clef combines that discipline with Kennedy-style measure inference and carries the resulting constraints into the PSG. The range and the selected representation are later coeffects, governed by [Width Inference](/spec/draft/width-inference/) and [Numeric Selection](/spec/draft/numeric-selection/). Hardware synthesis requires a target realization with validated routing, timing, and resource allocation.

## FSharp.UMX

**FSharp.UMX**[^4] demonstrates a useful extension of F# units of measure to primitive non-numeric types. Our Fidelity design draws on that precedent.

F# has supported units of measure since 2008, based on Andrew Kennedy's academic work on dimensional analysis in ML-family languages[^5]:

```fsharp
[<Measure>] type meters
[<Measure>] type seconds

let distance : float<meters> = 100.0<meters>
let time : float<seconds> = 9.58<seconds>
let velocity = distance / time  // float<meters/seconds>
 
```

This applies only to numeric types (`float`, `decimal`, `int`, etc.). FSharp.UMX extended this through the `[<MeasureAnnotatedAbbreviation>]` attribute:

```fsharp
// FSharp.UMX approach (library-based)
[<Measure>] type customerId
[<MeasureAnnotatedAbbreviation>] type CustomerId = string<customerId>

let processCustomer (id: CustomerId) =
    // id is typed distinctly from other strings
    ...
```

This approach works within standard F# and .NET. It provides non-numeric dimensional safety through the existing type system. It operates within the F# compiler and .NET representation model.

**Our Fidelity framework owes a conceptual debt to FSharp.UMX** for demonstrating that units of measure extend beyond numerics.

> Our approach differs from .NET implementation but shares the same fundamental insight: dimensional constraints are too valuable to limit to numbers.

## Beyond Library to Intrinsic

Clef specifies measured values as part of its native type universe, including non-numeric carriers. A measured identifier should retain its identity when passed through a function or stored in a collection, just as a measured force does during arithmetic. The source kind and dimension must survive until the relevant representation and preservation obligations have been established.

The distinction from the .NET path is when and how the information is released. A library can use the F# compiler's measure checking while the runtime carries only the underlying representation. Clef needs its dimensional facts available to the native analysis that establishes layout and lowering constraints.

Our intended native path retains those facts through the stages that need them:

```
Clef Source → CCS → PSG → Alex → MLIR → Target Backend
                ↑                    ↑
          Units preserved      Units inform
          in type checking     code generation
```

Dimensions participate alongside other constraints in:

- **Memory layout decisions**: Region and access constraints distinguish peripheral access from writable local storage
- **MLIR dialect selection**: Shape, effects, and target capabilities determine available tensor lowerings
- **Hardware synthesis**: Established ranges and selected layouts constrain hardware resource requirements

Consider how this works for [memory regions](/spec/draft/memory-regions/#region-typed-pointers):

```fsharp
// Conceptual region/access contract; the platform declares physical layout
type Ptr<'T, 'Region, 'Access>

[<Measure>] type Peripheral
[<Measure>] type Flash
[<Measure>] type Stack
[<Measure>] type ReadOnly
[<Measure>] type ReadWrite

// These constraints flow through compilation
let gpioReg : Ptr<int, Peripheral, ReadWrite> = ...
let flashData : Ptr<int, Flash, ReadOnly> = ...
let localBuffer : Ptr<float, Stack, ReadWrite> = ...

// Required checks for an applicable target realization:
// - Peripheral access uses appropriate memory barriers
// - Flash reads don't attempt writes
// - Stack allocations have appropriate lifetime
 
```

Our PSG carries dimensional identity together with the distinct region, access, and range facts used by lowering. A peripheral access needs the platform's memory contract, including any required ordering. A read-only view restricts writes. Those facts cannot be fabricated from a quantity's unit name. [Conformance §6](/spec/draft/conformance/#6-the-preservation-obligation-through-lowering) permits a source type representation to be released once its structural purpose has been served, provided the properties still needed below that edge are preserved or re-checked. Different backends may reach that boundary at different stages.

## The SSA Bridge

Appel's account of SSA and functional programming[^6] provides a useful correspondence between SSA bindings and lexically scoped function parameters. It helps expose value dependencies across a control-flow graph.

> SSA makes each value definition explicit and represents control-flow joins through value parameters or phi functions.

That representation supports dataflow analysis, while the graph still carries control dependencies and effects. A hardware realization must account for both.

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

The phi functions at loop headers express data dependencies. **This SSA representation is simultaneously**:

1. A control-flow graph (basic blocks connected by branches)
2. A dataflow graph (values flowing through phi functions and operations)

The dimensional constraints (`<meters>`) are preserved throughout. `sum`, `sum_next`, and `val` all carry the `meters` dimension. The phi function's type is `float<meters>`, ensuring dimensional consistency across loop iterations.

Our target pathways can use these explicit dependencies when realizing a computation on a CPU or a dataflow target. The dimensional equations remain the same. Synthesizability additionally depends on bounded resources, effects, and a supported target mapping.

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

HLS tools use dataflow and pipeline directives together with dependence analysis to construct concurrent hardware where the program permits it. The exact interfaces and schedule depend on the tool and target.

SSA exposes value dependencies. Memory aliasing, observable effects, and loop-carried dependencies still constrain which operations can execute concurrently.

Our Fidelity design retains source constraints alongside those dependencies. The compiler can use checked region and access facts without reconstructing them from an unqualified load or store. Dimensions continue to check quantity identity as the graph is transformed. With those facts available, the hardware pathway can assess the remaining dependencies and establish a valid schedule for the operations it supports.

The [Program Hypergraph design](/docs/internals/pipeline/hyping-hypergraphs/) extends these joint relationships. Its lowering boundary exposes settled consequences to witnesses, with preservation evidence attached to the affected operations.

## Random Updates and Memory Contracts

Irregular memory updates provide a useful test of this direction. The work is easy to describe, but the addresses being accessed make scheduling and memory behavior central to performance. NextSilicon's GUPS demonstration[^8] offers a concrete example on a dataflow-oriented processor. For our compiler, it motivates a practical question: which facts about an update can we retain so that a target pathway has enough information to realize it well?

For a Clef version, a table index and a random-generator state can carry distinct dimensions. The operation that derives an index from that state must establish its own contract: the index is in range, and the conversion between the two domains is explicit. Scheduling also needs the dependencies of the random recurrence and the effects of table updates. Keeping those facts together lets the target assess which operations may proceed concurrently.

| Fact | What it establishes |
|---|---|
| Table-index dimension | The value belongs to the index domain |
| Bound relative to table length | The indexed access is within the table |
| Region and access contract | The storage and operations permitted at that access |
| Dependency and effect analysis | Which updates may be reordered or scheduled concurrently |

These facts have distinct roles in our PSG. Keeping them together lets the target pathway consume the established memory contract while preserving the source quantity identities.

## The Program Hypergraph

In our earlier article [Hyping Hypergraphs](/docs/internals/pipeline/hyping-hypergraphs/), we described the evolution from the Program Semantic Graph (PSG) to the Program Hypergraph (PHG). This remains a future design goal - something "on the board" - but the architectural direction is clear.

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
    DimensionalConstraint = int<randomState>  // Preserved!
}
```

The proposed hyperedge records the quantity shared by its consumers. Joint constraint solving must establish the compatible uses and reify the consequences on the graph before witnesses consume them. Recording a relationship does not by itself discharge its proof obligation.

## Ownership and Dimensional Identity

Ownership and borrowing remain useful ways to describe permitted uses of storage. They do not determine a quantity's dimension, and a dimension does not establish ownership. A dataflow target still needs rules for duplicated values, shared mutable state, and effects at memory boundaries.

Our Fidelity design carries these constraints together. Immutable values can be copied or shared when the realization preserves their observable behavior. Mutable storage requires an explicit sharing and lifetime contract, including across closure capture and delayed demand. [Gaining Closure](/docs/design/memory/gaining-closure/#captured-constraints) describes that distinction for native flat environments.

## Beyond the Abelian Fragment

Clef's measure equality follows the free Abelian-group laws on its base units. Products and quotients normalize to exponent equations, which form the decidable measure-inference fragment described in [Units of Measure](/spec/draft/units-of-measure/) and the [DTS/DMM paper](https://arxiv.org/abs/2603.16437). Source-level numeric kind and dimension remain separate from the analyzed range and selected representation.

That algebra checks dimensional consistency. A guarded range or a claim about mutation requires additional evidence. The [tiered verification design](/docs/design/categorical-foundations/the-compilation-sheaf/) distinguishes those obligations from the parametricity results available in the Abelian fragment. Our implementation must preserve the conditions under which each result applies across lowering.

Non-Abelian symmetry and the negative and fractional type directions extend the research horizon. They motivate keeping constraint provenance and source identity available now, without claiming that the current dimensional implementation already decides those properties. [The braid as a fourth sheaf](/docs/design/categorical-foundations/braid-as-a-fourth-sheaf/) develops one such proposed extension.

## Validation Horizon

An engineer should be able to keep a quantity's meaning intact while changing where its calculation runs. Our current dimensional work establishes that continuity through the supported native language surface: measures must survive elaboration, participate in type identity, and remain available to joint range and representation constraints. Compiler regressions and native acceptance cases exercise the implemented paths, with each additional CPU, GPU, or spatial pathway responsible for its own preservation checks.

The next tooling step is to expose those actual Clef facts in the editor. A dimensional type, a pending range obligation, a selected physical layout, and a discharged proof should appear as distinct observations of the same program. That would let the developer inspect how a target choice affects the realization while continuing to write in the quantities of the problem.

---

### Related Articles

- [Hyping Hypergraphs](/docs/internals/pipeline/hyping-hypergraphs/) - The evolution from PSG to Program Hypergraph and targeting post-Von Neumann architectures
- [Why Clef Fits MLIR](/docs/design/structure-and-performance/why-clef-fits-mlir/) - The theoretical foundation connecting concurrent programming to modern compilation
- [The Abstract Machine Model Paradox](/blog/abstract-machine-model-paradox/) - Why ownership semantics assume Von Neumann architectures
- [Beyond Zero-Allocation](/docs/design/memory/beyond-zero-allocation/) - How async, arenas, and actors complete the Fidelity memory model
- [Context-Aware Compilation](/docs/internals/mlir/context-aware-compilation/) - Coeffects and their role in optimization decisions
- [Standing Art: Clef Metaprogramming in Composer](/docs/design/language/standing-art-clef-metaprogramming/) - Computation expressions, active patterns, quotations, and units of measure
- [Danger Close: Why Types Matter]({{< ref "danger-close-why-types-matter" >}}) - Real near-disasters (a freezer monitor's Fahrenheit/Celsius mix-up, the Mars Orbiter, microgram dosing errors) that Clef's zero-cost units of measure catch at compile time

---

[^1]: Mars Climate Orbiter Mishap Investigation Board. (1999). [*Phase I Report*](https://llis.nasa.gov/llis_lib/pdf/1009464main1_0641-mr.pdf). NASA. The spacecraft was lost due to a navigation error caused by ground software producing thrust data in pound-force seconds while the spacecraft expected newton-seconds.

[^2]: U.S. Department of Defense. (1983). [*Reference Manual for the Ada Programming Language*](https://swtch.com/ada-mil-std-1815a.pdf) (MIL-STD-1815A). Ada's derived types establish distinct nominal identities. See also: [Ada - Wikipedia](https://en.wikipedia.org/wiki/Ada_(programming_language)).

[^3]: IEEE. (1987). [*IEEE Standard VHDL Language Reference Manual*](https://en.wikipedia.org/wiki/VHDL) (IEEE Std 1076-1987). VHDL's physical type system extended Ada's strong typing to hardware description with concurrent dataflow semantics. Current standard: [IEEE 1076-2019](https://standards.ieee.org/ieee/1076/5179/).

[^4]: Tsarpalis, E. (2019). [*FSharp.UMX: F# Units of Measure for primitive non-numeric types*](https://github.com/fsprojects/FSharp.UMX). GitHub. The library uses `[<MeasureAnnotatedAbbreviation>]` to extend F#'s unit of measure system to non-numeric types.

[^5]: Kennedy, A. (1997). [*Relational Parametricity and Units of Measure*](https://dl.acm.org/doi/10.1145/263699.263761). Proceedings of the 24th ACM SIGPLAN-SIGACT Symposium on Principles of Programming Languages (POPL '97), pp. 442-455. This foundational work established the theoretical basis for F#'s units of measure system. Also available via [Microsoft Research](https://www.microsoft.com/en-us/research/publication/relational-parametricity-and-units-of-measure/).

[^6]: Appel, A. W. (1998). [*SSA is Functional Programming*](https://www.cs.princeton.edu/~appel/papers/ssafun.pdf). ACM SIGPLAN Notices, 33(4), 17-20. This paper demonstrates the mathematical equivalence between SSA form and functional programming with lexical scope.

[^7]: Cong, J., Liu, B., Neuendorffer, S., Noguera, J., Vissers, K., & Zhang, Z. (2011). [*High-Level Synthesis for FPGAs: From Prototyping to Deployment*](https://www.researchgate.net/publication/224225866_High-Level_Synthesis_for_FPGAs_From_Prototyping_to_Deployment). IEEE Transactions on Computer-Aided Design of Integrated Circuits and Systems, 30(4), 473-491. A landmark paper marking HLS's transition from research to production deployment.

[^8]: NextSilicon. (2025). [*Maverick-2 GUPS Demonstration*](https://www.youtube.com/watch?v=E6qMQQ47sIA). The demonstration showed 30x improvement on the GUPS benchmark through dataflow transformation of inherently sequential memory access patterns. NextSilicon's "mill cores" are software-defined processing units that adapt to application-specific dataflow graphs. See also: [NextSilicon Takes Aim At CPUs And GPUs](https://www.nextplatform.com/2025/10/22/nextsilicon-takes-aim-at-cpus-and-gpus-with-maverick-2-dataflow-engine/).
