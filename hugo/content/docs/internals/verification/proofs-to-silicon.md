---
title: "From Proofs to Silicon"
linkTitle: "From Proofs to Silicon"
description: "MLIR translation validation, platform-aware representation selection, and the cryptographic release certificate"
weight: 40
date: 2026-02-25
authors: ["Houston Haynes"]
tags: ["MLIR", "Formal Methods", "Posit Arithmetic", "Architecture"]
params:
  originally_published: 2026-02-25
  migration_date: 2026-02-25
---

> This article is part of the [Transparent Verification](..) series. It follows
> the verified PSG from [Memory Coeffect Algebra](../memory-coeffect-algebra)
> through MLIR lowering to the final cryptographic release certificate.

## Translation Validation via the MLIR SMT Dialect

After the PSG reaches saturation, with every node stamped with its dimensional proof certificate and its coeffect resolution, the code is lowered to MLIR. Lowering introduces a new risk: optimizations can violate verified properties. The MLIR SMT dialect is designed to close this gap through **translation validation**.

The approach builds on the work of Fehr et al. (2025), who demonstrated "First-Class Verification Dialects for MLIR," the ability to express verification constraints directly within the IR rather than as external artifacts that can become disconnected from the code. Their work found five miscompilation bugs in upstream MLIR through this approach.

In the planned Fidelity pipeline, verification properties from the saturated PSG would be expressed as SMT assertions within the MLIR representation:

```mlir
func.func @normalizeScore(%input: i32) -> i32 {
  // Input precondition from PSG proof certificate
  %zero = arith.constant 0 : i32
  %hundred = arith.constant 100 : i32
  %input_gte_zero = smt.bv.sge %input, %zero : !smt.bv<32>
  %input_lte_hundred = smt.bv.sle %input, %hundred : !smt.bv<32>
  %precondition = smt.and %input_gte_zero, %input_lte_hundred : !smt.bool
  smt.assert %precondition

  // Function implementation
  %c0 = arith.constant 0 : i32
  %c10 = arith.constant 10 : i32
  %c100 = arith.constant 100 : i32

  %lt_zero = arith.cmpi slt, %input, %c0 : i32
  %gt_hundred = arith.cmpi sgt, %input, %c100 : i32

  %div10 = arith.divsi %input, %c10 : i32

  %result = scf.if %lt_zero -> i32 {
    scf.yield %c0 : i32
  } else {
    %temp = scf.if %gt_hundred -> i32 {
      scf.yield %c10 : i32
    } else {
      scf.yield %div10 : i32
    }
    scf.yield %temp : i32
  }

  // Postcondition verification
  %result_gte_zero = smt.bv.sge %result, %zero : !smt.bv<32>
  %result_lte_ten = smt.bv.sle %result, %c10 : !smt.bv<32>
  %postcondition = smt.and %result_gte_zero, %result_lte_ten : !smt.bool
  smt.assert %postcondition

  return %result : i32
}
```

When an optimization pass transforms the MLIR, the SMT dialect validates that the transformation preserves the verified properties:

```mlir
// Translation validation: does the optimization preserve semantics?
%src_formula = smt.extract_semantics %original : !mlir.operation -> !smt.formula
%tgt_formula = smt.extract_semantics %optimized : !mlir.operation -> !smt.formula
%refines = smt.implies %src_formula, %tgt_formula : !smt.bool
smt.check_sat %refines : !smt.bool
```

If an optimization would violate a verified property, the SMT solver rejects the transformation. This is designed to ensure that the mathematically verified properties survive the entire journey from the high-level PSG down to the lowest-level hardware dialects (`hw`, `comb`, `rocdl`, or `spirv`).

### Proof Before Lowering

A critical architectural distinction in the Fidelity Framework is *when* these proofs are generated. CCS is designed to perform its rigorous analysis and generate the proof certificate **before** any MLIR lowering occurs. This guarantees that the source of truth is the high-level native AST. By solving the constraints at the highest possible semantic level, the system captures the developer's exact domain intent before it is lowered into control flow graphs and target-specific dialects. The Alex middle-end will embed the Z3 proofs already solved in the PSG directly into the Intermediate Representation.

## Platform-Aware Resolution

Because dimensional annotations are designed to survive through MLIR dialect lowering, the compiler can make representation decisions that erasure-based systems cannot. The same Clef source compiled for different targets would produce different numeric representations, each optimal for its platform:

```fsharp
let computeForce (m1: float<kg>) (m2: float<kg>) (r: float<m>) : float<newtons> =
    let g = 6.674e-11<m^3 * kg^-1 * s^-2>
    g * m1 * m2 / (r * r)
```

Lattice plans to display the cross-target resolution as a design-time diagnostic:

```
computeForce: float<kg> -> float<kg> -> float<m> -> float<newtons>
  ├─ x86_64:  float64 → float64 → float64 → float64
  │           Precision: 1.11e-16 relative error (uniform)
  ├─ xilinx:  posit32 → posit32 → posit32 → posit32
  │           Precision: ~1.5e-9 in [0.01, 100], ~3.9e-3 at regime extremes
  │           Quire: available, 512-bit register pipeline
  │           Dynamic range: [1e-36, 1e36] covers [1e-2, 1e25]
  └─ Transfer (xilinx → x86_64): posit32 → float64
             Protocol: BAREWire over PCIe
             Fidelity: 1.0 (lossless; float64 range exceeds posit32 range)
```

