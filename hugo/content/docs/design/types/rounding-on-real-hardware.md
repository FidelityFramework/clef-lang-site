---
title: "Rounding on Real Hardware"
linkTitle: "Rounding on Real Hardware"
description: "How rounding is selected and realized per target once representation is no longer fixed by the platform, across CPU, FPGA, posit, and fixed-point."
weight: 60
---

This page accompanies the [Rounding spec chapter](/spec/draft/rounding/) with the hardware background behind its requirements: why representation selection makes rounding an explicit per-target decision, and how that decision is realized on a CPU, an FPGA, and across the posit and fixed-point formats.

## Why rounding was implicit under IEEE 754

Rounding is normally invisible, because IEEE 754 fixed it. The standard defines one rounding rule, round-to-nearest with ties to even, and every mainstream processor implements it identically in hardware. A program adds two floats and the result rounds correctly with no instruction to do so. The rule was set once, in 1985, for every value on every machine.

That uniformity rests on a single assumption: every value uses the same representation, so a single rounding rule suffices. When representation is selected per target rather than fixed by the platform, that assumption no longer holds. A posit does not round the way an IEEE float does; a fixed-point value rounds at a scale the developer set; an interval rounds its two ends in opposite directions. No single rule covers them, so rounding has to become an explicit property selected and carried per target, in the same frame the design uses for representation and width. This chapter describes that design.

When numeric selection (see [Numeric Selection](/spec/draft/numeric-selection/)) yields an IEEE float supported on the target's hardware, which is the common case, the standard IEEE rounding and precision behavior applies unchanged. The mechanisms below engage only when selection chooses a non-IEEE representation, or an interval, to obtain precision or a dynamic-range profile a float does not provide. For the many computations where that additional precision is not warranted, the end-user-visible difference is minimal; the value of the apparatus is in the cases where it is.

## The two roles of rounding

Rounding occurs in two distinct roles, with different consequences.

The first is **converting a value from one representation to another**: a 64-bit result narrowed to 32 bits for a register, a quire's exact sum converted to a posit at the end of an accumulation, a value computed on the FPGA transferred to the host. Precision is lost, and the rounding discipline governs how. A wrong choice here reduces accuracy: the result is less precise than it could be, but it remains a valid number.

The second is **an operation committing a rounding direction as it computes**. This is normally invisible, since round-to-nearest is the default. The exception is the interval. An interval is a pair of endpoints, sound only if it contains the true value. Soundness requires the low end rounded down (toward negative infinity) and the high end rounded up (toward positive infinity) at every operation. Rounding the low end the wrong way by one bit makes the interval claim a value just below it, which it does not contain. That is not a less-accurate interval; it is an unsound one. The enclosure no longer encloses.

The two cases differ in kind, accuracy loss versus loss of soundness, and the spec carries them differently. The conversion case is a tracked fact carried as a coeffect and surfaced at the point of use. The interval case is enforced in the type: a value that cannot round outward is not a well-formed interval. The first is an annotation; the second is a well-formedness condition.

## Multiplication as the defining case

Addition is the misleading case. To add two intervals, add the low ends and add the high ends. Addition is monotone, so a larger input gives a larger output, and the low end of the result always derives from the low ends of the inputs. The mapping from inputs to output endpoints is fixed, which obscures what an interval operation requires in general.

Multiplication is where that requirement becomes visible, and the harder properties of interval arithmetic are already present in this one operation. Multiplying two intervals is not a matter of multiplying the low ends. Treating the two intervals as the sides of a rectangle in the plane, the product is the smallest and largest value any point in that rectangle can produce. When the rectangle lies entirely in the positive quadrant, the low end is still `aLo·bLo`. When it straddles an axis, because one interval contains zero, the corner producing the smallest product depends on the operands' signs and cannot be fixed in advance. The result endpoints are therefore the minimum and maximum over all four corner products:

\[\mathrm{lo} = \min(a_{\mathrm{lo}} b_{\mathrm{lo}},\; a_{\mathrm{lo}} b_{\mathrm{hi}},\; a_{\mathrm{hi}} b_{\mathrm{lo}},\; a_{\mathrm{hi}} b_{\mathrm{hi}})\]

