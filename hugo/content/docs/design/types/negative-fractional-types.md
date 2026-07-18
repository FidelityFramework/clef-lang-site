---
title: "Negative and Fractional Types"
linkTitle: "Negative & Fractional Types"
weight: 15
description: "The additive and multiplicative duals on the same abelian-group substrate NTU already carries, and what they would express: reversibility and constraint propagation as type-level disciplines."
date: 2026-06-02
authors: ["Houston Haynes"]
tags: ["NTU", "Reversibility", "Compact Closed Categories", "Formal Methods"]
---

A type fixes a category of data: the values that inhabit it, their layout in memory, and the operations they admit. That much an ML-family programmer takes for granted, and the inference engine settles it for every construct at compile time. But a working program routinely does two things that produce no such value. It runs a step backward: an audit trail reconstructs the state before a decision, a search abandons a branch and returns to the choice point. And it carries a demand not yet met: evidence still to emerge, a slot a later step will fill. Because neither is a value the program holds, a conventional type has nothing to attach to, and handling them falls to architecture instead of the type system. To retrace state, teams reach for event sourcing: every change is recorded as an event, current state is a fold over the log, and a prior state is recovered by replaying events up to a point, usually with CQRS splitting the write and read models and a dedicated event store to persist and query the stream. It is a serious, load-bearing commitment, and it is expensive: the log has to be written on every step, persisted durably, and folded back in order, layer over layer as the system grows. Moving the reversal into the type system removes that burden. The negative-typed adjoint stays in the compute substrate and runs as a structural operation, so a prior state comes from re-running the reverse computation rather than replaying a stored event log. That runs with higher integrity, since the reversal information is checked to be complete at compile time, and far faster, since there is no event stream to persist and replay.

A fractional type is a different kind of tool. To carry a value that has not arrived, developers reach for a promise or future, a placeholder that later code fills in, dependency injection with late binding, or a hand-wired constraint solver that resolves the unknown against gathered facts. Each works in their own way, and each leaves the same gap: the obligation to supply the value lives in computation, not in a type, so nothing at compile time checks that it is ever handled appropriately in all cases. A promise can be dropped, a placeholder read before it is set, a solver left with an unsatisfied variable, and the failure shows up at runtime. The fractional type puts the obligation in the type instead. `Recip<'T>` is the demand for a `'T`, carried as a value the substrate must settle: it is resolved at the unification site where the awaited value arrives, and a demand still open at the program boundary is a protective design-time diagnostic, not a runtime error. This keeps integrity intact: constraint resolution and proof discharge are handled where the substrate can check them, rather than in the promise, placeholder, or solver machinery that can silently leave a land mine to be found in edge cases.

This is not new mathematics. James and Sabry named these the two dualities of computation in 2012, the **negative type** for the backward-flowing value and the **fractional type** for the unmet demand, and Chen and Sabry later placed them in the setting of compact closed categories. The theory has been settled for over a decade. What has kept it on the shelf is that its soundness requires a strict condition: no value may be silently duplicated or discarded. Few languages enforce it.

The [Native Type Universe](/docs/design/types/bcl-to-ntu/) does, which is why these two constructors can be native here. NTU is built on an abelian-group algebraic substrate: Kennedy's units of measure established the pattern, where dimensional consistency reduces to unification over a finitely-generated free abelian group, [decidable in polynomial time](https://arxiv.org/abs/2603.25414) and composing with Hindley-Milner inference without sacrificing principal types. NTU carries that one pattern across several disciplines, the dimensional algebra, the memory-placement coeffects, the capability coeffects, the BAREWire schema, and the grade discipline of the [Program Hypergraph](https://arxiv.org/abs/2603.17627), each a finitely-generated abelian-group structure resolved by the same unification machinery. An abelian group has inverses, and once the substrate carries them, both constructors derive from the same algebra: the negative type as the **additive inverse**, the fractional type as the **multiplicative inverse**. The [Negative and Fractional Types pre-print](https://arxiv.org/abs/2606.04352) gives the full treatment. This page is the design orientation.

## The two dualities

The duality of computation has been treated under many banners, call-by-value versus call-by-name, value versus continuation, classical versus constructive, often as a single phenomenon. Liskov's CLU staked out a careful position inside the first of these decades ago, which we acknowledge in retrospect: call-by-sharing, where an argument is a reference passed by value, so mutation is visible to the caller but rebinding is not.[^clu] James and Sabry's result is that the duality is, more precisely, two orthogonal phenomena.

