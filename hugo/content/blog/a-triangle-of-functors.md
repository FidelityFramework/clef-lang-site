---
title: "A Triangle of Functors"
linkTitle: "A Triangle of Functors"
description: "Tarau's Combinatorial Isomorphisms, Dimensional Types, and Cellular Sheaves as Three Views of One Structure"
date: 2026-04-08
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Innovation"]
---

Three pieces of mathematics, drawn from three different communities and addressing three different problems, describe the same underlying structure. Recognizing this lets a working engineer borrow infrastructure across communities (a proof from one field, a decision procedure from another, an indexing trick from a third) without having to build the whole stack from scratch.

The three pieces are Paul Tarau's combinatorial isomorphisms with their Root-mediated groupoid structure, the Fidelity framework's Dimensional Type System with its compilation pipeline, and the recent treatment of cellular sheaves on finite posets as a foundation for compositional information flow [(arXiv:2502.15476)](https://arxiv.org/abs/2502.15476). All three are *functors from a finite poset to a target category, subject to a compositionality equation*. Once that observation is made, each of the three borrows from the other two for free.

## Corner One: Tarau's Root-Mediated Groupoid

Paul Tarau's work on bijective combinatorial encodings starts from a question that sounds elementary and turns out to be deep. Can finite mathematical objects of different kinds (natural numbers, finite sets, binary trees, lambda terms, multisets, permutations) be put into computable bijection with one another such that the round trips compose? If you can convert a binary tree to a natural number and back, and a natural number to a finite set and back, can the conversions be made to compose so that going tree → number → set → number → tree returns the original tree?

Tarau's answer is to route every encoding through a common type, called the Root, and to require that each encoder/decoder pair forms an isomorphism between its source type and the Root. With this discipline the round-trip equation falls out by composition. Any conversion between two sources is the composition of one source-to-Root encoding with one Root-to-target decoding, and the round trip is the composition of those compositions, which the isomorphism property guarantees is the identity.

The structure here is a one-object groupoid: a category with a single object (the Root) and a collection of invertible morphisms. Each encoded type contributes a pair of arrows in and out of the Root. The groupoid laws (associative composition, inverses for every morphism, identities) guarantee the round trips. The natural numbers \(\mathbb{N}\) are the canonical Root choice in much of Tarau's work because their order structure makes the encodings effective.

Read this as a functor. The base poset is trivial: a single point. The target category is the category of types and isomorphisms. The functor sends the point to the Root. Compositionality is automatic because there are no non-trivial morphisms in the base to compose. Tarau's contribution is the discipline (every encoder routes through Root) and the proof that the discipline is enforceable across a wide family of finite mathematical objects.

## Corner Two: Dimensional Types in the Fidelity Framework

The Fidelity framework's Dimensional Type System assigns to each value in a program an annotation that records its physical or mathematical units. Length, time, mass, temperature, currency, dimensionless count: each base unit is a generator of a free abelian group, and the annotation on a compound value is a vector in \(\mathbb{Z}^n\) where \(n\) is the number of base units in scope. The unit *meter per second* is the vector \((1, -1)\) in the basis (length, time). The unit *kilogram meter squared per second squared*, the dimension of energy, is the vector \((1, 2, -2)\) in the basis (mass, length, time).

Operations propagate annotations: addition requires equal vectors, multiplication adds vectors, division subtracts. Dimensional consistency reduces to a system of linear equations over \(\mathbb{Z}\), decided in polynomial time by Gaussian elimination. The result is either a consistent assignment or a contradiction. The engineer never writes a proof, because the inference is complete and principal. This is the Tier 1 fragment of the framework's verification architecture, and the [post on free proofs from dimensional types](/blog/proofs-from-dimensional-types/) makes the parametricity argument explicit.

The dimensional system has another structure that the parametricity story does not emphasize. The annotations live on every node of the Program Semantic Graph, on every operation in every MLIR dialect the program is lowered through, and on the residual evidence in the binary. The lowering passes are required to preserve them, and the dual-pass architecture re-discharges this requirement at each pass via Z3.

Read this as a functor. The base poset is the compilation pipeline: source < PSG < high-level MLIR < mid-level MLIR < low-level MLIR < binary. The target category is the category of \(\mathbb{Z}^n\)-valued annotation bundles and the homomorphisms that preserve them. The functor sends each compilation stage to its annotation bundle and each lowering pass to the structure map that translates annotations across the pass. Compositionality, meaning that two consecutive lowerings produce the same annotations as one direct lowering, is the property the dual-pass architecture enforces.

## Corner Three: Cellular Sheaves on Finite Posets

The recent paper on cellular sheaves on finite posets ([arXiv:2502.15476](https://arxiv.org/abs/2502.15476)) treats sheaves as a working tool for compositional information flow rather than as the abstract object of Grothendieck topology. Their definition: a cellular sheaf on a finite poset \(P\) is an assignment of a stalk (a value, a set, an algebraic structure) to each element of \(P\), together with structure maps \(D(s_1 < s_2)\) for each ordered pair, satisfying the compositionality equation

\[D(s_0 < s_1) \,;\, D(s_1 < s_2) \;=\; D(s_0 < s_2).\]

A *global section* of the sheaf is an assignment of one element of each stalk such that the structure maps send the chosen elements correctly. The paper's central computational fact is that to verify a global section it suffices to check the structure-map equations on the edges of the Hasse diagram of the poset. Transitivity propagates through compositionality.

The paper treats hypergraphs as a special case via the membership poset. A hypergraph becomes a bipartite poset where vertex \(v\) is below hyperedge \(h\) when \(v \in h\), and a sheaf on this poset is a cellular sheaf in the standard sense. The cohomology of such a sheaf, specifically the higher cohomology groups that vanish on graphs but exist on cell complexes of dimension greater than one, measures obstructions to extending local consistency to global consistency.

Read this as a functor. The base poset is whatever poset the application requires (a hypergraph's membership relation, a process calculus's reachability relation, a compilation pipeline). The target category is whatever category of values the application carries (abelian groups, distributions, capability lattices). The functor sends each poset element to its stalk and each ordered pair to its structure map, subject to the compositionality equation. The paper's contribution is the algorithmic infrastructure for computing global sections and cohomology on arbitrary finite posets, including the hypergraph case.

## The Triangle

The three corners are three instances of the same structure:

|  | Base poset | Target category | Compositionality |
|---|---|---|---|
| **Tarau** | One point | Types and isomorphisms (Root-mediated) | Automatic; base is trivial |
| **DTS** | Compilation pipeline | \(\mathbb{Z}^n\)-annotation bundles | Enforced by dual-pass / Z3 |
| **Cellular sheaves** | Arbitrary finite poset (hypergraph membership) | Application-determined stalks | Theorem: check edges of Hasse diagram |

In each case the object of interest is a functor from a finite poset to a target category. In each case the compositionality equation is what distinguishes a usable structure from a collection of disconnected encodings. In each case the value of the discipline is that it lets information flow from one part of the structure to another *without losing properties along the way*.

Tarau's groupoid is the degenerate case in which the base is a single point. The discipline reduces to "every encoder passes through Root." Compositionality is automatic because there is nothing to compose against, and the entire content of the discipline is at the level of the target category.

The Dimensional Type System is the case where the base is a finite linear order (the compilation pipeline) and the target category has enough structure (abelian groups and homomorphisms) for compositionality to be checked algorithmically. Gaussian elimination decides the structure-map equations at each stage, and the dual-pass architecture is the witnessing mechanism for the global section.

The cellular sheaf framework is the general case in which the base may be any finite poset and the target category may be any category in which composition is meaningful. The hypergraph case unlocks higher cohomology, which is the categorical reason joint constraints in kernel fusion or spatial tile placement cannot be reduced to binary edges. The obstruction classes that joint constraints generate live in cohomology groups that do not exist on a one-dimensional cell complex.

## Why the Triangle Matters for Engineering

Recognizing the three corners as instances of one structure means that infrastructure built for any one of them transfers, in principle, to the other two.

From Tarau, the engineering payoff is the *Root discipline*: a single canonical type through which all encoders pass, with isomorphisms guaranteeing round-trip consistency. The framework's use of \(\mathbb{Z}^n\) as the canonical representation for dimensional information is a Root in exactly Tarau's sense. Every dimensional annotation, regardless of which physical or mathematical domain it originates in, gets translated to a vector in \(\mathbb{Z}^n\), and the round trip from a domain-specific type to \(\mathbb{Z}^n\) and back is an isomorphism by construction. The Tarau discipline is what makes the compilation poset's structure maps composable in the first place.

From the Dimensional Type System, the engineering payoff is the *dual-pass witnessing strategy*: enforce the compositionality equation locally at each edge of the Hasse diagram, and let transitivity propagate the global section. The cellular-sheaf paper's central computational fact, that edge-local checks suffice, is exactly the strategy the framework uses operationally. The framework arrived at the strategy from MLIR engineering pressure, and the paper supplies the categorical justification for why the strategy is forced rather than chosen.

From cellular sheaves, the engineering payoff is the *cohomological diagnosis* of conservative findings and joint constraints. When the framework's range analysis returns a conservative bound, the cohomological reading is that the global-section problem has an uncharacterized \(H^1\) obstruction, and the resolution is to add a witness to the lemma library. When binary edges become inadequate for kernel fusion's joint resource constraints, the cohomological reading is that the obstructions live in degrees that a one-dimensional cell complex cannot represent, so hyperedges become a categorical requirement.

## What the Triangle Suggests

A triangle in mathematics is rarely the end of a story. It usually signals a fourth corner waiting to be identified: some object that ties the three together from above, or some application that requires all three at once.

The fourth corner this triangle suggests is a verification framework that uses *multiple sheaves over the same base poset simultaneously*. The four-tier Hoare correspondence is one sheaf over the compilation poset (the functional-correctness sheaf, with stalks ranging from \(\mathbb{Z}^n\) at Tier 1 to relational pRHL judgments at Tier 4). Access Hoare logic [(Beckmann & Setzer, arXiv:2511.01754)](https://arxiv.org/abs/2511.01754) is a second sheaf over the same poset, with capability-lattice stalks. Symmetry Hoare logic [(Mehta & Hsu, OOPSLA '25, arXiv:2509.00587)](https://arxiv.org/abs/2509.00587) is a third sheaf, with group-action stalks. Each sheaf has its own global-section problem, its own dual-pass discharge, and its own cohomological obstructions. They share the compilation poset and the Tarau-style Root discipline, and they can be checked simultaneously without interfering with one another.

The framework already has the operational infrastructure for this. The PSG carries the annotations for any number of sheaves simultaneously, the MLIR pipeline preserves them as opaque attributes, and the dual-pass architecture re-discharges them at each lowering. The triangle adds the categorical justification: the same base poset supports any number of compatible sheaves, and the global sections of distinct sheaves do not interfere because they are checked against distinct stalk categories. The same map applies to access control, symmetry verification, and probabilistic relational reasoning, which is why the map is worth drawing.
