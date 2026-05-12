---
title: "Mode Shifts In Fidelity Verification"
linkTitle: "Mode Shifts In Fidelity Verification"
description: "How explicit transitions between verification tiers complete our PHG joint constraint resolution"
weight: 50
date: 2026-05-12
authors: ["Houston Haynes"]
tags: ["MLIR", "Formal Methods", "Posit Arithmetic", "Architecture"]
params:
  originally_published: 2026-05-12
  migration_date: 2026-05-12
---

> This article extends the [Transparent Verification](..) series. It builds on
> [The Decidability Sweet Spot](../decidability-sweet-spot) and the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/)
> design to address how the Program Hypergraph's verification tiers connect
> structurally, rather than through operational decisions. The structural insight
> developed here draws on Hăvărneanu's recent work on adjoint classical logic
> with uniform mode connectives.

Recent work by Aram Hăvărneanu on adjoint classical logic with uniform mode connectives, extending Pfenning's adjoint logic and Paykin and Zdancewic's polarized classical linear logic, has recently been published on X. 

{{< x user="aramh" id="2053874165795405860" >}}

Encountering this work prompted a reconsideration of how the Fidelity Framework describes the connections between its four verification tiers. 

The system introduces mode-uniform shift operators (↑ and ↓) that provide explicit, typed coercions between modes in a preorder. The shift \(\uparrow^{k}_{m}\, A\) lifts a value at mode \(k\) to mode \(m\) when \(m \geq k\), and the dual \(\downarrow^{m}_{k}\, A\) projects in the opposite direction. The interesting structural feature is that exponentials are derived rather than primitive: \(!A\) becomes \(\downarrow\uparrow A\) and \(?A\) becomes \(\uparrow\downarrow A\), with the shift discipline doing the work that linear logic traditionally assigns to exponential connectives.

Our tiered proof architecture, as articulated so far in the [DTS+DMM whitepaper](/arxiv/2603.16437) and the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) design notes, treats each tier as a sheaf over a shared compilation poset with its own stalk category. Tier 1 carries dimensional types and memory lifetimes through abelian group structure. Tier 2 carries QF_LIA constraints (for dimensional algebra, range bounds, and lifetime orderings) and QF_BV constraints (for bit-level reasoning that emerges from representation selection and word-width decisions) through Z3. Tier 3 carries probabilistic reasoning through distributional refinement. Tier 4 carries relational properties through pRHL judgments.

Our layered articulation in papers and blog entries to this point has served well for communicating the framework's scope and constraining the engineering space. The connections between tiers were always going to be a central architectural question; the layered presentation was a pedagogical choice that deferred the question rather than answering it. Hăvărneanu's shift discipline suggests a structural form the answer might take.

## What Mode Shifts Could Provide

The proposal explored here would add a single structural element to the PHG: the **mode shift**, an explicit transition between verification tiers carried as a hyperedge in the graph. Adapted to the Fidelity Framework's tier system, a mode shift \(\uparrow_{2,3}\) would mark a transition from Tier 2 verification to Tier 3 verification at a specific PSG node, carrying with it the proof obligation that the Tier 2 structure at the node admits the Tier 3 refinement claimed. The contribution Hăvărneanu's work could make to this framework is structural: explicit shifts as first-class elements with their own composition laws, their own duality properties, and their own discipline for how they interact with other connectives.

This would be structurally minimal in implementation terms. Mode shifts would not be new tiers, new logics, or new verification fragments. They would be explicit names for transitions that the framework already performs implicitly through operational decisions. The contribution would be making these transitions visible in the graph structure rather than leaving them buried in compiler decision logic.

What would make this addition non-trivial is what mode shifts would have to respect. A mode shift applied to a value must preserve the value's complete joint constraint structure across the shift. For a flat closure, this means the closure's captured environment, region annotation, and dimensional type must all lift coherently to the target tier's verification structure. For an actor message, this means the message's region annotation, captured environment, and the receiving actor's lifetime constraints must all transit the shift together. The shift discipline operates on hyperedges, not on individual nodes.

This is what would make mode shifts a natural extension of what the PHG already carries. The hyperedge structure that flat closures, regions, and actor lifetimes produce is already the right shape for mode shifts to operate on. The framework would not be adding a new structural element to the PHG; it would be recognizing that the existing hypergraph structure can carry verification transitions as well as joint constraints.

## Extending the PHG's Structural Dimensions

The PHG already carries hyperedge structure along multiple dimensions. Mode shifts would add another. Three dimensions are visible in the current discussion, though the framework's longer development may reveal others:

**The compilation axis (temporal)**: The existing compilation poset connecting source through MLIR levels to binary. This axis carries the framework's existing concern with how representations transform through lowering passes.

**The joint constraint axis (structural)**: The existing hyperedge structure connecting values, closures, regions, and actors. This axis carries the framework's existing concern with how values relate to each other through shared regions, captured environments, and message-passing relationships.

**The verification strength axis (tier)**: The dimension that mode shifts would introduce, connecting different verification tiers' stalks at each compilation stage. This axis would carry the framework's concern with how verification strength varies across a computation.

A single PSG node would participate in hyperedges along all these dimensions simultaneously. The dual-pass architecture, which currently verifies preservation along the compilation and joint constraint dimensions, could extend naturally to verify preservation along the verification strength dimension as well. The local-edge-check strategy that makes the existing dual-pass tractable would continue to apply: each lowering pass verifies its local edges along all relevant dimensions, and the compositionality of the cell complex propagates the guarantee through longer chains.

This would be structurally consistent with how the framework already operates. The PHG's existing hyperedge structure handles the joint constraint dimension. The compilation poset structure handles the temporal dimension. Adding the verification strength dimension would introduce no new architectural pattern; it would add another instance of an existing pattern.

## SMT Dialect Integration

The practical question is how mode shifts would get represented in MLIR for joint constraint resolution during lowering. The framework's existing use of MLIR's SMT dialect provides the natural representation path.

The SMT dialect in MLIR provides operations for constructing SMT-LIB2 formulas as IR, with operations corresponding to the standard SMT theories: `smt.declare_fun` for sort declarations, `smt.assert` for axiom assertions, `smt.check` for satisfiability queries, and operations for the boolean, integer, bitvector, and array theories. The dialect's design allows SMT reasoning to participate in the MLIR pass pipeline as first-class IR, with Z3 invoked as the backend solver. This is the same infrastructure the framework already uses for Tier 1 and Tier 2 verification, where QF_LIA handles the linear arithmetic of dimensional constraints and QF_BV handles the bit-level constraints that representation selection introduces.

Mode shifts would extend this existing pattern through conjunctive composition of SMT constraints. A mode shift \(\uparrow_{2,3}\) at a PSG node would correspond to an SMT-LIB2 fragment that conjoins the source tier's constraint with the obligation that justifies the shift:

```
;; Mode shift ↑₂₃ at a PSG node:
;; The Tier 2 constraint that holds before the shift
(assert (and 
  ;; Tier 2 constraint on the value
  (< x_value upper_bound)
  (> x_value lower_bound)
  
  ;; Shift justification: the value admits the Tier 3 refinement
  ;; (this is the proof obligation the shift carries)
  (refines_to_distribution x_value distribution_params)))
```

The shift itself would not be a new dialect operation. It would be the conjunctive extension of the existing constraint system with the additional obligation that the transition between tiers requires. The SMT dialect already supports this composition through its standard boolean connectives. Z3 would discharge the conjoined constraint in the same query that it would discharge the source tier's constraint alone, with the shift obligation either satisfied (the shift is valid) or producing an unsat core (the shift cannot be justified and the framework reports the failure as a conservative finding).

This integration would be structurally important. The framework's existing verification operates through SMT-LIB2 formulas constructed during PSG elaboration, lowered through the SMT dialect, and discharged by Z3. Mode shifts would participate in this same pipeline by extending the formulas with shift obligations rather than introducing parallel infrastructure. The conjunctive composition means mode shifts would compose with existing constraints automatically: Z3 would see a single SMT problem to solve, not a tier-specific problem followed by a separate shift problem.

The Hăvărneanu correspondence would make this composition principled. The adjoint classical logic's shift operators have well-defined composition laws (consecutive ups compose, round-trip shifts cancel, identity shifts collapse), and these laws would translate directly into how the SMT-LIB2 fragments combine. A shift \(\uparrow_{2,3}; \uparrow_{3,4}\) would compose into \(\uparrow_{2,4}\) with the corresponding conjunctive composition of the two shifts' obligations. A round-trip \(\downarrow_{2,3}; \uparrow_{3,2}\) would cancel with the conjunction simplifying back to the source constraint alone. The shift algebra would constrain how the SMT formulas can be constructed and simplified.

## Implications for the Baker Component

The Baker component of the Clef Compiler Service is responsible for elaborating syntactic Clef code into the saturated PSG that downstream components consume. The PSG that Baker produces carries dimensional annotations, lifetime coeffects, and the joint constraint structure that flat closures and region inference generate. This is the elaboration that makes transparent verification possible.

