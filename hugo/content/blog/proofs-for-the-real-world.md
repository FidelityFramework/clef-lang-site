---
title: "Building Proofs for the Real World"
linkTitle: "Proofs for the Real World"
description: "How Range Propagation Extends Design-Time Verification from Dimensional Consistency to Physical, Financial, and Clinical Safety Constraints"
date: 2026-04-02
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Safety", "Innovation"]
---

## Beyond Dimensional Consistency

Our [companion post](/blog/parametricity-and-dimensional-types/) that precedes this one expands on the formal lineage connecting Reynolds' abstraction theorem and Wadler's parametricity result to the Dimensional Type System's design-time verification. That lineage is one of several converging influences on the DTS design. Kennedy's Units of Measure [5] demonstrated that dimensional inference is practical in an ML-family language. Syme's work on F# [6] proved that the approach scales to production engineering. Gustafson's posit arithmetic [7] showed that numeric representation can be matched to the value ranges that dimensional analysis reveals. Wadler's contribution [1] provides the formal guarantee that the inferred types generate correct theorems. Together, these four lines of work inform a type system where dimensional annotations persist through compilation, guide representation selection, and, as this post will argue, generate physical safety proofs that establish a compute graph that is observent of reality.

This raises a useful question: how far can the proofs extend without annotation? Dimensional consistency tells us that a force in Newtons will not be accidentally added to a velocity in meters per second. That is valuable. But a practitioner working in aerospace, finance, or clinical medicine needs more than dimensional consistency. An aerospace engineer needs to know that the shear stress on a wing span never exceeds the yield strength of the material. A financial risk analyst needs to know that a portfolio's value-at-risk stays within regulatory limits. A medical clinician needs to know that a drug dosage stays within the therapeutic window for a given patient weight. Can the compiler derive these constraints from the computation graph, and can it show proof that those constraints hold?

The answer is yes, within well-characterized boundaries, and the mechanism is one the DTS/DMM paper already describes as foundational to Fidelity Framework's design.

## Range Propagation as Proof Machinery