The **additive duality** gives every type `'T` a negative type `Neg<'T>`, with the isomorphism `'T + Neg<'T> ↔ 0`. A value of `Neg<'T>` is an ordinary `'T` value flowing in the reverse direction of evaluation. When it enters a computation, the operational reading is that execution reverses to satisfy the demand it represents. This is the type-level account of backtracking.

The **multiplicative duality** gives every type `'T` a fractional type `Recip<'T>`, with the isomorphism `'T × Recip<'T> ↔ 1`. A value of `Recip<'T>` is a constraint on the surrounding context, a logic variable whose value is fixed by unification at a corresponding site. When it flows through a computation, it carries the demand that some `'T` will eventually be supplied. This is the type-level account of constraint propagation.

The two dualities are orthogonal. Prior work treated continuations as a single phenomenon. The claim here is that they are two. At the type level they split into backtracking (negative) and constraint propagation (fractional), and the type system encodes which discipline governs which value. These are categorical entities, distinct from the operational delimited continuations the framework uses at runtime through the DCont dialect. The decomposition is at the type level and leaves the operational primitives intact.

## Why the substrate matters

The literature on negative and fractional types shows a consistent pattern: each construction requires the surrounding language to prevent values from being silently duplicated or erased. Filinski's declarative continuations impose linearity to prevent duplication; Reddy's acceptors track acceptance points to prevent erasure; Crolard's subtractive logic isolates local environments; James and Sabry's own calculus is built atop a reversible language that enforces information preservation at the level of primitive operations. In every case the negative or fractional value is a first-class entity flowing through computation, and its soundness depends on the language not duplicating or erasing it.

Most general-purpose languages do the opposite. Garbage collection erases values whose references go out of scope; aliased pointers duplicate them; implicit conversions erase precision and dimensional information; unsafe escape hatches bypass the tracking entirely. Implementations of these types in conventional languages have therefore been research artifacts carrying substantial runtime machinery to enforce what the language does not.

The framework's primary development has gone into those structural commitments. The memory-placement coeffect tracks every allocation and its lifetime, with escape classification preventing values from being silently captured across boundaries, and the flat closure representation makes captured environments structurally explicit. BAREWire extends the same guarantees across process boundaries as zero-copy typed transport. The capability coeffect surfaces an unsupported operation as a design-time error. Information preservation runs through all of them: every value in a Clef program is structurally accounted for at the type level, so duplication would violate the escape classification, erasure would violate the lifetime discipline, and silent transformation would violate the dimensional algebra. Negative and fractional types have always depended on that discipline, supplied here by infrastructure built for other reasons.

## What it would look like in Clef

The additive dual would be written `Neg<'T>` or, in infix notation, `-'T`. The multiplicative dual would be written `Recip<'T>` or `1/'T`. The duals inherit the appropriate dimensional transformation through Kennedy's algebra:

```fsharp
// Negative types inherit the dimension of their positive counterpart;
// the reversal is in the direction of evaluation, not the dimension.
type ReverseForce   = Neg<float<N>>      // dimension N, reverse direction
type ReverseCurrent = -float<A>          // dimension A, reverse direction

// Fractional types invert the dimension through the abelian-group inverse.
type Compliance  = Recip<float<N>>       // dimension N⁻¹, a constraint
type Conductance = 1/float<ohm>          // dimension ohm⁻¹, a constraint
 
```

The `η` and `ε` morphisms that establish the compact closed structure appear as primitive operations, `eta_plus` introducing a `('T + Neg<'T>)` pair and `epsilon_plus` annihilating it, with multiplicative variants `eta_times` and `epsilon_times` for the fractional case. These are not function calls. They are type-level structural transitions that Baker recognizes during elaboration and settles on the Program Semantic Graph as codata, which the middle end later witnesses and elides into the corresponding MLIR constructs, the same mechanism the dimensional and lifetime annotations already use.

Inference would extend the existing HM unification with a direction annotation on each judgment, a forward judgment (`e` produces a `'T`) and a backward judgment (`e` demands a `'T`), dispatching to different operational semantics. A complete program typechecks by producing only forward judgments at its boundary; an unresolved backward judgment or an unsatisfied fractional constraint at program scope is a design-time error. This matches the observability constraint of the source calculus, where complete programs must have positive, non-fractional types at their boundaries.

