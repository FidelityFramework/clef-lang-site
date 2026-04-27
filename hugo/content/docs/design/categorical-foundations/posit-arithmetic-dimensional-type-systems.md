---
title: "Posit Arithmetic and Dimensional Type Systems"
linkTitle: "Posit Arithmetic & DTS"
description: "Representation Selection for Domain-Aware Computation"
date: 2025-08-17T10:00:00+06:00
weight: 02
authors: ["Houston Haynes"]
tags: ["Architecture", "HPC", "Innovation"]
params:
  originally_published: 2025-08-17
  original_url: "https://speakez.tech/blog/posit-arithmetic-dimensional-type-systems/"
  migration_date: 2026-02-15
---

## The Representation Selection Problem

IEEE 754 was ratified in 1985, and nearly every numeric computation written since has passed through it. The standard answered a real question about reproducibility across hardware vendors, and answered it well enough that four decades of software have been built on top of it. The design choices it accepted in 1985 have begun to show their limits on workloads that emerged long after: high-performance computing at extreme dynamic range, machine learning at distributions concentrated near zero.

The design allocates precision uniformly across the representable range. A `float64` gives the same number of mantissa bits to values near 1.0 as to values near \(10^{300}\). For many computational domains this uniformity is wasteful. Gravitational forces span roughly \(10^{-11}\) to \(10^{30}\) newtons. Membrane potentials range from -80 to +40 millivolts. Sensor readings cluster between 0 and 100 celsius. In each case, the majority of IEEE 754's precision budget is allocated to ranges the computation will never visit.

Gustafson and Yonemoto's posit arithmetic [1] addresses this with *tapered precision*: a variable-length regime field concentrates mantissa bits near 1.0, where most computed values reside, and reduces precision toward the extremes. The Posit Standard (2022) [2] unified the exponent size (es = 2) across all bit widths, simplifying both hardware implementation and compiler modeling.

The two representations make different tradeoffs. IEEE 754 provides uniform relative error of approximately \(2^{-52}\) for `float64`, independent of value magnitude. Posit32 with es = 2 provides approximately \(2^{-27}\) relative error near 1.0, degrading to approximately \(2^{-8}\) at regime extremes. For computations whose values concentrate near unity, posit provides better precision per bit. For computations that span the full representable range with equal probability, IEEE 754's uniformity is the correct choice.

Jonnalagadda, Thotli, and Gustafson's bounded posit (b-posit) [4] constrains the regime field so that decode and encode circuits match IEEE 754 in area and latency while preserving posit's tapered-precision accuracy. With the hardware gap closed, the choice between IEEE 754 and a posit variant becomes purely an accuracy-range question within the dimensional range the value actually occupies, which is the question dimensional annotations let the compiler answer.

The question is: how does the compiler know which case applies?

## Dimensional Annotations as Range Constraints

This is where Dimensional Type Systems (DTS) enter. In the Fidelity framework's type system, every numeric value carries a dimensional annotation that survives compilation. A `float<newtons>` does not reify to `float64` during code generation; the annotation persists as an MLIR attribute through each lowering stage. This is the key distinction from approaches like F#'s Units of Measure [3], where dimensions are erased before code generation.

The dimensional annotation constrains the value's semantic range. A value with dimension *newtons* in a gravitational simulation operates within the range determined by the gravitational constant (\(\sim 6.674 \times 10^{-11}\;\text{m}^3\,\text{kg}^{-1}\,\text{s}^{-2}\)), plausible masses, and plausible distances. This range is computable from the dimensional constraints, domain annotations, or platform binding specifications available at compile time.

Given this range, the compiler can evaluate representation candidates against a concrete criterion: worst-case relative error within the value domain.

Formally, given a value \(v\) with dimension \(d\), a value range \([a, b]\) inferred from dimensional constraints, and a set of available representations \(R = \{r_1, \ldots, r_k\}\) on target \(T\), the compiler selects:

\[r^* = \arg\min_{r \in R} \max_{x \in [a,b]} \frac{|x - \text{round}_r(x)|}{|x|}\]

This is a deterministic function from dimensional constraints and target capabilities to representation choice. It is computable at compile time, and its inputs are properties of the Program Semantic Graph (PSG) that the compiler already maintains for other purposes.

## The Quire: A Coeffect Case Study

The posit quire accumulator illustrates how representation selection interacts with memory management. A quire holds intermediate results of multiply-add operations without rounding; rounding occurs once, when the final result is converted back to a posit value [1]. The Posit Standard (2022) [2] defines the quire width as \(n^2/2\) bits for an \(n\)-bit posit, yielding a 512-bit accumulator for posit32.

This fixed relationship between posit precision and quire width makes compiler modeling straightforward. For posit32:

- **Size:** 512 bits = 64 bytes = exactly one cache line on typical architectures
- **Allocation:** stack-eligible for short-lived accumulations, arena-eligible for long-lived ones
- **FPGA mapping:** a 512-bit value mapped to fabric by the synthesis tool, pipelined for single-cycle FMA throughput
- **Neuromorphic:** unavailable (the target lacks sufficient accumulator width)

In the Fidelity framework, these properties are tracked as coeffects, contextual requirements that a computation imposes on its environment. The quire requires a specific amount of memory (allocation coeffect), must persist for the duration of the accumulation loop (lifetime coeffect), and may not be available on all targets (capability coeffect).

The convergence is visible in the PSG. A quire node carries:

- Dimensional annotations from DTS (e.g., *joules*, inferred from the operands of fused multiply-add)
- Allocation and lifetime annotations from DMM (stack, loop scope, no escape detected)
- Capability annotations from the coeffect system (available on CPU and FPGA, unavailable on neuromorphic)

All three are properties of the same graph node, resolved by the same inference pipeline, and visible through the same language server interface:

```
q: Quire (exact accumulator)
  Dimension: joules (inferred from fma operands)
  ├─ x86_64:  stack, 64 bytes, 1 cache line, ~50 cycles/fma
  ├─ xilinx:  512-bit fabric pipeline, 1 cycle/fma
  └─ loihi2:  ✗ not available (no exact accumulation support)
  Lifetime: loop scope (lines 3-5), no escape detected
```

The quire is a value with dimensional, allocation, and capability properties that the existing DTS+DMM framework handles through its standard inference and coeffect machinery, with no custom compiler support required.

## Cross-Target Transfer Fidelity

When a computation spans multiple hardware targets, values must cross target boundaries. A posit32 result computed on an FPGA may need to be consumed by IEEE 754 code running on the host CPU. The transfer has a precision cost, and DTS provides the mechanism for quantifying it.

Every posit32 value within its representable range (\(\sim 10^{-36}\) to \(\sim 10^{36}\)) is exactly representable in `float64`, which covers \(10^{-308}\) to \(10^{308}\). The transfer from posit32 to `float64` is lossless; the compiler can prove this at compile time from the representation specifications. The reverse direction (float64 to posit32) incurs precision loss that depends on the dimensional range of the value being transferred.

The BAREWire protocol handles the binary encoding and transport. The DTS framework handles the semantic analysis: what precision is lost, where, and whether the loss is acceptable for the computation's dimensional requirements. The language server can display this analysis as a diagnostic on any value that crosses a target boundary:

```
Transfer (xilinx → x86_64): posit32 → float64
  Protocol: BAREWire over PCIe
  Fidelity: 1.0 (lossless; float64 range exceeds posit32 range)
```

```
Transfer (x86_64 → xilinx): float64 → posit32
  Protocol: BAREWire over PCIe
  Fidelity: 0.87 (precision loss at range extremes)
  Dimensional range [1e-11, 1e72] exceeds posit32 dynamic range [1e-36, 1e36]
  Suggestion: scale to AU (fits posit range)
```

The suggestion to rescale is itself a dimensional operation. The compiler knows the conversion factor and can verify that the rescaled computation remains dimensionally consistent. This guidance is possible only because the dimensional annotation survives to the point where representation selection and transfer analysis occur.

In the sheaf-theoretic framing developed in [the compilation sheaf design document](/docs/design/categorical-foundations/the-compilation-sheaf/), representation selection is a global section optimization on the representation sheaf: which assignment of representations to PSG nodes minimizes total worst-case relative error while remaining globally coherent with respect to the transfer boundary structure maps? The argmin formulation above is the local objective at each node; the global section condition is what ties the local choices together so that values flowing across target boundaries arrive in formats that the receiving target can handle without additional precision loss. Cross-target transfer fidelity is structure map evaluation: a lossless transfer is an isomorphism of stalks (the source representation embeds exactly into the target representation), and a lossy transfer is a structure map with a non-trivial kernel (information about the source value that does not survive into the target value). The fidelity score is a measure of how much of the source stalk the structure map preserves, quantified by the dimensional range analysis.

## The Complementarity

Posit arithmetic and dimensional type systems were not designed with each other in mind. Gustafson's work [1] addresses the numeric representation problem from the hardware and arithmetic side. Kennedy's work on Units of Measure [3] addresses the dimensional correctness problem from the type theory side. The Posit Standard (2022) [2] formalized quire widths and exponent sizes. The DTS/DMM paper formalizes dimensional preservation and coeffect-based memory management.

The complementarity is structural. Posit arithmetic presupposes that the compiler or engineer knows which value ranges matter for a given computation. DTS provides the formal mechanism for that knowledge. The quire presupposes that memory management is deterministic and verifiable. DMM as a coeffect discipline provides that guarantee.

Posits without dimensional range analysis require manual representation selection; the engineer must know the value distribution and choose the posit width accordingly. DTS without domain-matched representations can verify dimensional consistency but cannot exploit it for precision optimization. Combined in the PSG, the compiler selects representations automatically, tracks their memory and lifetime requirements, verifies cross-target transfer fidelity, and surfaces the complete analysis at design time.

The [DTS/DMM paper](/publications/dts-dmm/) provides the formal treatment. Sections 2.6 (representation selection), 3.5 (quire as coeffect case study), and 5.6 (posit arithmetic in related work) contain the complete analysis. This entry provides the practitioner-facing summary.

## References

[1] J. L. Gustafson and I. T. Yonemoto, "Beating Floating Point at its Own Game: Posit Arithmetic," *Supercomputing Frontiers and Innovations*, vol. 4, no. 2, 2017.

[2] Posit Working Group, "Standard for Posit Arithmetic (2022)," posithub.org, 2022.

[3] A. Kennedy, "Types for Units-of-Measure: Theory and Practice," in *Central European Functional Programming School*, Springer LNCS 6299, 2009.

[4] A. A. Jonnalagadda, R. Thotli, and J. L. Gustafson, "Closing the Gap Between Float and Posit Hardware Efficiency," arXiv:2603.01615, 2026.
