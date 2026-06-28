---
title: "The Decidability Map for Coexponential Session Inference"
linkTitle: "Decidability Map"
description: "Where inference of a coexponential session type could be complete and principal, where it provably cannot be, and what is recoverable in between"
weight: 40
authors: ["Houston Haynes"]
tags: ["Research", "Session Types", "Decidability", "Concurrency"]
draft: true
status: working-draft
---

This is a research working document, not a site document. Nothing here is built. The whole point of the document is to state, as precisely as the present understanding allows, where the inference problem this scaffold is about could have an answer of the kind we already have for dimensional types, and where it cannot. The honest version of that statement includes admitting that the boundary itself is an open question and that finding it may require building the thing whose feasibility we are trying to settle.

The model to hold in mind throughout is [the decidability sweet spot for dimensional types](/docs/internals/verification/decidability-sweet-spot/). There, the win is not "a solver can check a dimensional program." It is that dimensional consistency reduces to linear algebra over the integers, lands in `QF_LIA`, and so admits inference that is complete, principal, and decidable, the same three properties Hindley-Milner inference has and that dependent-type inference does not. The framework's entire game is staying inside fragments with that profile. Type *checking* given the term is decidable across the board; inference and proof search are undecidable in general. The discipline is to find the algebraic niche where inference is also decidable and then refuse to leave it. This document asks whether coexponential session inference has such a niche.

## The decidability question, stated precisely

The connective is the coexponential of the work by Qian, Kavvos, and Birkedal (arXiv:2010.13926, ICFP 2021). It types a stateful server threading state across an unbounded, dynamically determined client pool. Its server side is the greatest fixed point

\[\text{¡}A \cong \bot\ \&\ A\ \&\ (\text{¡}A \mathbin{⅋} \text{¡}A), \qquad H_A(X) = \bot\ \&\ A\ \&\ (X \mathbin{⅋} X)\]

and its dual, the client pool, is the least fixed point

\[\text{¿}A \cong \mu K_A, \qquad K_A(X) = 1 \oplus A \oplus (X \otimes X), \qquad (\text{¡}A)^{\perp} = \text{¿}(A^{\perp}).\]

The `¿` side carries the costructural rules directly: `QueW` is the empty pool (coweakening) and `QueA` absorbs a client (cocontraction). This is classical linear logic (CLL), not its differential extension. The connective is a modality, so none of this lives in MALL and none of it is "the linear fragment." Cut elimination is established. The dynamics is deliberately nondeterministic: derivations are quotiented up to permutation of client formation so cut elimination may serve any constituent client, which is the racy Compare-and-Set register the authors use as the headline example. That nondeterminism is operational, a reduction relation in the character of the π-calculus, not a sum in the proof. The DiLL boundary rests on the calculus being Mix-free with no sum-valued semantics, not on determinism or the absence of costructural rules.

With the object fixed, the inference question splits into a checking half that is already settled and a reconstruction half that is the whole subject here.

**Checking.** Given a term and a candidate session type `S`, deciding whether the term inhabits `S` is type checking. For the coexponential calculus this is decidable, because cut elimination is established and the typing rules are syntax-directed once the type is in hand. This is not in dispute and is not what the scaffold is about.

**Reconstruction.** Given only actor behavior, with no developer-supplied session type, produce `S`. The question this document exists to map is whether there is a fragment of coexponential session types where this reconstruction is

- *complete*: every behavior that is typeable by some coexponential session gets a type from the algorithm,
- *principal*: the algorithm returns a most general session that every other valid typing specializes, and
- *decidable*: the algorithm terminates on every input,

in exactly the sense those three words hold for dimensional inference. The claim we would like to be able to make, and cannot yet, is "there is a fragment of coexponential session types for which reconstruction is complete, principal, and decidable, and here is where it stops." This document does not prove that claim. It states what the fragment might be, what the obstruction is on the far side of it, and what would have to be shown to turn the conjecture into a result.