Adding mode shifts to the PHG would extend Baker's responsibilities in specific ways. The elaboration would need to identify points where verification tier transitions are required, mark them with explicit mode shift hyperedges, and ensure the shifts respect the joint constraint structure they cross. This is delicate work because the tier transitions would need to be identified at principled, low levels in the compute graph rather than imposed retroactively by analysis decisions.

The principled identification would operate on specific structural signals. When the elaboration encounters an arithmetic operation whose interval propagation produces a conservative bound that interval arithmetic alone cannot tighten, Baker would mark the operation with an explicit mode shift toward Tier 3's distributional verification. When the elaboration encounters a relational property that requires reasoning across multiple program states, Baker would mark the corresponding hyperedge with a mode shift toward Tier 4's pRHL verification. These identifications would not be heuristic; they would emerge from structural patterns in the code that Baker can recognize during elaboration.

The precision this would demand is significant. A mode shift inserted at the wrong place either over-verifies (invoking a higher tier when a lower tier would suffice) or under-verifies (failing to mark a transition that the verification certificate must record). Both failures would undermine the framework's compositionality guarantees. Over-verification would add spurious obligations that the dual-pass architecture must discharge. Under-verification would leave gaps in the certificate that the reconciliation tool cannot bridge.

The work Baker does to identify mode shifts would be structurally similar to the work it already does to identify joint constraints. When Baker recognizes a flat closure during elaboration, it generates a hyperedge connecting the function part, the captured environment, and the region annotation. This identification is principled because the closure's structure makes the hyperedge inevitable. Mode shift identification would operate on the same principle: when Baker recognizes a tier transition during elaboration, it would generate a mode shift hyperedge connecting the source and target tier annotations along with the joint constraint structure they preserve.

The Baker layer's XParsec-based composability makes this extension tractable. The same combinator pattern that handles dimensional type elaboration and lifetime coeffect inference can handle mode shift identification. The four pillars pattern used throughout Baker's existing implementation applies directly: parse the structural signal, elaborate the mode shift annotation, propagate the joint constraint references, and emit the hyperedge into the saturated graph. The SMT-LIB2 generation that Baker already performs for Tier 1 and Tier 2 constraints would extend to include the conjunctive shift obligations that mode shifts introduce.

## What This Might Look Like in Practice

Consider the clinical dosing calculation from earlier work. The computation involves arithmetic on patient state values (weight, creatinine clearance, infusion rate) that Baker can verify at Tier 1 through dimensional types and at Tier 2 through interval propagation. The computation reaches the `exp()` term, where interval propagation alone produces a conservative bound.

With mode shifts as first-class structure, Baker would elaborate this transition explicitly. At the PSG level, the elaboration would produce hyperedges marking the tier transitions. At the SMT dialect level, the resulting formulas would conjoin Tier 2 interval constraints with Tier 3 distributional obligations and back again:

```
;; Constraint structure for the dosing calculation:

;; Tier 2 portion: infusion_rate * patient_weight
(assert (and 
  (>= infusion_rate 0.5) (<= infusion_rate 2.0)
  (>= patient_weight 2.5) (<= patient_weight 4.0)))

;; Mode shift ↑₂₃ at the exp node:
;; Conjunctive extension with the shift obligation
(assert (and
  ;; Source Tier 2 constraint on the exponent argument
  (<= elim_rate_times_t 0.0)
  (>= elim_rate_times_t exponent_lower_bound)
  
  ;; Shift obligation: exp on a negative interval has monotonic bounds
  ;; (This is the Tier 3 lemma the shift invokes)
  (and (>= exp_result (exp exponent_lower_bound))
       (<= exp_result 1.0))))

;; Mode shift ↓₃₂ back to Tier 2 for the threshold comparison:
;; The distributional structure projects back to interval bounds
(assert (and
  (= peak_concentration (* k_factor exp_result))
  (>= peak_concentration 5.0)
  (<= peak_concentration 20.0)))
```

Z3 would discharge this entire formula as a single conjunctive constraint. The mode shifts would be visible in the formula's structure (the shift obligations are explicit), but they would not require separate verification machinery. The shift discipline would ensure the formula is well-formed according to Hăvărneanu's composition laws, and the SMT dialect would ensure Z3 can solve it efficiently.

For the existing HelloArty target, this extension would be invisible at the source code level. The dimensional type checking, width inference, and combinational depth analysis all operate within Tier 1's structural verification, with no tier transitions required. The mode shift infrastructure would exist but would not engage because the computation does not demand it. This would be the right behavior: the extension would add capability for computations that need multiple tiers without imposing overhead on computations that don't.

## The Verification Cell Complex