The cost of the extension is the cost of the additional unification step. The negative constructor introduces the additive inverse element, the fractional constructor the multiplicative inverse, and both preserve the abelian-group structure, with the dimensional algebra extending from integer to rational exponents. Unification at `η` and `ε` sites reduces to algebraic identity checking, which the existing engine already performs for dimensional consistency, so the cost remains polynomial.

## A worked case: reversible decision support

Consider a clinical decision-support component that maps a patient identifier to a recommended dosage, with the requirement that every lookup be reversible so the audit log can reconstruct the prior state of any decision. The ordinary forward function has type `PatientId -> Dosage`. A reversible version carries the audit commitment as a type-level structure, with the negative type of the result providing the reversal:

```fsharp
let dosageLookup_reversible
    (patient : PatientId)
    : (Dosage * Neg<PatientId>) =
    let (weight, weight_rev)       = getWeight_reversible patient
    let (condition, condition_rev) = getCondition_reversible patient
    let dosage      = computeDosage weight condition
    let patient_rev = negate(reconstitute(weight_rev, condition_rev))
    (dosage, patient_rev)
```

The function produces both the forward result and a negative-typed proof object that, annihilated against the result, reproduces the original input. The compiler would verify the reversal information is structurally complete: every piece of state the forward computation depends on has a corresponding piece of reversal information in the negative-typed output, and the verification stays decidable because the dependencies are structurally explicit in the flat closure representation. An audit trail on this discipline replays in reverse as a structural operation, re-running each adjoint and annihilating it against the forward result, recovering the prior state without a stored record of values.

The fractional dual carries a conditioning demand instead. Where the dosage depends on a population prior refined as evidence arrives, `Recip<Evidence>` records the unsatisfied obligation, and an `epsilon_times` at the application site satisfies it by unifying the supplied evidence with the demand:

```fsharp
let dosageLookup_bayesian
    (patient : PatientId)
    (prior   : Distribution<Dosage>)
    : (Dosage * Recip<Evidence>) =
    let (proposed, demand_evidence) = eta_times<Evidence>()
    let dosage = sampleFromPosterior prior proposed
    (dosage, demand_evidence)
```

The two duals compose: a function that must be reversible for audit and conditioning-dependent for refinement carries both annotations, with the `η`/`ε` operations for each duality dispatched separately.

## Where this sits in the framework

Pair annihilation, the `η`/`ε` cancellation, is not new to the framework with these types; it recurs across the architecture. The Program Hypergraph's grade-annihilation operations (`a ∧ a = 0` for a grade-1 element) capture the elimination of paired elements at the hyperedge level, where `a ∧ a` equals the zero of the geometric algebra. The mode-shift discipline's round-trip tier coercions collapse back to the source constraint. The negative and fractional constructors would add the same pattern at the type level. Categorically, they promote the framework's existing symmetric-monoidal PSG semantics to compact closed: every object gains a dual, with `η` and `ε` connecting them, along a fourth structural dimension, the duality dimension, parallel to the compilation, joint-constraint, and verification-strength dimensions the hypergraph already carries.

Several domains motivate the work. Negative types provide a type-level adjoint for reversible computation and for the unitarity of quantum circuit expression. Fractional types express the conditioning obligations of Bayesian inference as constraint propagation. The combined discipline expresses adiabatic computation, Hamiltonian deformation as a reversible constraint-propagation process. The [reversible ThreeBody demonstrator](/docs/design/types/rounding-on-real-hardware/) is the watchable instance of the reversibility half.

These are proposed extensions. The framework's published type universe, the dimensional system, the memory discipline, and the schema verification, is complete and sound within its current scope and does not depend on them. What the extension would add is the structural capacity to express reversible and constraint-propagating computations as type-level disciplines, resolved through the same compilation infrastructure that carries the rest. The depth here is not a requirement the everyday surface imposes; the framework is conceived as a gradient, broadest at ordinary concurrent programming and narrowing to the depth a given domain requires.

[^clu]: Liskov, Barbara, Alan Snyder, Russell Atkinson, and Craig Schaffert. "Abstraction Mechanisms in CLU." *Communications of the ACM* 20.8 (1977): 564-576. <!-- DOI: add https://doi.org/10.1145/… --> CLU introduced call-by-sharing as its argument-passing discipline, the reference-by-value semantics later named as a distinct point in the call-by-value/name/need family.
