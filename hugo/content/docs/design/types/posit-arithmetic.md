---
title: "Bringing Posit Arithmetic to Clef"
linkTitle: "Posit Arithmetic"
weight: 50
description: "Posit representation, finite exact accumulation, and their proposed selection and placement in Fidelity."
date: 2025-12-20T14:00:00-05:00
lastmod: 2026-09-10
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2025-12-20
  migration_date: 2026-02-15
---

**Status: design and arithmetic requirements.** Clef's source model has one real
kind, `float`, optionally carrying a dimension. Posit, IEEE and fixed-point are
candidate representations selected from established range evidence and target
capabilities. This page does not establish an implemented posit selector, quire
pass, hardware datapath or ThreeBody benchmark. The governing design is
[Numeric Selection](/spec/draft/numeric-selection/).

## IEEE 754 and Its Trade-offs

A representation supplies a finite set of values and operation semantics. IEEE
binary formats provide approximately uniform relative precision across their
normal range; subnormal behavior needs separate treatment. Posits redistribute
precision with magnitude. Fixed-point provides a constant absolute spacing within
its finite range. None dominates without specifying the admitted values, error
criterion and operations.

A dimension such as force does not supply that range. Scaling into different
units also does not prove that a candidate covers every input and intermediate.
The selection design first checks coverage and permitted capabilities, then
compares accuracy among feasible representations. Its treatment of near-zero
ranges must account for absolute error and representable spacing rather than
assuming a useful relative-error bound at zero.

## Posit: A Different Trade-off

