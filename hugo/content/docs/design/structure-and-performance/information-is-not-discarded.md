---
title: "Information Is Not Discarded"
linkTitle: "Information Accrual"
description: "Why a design-time fact, once the compiler establishes it, is carried through lowering rather than recomputed or lost"
date: 2026-07-01
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Compilation"]
weight: 15
---

Most compilers throw information away as they lower. The front end proves something, uses it to make a decision, and discards the proof; the middle end sees the decision but not the reason for it; the back end sees neither. By the time code reaches the target, the facts that made the source safe or fast are gone, and a later pass, with no record of them, can undo the structure they depended on.

Fidelity does not lower this way. A design-time fact the compiler establishes is carried forward as an annotation later stages hold across passes, not a value they consume at one and discard. Each stage of lowering has strictly more information than the one before it, never less. This is the mechanical discipline behind several guarantees that would otherwise require a runtime, and it carries those guarantees all the way to native code.

## What "Carried" Means

When escape analysis decides where a closure's environment lives, or when dimensional inference fixes a value's units, that result is attached to the [Program Semantic Graph](/spec/draft/program-semantic-graph/) as codata, structural data that persists with the value through every pass that does not touch it. At the MLIR stage the same facts become attributes on operations. At the boundary between dialects they are preserved, not dropped. A pass that would perturb a carried fact either preserves it by construction or re-establishes it, and a fact silently lost is a defect the pipeline is built to catch.

The alternative, recomputing a fact at the stage that needs it, often fails outright. Once information is erased, the stage that needs it back may have no way to recover it. The "arity curtain" is the canonical case: a function's argument count, discarded early, cannot in general be reconstructed once the function passes through an abstraction. Carrying the fact forward is not an optimization over recomputing it; it is the only way the fact is available at all.

## Where the Discipline Shows

The same commitment appears at every level of the compiler, applied to whatever design-time fact that level establishes:

- **Arity.** A curried function's argument count is recorded and carried, so the compiler can tell a saturated call (a direct function call) from a partial application (a closure) rather than guessing. See [Arity On The Side of Caution](/docs/design/structure-and-performance/arity-on-the-side-of-caution/).
- **Closure lifetime and inhabitance.** Escape analysis resolves where a closure's environment lives at construction, and that placement is carried through lowering rather than recomputed at the bottom. The closure's safety is a property of a fact the compiler still holds, and the verification conditions raised at the closure site survive because the flat environment they range over survives. See [Gaining Closure](/docs/design/memory/gaining-closure/) and [ByRef Resolved](/docs/design/types/byref-resolved/).
- **Dimensional types.** Units and dimensions are preserved through the PSG and MLIR generation, erased only at the final lowering stage, after every decision that could benefit from them has been made. See [Dimensional Type Safety](/docs/design/types/dimensional-type-safety/).
- **Verification facts.** A property discharged by the verifier travels the middle end as a carried annotation, so the guarantee is available at each stage rather than re-derived from the finished binary. See [Proofs to Silicon](/docs/internals/verification/proofs-to-silicon/).

None of these is a separate feature. Across all four, the compiler carries a fact it has established to each lower stage rather than leaving that stage to recompute it.

## The MLIR Substrate

This discipline needs an intermediate representation that can hold a fact across a lowering step, and most cannot. MLIR can. Its SSA form already treats some structure as inviolable: a value has one definition, and a pass that would break it fails IR verification. Its attribute system generalizes that from definitions to arbitrary carried facts, so a lifetime, a dimension, or a discharged obligation is preserved through the dialect conversions rather than dropped at each boundary. Building on MLIR is what lets a design-time fact remain a real thing at every stratum instead of a front-end promise.

MLIR arrives through C++, the "worse is better" lineage in Gabriel's sense. Clef is an ML-family language whose correctness properties are structural, and it uses that pragmatic C++-borne scaffold to carry them to the metal. The [Fixed-Point Scaffolding pre-print](https://arxiv.org/abs/2606.02854) develops that arrangement, and why a scaffold chosen for reach rather than purity carries a structurally-correct language to real hardware while preserving its guarantees.

## An Annotation Is Not a Pragma

