---
title: "Building Proofs for the Real World"
linkTitle: "Proofs for the Real World"
description: "How Range Propagation Extends Design-Time Verification from Dimensional Consistency to Physical, Financial, and Clinical Safety Constraints"
date: 2026-04-02
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Safety", "Innovation"]
---

## Beyond Dimensional Consistency

A companion line of work expands on the formal lineage connecting Reynolds' abstraction theorem and Wadler's parametricity result to the Dimensional Type System's design-time verification. That lineage is one of several converging influences on the DTS design. Kennedy's Units of Measure [5] demonstrated that dimensional inference is practical in an ML-family language. Syme's work on F# [6] proved that the approach scales to production engineering. Gustafson's posit arithmetic [7] showed that numeric representation can be matched to the value ranges that dimensional analysis reveals. Wadler's contribution [1] provides the formal guarantee that the inferred types generate correct theorems. Together, these four lines of work inform a type system where dimensional annotations persist through compilation, guide representation selection, and, as this post argues, generate physical safety proofs that hold the computation graph to declared physical bounds.

This raises a useful question: how far can the proofs extend without annotation? Dimensional consistency tells us that a force in Newtons will not be accidentally added to a velocity in meters per second. That is valuable. But a practitioner working in aerospace, finance, or clinical medicine needs more than dimensional consistency. An aerospace engineer needs to know that the shear stress on a wing span never exceeds the yield strength of the material. A financial risk analyst needs to know that a portfolio's value-at-risk stays within regulatory limits. A medical clinician needs to know that a drug dosage stays within the therapeutic window for a given patient weight. Can the compiler derive these constraints from the computation graph, and can it show proof that those constraints hold?

The answer is yes, within well-characterized boundaries, and the mechanism is one the DTS/DMM paper already describes as foundational to Fidelity Framework's design.

## Range Propagation as Proof Machinery