## The candidate inferable fragment

The fragment we conjecture is reconstructible is the one where the server's interaction surface is pinned down by static structure before any value is examined. Three conditions characterize it. The argument for each is that the corresponding piece of the session type is determined by behavior that the front end can read off the term, so there is no search over an unbounded space.

**A fixed handler set.** The server dispatches on a closed, statically known set of message cases. In our Clef actor code this is the discriminated union the actor matches on, the `RegistryMsg` shape from the worked registry example. A closed match means the external-choice structure `&` of the session, the menu of moves the server offers, is bounded and enumerable from the type of the inbound message. The `&` factors of `H_A` are determined by the cases, so the part of the fixed point that says *what choices are on offer* is read, not searched.

**Statically resolvable reply targets.** Every reply goes to a target the front end can name at the call site. In practice this is the `replyTo: IActorRef` a request carries, used once, on the inbound client's own channel, the deterministic per-request reply the registry example shows. When the reply target is statically resolvable, the `⅋`/`⊗` exchange that pairs a request with its answer is pinned to a known dual endpoint, so the multiplicative structure of each round is determined rather than inferred against an open set of possible recipients. This is the protocol-level analogue of the `AcyclicStatic` condition the wait-for analyzer already relies on, where a statically resolvable callee is what makes the wait-for relation `W` a finite graph.

**Bounded choice structure.** The branching the protocol exposes per step is bounded and does not itself grow as a function of runtime values. The fixed point still unfolds an unbounded number of times, one per client served, which is the point of the coexponential and is fine. What the fragment forbids is the *shape* of a single unfolding depending on data, so the recurrence the inference would reconstruct has a fixed functor `H_A` whose only growth is the `(X ⅋ X)` self-reference the connective already prescribes. Bounded per-step branching is what keeps the fixed-point reconstruction a finite problem: the algorithm is solving for the body of a known recursion, not discovering the recursion's shape.

The reason these three pin the type down is uniform. The coexponential's structure is `\bot \& A \& (\text{¡}A ⅋ \text{¡}A)`: a termination option, a single client interaction `A`, and a continuation that carries state forward and admits further clients. If the handler set fixes the `&` menu, the reply targets fix the `⅋` pairing inside `A`, and the per-step branching is bounded, then everything in the fixed point except the recurring `¡A` self-reference is determined by static structure, and the self-reference is supplied by the connective. What remains for the algorithm is unification over a finite system, which is the regime where complete-and-principal reconstruction is plausible by analogy to Hindley-Milner over the dimensional carrier. We say plausible, not proven, deliberately. The analogy is the source of the conjecture, not a substitute for the termination and principality arguments the next-to-last section says are still owed.

## The undecidable region

Each condition above has a failure mode, and each failure mode is a place where the literature on asynchronous, cyclic process networks already documents general undecidability. These are the patterns the shipped wait-for analyzer classifies as `Unresolved` today. The session-inference problem inherits the same wall one connective up, because a session type is a richer object hung on the same edges.

**Content-based routing.** The callee, and therefore the dual endpoint of the exchange, depends on the content of a message rather than on its static case. The handler set is no longer the thing that determines the next move; a value does. The `&` menu the fragment relied on is replaced by a dispatch that the front end cannot enumerate, because enumerating it is deciding a property of arbitrary values.

**Value-carried continuations.** A continuation, an `IActorRef` or a closure standing in for "what to do next," is itself passed as data and invoked later. The protocol's future shape then lives in a value, so reconstructing the session means reconstructing the behavior of an arbitrary carried program. This is the higher-order case, and it is the direct generalization of the wait-for analyzer's value-carried callee, the `Unresolved` routing that the deadlock work already drops out of the static guarantee.

**Runtime-spawned handles.** New endpoints are created at runtime and their identities are not statically pinned, so the set of participants the session ranges over is not known when the type would be reconstructed. The fixed point is no longer over a fixed functor; the functor's interaction surface grows with the run.