The mathematical structure that mode shifts could complete is what the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) framework called a stack of sheaves with explicit coercions between them. In categorical terms, this would be a fibration of sheaves over a mode preorder, where the base remains the compilation poset and the fibers are the verification tiers connected by mode shifts.

Hăvărneanu's adjoint classical logic provides the proof-theoretic vocabulary for this fibration. His mode preorder (Producer > Linear > Consumer in his original system) generalizes to any preorder of verification tiers, and his shift operators provide the structural element that the fibration requires. The framework's adaptation would specialize his system to the verification tier preorder while keeping the underlying discipline intact: shifts as explicit, typed, composable transitions with well-defined duality.

The PHG, understood as a cellular structure, could be read as a cell complex with additional dimensions corresponding to verification transitions. The cells of dimension zero would be PSG nodes. The cells of dimension one would be edges along any of the relevant dimensions (compilation, joint constraint, verification strength). The cells of higher dimension would be hyperedges that connect multiple nodes along multiple dimensions simultaneously: a flat closure with mode shift annotations crossing multiple compilation stages would be a higher-dimensional cell connecting nodes along all the relevant dimensions.

The verification certificate that the dual-pass architecture produces could be understood as a section of this cell complex: an assignment of properties to each cell such that the boundaries of higher-dimensional cells respect the properties of their lower-dimensional boundaries. This would be the categorical version of what the framework already implements operationally. The certificate's structure would be dictated by the cell complex's structure, and the reconciliation tool's job would be to verify that the binary realizes this structure faithfully.

What would be new here is not the mathematical formalism but its predictive power. The cell complex framing would tell us that mode shifts must operate on hyperedges (because they're higher-dimensional cells), that their preservation through lowering follows the same rules as joint constraint preservation (because both are properties of higher-dimensional cells), and that their collapse rules must respect the cell complex's structural integrity (because incoherent collapses would violate the boundary conditions that make the complex well-defined).

## Boundary and Scope

This extension is not foundational to the framework's current operation. HelloArty compiles and synthesizes without mode shift infrastructure. The dimensional type system, the Mealy machine model, the two-layer timing analysis, all of these operate within Tier 1's structural verification and don't require explicit tier transitions. The framework as it exists today is sound, complete within its current scope, and demonstrates the architectural patterns that the mode shift extension would build on.

What mode shifts could add is the ability to express computations that span multiple verification tiers without losing the compositionality guarantees that the dual-pass architecture provides. This would become important for the framework's longer trajectory: clinical decision support requires Tier 3 for probabilistic safety properties, cryptographic verification requires Tier 4 for relational reasoning, and physics-informed AI training requires both. Each of these applications produces computations where some portions fall naturally within Tier 1 or Tier 2 decidability while other portions require higher-tier verification.

The extension would be delicate because it would operate at the elaboration boundary where Baker constructs the saturated graph. Errors at this layer propagate throughout the rest of the compilation pipeline and undermine the verification certificate's integrity. The precision required would be comparable to the precision required for joint constraint identification: principled, structural, and based on patterns that the elaboration can recognize from the code's mathematical structure rather than from heuristic guesses about intent.

The work would be justified by the architectural coherence it could provide. Without mode shifts, the verification tiers operate as separate concerns connected operationally. With mode shifts as fiber between sheaves, the tiers could become an interlocking proof construction where transitions are first-class structural elements verified through the same dual-pass discipline that handles compilation and joint constraint preservation. The framework's tier story could become uniform rather than layered: hypergraph structure throughout, with mode shifts marking the verification strength variations that some computations require.

This is the integration point worth pursuing for the framework's longer arc. It would extend established art rather than introducing new architectural patterns. It would apply the same disciplines (XParsec elaboration, SMT dialect representation, dual-pass verification, hypergraph structure) that the existing implementation already demonstrates. The Hăvărneanu correspondence provides the structural insight that would make the extension principled: explicit shifts as fiber between sheaves, with composition laws that translate directly into how SMT formulas combine and simplify. The integration would complete the architectural picture that the framework's other design decisions have been moving toward, giving the verification system the structural scaffolding that makes its tier transitions explicit, verifiable, and compositionally sound.

## References

Hăvărneanu, A. (2026). Classical SNAX: An adjoint classical logic with uniform mode connectives. Working notes.

Pfenning, F. (2015). A logical foundation for session-based concurrent computation. Working draft.

Paykin, J., & Zdancewic, S. (2016). The linearity monad. In *Proceedings of the 2016 ACM SIGPLAN International Symposium on Haskell*.

Tofte, M., & Talpin, J. P. (1997). Region-based memory management. *Information and Computation*, 132(2), 109-176.