The DTS includes a representation selection function (detailed in Section 2.6 of the [DTS/DMM paper](https://arxiv.org/abs/2603.16437)). Given a value with a declared dimensional range, the compiler evaluates candidate numeric representations (IEEE 754, [b-posit](https://arxiv.org/abs/2603.01615) at various widths) against worst-case relative error within the range. The mechanism is interval arithmetic over the computation graph: declared ranges at the inputs propagate through arithmetic operations to produce derived ranges at the outputs.

This mechanism was designed to select posit widths. It also generates safety proofs.

`Fidelity.Physics` supplies the type that makes this possible. Alongside its measure type declarations, it provides a generic `Range` type:

```fsharp
type Range<[<Measure>] 'T, [<Measure>] 'U> =
    Range of lower: 'T<'U> * upper: 'T<'U>
```

Both type parameters are measures in the abelian group algebra. `Range` is what distinguishes a pair of propagation bounds from an ordinary pair of values. When the application writes `Range(5000.0<kg>, 80000.0<kg>)`, the compiler knows to propagate that interval through every arithmetic operation in the computation graph.

Sound transfer rules propagate ranges through supported arithmetic operations, accounting for conditions such as a divisor excluding zero. When an output enclosure exceeds a declared bound, the compiler reports a potential violation. Establishing an actual violation requires a reachable counterexample or a sufficiently precise analysis. The obligations come from the computation graph and its input contracts without per-function proof annotations.

## Domain Case: Aerospace Structural Integrity

The following examples illustrate how range propagation produces domain-specific safety findings. Each uses the same underlying mechanism: interval arithmetic over the computation graph, compared against a declared bound. The domains differ; the compiler's role is identical. `Fidelity.Physics` provides the dimensional algebra, the measure types that make quantities type-safe. The application code declares constants, material properties, operating ranges, and the computation graph. The compiler provides the proof.

### Flight Envelope Analysis

Consider a simplified structural analysis. `Fidelity.Physics` provides the measure types, derived measures like `Pa`, and universal constants like `gravitational_acc`. The application declares its own material properties and operating ranges:

```fsharp
open Fidelity.Physics
// Fidelity.Physics provides:
//   [<Measure>] type kg
//   [<Measure>] type m
//   [<Measure>] type s
//   [<Measure>] type Pa = kg * m^-1 * s^-2
//   let gravitational_acc = 9.81<m * s^-2>

let yield_strength = 2.7e8<Pa>     // aluminum 7075-T6

let aircraft_mass  = Range(5000.0<kg>, 80000.0<kg>)
let load_factor    = Range(1.0, 9.0)
let wing_spar_area = Range(0.01<m^2>, 0.1<m^2>)
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
⚠ Range exceeded: shear_stress upper bound 7.06e8 <Pa>
  exceeds yield_strength 2.70e8 <Pa>
  Occurs when load_factor > 3.44
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

The same mechanism applies to financial computation, where the "physical" constants are regulatory limits and the "material properties" are risk thresholds. `Fidelity.Physics.Finance` provides the currency and time measure types. The application declares its regulatory constants and portfolio-specific operating ranges:

```fsharp
open Fidelity.Physics.Finance
// Fidelity.Physics.Finance provides:
//   [<Measure>] type USD
//   [<Measure>] type EUR
//   [<Measure>] type days
//   [<Measure>] type years
//   let tradingDaysPerYear = 252.0<days * years^-1>

// Regulatory and statistical constants
let confidence_z     = 2.326        // 99th percentile
let regulatory_limit = 5.0e7<USD>   // Tier 1 capital

// Portfolio-specific operating ranges
let notional       = Range(1e4<USD>, 1e9<USD>)
let leverage_ratio = Range(1.0, 30.0)
let volatility     = Range(0.05, 0.80)  // annualized
let holding_period = Range(1.0<days>, 10.0<days>)
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
⚠ Range exceeded: var_estimate upper bound 1.76e10 <USD>
  exceeds regulatory_limit 5.0e7 <USD>
  Occurs when leverage_ratio > 1.27 at max volatility
  (derived from computation graph)

  Dimensional consistency: verified ✓
  Range analysis confidence: exact
```

The dimensional types prevent a category of error that financial systems encounter routinely: confusing notional with exposure, mixing annualized volatility with daily volatility (different time dimensions), or computing VaR in one currency and comparing against a limit denominated in another. The range propagation then adds a second layer: even when the dimensions are correct, the computation may produce values that breach regulatory thresholds at certain points in the parameter space. The compiler identifies those points.

The computation graph is the risk model. The compiler verifies both dimensional consistency and regulatory compliance as design-time properties of the same graph traversal.

## Domain Case: Clinical Dosage Safety

In clinical pharmacology, the "material property" is the therapeutic window: the range of drug concentrations that are effective without being toxic. `Fidelity.Physics.Clinical` provides the clinical measure types and derived concentration dimensions. The application declares pharmacokinetic constants, therapeutic bounds, and protocol-specific operating ranges:

```fsharp
open Fidelity.Physics.Clinical
// Fidelity.Physics.Clinical provides:
//   [<Measure>] type mg
//   [<Measure>] type hr
//   [<Measure>] type L
//   [<Measure>] type concentration = mg * L^-1
//   [<Measure>] type doseRate = mg * kg^-1 * hr^-1

// Drug-specific pharmacokinetic properties
let clearance_rate = 0.15<hr^-1>
let volume_dist    = 0.25<L * kg^-1>

// Therapeutic window (the safety constraint)
let min_effective  = 10.0<mg * L^-1>
let max_safe       = 40.0<mg * L^-1>

// Protocol-specific operating ranges
let patient_mass      = Range(40.0<kg>, 150.0<kg>)
let dose_rate         = Range(0.5<mg * kg^-1 * hr^-1>, 5.0<mg * kg^-1 * hr^-1>)
let infusion_duration = Range(0.5<hr>, 4.0<hr>)
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
⚠ Range exceeded: peak_concentration upper bound 300 <mg * L^-1>
  exceeds max_safe 40.0 <mg * L^-1>
  Occurs when dose_rate > 1.0 at patient_mass = 40 kg,
  infusion_duration = 4.0 hr
  (derived from computation graph)

⚠ Below range: peak_concentration lower bound 0.4 <mg * L^-1>
  below min_effective 10.0 <mg * L^-1>
  Sub-therapeutic when dose_rate < 1.25 at patient_mass = 150 kg,
  infusion_duration = 0.5 hr
  (derived from computation graph)
```

The dimensional types catch a class of clinical error that dimensional analysis was originally designed to prevent: confusing mg/kg (dose per body mass) with mg/L (plasma concentration), or applying a dose rate in mg/kg/hr to a duration in minutes without converting. These errors have caused patient harm in practice. The range propagation adds the therapeutic window check: the computation is dimensionally correct, but the dosage at certain combinations of patient mass, rate, and duration falls outside the safe range. The compiler identifies the specific parameter combinations.

The compiler does not know pharmacology. It checks dimensional types, established ranges, and supported arithmetic rules. The application's model and boundary contracts supply the pharmacological assumptions. `Fidelity.Physics.Clinical` contributes the measure types that make those declarations dimensionally consistent. Any derived bound remains conditional on that model; it does not independently establish clinical safety.

## The Common Pattern

An aerospace engineer, a risk analyst, and a clinical pharmacologist all receive the same class of compiler guarantee from the same toolchain. The three cases share a single underlying mechanism. The domains differ in their dimensional vocabularies. The physical constants, safety bounds, and operating ranges are all application concerns. The compiler's role is identical in all three: propagate ranges through the computation graph, compare the result against declared bounds, report findings with traces and confidence levels.

`Fidelity.Physics` provides dimensional algebra, not domain knowledge. Each sub-namespace (`Aerospace`, `Finance`, `Clinical`) declares the measure types that make a domain's quantities type-safe. The constants, material properties, regulatory limits, therapeutic windows, and operating ranges belong to the application. A financial risk model and a structural analysis both use the same compiler machinery. They differ in the measure types they import and in the constants and ranges they declare.

## The Mechanism Is Not New; the Application Is

The engineer declares constants, bounds, and operating ranges using the measure types that `Fidelity.Physics` provides. The compiler propagates them, compares against the declared bounds, and produces the diagnostics as part of the same compilation pass. The interval tracking and the constraint check are steps in the build, not a separate verification stage the engineer runs by hand.

Interval arithmetic itself is not novel; it has been studied extensively since Moore's foundational work in the 1960s. What is new is the integration with dimensional types and the computation graph. In conventional interval arithmetic, ranges are manually specified and manually propagated. In our Fidelity framework, the compilation graph provides the structure through which ranges propagate, and the DTS ensures that every propagation step is dimensionally consistent.

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

A sound analysis can propagate ranges through `computeStress` using its body or a checked summary. The division rule must account for its input intervals, a nonzero denominator, and the selected numerical representation. The dimensional relation and the numerical bound have separate justifications.

For functions that compose multiple range-carrying operations, the propagation chains:

```fsharp
let fullAnalysis mass gForce sparArea yieldStrength =
    let lift = mass * g * gForce
    let stress = lift / sparArea
    let safetyMargin = yieldStrength - stress
    safetyMargin
    // compiler derives the range of safetyMargin
```

The safety margin's enclosure is computed from the chain of operations. A nonnegative lower bound establishes a nonnegative margin under the input assumptions. A negative lower bound can signal a potential violation; identifying a particular violating input requires further evidence.

## When Range Propagation Establishes a Bound

A sound range analysis encloses every result allowed by its input assumptions and operation semantics. If that enclosure lies within a required bound, it establishes the bound even when the enclosure is conservative. An enclosure that crosses the bound leaves the safety obligation unresolved; it does not by itself demonstrate a violating execution.

Monotonic arithmetic over independent input intervals can yield tight bounds. Straight-line code alone does not guarantee tightness: repeated uses of one value can lose their relationship under ordinary interval propagation. For example, independent interval treatment of `x - x` can overestimate its range although the mathematical result is zero. The PSG can retain such relationships for an analysis that knows how to use them.

Ranges may come from constants, guards, checked library contracts, or explicit declarations at input boundaries. Numerical guarantees also require the selected representation's rounding and overflow behavior. None of these obligations requires a proof attribute on every arithmetic operation.

Stated in Hoare logic, the analysis discharges the triple:

$$
\{\,\forall i.\; \mathit{input}_i \in \mathit{declared\_range}_i\,\}
\quad
\mathit{computation\_graph}
\quad
\{\,\mathit{output} \in \mathit{computed\_range} \;\wedge\; \mathit{computed\_range} \subseteq \mathit{safe\_bound}\,\}
$$

The established input ranges are the precondition. The computation graph is the command. A sound propagated enclosure supplies an output condition, and the consequence rule applies when that enclosure is contained in the required safety bound. A specialized analysis or solver checks the generated condition in its supported theory. Linear integer conditions can use `QF_LIA`; real-valued, nonlinear, and representation-specific conditions need their applicable encodings and procedures.

Structural analysis, thermal analysis, sensor processing, and power budgets provide useful applications for this automatic analysis. Their domain models and boundary assumptions still determine what the resulting proof says about a physical system.

## Where Range Propagation Is Conservative

An unresolved bound can require a more precise local analysis or a reusable domain law. This is a distinction between sources of evidence, not between levels of mandatory source annotation.

### Tier 2: Local facts and graph coeffects

**Branching control flow with narrowing.** Guards, clamps, and saturation operations can supply range facts directly from code. The intended analysis retains those facts through the PSG and checks the bound at each relevant branch. To conclude a common bound from branch conditions `Q₁` and `Q₂`, it must establish `Q₁ ⇒ bound` and `Q₂ ⇒ bound`. An explicit assertion can state an additional requirement, but its presence is not evidence that the requirement holds.

**Bounded loops with linear invariants.** A supported loop analysis can generate an invariant from program structure or a registered rule. Checking it requires initialization, preservation by each iteration, and the exit implication. When those conditions fall within an admitted local arithmetic fragment, they remain Tier 2 obligations. A developer may supply missing domain information, but Tier 2 is not defined by manual invariant annotation. If the available analysis cannot establish the invariant, the obligation stays unresolved.

**External inputs.** Values from sensors, APIs, or user input need a justified boundary contract. A declared range remains an assumption unless validation or evidence establishes it. The analysis's conclusion is conditional on that contract; the compiler cannot infer a device's behavior from a unit declaration.

### Resolved at Tier 3 by a parameterized lemma

**Transcendental bounds.** Coarse bounds for sine or exponential may be insufficient for a particular safety condition. A checked theorem can supply a tighter result under specified interval and operation premises. In the intended library workflow, a domain author proves that theorem once, potentially in Rocq, and the compiler instantiates its parameters from the PSG. Local analysis checks the applicable premises. The application author need not attach the lemma to every call.

**Domain and system invariants.** A convergence theorem, resource-handoff law, or distributed reduction result can depend on several related operations. Tier 3 uses reusable laws whose participants and shared premises are retained in PSG hyperedges. The compiler instantiates a law only for its supported construction and checks its premises. Falling outside `QF_LIA` does not by itself select Tier 3: the obligation's reasoning role and the available procedures determine dispatch.

An illustrative diagnostic for an unresolved range obligation could be:

```
⚠ Potential range exceeded: thermal_stress upper bound 3.1e8 <Pa>
  may exceed yield_strength 2.7e8 <Pa>
  Range analysis confidence: conservative
    (branch at line 47 widens interval; actual range may be narrower)
  Needed: a justified tighter range or an applicable checked rule
```

The diagnostic distinguishes an established bound from an unresolved requirement. More precise transfer rules and additional domain laws can extend automatic coverage. A library theorem applies only when its instantiated premises hold. A theorem proved in Rocq retains that foundation even when a solver checks its local arithmetic premises.

The [compilation sheaf design](/docs/design/categorical-foundations/the-compilation-sheaf/#conservative-findings-as-uncharacterized-cohomology) organizes the compatibility of evidence across analyses and lowering stages. Interpreting an unresolved condition as a cohomological obstruction would require an additional mathematical correspondence; an interval overestimate alone does not establish such a class.

## The Revised Tier Boundary

The current [Decidable By Construction](https://arxiv.org/abs/2603.25414) model organizes proofs by the justification they need. An automatically generated range proof belongs to Tier 2 even when its bound is tight; automation does not make every obligation Tier 1.

The four tiers support the same ordinary programming workflow:

| Tier | Coverage | Dispatch |
|---|---|---|
| 1 | Dimensional equality and admitted structural rules | Inference and structural derivations from typed code |
| 2 | Range, layout, and local arithmetic conditions | Graph coeffects and automatically generated analysis or solver obligations |
| 3 | Parameterized domain and system properties spanning PSG relationships | Reusable lemmas instantiated from hyperedges and checked premises |
| 4 | Relations between executions or realizations | Supported compiler-relational (cRHL) and probabilistic-relational (pRHL) rules |

Representation selection and physical bound checking can use the same propagated range against different requirements. Both need the applicable numerical semantics and assumptions. A physical bound additionally depends on the domain model that makes that comparison meaningful.

Application developers receive supported coverage through types, ordinary operations, and domain libraries. Framework and library authors establish reusable laws; the compiler generates their supported instances and dispatches the premises. This includes supported Tier 4 derivations. New requirements may need explicit formulation, but the tiers do not impose a staircase of per-function proof annotations. Unsupported obligations and timeouts remain unresolved.

The PSG retains the relationships among dimensional, range, lifetime, and resource facts while respecting their different analysis domains. The [verification internals](/docs/internals/verification/) describe that design. As the whitepaper records, the Rocq integrations, semantic adapters, and automatic Tier 3/4 compositions remain work to implement and check.

## Implications for Domain Libraries

`Fidelity.Physics` provides the dimensional vocabulary: measure types and the `Range<'T, 'U>` generic introduced earlier. Building a domain library is declaring which measure types exist and how they compose. The application then uses those types, along with `Range`, to declare its own constants, material properties, safety bounds, and operating ranges. The compiler enforces the consequences across every computation.

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

The compiler combines these application-level declarations with the computation graph to generate obligations without per-function proof annotations. Libraries provide types and checked laws; application code supplies the computation and its boundary requirements. A sound propagated enclosure can establish a bound without being the tightest possible enclosure.

## From Findings to Certificates

The design-time diagnostics illustrated above correspond to obligations generated from the PSG. A range finding needs the analysis result or checked proof for the relevant arithmetic semantics. The release path we are designing would carry that evidence through lowering and associate it with the resulting artifact.

A certificate must name the checked claim, its premises, the artifact, and the proof dependencies. Establishing that a source-level bound describes the compiled output also requires checked preservation across the affected lowering steps. A hash identifies evidence or an artifact; it does not establish those semantic connections. The [compilation sheaf design](/docs/design/categorical-foundations/the-compilation-sheaf/) describes the intended preservation account.

The intended result is an inspectable connection between an editing-time finding and the released program, with unresolved obligations still visible at the boundary that requires them.

## Better Design, Safer Results

Range propagation can establish useful bounds for structural loads, thermal calculations, power budgets, and sensor operating envelopes. Those results are conditional on the domain model, input contracts, and numerical semantics. A sound enclosure within a required bound is enough to establish that particular obligation.

Control flow and runtime inputs can contribute facts to automatic analysis. More demanding properties may need reusable lemmas or relational rules. The boundary is the available checked construction and its premises, not a requirement that developers annotate every function above Tier 1.

Representation selection and safety constraint checking can share propagated ranges and comparison machinery. Their conclusions have different requirements: a representation must cover the admitted values and numerical error criteria, while a physical or application bound also depends on a justified domain model. Extending coverage requires the rules and evidence for those additional premises.

The influences on this design contribute distinct ideas: Kennedy's dimensional inference, Syme's work on F#, Gustafson's numerical representations, and Wadler's account of parametricity. Dimensional consistency, numerical enclosure, and domain adequacy remain different claims. The four-tier architecture connects their evidence while retaining the assumptions and proof rules each requires.

The compiler's task is to derive supported obligations from the program, dispatch them to the applicable analysis or rule library, and retain their evidence. Growing that coverage through reusable laws lets applications benefit without reconstructing each domain proof at every use.

## References

[1] P. Wadler, "Theorems for free!" in *Proceedings of the Fourth International Conference on Functional Programming Languages and Computer Architecture*, pp. 347-359, ACM, 1989.

[2] H. Haynes, "Dimensional Type Systems and Deterministic Memory Management: Design-Time Semantic Preservation in Native Compilation," [arXiv:2603.16437](https://arxiv.org/abs/2603.16437), 2026.

[3] R. E. Moore, *Interval Analysis*, Prentice-Hall, 1966.

[4] H. Haynes, "Decidable By Construction: Design-Time Verification for Trustworthy AI," [arXiv:2603.25414](https://arxiv.org/abs/2603.25414), 2026.

[5] A. Kennedy, "Types for Units-of-Measure: Theory and Practice," in *Central European Functional Programming School*, Springer LNCS 6299, 2009.

[6] D. Syme, A. Granicz, and A. Cisternino, *Expert F# 4.0*, Apress, 2015.

[7] J. L. Gustafson and I. T. Yonemoto, "Beating Floating Point at its Own Game: Posit Arithmetic," *Supercomputing Frontiers and Innovations*, vol. 4, no. 2, 2017.

[8] J. C. Reynolds, "Types, abstraction and parametric polymorphism," in *Information Processing 83*, pp. 513-523, North-Holland, 1983.

## See also

- [Fewer Tests; Greater Safety]({{< ref "fewer-tests-greater-safety" >}}): the broader case that discharged proof obligations replace whole categories of tests, with the "proof over the declared operating envelope, not a test" framing made concrete here.