\[\mathrm{hi} = \max(a_{\mathrm{lo}} b_{\mathrm{lo}},\; a_{\mathrm{lo}} b_{\mathrm{hi}},\; a_{\mathrm{hi}} b_{\mathrm{lo}},\; a_{\mathrm{hi}} b_{\mathrm{hi}})\]

This four-product minimum and maximum is the actual form of the operation, not an unoptimized shortcut. An interval denotes a set, not a pair, and multiplying two sets is a question of the range their product spans. This is what addition concealed: interval operations are sign-case analyses, not fixed endpoint formulas.

This also accounts for the sign-crossing reciprocal the spec treats elsewhere, where `1/[lo, hi]` splits into two pieces when the interval contains zero. Reciprocal and multiplication are the same phenomenon: an output whose structure changes when an input crosses zero. The reciprocal's pieces are unbounded; multiplication's stay bounded, which makes it the clearer illustration of the general rule.

This is where rounding connects to the hardware. Each of the four products must be rounded, and the direction depends on which endpoint it feeds: products feeding the low end round down, toward negative infinity; products feeding the high end round up. The rounding must occur *before* the minimum and maximum, not after. Round first, then reduce:

```text
correct:   lo = min( roundDown(aLo·bLo), roundDown(aLo·bHi), ... )
broken:    lo = roundDown( min(aLo·bLo, aLo·bHi, ...) )
```

The broken version rounds the selected product once, at the end. If the true smallest product was already below the value the hardware computed, rounding the unrounded minimum downward does not recover the precision lost in the comparison, and the enclosure fails to contain a value it should bound. Directed rounding is therefore not a single attribute of the operation; it applies to each sub-product, according to that product's destination endpoint, before any comparison.

Multiplication also exposes the dependency problem, which affects the three-body force calculation directly. Interval arithmetic does not record that two operands are the same quantity. Computing `X · X` evaluates the two \(X\)s as independent, so for \(X = [-2, 3]\) it admits the combination \(-2 \times 3\) and yields \([-6, 9]\), where the true range of \(X^2\) is \([0, 9]\). The phantom \(-6\) is the cost of treating one variable as two. A dedicated squaring operation is therefore a requirement rather than a convenience: `sqr(X)` evaluates a single variable and yields \([0, 9]\), where generic `X · X` cannot. The over-estimate is sound, since it only widens the enclosure, but it is looser than the true range, and on an expression such as the \(1/r^2\) of a gravitational force that looseness directly degrades the result.

## CPU: rounding direction is a global runtime mode

A CPU exposes one rounding mode, global to all floating-point operations. On x86 it is the rounding-control field of the MXCSR register; on ARM it is the RMode field of the FPCR. Two bits select the direction, and once set, that direction applies to every floating-point instruction until it is changed. The mode is a single global setting, not a per-instruction one.

For ordinary arithmetic this is adequate, since the mode stays on round-to-nearest. Interval arithmetic is the exception: a sound enclosure requires the low end rounded down and the high end rounded up, which means changing the global mode between the two halves of every operation. Mode changes are expensive. In one published benchmark, changing the rounding mode and changing it back cost on the order of thirty times a normal operation on an Apple M1, and close to seventy times on a high-end x86 part, because the pipeline stalls on each change.

A program can mitigate this. Compute all low ends with the mode set one way, change it once, then compute all high ends, so the change occurs a handful of times rather than per operation; the alternative is to accept the per-operation penalty. Either way, directed rounding on a CPU is available but not free. In the capability model the design proposes, this is the *emulated* case: the capability exists, and the cost is what distinguishes it from a native one.

## FPGA: rounding direction is a synthesis-time property

An FPGA has no rounding-mode register, because it has no fixed instruction set to which a mode would apply. Each operation is synthesized as logic, and rounding is part of that logic: the gates that conditionally increment the truncated result, with the direction determined by how those gates are wired. Round-down and round-up are distinct wirings, settled when the circuit is built.