IEEE 754 distributes precision uniformly across its representable range. A `float64` allocates the same number of mantissa bits to values near 1.0 as to values near \(10^{300}\). For computations whose values span a narrow dimensional range, the majority of IEEE 754's precision budget is allocated to ranges the computation will never visit. Gustafson's posit arithmetic uses *tapered precision*, concentrating mantissa bits near 1.0 where most computations cluster. DTS is designed to provide the formal mechanism for what posit arithmetic presupposes: knowledge of which value ranges matter for a given computation. The dimensional annotation constrains the semantic range; the compiler would evaluate how different representations distribute precision across that range and select the one that minimizes worst-case relative error.

The representation selection is deterministic. Given a value \(v\) with dimension \(d\) and a value range \([a, b]\) inferred from dimensional constraints, and a set of available representations \(R = \{r_1, \ldots, r_k\}\) on target \(T\), the compiler selects:

\[r^* = \arg\min_{r \in R} \max_{x \in [a,b]} \frac{|x - \text{round}_r(x)|}{|x|}\]

This is a deterministic function from dimensional constraints and target capabilities to a code generation decision, designed to be computable at compile time from properties of the PSG.

## The Quire: Where DTS and DMM Converge

The posit quire accumulator provides a concrete illustration of how DTS and DMM converge on a single construct. A quire is a fixed-width exact accumulator that holds intermediate results of multiply-add operations without rounding; rounding occurs once when the final result is converted back to a posit value. The Posit Standard (2022) defines the quire width as \(n^2/2\) bits for an \(n\)-bit posit, yielding a 512-bit accumulator for posit32.

```fsharp
let work (forces: Span<float<newtons>>) (distances: Span<float<meters>>) : float<joules> =
    let mutable q = Quire.zero
    for i in 0 .. forces.Length - 1 do
        q <- Quire.fma q forces.[i] distances.[i]  // dimension: newtons * meters = joules
    Quire.toPosit q  // single rounding, dimension preserved
```

From the DTS perspective, the quire is a numeric container whose dimensional semantics are determined by the posit values it accumulates. DTS would infer that `q` carries dimension `joules` and that the final conversion preserves this dimension. The source code carries no dimensional annotations beyond the parameter types.

From the DMM perspective, the quire is a memory resource with specific coeffect requirements:

| Target | Quire Support | Coeffect Resolution |
|---|---|---|
| x86_64 | Software emulation (64 bytes on stack for posit32) | Allocation: stack; Performance: ~50 cycles per FMA |
| Xilinx FPGA | Dedicated register pipeline | Allocation: register file; Performance: 1 cycle per FMA |
| RISC-V + Xposit | Hardware quire instruction | Allocation: architectural register; Performance: 1 cycle per FMA |
| Neuromorphic (Loihi 2) | Not available | Capability failure: "exact accumulation requires quire support" |

The convergence is in the PSG. The quire node would carry dimensional annotations (from DTS), allocation and lifetime annotations (from DMM), and capability annotations (from the coeffect system). All three are properties of the same graph node, resolved by the same inference pipeline, and planned to be visible through the same language server interface:

```
q: Quire (exact accumulator)
  Dimension: joules (inferred from fma operands)
  ├─ x86_64:  stack, 64 bytes, 1 cache line, ~50 cycles/fma
  ├─ xilinx:  register pipeline, 16 × 32-bit, 1 cycle/fma
  └─ loihi2:  ✗ not available (no exact accumulation support)
  Lifetime: loop scope (lines 3-5), no escape detected
```

The quire is a value with dimensional, allocation, and capability properties that the DTS+DMM framework is designed to handle through its standard inference and coeffect machinery, requiring no special-case compiler support.

## The Cryptographic Release Certificate

The complete pipeline, from DTS inference through DMM coeffect resolution, Z3 proof discharge, PSG saturation, and MLIR translation validation, is designed to culminate in a single artifact: the **cryptographic release certificate**.

The culmination of this architecture occurs when the developer executes `clef build --release`. At this point, the compilation shifts from interactive design-time guidance to immutable, cryptographic certification:

1. **The Final Freeze.** The PSG is locked. The boundary conditions are fixed across all target architectures (Zen 5 CPU, RDNA 3.5 GPU, XDNA 2 NPU, and Arty A7 FPGA).
2. **The Global SMT Theorem.** CCS aggregates the constraints derived from the entire dependency graph into a single, comprehensive SMT-LIB2 problem.
3. **Witness Generation.** Z3 executes a strict, global verification run. Upon a `SAT` result, proving that no dimensional bounds are exceeded, no memory lifetimes are violated, and no BAREWire layouts are breached, Z3 generates a mathematical witness.
4. **Binary Stamping.** CCS compiles the final LLVM (or CIRCT SystemVerilog) output and cryptographically hashes the binary alongside the Z3 witness, embedding the certificate directly into a `.proofcert` file or a dedicated ELF section.