An attribute system can decay into pragmas, and the failure mode is worth naming because MLIR's own contract permits it: discardable attributes are namespaced, attachable to any operation, and legitimately droppable by a pass that rebuilds an op it does not fully understand. A pragma is an assertion injected as an input, honored or ignored by convention, with nothing behind it. Held to that default, "carried as an annotation" would be a polite name for hope. Three properties separate the carried facts of this pipeline from that default.

**Provenance.** An annotation is derived at emission from a fact saturation already established on the graph. Nothing downstream is asked to trust it as a claim. The discharge that consumes it re-checks the obligation it feeds. A pragma is an input. A carried fact is a consequence.

**Residence.** The Program Semantic Graph remains the system of record, and the annotation is an emission artifact, closer to a debug symbol than to state. This is what makes the defect-catching clause above mechanical rather than aspirational: because the graph retains every fact it emitted, the seam cross-checks what it gathers against what the graph holds, and a dropped annotation surfaces as an emission defect instead of a silent unsoundness.

**Lifetime.** An annotation is written by the emission traversal and consumed one step later by its named consumer. No pass in between reads it to make a decision. A pass that needs to is the under-saturation signal the [duality piece](/docs/design/concurrency/dcont-inet-duality/) states as a standing law, and the remedy is moving the decision onto the graph, never enriching the annotation.

The lowering target then informs the form the reified fact takes, because the consumer's contract governs it. An obligation bound for the seam expands into the SMT dialect, with per-op anchors retained so an unsat core can name a source span. [Deadlock freedom as an obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) shows the worked shape, anchors cross-checked against the graph's wait relation. A fact a backend dialect genuinely consumes is reified in that dialect's own vocabulary, the form a tile assignment takes for spatial targets or a mapping attribute takes on a parallel loop, target vocabulary in the same sense the LLVM dialect is target vocabulary. And a fact that must survive an open boundary becomes a [constructed witness](/docs/design/javascript-targeting/constructed-witnesses/), a generated guard beside the emitted program. None of the three mints an operation vocabulary of ours. Where a consumer owns a vocabulary, the emission writes into it.

The discipline compresses to a sentence a future pass author can be held to: an annotation is derived at emission, consumed by a named contract, and never read to make a decision the saturated graph should have settled.

## The Formal and Normative Statements

The account here is the mechanical one. The same principle has a formal statement and a normative one, and this discipline is where they meet.

Formally, the compilation pipeline is a monotone sheaf: each lowering pass adds annotations to the stalk and removes none, and the staged-discharge architecture is the witness that no structure map silently drops a fact. That reading, with its cohomological consequences, is developed in [The Compilation Sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/).

Normatively, the specification requires it. A design-time property the specification obliges an implementation to establish must be preserved through lowering, or re-checked at the lowering steps that could perturb it. A property silently lost in lowering is a conformance violation. The obligation is stated in [Conformance §6, the preservation obligation through lowering](/spec/draft/conformance/).

## See Also

- [The Compilation Sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) - the formal, cohomological account of the same principle
- [Conformance](/spec/draft/conformance/) - the normative preservation obligation (§6)
- [Arity On The Side of Caution](/docs/design/structure-and-performance/arity-on-the-side-of-caution/) - arity as a carried fact
- [Gaining Closure](/docs/design/memory/gaining-closure/) - closure lifetime resolved at construction and carried
- [Dimensional Type Safety](/docs/design/types/dimensional-type-safety/) - dimensions preserved to the final lowering stage
- [Proofs to Silicon](/docs/internals/verification/proofs-to-silicon/) - verification facts carried through the middle end
- [Deadlock Freedom as an Obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) - the anchor-and-cross-check shape worked in full
- [Constructed Witnesses](/docs/design/javascript-targeting/constructed-witnesses/) - the reification a fact takes at an open boundary
- [Fixed-Point Scaffolding](https://arxiv.org/abs/2606.02854) - why an MLIR/C++ scaffold carries a structurally-correct language to hardware
- [Opining Upon Reflection](/blog/opining-upon-reflection/) - the accrual principle read against runtime reflection, for readers arriving from managed platforms