The DTS includes a representation selection function (detailed in Section 2.6 of the [DTS/DMM paper](https://arxiv.org/abs/2603.16437)). Given a value with a declared dimensional range, the compiler evaluates candidate numeric representations (IEEE 754, b-posit at various widths) against worst-case relative error within the range. The mechanism is interval arithmetic over the computation graph: declared ranges at the inputs propagate through arithmetic operations to produce derived ranges at the outputs.

This mechanism was designed to select posit widths. It also generates safety proofs.

The interval arithmetic that propagates ranges through multiplication, division, addition, and subtraction is deterministic, bounded-time, and closed under the same operations that dimensional types are closed under. When the compiler propagates a range through a computation graph and the output range exceeds a declared bound, the violation is a design-time finding. No annotation is required beyond the input ranges and the material property that defines the bound. The computation graph determines the rest.

## Domain Case: Aerospace Structural Integrity

The following examples illustrate how range propagation produces domain-specific safety findings. Each uses the same underlying mechanism: interval arithmetic over the computation graph, compared against a declared bound. The domains differ; the compiler's role is identical. `Fidelity.Physics` provides the dimensional algebra — the measure types that make quantities type-safe. The application code declares constants, material properties, operating ranges, and the computation graph. The compiler provides the proof.

### Flight Envelope Analysis

Consider a simplified structural analysis. `MyApplication.Aerospace` provides the measure types (`Pa`, `N`, `gForce`, etc.) that make these quantities dimensionally typed. The constants, material properties, and operating ranges are all declared in the application:

```fsharp
open Fidelity.Physics


let gravitational_acc = 9.81<m * s^-2>
let yield_strength    = 2.7e8<Pa>     // aluminum 7075-T6

let aircraft_mass  = (5000.0<kg>, 80000.0<kg>)
let load_factor    = (1.0, 9.0)
let wing_spar_area = (0.01<m^2>, 0.1<m^2>)
```

The computation graph encodes the physics:

```fsharp
let lift_force   = aircraft_mass * gravitational_acc * load_factor
// inferred: float<N>, range [49050, 7063800]

let shear_stress = lift_force / wing_spar_area
// inferred: float<Pa>, range [490500, 706380000]

```

The compiler propagates the ranges through the arithmetic. Multiplication of intervals multiplies the endpoints (with appropriate handling of signs). Division divides with the corresponding endpoint pairing. At the output, the computed range of `shear_stress` is [490500, 706380000]. The declared yield strength is \(2.7 \times 10^8\) Pa. The upper bound of the computed range (\(7.06 \times 10^8\)) exceeds the yield strength.

The compiler reports this without any safety assertion from the programmer:

```
⚠ Range exceedance: shear_stress upper bound 7.06e8 <Pa>
  exceeds yield_strength 2.70e8 <Pa>
  Violation occurs when load_factor > 3.44
  (derived from computation graph)

  Trace:
    lift_force = aircraft_mass * gravitational_acc * load_factor
    shear_stress = lift_force / wing_spar_area

  Dimensional consistency: verified ✓
  Range analysis confidence: exact
    (monotonic arithmetic, all inputs range-declared,
     no control flow, no iteration)
```

The diagnostic tells the engineer not only that a violation exists but where in the operating envelope it occurs: load factors above 3.44 produce shear stresses that exceed the material limit. The engineer can then make an informed decision: restrict the operating envelope, increase the spar area, or select a stronger material. Each of these changes modifies the input ranges or the computation graph, and the compiler re-evaluates automatically.

## Domain Case: Financial Risk Constraints

The same mechanism applies to financial computation, where the "physical" constants are regulatory limits and the "material properties" are risk thresholds. The application opens `MuAuditApplication.Finance` for its measure types and declares everything else:

```fsharp
open Fidelity.Physics.Finance

// Regulatory and statistical constants
let confidence_z     = 2.326        // 99th percentile
let regulatory_limit = 5.0e7<USD>   // Tier 1 capital

// Portfolio-specific operating ranges
let notional       = (1e4<USD>, 1e9<USD>)
let leverage_ratio = (1.0, 30.0)
let volatility     = (0.05, 0.80)  // annualized
let holding_period = (1.0<days>, 10.0<days>)
```

The computation graph encodes the risk model:

```fsharp
let exposure     = notional * leverage_ratio
// inferred: float<USD>, range [1e4, 3e10]

let var_estimate = exposure * volatility * confidence_z * sqrt(holding_period)
// inferred: float<USD>, range [1162, 1.76e10]
 
```

The compiler reports:

```
⚠ Range exceedance: var_estimate upper bound 1.76e10 <USD>
  exceeds regulatory_limit 5.0e7 <USD>
  Violation occurs when leverage_ratio > 1.27 at max volatility
  (derived from computation graph)

  Dimensional consistency: verified ✓
  Range analysis confidence: exact
```

The dimensional types prevent a category of error that financial systems encounter routinely: confusing notional with exposure, mixing annualized volatility with daily volatility (different time dimensions), or computing VaR in one currency and comparing against a limit denominated in another. The range propagation then adds a second layer: even when the dimensions are correct, the computation may produce values that breach regulatory thresholds at certain points in the parameter space. The compiler identifies those points.

The computation graph is the risk model. The compiler verifies both dimensional consistency and regulatory compliance as design-time properties of the same graph traversal.

## Domain Case: Clinical Dosage Safety

In clinical pharmacology, the "material property" is the therapeutic window: the range of drug concentrations that are effective without being toxic. The application opens `Fidelity.Physics.Clinical` for its measure types and declares the pharmacokinetic constants, therapeutic bounds, and protocol-specific operating ranges:

```fsharp
open Fidelity.Physics.Clinical

// Drug-specific pharmacokinetic properties
let clearance_rate = 0.15<hr^-1>
let volume_dist    = 0.25<L * kg^-1>

// Therapeutic window (the safety constraint)
let min_effective  = 10.0<mg * L^-1>
let max_safe       = 40.0<mg * L^-1>

// Protocol-specific operating ranges
let patient_mass      = (40.0<kg>, 150.0<kg>)
let dose_rate         = (0.5<mg * kg^-1 * hr^-1>, 5.0<mg * kg^-1 * hr^-1>)
let infusion_duration = (0.5<hr>, 4.0<hr>)
```

The computation graph encodes the pharmacokinetic model:

```fsharp
let total_dose = dose_rate * patient_mass * infusion_duration
// inferred: float<mg>, range [10, 3000]

let peak_concentration = total_dose / (volume_dist * patient_mass)
// inferred: float<mg * L^-1>, range [0.4, 300]
 
```

The compiler reports two findings:

```
⚠ Range exceedance: peak_concentration upper bound 300 <mg * L^-1>
  exceeds max_safe 40.0 <mg * L^-1>
  Violation occurs when dose_rate > 1.0 at patient_mass = 40 kg,
  infusion_duration = 4.0 hr
  (derived from computation graph)

⚠ Range deficiency: peak_concentration lower bound 0.4 <mg * L^-1>
  below min_effective 10.0 <mg * L^-1>
  Sub-therapeutic when dose_rate < 1.25 at patient_mass = 150 kg,
  infusion_duration = 0.5 hr
  (derived from computation graph)
```

The dimensional types catch a class of clinical error that dimensional analysis was originally designed to prevent: confusing mg/kg (dose per body mass) with mg/L (plasma concentration), or applying a dose rate in mg/kg/hr to a duration in minutes without converting. These errors have caused patient harm in practice. The range propagation adds the therapeutic window check: the computation is dimensionally correct, but the dosage at certain combinations of patient mass, rate, and duration falls outside the safe range. The compiler identifies the specific parameter combinations.

Note that the compiler does not know pharmacology. It knows dimensional types, declared ranges, and interval arithmetic. The pharmacological knowledge is encoded in the application's constants, therapeutic bounds, and range declarations. `Fidelity.Physics.Clinical` contributes only the measure types that make those declarations dimensionally safe. The compiler's contribution is propagating the consequences through the computation graph exhaustively and exactly.

## The Common Pattern

An aerospace engineer, a risk analyst, and a clinical pharmacologist all receive the same class of compiler guarantee from the same toolchain. The three cases share a single underlying mechanism. The domains differ in their dimensional vocabularies. The physical constants, safety bounds, and operating ranges are all application concerns. The compiler's role is identical in all three: propagate ranges through the computation graph, compare the result against declared bounds, report findings with traces and confidence levels.

`Fidelity.Physics` provides dimensional algebra, not domain knowledge. Each sub-namespace (`Aerospace`, `Finance`, `Clinical`) declares the measure types that make a domain's quantities type-safe. The constants, material properties, regulatory limits, therapeutic windows, and operating ranges belong to the application. A financial risk model and a structural analysis both use the same compiler machinery. They differ in the measure types they import and in the constants and ranges they declare.

## The Mechanism Is Not New; the Application Is

The engineer declares constants, bounds, and operating ranges using the measure types that `Fidelity.Physics` provides. The compiler propagates them automatically, compares against declared bounds automatically, and produces diagnostics automatically. No manual interval tracking. No separate verification step.

Interval arithmetic itself is not novel; it has been studied extensively since Moore's foundational work in the 1960s. What is new is the integration with dimensional types and the computation graph. In conventional interval arithmetic, ranges are manually specified and manually propagated. In the Fidelity framework, the compilation graph provides the structure through which ranges propagate, and the DTS ensures that every propagation step is dimensionally consistent.

The same PSG node that carries a dimensional annotation (`<Pa>`) and a representation selection hint (posit32, bias at 1 MPa) also carries a propagated range ([490500, 706380000]). The three are computed by the same elaboration and saturation process. Dimensional consistency, representation adequacy, and physical range safety are three views of the same graph traversal.

## Higher-Order Range Propagation

The range propagation extends through higher-order functions by the same mechanism that dimensional types extend through them.

A first-order function with declared input ranges produces output ranges deterministically:

```fsharp
let computeStress (load : float<N>) (area : float<m^2>) : float<Pa> =
    load / area
     
```

The range of the output is the range of the numerator divided by the range of the denominator. When this function is called from a higher-order context, the compiler inlines the range propagation:

```fsharp
let safetyEnvelope (massRange : Range<kg>) (gRange : Range<1>) =
    let liftRange = massRange * g * gRange
    computeStress liftRange sparArea
 
```

The function `computeStress` has a known computation graph (a single division). Its range behavior is deterministic: output range equals input range divided by input range. The call from `safetyEnvelope` passes a derived range (the product of mass, gravitational acceleration, and G-force ranges) into `computeStress`, and the range propagation continues through the function boundary without interruption.

Parametricity ensures this works. The function `computeStress` is parametric in the magnitude of its inputs; it divides whatever it receives. The range propagation commutes with the function application for the same reason that dimensional annotations commute with compilation passes: the function does not inspect or modify the metadata, only the computational structure.

For functions that compose multiple range-carrying operations, the propagation chains:

```fsharp
let fullAnalysis mass gForce sparArea yieldStrength =
    let lift = mass * g * gForce
    let stress = lift / sparArea
    let safetyMargin = yieldStrength - stress
    safetyMargin
    // Compiler derives: range of safetyMargin
    // If lower bound < 0, the design is unsafe at some operating point
    // The specific operating point is derivable from the graph
     
```

The safety margin's range is computed from the chain of operations. If the lower bound is negative, the compiler reports the specific combination of input values that produces a negative margin. This is not a test; it is a proof over the declared operating envelope.

## Where Range Propagation Is Exact

The compiler's range analysis produces exact bounds (no false positives, no false negatives) when the computation satisfies three conditions:

**Monotonic arithmetic.** Multiplication of positive values, addition, subtraction, and division by positive values are all monotonic: the output range endpoints correspond to specific input range endpoints. The compiler evaluates the operation at the endpoint combinations and takes the extremes. This is exact.

**No control flow.** The computation is a straight-line sequence of arithmetic operations. There are no branches, no conditionals, no pattern matches that would split the range into cases. The range at every node is determined by a single arithmetic path from the inputs.

**All inputs are range-declared.** Every leaf value in the computation graph has a declared range or a known value (from explicit annotation, from a constant declaration, or from a material property). There are no unranged inputs whose range defaults to the full representable interval.

When all three conditions hold, the range analysis is a proof: the output is guaranteed to lie within the computed range for all inputs within the declared ranges. The compiler reports this as "range analysis confidence: exact."

Structural analysis, thermal analysis, fluid dynamics boundary conditions, sensor fusion pipelines, and electrical power budgets are typically straight-line arithmetic over declared physical ranges. They satisfy all three conditions. For these computations, the proofs are free for the same reason dimensional consistency is free: the computation graph determines the result.

## Where Range Propagation Is Conservative

The analysis becomes conservative (may report violations that cannot actually occur) in four specific cases:

**Branching control flow.** When a computation branches on a runtime condition, the range at the join point is the union of the ranges from both branches. The union may be wider than the actual range because the compiler does not know which branch will execute. For physical computations where branches correspond to operating regimes (subsonic vs. supersonic, laminar vs. turbulent), the union is often the correct range. But for branches that narrow the range (e.g., clamping a value), the conservative analysis may report a violation that the clamp prevents.

**Iteration with runtime-dependent bounds.** A loop whose iteration count depends on runtime data produces a range that the compiler must compute by fixed-point iteration over the interval. For bounded loops (whose bounds the coeffect system verifies), this converges. For unbounded loops, the range is not statically determinable.

**External inputs.** Values that enter from outside the compilation boundary (sensor readings, API responses, user input) carry only their declared range. The compiler cannot verify that the external source respects the declared range. The analysis is sound with respect to the declaration; it is not sound with respect to the actual source.

**Wide-interval transcendentals.** The sine of a wide interval is [-1, 1]. The exponential of a wide interval is [0, infinity). For computations that pass through transcendental functions with wide input ranges, the output range may be too wide for useful constraint checking. For narrow intervals around a specific operating point, the transcendental range propagation is precise.

In each of these cases, the compiler reports the confidence level alongside the finding:

```
⚠ Potential range exceedance: thermal_stress upper bound 3.1e8 <Pa>
  may exceed yield_strength 2.7e8 <Pa>
  Range analysis confidence: conservative
    (branch at line 47 widens interval; actual range may be narrower)
  Consider: add range assertion or narrow input range declaration
```

The diagnostic distinguishes between exact findings (proofs) and conservative findings (warnings). The engineer can tighten a conservative finding into an exact one by adding a range assertion at the branch point, which narrows the interval and may allow the analysis to discharge the constraint. This is the transition from Tier 1 (automatic) to Tier 2 (annotated), and it occurs only when the automatic analysis is insufficient.

## The Revised Tier Boundary

The Fidelity framework's [formal verification design](/docs/design/categorical-foundations/formal-verification-compilation-byproduct/) defined Tier 1 as dimensional consistency, escape classification, allocation verification, and capability checking. Range consistency and physical safety constraint checking belong in Tier 1 as well, when the computation satisfies the exactness conditions above.

The revised Tier 1 coverage:

| Property | Mechanism | Free? |
|---|---|---|
| Dimensional consistency | Abelian group algebra over type annotations | Yes |
| Escape classification | Coeffect propagation through PSG | Yes |
| Allocation verification | Escape classification mapped to target memory | Yes |
| Capability checking | Coeffect requirements against target profile | Yes |
| Representation selection | Interval arithmetic over dimensional ranges | Yes |
| Physical range safety | Same interval arithmetic, compared against physical bounds | Yes, when exact |

The last two rows use the same mechanism. The difference is what the computed range is compared against: a representation's dynamic range (for representation selection) or a physical property's value (for safety checking). The comparison target determines whether the finding is "use posit32" or "the wing breaks at G-force 3.44." The propagation is identical.

For the majority of safety-critical arithmetic, the engineer writes no proof code and no verification annotations. The compiler does the work at Tier 1. Tier 2 (scoped assertions) is needed only when the range analysis is conservative and the engineer requires a tighter bound. Tier 3 (full SMT proof generation) is needed for properties that range propagation cannot express: convergence, termination of iterative procedures, and correctness of algorithms whose safety depends on control flow, not arithmetic bounds.

The coeffect algebra that emerged from evaluating and departing from F*'s dependent type approach is precisely what makes range propagation composable: the same algebraic structure that tracks escape classification and memory lifetimes also tracks value ranges through the computation graph. The [verification internals](/docs/internals/verification/) cover this design path in full.

## Implications for Domain Libraries

The `Fidelity.Physics` library's role is to provide the dimensional vocabulary that makes a domain's quantities type-safe. Building a domain library is declaring which measure types exist and how they compose. The application then uses those types to declare its own constants, material properties, safety bounds, and operating ranges. The compiler enforces the consequences across every computation.

Three domain libraries illustrate the pattern:

```fsharp
module Fidelity.Physics.Aerospace =
    [<Measure>] type gForce     // dimensionless load factor
    [<Measure>] type knot       // nautical miles per hour

module Fidelity.Physics.Finance =
    [<Measure>] type USD
    [<Measure>] type EUR
    [<Measure>] type days
    [<Measure>] type years

module Fidelity.Physics.Clinical =
    [<Measure>] type mg
    [<Measure>] type hr
    [<Measure>] type L        // volume
     
```

Each library declares its own dimensional vocabulary. The application declares everything else: constants, bounds, and ranges for the specific design or protocol under analysis. The compiler treats all domains identically: propagate the application's ranges through the computation graph, compare against the application's bounds, report the findings. The dimensional algebra is in the library; everything else is in the application; the verification is in the compiler.

An aerospace application would use these types to declare its own material properties:

```fsharp
let aluminum7075 = {
    YieldStrength = 2.7e8<Pa>
    UltimateStrength = 5.7e8<Pa>
    Density = 2810.0<kg * m^-3>
    ThermalExpansion = 23.6e-6<K^-1>
}
```

The compiler combines these application-level declarations with the computation graph to produce safety findings without any per-function annotation. The library provides the type algebra; the application provides the constants, bounds, ranges, and computation graph; the range propagation is the verification. All compose at design time, and the proofs, where they are exact, are free.

## From Findings to Certificates

The design-time diagnostics shown in the examples above are not transient warnings. They are derived from Z3 proof obligations that CCS generates as it saturates the PSG. When the range analysis for `shear_stress` produces an exact finding, that finding corresponds to a resolved `QF_LIA` assertion in the PSG node. When `clef build --release` executes, those resolved assertions are aggregated into a global SMT problem, verified by Z3, and the resulting witness is cryptographically hashed alongside the compiled binary into a `.proofcert` artifact.

The certificate guarantees that every range finding reported at design time holds in the compiled output. The range exceedance at G-force 3.44, the VaR breach at leverage ratio 1.27, the therapeutic window violation at dose rate 1.0 for a 40 kg patient: each is a Z3-verified constraint that survives MLIR lowering through translation validation. The [verification internals](/docs/internals/verification/) document this pipeline from PSG saturation through the cryptographic release certificate.

The practical consequence is that the engineer who sees a range finding in Lattice during development can trust that the same constraint is enforced in the release binary. The proof does not exist only at design time. It ships with the artifact.

## Better Design; Safer Retults

This approach satisfies proofs for a large portion of the physical computations that safety-critical engineering depends on: structural loads, thermal gradients, pressure differentials, electrical power budgets, and sensor operating envelopes. Range propagation through the computation graph produces exact safety proofs for a well-defined class of computations: straight-line arithmetic over declared ranges with monotonic operations. 

The class does not include computations whose safety depends on control flow, iterative convergence, or runtime-dependent data. For those, the compiler produces conservative warnings that may require Tier 2 annotation to resolve. The boundary between "free proof" and "requires annotation" is determined by the computation's structure, not by the engineer's diligence. The compiler knows which case applies and reports accordingly.

The deeper point is that representation selection and safety constraint checking are the same mechanism applied to different comparands. The infrastructure for safety checking was present in the DTS from the beginning; it was designed for representation selection and deployed for that purpose. Extending it to physical safety constraints requires no new machinery, only the recognition that a range bound compared against a material property is the same operation as a range bound compared against a numeric format's dynamic range.

The four influences on this design each contribute a specific element. Kennedy's Units of Measure showed that dimensional inference is practical in a production language. Syme's F# proved it scales. Gustafson's posit arithmetic connected dimensional ranges to numeric representation selection, creating the interval propagation machinery. Wadler's parametricity result provides the formal guarantee that the propagated types generate correct theorems. Range propagation extends these theorems from dimensional consistency into the physical, financial, and clinical constraints that the types were designed to represent.

The proofs follow the computation graph as far as the arithmetic is monotonic, the inputs are declared, and the control flow is absent. For a significant and practically important class of computations across multiple domains, that is far enough. The domain library is the design-time decision that scopes the proofs to a specific field. And our compiler is a unique mechanism that delivers them.

## References

[1] P. Wadler, "Theorems for free!" in *Proceedings of the Fourth International Conference on Functional Programming Languages and Computer Architecture*, pp. 347-359, ACM, 1989.

[2] H. Haynes, "Dimensional Type Systems and Deterministic Memory Management: Design-Time Semantic Preservation in Native Compilation," [arXiv:2603.16437](https://arxiv.org/abs/2603.16437), 2026.

[3] R. E. Moore, *Interval Analysis*, Prentice-Hall, 1966.

[4] H. Haynes, "Decidable By Construction: Design-Time Verification for Trustworthy AI," [arXiv:2603.25414](https://arxiv.org/abs/2603.25414), 2026.

[5] A. Kennedy, "Types for Units-of-Measure: Theory and Practice," in *Central European Functional Programming School*, Springer LNCS 6299, 2009.

[6] D. Syme, A. Granicz, and A. Cisternino, *Expert F# 4.0*, Apress, 2015.

[7] J. L. Gustafson and I. T. Yonemoto, "Beating Floating Point at its Own Game: Posit Arithmetic," *Supercomputing Frontiers and Innovations*, vol. 4, no. 2, 2017.

[8] J. C. Reynolds, "Types, abstraction and parametric polymorphism," in *Information Processing 83*, pp. 513-523, North-Holland, 1983.