**Unbounded data-dependent protocol shape.** The shape of a single unfolding depends on runtime data, so the recurrence the inference would solve for is not a fixed `H_A` but a family selected by values. Reconstructing it is reconstructing a data-dependent program shape, which is where the encoding of an undecidable problem becomes available.

The connection to the general result is the same one the wait-for work cites. Acyclicity over value-determined routing is undecidable in the asynchronous-and-cyclic process-network setting, and that is the wall the [deadlock-freedom obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) names at its `Unresolved` case. Session inference sits strictly above wait-for inference: the wait-for relation `W` is a may-wait projection of the protocol, one edge per blocking reply, while the session type is the full conversation those edges abstract. Anything undecidable for `W` is undecidable for the session type, because `W` is the cheaper shadow the session casts. The four failure modes above are exactly the inputs on which the shadow itself goes dynamic. So the undecidable region for session inference contains, at minimum, the inputs already known to defeat wait-for inference, and the conjecture in the previous section is in part the conjecture that it does not contain very much more.

## What is recoverable when full inference fails

When reconstruction cannot return a principal session, the design target is not to reject the program and not to silently admit it. It is to return a *sound over-approximation* of the session type, in the precise sense the wait-for analyzer already returns a may-wait over-approximation of `W`. The shipped precedent is the model. `W` is built so that an actor which can call either of two callees depending on message content contributes an edge to each. The relation thereby claims at most the waiting that any execution could exhibit and never less, so an acyclicity proof over the over-approximation is sound for every run, at the cost of sometimes flagging a cycle no execution reaches. The escape analysis takes the same posture, where `EscapeKind` over-approximates escape and never under-approximates it. The HelloArty precedent shows the symmetric posture on bounds: a width inferred from a modulus bound is the *over*-estimate that is safe to allocate, the counter's range `[0, 399999999]` widening up to 29 bits, never down. Soundness in this framework consistently means widening in the conservative direction and carrying the widened object forward.

The design target for session inference is the same widening, one connective up. Where a principal session cannot be reconstructed, the algorithm would return a session that admits a superset of the conversations the program can actually conduct, sound in that it never claims a protocol restriction the program might violate. The over-approximated session is to the principal session what the may-wait `W` is to the exact wait relation: a conservative abstraction that supports the proofs that matter even though it is not tight.

This raises the genuinely open and genuinely hard question, and it is the one this document refuses to paper over. A widened wait-for relation is still useful because acyclicity is a coarse property and survives widening: extra edges can only make a cycle more likely to be reported, so a clean over-approximated `W` still certifies progress. It is not obvious that a widened *session* retains anything comparable. A session type carries ordering, choice menus, and state-threading discipline, and an over-approximation that admits enough extra conversations to cover the dynamic cases may admit so much that the protocol content erodes. At the limit, an over-approximated session that permits every interleaving and every choice is no longer a protocol; it carries nothing the data layer and the wait-for slice do not already carry. The open question is where, between a tight principal session and that vacuous limit, the widened approximation still says something a developer or a downstream tier can use. It is possible that the useful range of the over-approximation collapses precisely to the wait-for projection already in place, in which case the recoverable content on the failing side of the boundary is exactly the liveness slice and nothing richer. We do not know, and we do not think this can be settled by argument from the present vantage. It is one of the things that has to be measured against a real reconstruction algorithm on real actor code.

## The fragment boundary is an open research question

The previous sections are conjecture and design intent, and it would be a verification facade to present them as anything else. To claim a result of the kind the dimensional sweet spot is, several things would have to be proven that are not proven here.

First, that the candidate fragment admits a reconstruction algorithm that terminates on every input in the fragment. The fixed-handler, static-reply, bounded-choice conditions are an argument that the reconstruction problem is finite, not a termination proof. A termination proof has to exhibit the algorithm and its well-founded measure over the unfolding of `H_A`.