A rounding direction on an FPGA therefore has no runtime cost: it is fixed at synthesis rather than selected while the circuit runs. An interval operation that costs seventy times a normal operation on a CPU, from the mode changes, costs nothing additional on an FPGA, because the low-end datapath is synthesized to round down and the high-end datapath to round up, in parallel at full throughput. The cost is paid once, at synthesis.

This asymmetry is the reason the design targets fabric. On a CPU, rounding direction is a runtime mode that opposes directed-rounding workloads; on an FPGA, it is a synthesis-time design property. The same `Interval<Posit32>` is expensive on a host and lowers to a native construct on fabric, and the capability gate is where that distinction between targets is drawn.

The gap widens with operation complexity. An interval multiplication is the clearest case: the four corner products each require their own rounding direction. A CPU must set the mode around all four before reducing them, incurring the change cost four times per multiply. On fabric the same multiply is four multipliers in parallel, two wired to round down and two to round up, feeding a comparator tree, resolved in a single cycle. The more complex the arithmetic, the larger the separation between the mode-driven and the synthesis-fixed approach.

## The target landscape

The design spans a graded range of substrates between the CPU and FPGA poles, and each has a different relationship to non-IEEE representation, directed rounding, and exact accumulation. The capability gate mediates that range: selection runs against the set a target offers, `R(T) = { r : capability(T, r) ≠ unavailable }`, and proposes a representation only where it can be realized. A developer may also reach for [b-posit](https://arxiv.org/abs/2603.01615) and the quire deliberately, to obtain precision a target's native float path does not provide, accepting a representation that is emulated or synthesized rather than native. One source then resolves to the appropriate realization on each target, with the selected realization surfaced at design time.

The substrate axis here is representation and rounding capability, which is distinct from the data-flow placement axis surveyed in [flow-loss analysis](/blog/going-deep-with-flow-loss-analysis/). On that axis a discrete GPU and an integrated APU differ by memory locality; for rounding and representation they are the same case, since both are SIMT float engines that do not synthesize custom numeric formats, so they share a row below.

The table reads as what each substrate *affords*, the realization a representation would take if selected for it, and the cost class that realization falls into.

| Substrate | Posit / b-posit | Directed rounding | Quire (exact accumulation) |
|---|---|---|---|
| **CPU** (x86, ARM64) | emulated in software, unless a posit instruction set is present | a global runtime mode (`MXCSR`, `FPCR`); directed rounding for intervals incurs mode-change cost | emulated on the stack, exact but slow |
| **RISC-V + Xposit** | native, via the extended posit instructions | native posit rounding; directed rounding follows the extension's definition | a hardware quire instruction backed by an architectural register, one cycle per FMA |
| **FPGA** (Xilinx, Lattice) | synthesized as the datapath at any width and configuration | a synthesis-time property: each datapath is wired to its rounding direction, no runtime cost | a fabric pipeline (512 bits for posit32), one MAC per cycle |
| **CGRA** (NextSilicon Maverick, Efficient Computer E1, SambaNova) | configurable at word granularity on the reconfigurable fabric, coarser than gate-level | set per processing-element configuration rather than per gate; directed rounding is a configuration property, not a runtime mode | mapped onto the PE array where the configuration admits a wide accumulator |
| **NPU / tile mesh** (AMD XDNA2, Tenstorrent) | the tile's supported formats, fixed by the architecture; custom formats only where the tile ALU admits them | fixed by the tile ALU; not generally a free per-operation choice | the tile's accumulator path, where one of adequate width is present |
| **GPU / APU** (SIMT; discrete or integrated) | not synthesizable; limited to the formats the lanes implement, so posit support is emulated | the lane's fixed rounding; directed rounding is emulated, at the per-operation cost of any software rounding | emulated in software; no native wide accumulator |
| **Neuromorphic** (Loihi 2) | not applicable to its arithmetic model | not applicable | unavailable |

Two groups divide the table. Reconfigurable substrates (FPGA, then CGRA) realize a chosen representation in their structure, so directed rounding and a quire carry no runtime cost. Fixed-ISA substrates (RISC-V, CPU, GPU/APU, NPU) realize in hardware only the formats their instruction set or tile defines; any other representation has to be emulated, or is out of reach. RISC-V with the Xposit extension is the fixed-ISA case that carries posits and the quire natively, which is what makes naming that target change the selection outcome. Where a target has no sound realization of a required capability, the capability coeffect simply does not discharge, and the mismatch is witnessed at design time rather than resolved into a lossy approximation.

## The posit's missing direction

The Posit Standard defines a single rounding mode, round-to-nearest, and no directed modes. Posit arithmetic addresses the usual motivation for directed rounding through a different mechanism, the quire, which makes accumulation exact rather than offering directed rounding.

A sound interval over posits therefore cannot obtain outward rounding from the posit arithmetic directly. The standard construction is to compute each endpoint round-to-nearest, then widen the low endpoint down by one unit in the last place and the high endpoint up by one. This is Moore's outward widening: it guarantees containment, at the cost of an enclosure slightly looser than one produced by native directed rounding. The design calls for this construction, with a diagnostic distinguishing a widened posit interval from a natively directed one, so the two are not conflated.

## The Quire

The quire is the clearest case of rounding as a deliberate design choice rather than a default.

An ordinary floating-point running sum rounds after every addition, and those roundings accumulate over a long sum. The quire avoids this. It is a wide accumulator, 512 bits for a 32-bit posit, large enough to hold every partial product of a long sum exactly, with no intermediate rounding. Rounding occurs once, at the final conversion of the accumulated value back to a posit: one rounding for the entire sum rather than one per step.

This single-rounding discipline is the source of the quire's two benefits. It keeps the structural zeros of a geometric-algebra computation exactly zero through training, because an exact sum of zeros remains zero and no intermediate rounding populates a component the algebra requires to be empty. And it defeats catastrophic cancellation, the loss of significance when nearly-equal large quantities are subtracted, by deferring all rounding until after the cancellation. None of this concerns precision near zero, which posits do not provide; it follows from rounding once, at the conversion, rather than at every step.

## Fixed-point and the overflow question

Fixed-point rounds at the least-significant retained bit, with the usual set of directions: toward nearest, toward zero, toward an extreme. The distinguishing concern for fixed-point is overflow, since a fixed-point format has a fixed maximum and minimum and a result can exceed them.

Overflow is handled one of two ways. Saturation clamps the result to the maximum or minimum representable value. Wrapping reduces the value modulo the range, as an odometer rolls past its last digit. For a physical quantity, saturation is generally correct: a clamped force or voltage is a bounded, identifiable error, whereas a wrapped value is unbounded and plausible-looking. The design defaults to saturation for dimensioned values for this reason, and treats the choice as one to be stated rather than assumed, because a silent wrap produces a value that passes testing and corrupts a long run.

## How the design carries rounding

The design treats rounding the way it treats representation and width: a property to be inferred and carried, not assumed. A precision-losing conversion carries its rounding choice as a coeffect, surfaced at the point of use. An interval holds its directed-rounding requirement in its type, so the requirement is checked when the value is formed rather than at runtime. Each rounding mode a value requires is gated against its target as a capability coeffect, with three outcomes: the target supplies the mode in hardware, supplies it at a cost the coeffect carries, or cannot supply it soundly, in which case the coeffect does not discharge and the mismatch is witnessed, never resolved by silently substituting a different mode.

The integer half of this discipline lowers to fabric today. Width inference ships, and these rounding rules ride the same coeffect carriage, the codata navigated through the program graph by the Huet-style zipper that carries every other inferred fact. For the real-valued half, our preliminary designs lean toward an interval type, per-operation rounding control, and a conversion syntax that names a rounding mode; the spec leaves the conversion and seal syntax [Not yet specified], and the [deferred-inference](/blog/deferred-inference/) and [posit-arithmetic](/docs/design/types/posit-arithmetic/) pages mark the same open territory.

In summary: IEEE 754 fixed rounding for every value because every value shared one representation. Selecting representation per target removes that uniformity, and this chapter specifies the consequence. Rounding becomes an explicit decision; it resolves differently on a mode-driven CPU than on a synthesis-fixed FPGA; and the design requires it to be made per target and never applied silently.