The Posit Standard (2022) specifies a sign, variable-length regime, exponent and
fraction encoding. Its exponent field has two bits; the regime consumes more of
the encoding away from magnitude one, leaving fewer explicit fraction bits. It
has one zero encoding and one exceptional NaR value. Standard widths are not
restricted to four source-language types. See the
[format definition, §§3.1–3.3](https://posithub.org/docs/posit_standard-2.pdf).

Tapered precision makes range evidence relevant to accuracy selection. Values
near magnitude one can benefit from a larger fraction; very small nonzero values
and large magnitudes have different spacing. Claims about a workload therefore
need its actual range and error objective, including cancellation and exceptional
inputs.

## Posits in Clef

The source states the quantity and computation. It does not select a representation
by declaring `Posit32`, wrapping a `uint32` field, or calling a narrowing cast.
For example, a `float<newtons>` value remains a dimensioned real while the graph
retains its range obligations. Representation commitment occurs only after the
required evidence is available.

An external format or hardware boundary can constrain the representation through
its declaration. Such a boundary must cover the admitted range and satisfy the
transfer's exactness or error contract. The compiler cannot silently introduce
saturation or wrapping to make a non-covering format appear usable. Intended
loss is expressed as arithmetic such as `clamp`, `%`, `floor` or `round`, with
its resulting range analyzed. There is no source conversion or seal form in the
current numeric-selection design.

<a id="concrete-types-vs-parameterized-generics"></a>
<a id="srtp-integration"></a>
### Source kinds and lowered representations

Names such as posit32 or a concrete quire layout describe the selection result
and backend representation. They are not additional source numeric kinds.
Implementation specialization, including any internal structural dispatch, must
preserve that distinction. Earlier examples of public `Posit8/16/32/64` structs
and SRTP overload families described a different API and are superseded here.

## The Full Posit Family

The standard quire width is **16n bits**, where n is the posit width. Consequently:

| Posit width | Standard quire width | Packed storage |
| --- | ---: | ---: |
| 8 | 128 bits | 16 bytes |
| 16 | 256 bits | 32 bytes |
| 32 | 512 bits | 64 bytes |
| 64 | 1,024 bits | 128 bytes |

The older n²/2 formula does not describe the 2022 standard. Storage figures are
payload sizes; a target's alignment or allocation requirements can add overhead.
The authoritative layout is [§§3.1–3.4](https://posithub.org/docs/posit_standard-2.pdf).

### Quires Across the Family

The bounded b-posit proposal is a distinct format. Jonnalagadda, Thotli and
Gustafson describe a bounded regime and an **800-bit quire for n > 12**, with
25 32-bit words of payload. This is not the standard posit quire formula and
must have a separate format identity in any target declaration. The paper's
reported circuit comparisons concern its specified implementations, not Fidelity
application performance. [B-posit paper](https://arxiv.org/abs/2603.01615)

### Mixed-Precision Workflows

Storage, computation and transport can have different representations. Each
transfer needs a directional claim: coverage alone does not establish exact
representability. A destination can cover the source's magnitude range while
still losing significand information.

For a mixed-format dot product, establish that each represented operand is
interpreted correctly and that every product fits the chosen accumulator's scale
and range. A quire specified for one format cannot be assumed to accumulate any
other format merely because its total bit count is larger. The final boundary
must also state which rounding semantics apply.

<a id="contrast-with-stillwater-universal"></a>
### Reference implementations

Independent arithmetic implementations can provide test vectors and comparison
oracles. Their supported standard revisions, exceptional behavior and fused
operation semantics must be pinned. A C++ template API, a software encoding and a
Clef lowering strategy are different engineering choices; no accuracy or speed
advantage follows from the language used to express them.

## The Quire: Exact Accumulation

For represented operands xᵢ and yᵢ, the desired operation is an exact sum of their
exact products followed by one specified rounding. A finite accumulator supplies
that operation only while two conditions hold:

1. Every product is exactly representable at the accumulator's scale.
2. Every reachable local partial sum and merge result in every permitted
   decomposition fits its range.

A small final result does not establish the second condition: cancellation can
follow an oversized intermediate. A parallel reduction also needs exact partial
results and merges that fit. Lossy rounding of a partial block before merging
changes the operation and cannot be substituted silently. An intermediate encoding
conversion is permissible when it is proved exact for every admitted partial.

Exact accumulation removes rounding *inside that accumulation*. It cannot recover
information lost when inputs were quantized, approximate a reciprocal exactly,
or force an entire geometric-algebra computation to satisfy every mathematical
identity. If represented products cancel exactly and the capacity conditions
hold, that cancellation is retained. Structural zero components established by
an algebraic proof and numerical residual bounds remain separate evidence.

The [quire-adequacy invariant](/spec/draft/numeric-selection/#1021-the-quire-adequacy-invariant)
is the design obligation that connects the fixed layout to the program's bounds.

## Lowering Through Clef: First Steps

The proposed sequence is:

```text
quantity and range evidence
    → feasible representation selection
    → accumulation recognition and adequacy checks
    → layout, lifetime and placement requirements
    → target-specific implementation
```

Selecting a format does not implement its arithmetic. CPU software, custom
instructions and synthesized fabric must each preserve the chosen operation
semantics, including boundaries, exceptional values and final rounding.
[Arithmetic construction and placement](/docs/internals/numerics/arithmetic-construction-and-placement/)
describes the compiler and execution responsibilities in more detail.

### The Compiler Roadmap

The numerical work requires a real interval domain, a representation-selection
consumer, quire recognition, adequacy evidence and target realizations. Existing
integer range inference, graph carriage and target selection provide reusable
infrastructure; they do not establish that these real-arithmetic passes already
exist. Each new consumer needs independent semantic and lowering tests.

Reproducibility requires more than using the same encoding. The standard restricts
changes to evaluation order, fusion and precision when they affect specified
rounding. Optimizations must preserve that operation contract, and parallel
execution must communicate enough exact state to preserve it. Bit identity is
not independent of arbitrary compiler transformations.
[Posit execution restrictions, §4.3](https://posithub.org/docs/posit_standard-2.pdf)

## Performance Considerations

Measure complete kernels with controlled input distributions, equivalent error
requirements and explicit compiler settings. Include arithmetic, accumulator
initialization/finalization, spills, transfer and synchronization. A smaller
value encoding can reduce bandwidth while more expensive arithmetic increases
execution time. A faster decoder alone does not settle the result.

### Practical Viability by Posit Size

On a fixed instruction set, a software posit path may require decoding, integer
operations and re-encoding. A wide accumulator can occupy several registers or
memory words. Whether values remain in registers depends on allocation, live
state and the target; a source-local accumulator is not a register-residency
promise. SIMD and GPU implementations additionally depend on how work and carry
propagation are distributed.

### FPGA: Custom Posit Silicon from Clef Source

Fabric permits a specialized datapath, but its width, shifters, carry propagation,
registers and routing consume resources. Initiation interval, latency, clock
frequency and energy require synthesis and timing evidence for the selected
product. A finite quire may use a pipelined or multi-cycle organization; neither
one-cycle updates nor one update per cycle follows from its type.

This is a proposed realization path. Existing integer-to-fabric work does not
constitute a synthesized posit or b-posit implementation.

## Where Posits Excel

A posit candidate is useful when it meets coverage and capability requirements
and provides a better justified error result for the workload's range. Exact
accumulation can be useful independently of whether every stored value uses a
posit. IEEE, fixed-point and other exact-accumulator designs remain meaningful
comparators. No ThreeBody comparison has been completed for this proposal, and
no posit-versus-FP64 performance or reversibility ordering is established.

## Integration with Fidelity's Memory Model

An accumulator has a concrete owner, lifetime and storage requirement. Composer
would analyze the reduction and compare placement candidates. Prospero supplies
arena ownership and orchestration; Olivier actors perform work, while Ariel
schedules ready turns. These responsibilities do not presume a managed runtime
or collector.

A GPU register file is execution-local state. Sharing an address space or coherent
memory with a CPU does not expose those registers to CPU loads. Cross-processor
use requires an explicit materialized result and the appropriate transfer,
visibility and synchronization contract. Placement should compare retaining the
accumulator near its producers and transferring one result with transporting the
operands or partial state. Exactness obligations apply to that transport too.

## The Path Forward

An initial implementation should pin one arithmetic format, one operation contract
and one target. Its acceptance should include encoding/exception vectors,
rounding boundaries, exact-product and partial-sum failures, and checks that
optimization preserves the required evaluation semantics. Hardware claims then
need target-specific resource and timing reports; application claims need measured
results and a stated reference.

<a id="fidelity-framework"></a>
<a id="ieee-754-history-and-context"></a>
<a id="posit-fundamentals"></a>
<a id="fpga-and-hardware-compilation"></a>
<a id="simd-and-vector-acceleration"></a>
## Further Reading

- [Numeric Selection](/spec/draft/numeric-selection/): source kinds, coverage, capabilities and finite quire adequacy.
- [Rounding on Real Hardware](/docs/design/types/rounding-on-real-hardware/): operation and boundary semantics.
- [Arithmetic Construction and Placement](/docs/internals/numerics/arithmetic-construction-and-placement/): proposed compiler and execution organization.
- [Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/): the broader design discussion.
- [The Lyapunov Window](/docs/design/types/lyapunov-window/): numerical reconstruction and its evidence requirements.
