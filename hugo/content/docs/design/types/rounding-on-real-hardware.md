---
title: "Rounding on Real Hardware"
linkTitle: "Rounding on Real Hardware"
description: "Operation-specific rounding capabilities, sound enclosures, finite accumulation and the evidence required for target lowering."
weight: 60
lastmod: 2026-09-10
---

This page explains the hardware considerations behind the
[Rounding](/spec/draft/rounding/) and [Numeric Selection](/spec/draft/numeric-selection/)
designs. It distinguishes an arithmetic requirement from the instruction or
circuit that realizes it. The real-representation selector, rounding capability
consumer and quire pipeline discussed here are design work; existing integer
width inference does not establish their implementation.

<a id="why-rounding-was-implicit-under-ieee-754"></a>
## IEEE 754 already has several rounding directions

IEEE arithmetic is not limited to one rounding rule. Nearest with ties to even
is the usual binary default; directed modes include toward positive infinity,
negative infinity and zero. A default is an execution choice, not a requirement
that every operation use it. CUDA, for example, exposes explicit nearest and
directed forms of several floating-point operations.
[NVIDIA's floating-point guide](https://docs.nvidia.com/cuda/floating-point/index.html#rounding-modes)

Even when all values use IEEE formats, reassociation, fused multiply-add,
intermediate precision, subnormal handling and approximate instructions can
change results. Selecting a representation therefore does not finish the
operation contract. The compiler must preserve whichever rounding and evaluation
semantics its analysis relies on.

## The two roles of rounding

Rounding can occur when an operation produces a representable value and when a
boundary changes representation. Either location can affect an error budget;
either is inherently harmless. In particular, converting an interval endpoint
can destroy an enclosure just as an incorrectly rounded arithmetic operation can.

The design gives soundness-critical requirements a representation/capability
constraint and carries precision-loss evidence on the program graph. The useful
separation is the obligation being preserved, not a claim that all conversions
merely reduce accuracy. An interval endpoint needs a justified outward bound
through every operation and boundary that contributes to it.

This description does not introduce source casts, seal forms or `Posit32` types.
Clef source retains its single `float` kind; boundary declarations and the
selected operation contract determine representation behavior. Deliberate loss
is stated as arithmetic, whose resulting range is analyzed.

## Multiplication as the defining case

For finite closed intervals A = [aLo, aHi] and B = [bLo, bHi], the real product
range is determined by the four corner products. A straightforward sound
implementation is:

```text
lo = min(down(aLo*bLo), down(aLo*bHi), down(aHi*bLo), down(aHi*bHi))
hi = max(  up(aLo*bLo),   up(aLo*bHi),   up(aHi*bLo),   up(aHi*bHi))
```

Here each down/up result bounds the exact corresponding product. Correctly
selecting a different evaluation scheme is also possible: an exact intermediate
calculation followed by outward rounding of the exact extrema is sound. What is
insufficient is to round the products to nearest, take their minimum/maximum,
and assume a later rounding-mode change recovers the discarded information.
Once the minimum is already representable, rounding that same value downward
need not change it at all.

The dependency problem is distinct from rounding. Generic interval multiplication
of X = [-2, 3] by itself treats the occurrences independently and gives [-6, 9].
The actual square range is [0, 9]. The first enclosure is sound but loose; a
specialized square transfer function can retain the dependency and be tighter.
For a reciprocal, a range containing zero requires explicit domain treatment,
such as split unbounded pieces, rather than a finite denominator bound inferred
from a label or dimension.

Unbounded endpoints, empty intervals and exceptional operations need additional
rules. The finite example above is not a complete interval implementation.

<a id="cpu-rounding-direction-is-a-global-runtime-mode"></a>
## CPU: control state and instruction-specific rounding

On x86, ordinary SSE/AVX arithmetic uses MXCSR rounding-control state; x87 has
its own control word. This is execution-context state, not one mode shared by
all processors and threads. Some instruction forms override it. In particular,
Intel AVX-512 provides embedded rounding for supported operations, subject to
encoding and operand-form restrictions.
[Intel floating-point reference](https://www.intel.com/content/www/us/en/developer/articles/technical/floating-point-reference-sheet-for-intel-architecture.html),
[Intel AVX-512 embedded rounding](https://www.intel.com/content/www/us/en/developer/articles/technical/xeon-processor-scalable-family-technical-overview.html)

A realization using control-state changes must preserve that state across its
execution boundaries and ensure the compiler respects the changes. Grouping
operations by direction can reduce changes when dependencies permit it, but
there is no universal requirement to switch once per corner product. The cost
is target- and instruction-dependent and needs measurement. Hardware support
does not become software emulation merely because changing its state is costly.

For other CPU architectures, the platform description must identify the actual
control registers, instructions, ABI obligations and supported precisions. A
CPU-family label is not enough to choose an implementation.

## FPGA: rounding direction is a synthesis-time property

A custom circuit can fix a rounding direction in its datapath, or implement a
selectable mode. Fixed direction avoids changing an instruction control register;
it does not eliminate hardware cost. Guard information, increment logic,
comparisons, wider intermediates and extra endpoint paths consume area and
energy and affect timing. Resource sharing trades throughput for area; pipelining
trades latency and registers for clock frequency or initiation interval.

An interval multiplication may compute eight directed corner results, or use a
proved construction sharing exact products and rounding logic. Neither arrangement
is automatically one cycle. Vendor IP also has specific limitations: the cited
AMD Floating-Point Operator guide supports nearest rounding on most operators,
not arbitrary directed modes on every instance.
[AMD Floating-Point Operator PG060](https://docs.amd.com/v/u/en-US/pg060-floating-point)

The target claim must identify the instantiated IP or generated circuit and its
configuration. Synthesis and timing reports establish resource and throughput
claims for that design. An FPGA's reconfigurability alone does not discharge a
rounding capability obligation.

## The target landscape

| Target selection | Evidence needed for rounding and accumulation |
| --- | --- |
| CPU | Exact instruction forms or software algorithm, precision, control-state handling, subnormal and exceptional behavior |
| NVIDIA CUDA GPU | Supported per-operation directed intrinsics and compiler flags; separate validation for approximate operations and custom formats |
| Other GPU or NPU | Vendor operation semantics and supported precisions; do not infer capabilities from the preceding GPU row |
| FPGA | Selected IP or generated arithmetic, proof/test of its rounding rule, finite accumulator layout, and implementation reports |
| RISC-V with a proposed posit extension | The particular extension and implementation; generic RISC-V does not establish posit or quire instructions |
| CGRA or programmable tile array | Actual processing-element operations, routing and wide-state support; configurability does not imply arbitrary arithmetic |

NVIDIA documents instruction-specific rounding for basic arithmetic in several
precisions. It also documents that fast-mode settings can change subnormal,
division and square-root behavior. These facts directly contradict treating
all GPU directed rounding as software emulation, while leaving other GPU vendors
and other operations to their own contracts.
[NVIDIA operation modes and flags](https://docs.nvidia.com/cuda/floating-point/index.html#cuda-and-floating-point)

The intended capability classification is native, emulated or unavailable for a
particular operation contract. Cost is additional evidence. Software may soundly
realize a missing native operation; a soundness-critical requirement cannot be
satisfied by silently substituting a different rounding rule.

## The posit's missing direction

The Posit Standard (2022) defines a single rounding algorithm, including its
encoding thresholds and extreme-value behavior, rather than selectable IEEE-style
directed modes. Its full rule must be followed; an unqualified nearest-even
summary is insufficient at every tapered boundary.
[Posit Standard, §4](https://posithub.org/docs/posit_standard-2.pdf)

A proposed interval construction can bound a correctly rounded finite result
using neighboring representable values, provided the construction proves those
neighbors enclose the exact operation result in the admitted range. This requires
format-aware predecessor/successor semantics and boundary handling. Subtracting
one fixed minimum ULP is not a general construction for nonuniform spacing.

Nor does widening a completed multi-operation approximation by one neighbor
bound all accumulated error. Each primitive or compound operation needs its own
sound bound. If the exact result lies outside the finite representable range,
a finite neighboring endpoint cannot enclose it; an explicitly supported extended
bound or a capability/coverage failure is required. NaR is not an ordered infinity.

## The Quire

The [posit arithmetic page](/docs/design/types/posit-arithmetic/#the-quire-exact-accumulation)
separates standard 16n-bit quires from the bounded b-posit proposal's 800-bit
accumulator for n > 12. Both are finite. Exact accumulation requires representable
products and sufficient range for every reachable partial sum, including merges.
The final result is then rounded according to the selected output contract.

This removes intermediate accumulation rounding, not input quantization or every
other operation's error. It can preserve cancellation of represented products
without guaranteeing exact structural identities throughout a larger algorithm.
A quire used to produce interval endpoints would still need a sound outward
boundary operation; single rounding alone does not establish an enclosure.

## Fixed-point and the overflow question

Fixed-point spacing is determined by the selected scale. Discarding fractional
bits requires an operation-specific rounding rule; floor and truncation toward
zero differ for negative values. Overflow policy is a separate axis.

Clef's selection design does not default dimensioned values to saturation. If an
admitted range exceeds a boundary, coverage fails. A program can state `clamp`
or modular arithmetic explicitly; the compiler may use a target's saturating or
wrapping operations when they realize that stated arithmetic. Clamping can be
wrong for a physical model, while wrapping can be correct for a deliberately
modular quantity. Neither is universally safe, and neither restores information
lost outside a finite representation.

## How the design carries rounding

Composer would retain operation semantics, range evidence, selected representation,
rounding obligations and transfer error in the graph. A target realization must
consume those requirements and preserve them through optimization. Unresolved
soundness requirements must remain pending during analysis and fail at commitment
if no sound realization is available.

The proposed execution organization assigns arena ownership and orchestration to
Prospero, work to Olivier actors and ready-turn scheduling to Ariel. Scheduling
must respect any arithmetic control state and visibility obligations, but it
does not choose a different numeric meaning for a ready task.

See [Arithmetic Construction and Placement](/docs/internals/numerics/arithmetic-construction-and-placement/)
for those boundaries, [The Lyapunov Window](/docs/design/types/lyapunov-window/)
for the proposed numerical reconstruction experiment, and
[Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/) for the
broader discussion. No completed ThreeBody comparison is asserted here.