Second, that the reconstruction is principal: that the session it returns is the most general one, with every valid typing of the term a specialization. Principality for a fixed-point connective with the nondeterministic, permutation-quotiented dynamics of the coexponential is not a corollary of the Hindley-Milner analogy. The quotient up to permutation of client formation means the "most general type" has to be most general up to that quotient, and stating principality correctly requires saying what the order relation on candidate sessions is in the presence of that quotient.

Third, that the boundary is sharp: that the four failure modes are not merely sufficient for undecidability but characterize it, so the fragment is genuinely the inferable one and not a conservative under-approximation of it. The deadlock work establishes the wait-for wall by citation to the asynchronous-cyclic literature. Lifting that to the session connective, with its costructural `¿` rules and its operational nondeterminism, is a separate reduction that has to be carried out against this specific calculus, not inherited.

Until those three are in hand, the correct status of the candidate fragment is conjecture with a precedent-shaped motivation, and the correct status of the over-approximation is design target with an open question about whether it carries content.

There is a further, more uncomfortable point to state plainly, because it governs how this question can even be approached. The coexponential calculus is recent, its inference theory does not exist in the standing literature we have reviewed, and the object we want to reconstruct is defined by a fixed-point isomorphism with a deliberately nondeterministic dynamics. It is entirely possible that settling whether this inference is feasible requires our Fidelity Framework itself to become the mechanism that expresses and tests it: the CCS front end, the PSG and PHG that already carry the wait-for edges, and the tier architecture that already discharges the liveness projection, used as the apparatus in which a reconstruction algorithm is built and run against real Clef actor code. On that reading, building the inference is not the step that follows answering the decidability question. Building it is part of answering it. The map in this document marks where we expect the inferable region to be and where we expect the wall to be, and the act of constructing the analysis inside the framework is how those expectations get confirmed or moved.

## Where this sits relative to the rest of the architecture

For orientation, the decidability picture for the protocol layer sits alongside the ones the framework already holds, and stating it in the same table the dimensional work uses makes the open cells visible.

| Property | Dimensional types (settled) | Wait-for relation (shipped) | Coexponential session (open) |
|---|---|---|---|
| Type checking given the object | Decidable | Decidable | Decidable |
| Inference, candidate fragment | Complete, principal, decidable | `AcyclicStatic`/`OrderedCyclic` rank in `QF_LIA` | Conjectured complete and principal; unproven |
| Inference, general case | (no general case; the algebra is the fragment) | Undecidable, `Unresolved` fallback | Undecidable on the four dynamic modes |
| Recovery on failure | (not needed) | May-wait over-approximation of `W`, sound | Over-approximated session, sound; content open |
| Trusted base | Z3, `QF_LIA`, Tier 1 free | Z3, `QF_LIA`, Tier 2 rank | unsettled; liveness projection is Tier 2 |

The settled column is the existence proof that this profile is reachable for an algebraic object. The shipped column is the existence proof that the over-approximation posture works in our own analyzer on a projection of this very connective. The open column is what this scaffold is for. The two left columns are why the conjecture is worth stating at all, and the gap between the middle and right columns is the research, not a foregone conclusion.

The protocol layer reaches our Composer compiler as an attribute on the MLIR the seam reads, the same way the discharged data contract and the Tier 2 acyclic-wait rank do. For the inferable fragment the design admits as target, that attribute would carry a reconstructed session the seam checks rather than solves, which is the inferred-with-override posture the rest of the boundary holds. For the dynamic region, the attribute would carry the over-approximation and, where even that collapses, would fall back to the wait-for slice already in place. That fallback is the conservative floor: the worst case for session inference is not a crash and not a false guarantee, it is dropping back to the liveness projection the framework already proves. Whether the protocol layer earns more than that floor across enough real code to be worth building as a distinct layer is the question the map is drawn to make answerable, and it is the direction we will keep building toward as the reconstruction apparatus comes into focus.