The certificate is designed to guarantee:

- **Dimensional consistency.** Every arithmetic operation respects the algebraic constraints of its physical domain.
- **Memory safety.** Every value's lifetime is sufficient for its usage, verified through escape analysis and coeffect propagation.
- **Representation fidelity.** Every cross-target transfer preserves numeric precision within documented bounds.
- **Optimization correctness.** Every MLIR transformation has been validated to preserve the verified properties of the source.

### Static Elimination of Runtime Safety Mechanisms

Because memory layout decisions are made at the Clef source level and their access patterns are strictly mapped and verified via SMT constraints, the resulting binary is designed to impose no runtime overhead for the verified properties. The DMM tracking would prove that memory lifetimes are strictly bounded, eliminating the need for a garbage collector or an OOM killer to intervene. For stack-scoped buffers with statically known sizes, escape analysis closes the set of access sites, allowing the compiler to verify bounds at compile time and eliminate runtime bounds checks for those allocations.

When a Clef `CpuOrchestrator` executes a 330-microsecond BAREWire Ethernet loop to coordinate a heterogeneous physics simulation across Zen 5, XDNA 2, and an Arty A7, the goal is for it to run at the full speed of the silicon, backed by a cryptographic guarantee that every dimension, lifetime, and transfer has been verified.

## The Fidelity Framework Difference

The Fidelity Framework's integrated approach to verification is designed to collapse several traditionally separate concerns into a single design-time resource:

| Concern | Traditional Approach | Fidelity/Clef Approach |
|---|---|---|
| Dimensional correctness | Erasure (F# UoM) or annotation burden (F\*) | Transparent inference via DTS + Z3 |
| Memory safety | Manual lifetime annotations (Rust) or runtime checks | Coeffect inference with escape classification |
| Cross-target compilation | Separate build configs, no semantic preservation | Dimensional preservation guides representation selection |
| Optimization verification | Trust the optimizer | Translation validation via MLIR SMT dialect |
| Design-time feedback | Separate linting/analysis tools | PSG is the analysis, a compilation byproduct |

The PSG is designed to persist as a long-lived data structure maintained by the language server. The elaborated, saturated graph would serve as the data source for all design-time services: hover information, resolution panels, diagnostic overlays, and restructuring suggestions. These feedback categories are all properties of the PSG that the compiler computes as part of normal compilation. Lattice will read the PSG; the design-time tooling is a view over the compilation graph.

The information accrual principle formalizes why preservation matters. Each compilation stage has strictly more information than its predecessor:

\[I_{\text{source}} \subset I_{\text{PSG}} \subset I_{\text{MLIR}} \subset I_{\text{MLIR-opt}} \subset I_{\text{LLVM}} \subset I_{\text{native}}\]

Decisions that can be deferred to later compilation stages should be, because later stages have strictly more information. Dimensional annotations preserved through early stages would enable representation selection at the MLIR level, where the target architecture is known. If the dimensions were erased at the source level, the representation selection decision would be impossible at the point where it can be made optimally.

## Patent-Pending Innovation

SpeakEZ has a patent pending for this innovation: "System and Method for Verification-Preserving Compilation Using Formal Certificate Guided Optimization" (US 63/786,264). This patent application covers the approach to verification-preserving compilation that the Fidelity Framework represents.

As computing evolves toward greater specialization of hardware, the challenge of correctly interfacing with diverse architectures becomes increasingly critical. The patent-pending technology enables hardware/software co-design where verification properties are maintained across the entire compilation pipeline despite aggressive optimizations targeting specialized hardware. This becomes especially important as heterogeneous computing environments with CPUs, GPUs, FPGAs, and domain-specific accelerators become the norm.

---

## References

1. Kennedy, A. "Types for Units-of-Measure: Theory and Practice," in *Central European Functional Programming School*, Springer LNCS 6299, 2009.
2. Swamy, N. et al. "Dependent types and multi-monadic effects in F\*," in *POPL*, 2016.
3. Fehr, M., Fan, Y., Pompougnac, H., Regehr, J., & Grosser, T. "First-Class Verification Dialects for MLIR." *Proceedings of the ACM on Programming Languages*, 9(PLDI), Article 206, 2025.
4. Petricek, T., Orchard, D., and Mycroft, A. "Coeffects: A calculus of context-dependent computation," in *ICFP*, 2014.
5. Gustafson, J. L. and Yonemoto, I. T. "Beating Floating Point at its Own Game: Posit Arithmetic," *Supercomputing Frontiers and Innovations*, vol. 4, no. 2, 2017.
6. Posit Working Group, "Standard for Posit Arithmetic (2022)," posithub.org, 2022.
7. Baydin, A. G., Pearlmutter, B. A., Syme, D., Wood, F., and Torr, P. "Gradients without Backpropagation," *arXiv preprint arXiv:2202.08587*, 2022.
8. Lattner, C. et al. "MLIR: Scaling compiler infrastructure for domain specific computation," in *CGO*, 2021.
