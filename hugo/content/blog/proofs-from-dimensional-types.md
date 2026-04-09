---
title: "'Free' Proofs from Dimensional Types"
linkTitle: "'Free' Proofs from Dimensional Types"
description: "How Wadler's Free Theorems Provide the Formal Foundation for Design-Time Dimensional Verification"
date: 2026-03-31
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Innovation"]
---

In 1989, Philip Wadler published ["Theorems for free!"](https://people.mpi-sws.org/~dreyer/tor/papers/wadler.pdf), a paper that demonstrated a remarkable property of polymorphic type systems: the type of a function, by itself, determines non-trivial theorems about that function's behavior. No implementation needs to be examined. No test cases need to be run. The type is the theorem.

This result, grounding John Reynolds' earlier abstraction theorem (1983) in a form accessible to working programmers, has quietly underpinned the design of ML-family type systems for over three decades. Its influence on the Fidelity framework's Dimensional Type System is direct and foundational, and it deserves explicit acknowledgment.

## The Parametricity Result

Wadler's central observation is best illustrated by his own method. Write down the type of a polymorphic function. Do not look at the function's definition. From the type alone, derive a theorem that every function of that type must satisfy.

Consider a function with type \(\forall a.\ [a] \to [a]\). This function takes a list of any type and returns a list of the same type. Parametricity guarantees that for any such function \(g\) and any total function \(f\):

\[\operatorname{map}\ f \circ g = g \circ \operatorname{map}\ f\]

The function \(g\) cannot inspect the elements of the list (they are abstract; \(g\) does not know what \(a\) is). It can only rearrange, duplicate, or drop elements. Whatever rearrangement it performs must commute with any element-wise transformation. This is not a property of any specific function; it is a property of the type. Every function with this type satisfies the theorem.

Wadler called these "free theorems" because they cost nothing: no annotation, no proof effort, no verification step. They fall out of the type.

## The Connection to Dimensional Types

The DTS extends Hindley-Milner unification with dimensional annotations drawn from finitely generated abelian groups. A function with type `float<'d> -> float<'d> -> float<'d * 'd>` (multiply two dimensioned values) carries a dimension variable `'d` that is polymorphic in exactly the sense Wadler describes: the function cannot inspect the dimension. It must behave uniformly across all dimensional instantiations.

Parametricity guarantees this uniformity. A multiplication function that works correctly for meters must work correctly for kilograms, for seconds, for any dimension, because the dimension variable is abstract. The function has no mechanism to dispatch on the dimension and do something different. The type prevents it.

This is not a minor technical point. It is the formal reason that dimensional type inference is sound. When the DTS infers that a computation is dimensionally consistent, it is deriving a free theorem from the computation's polymorphic type. The inference is decidable (polynomial time, complete, principal) because the dimensional constraints form a system of linear equations over the integers, solved by Gaussian elimination. But the correctness of that inference, the reason the inferred types actually guarantee dimensional consistency of the compiled artifact, rests on parametricity.

## Persistence Through Lowering

The DTS paper's central claim is that dimensional annotations persist through multi-stage MLIR lowering. Each lowering pass transforms the program's structure (from high-level operations to target-specific instructions) while preserving the dimensional annotations as MLIR attributes. Parametricity provides the formal justification for this claim.

Each lowering pass is a structure-preserving transformation, a function from one program representation to another. The dimensional annotations are polymorphic metadata that the pass carries through. Wadler's map-commutation theorem applies directly: if the lowering pass is parametric in the dimension (it does not inspect or modify dimensional annotations, only the computational structure), then lowering and reading the dimension gives the same result as reading the dimension and lowering.

In more concrete terms: it does not matter whether the compiler checks dimensional consistency before or after lowering to the LLVM dialect. The result is the same, because the lowering is parametric in the dimension. This is not an implementation property of specific MLIR passes; it is a consequence of the type structure that Wadler formalized.

The DTS paper's information accrual principle (Section 6.6) states that each compilation stage has strictly more information than its predecessor. Parametricity is the mechanism that ensures this accrual is monotonic: dimensional information established at an early stage cannot be contradicted at a later stage, because the later stage's transformations are parametric in the dimensions.

## Free Theorem Cascade in the Fidelity Framework

Several properties that the DTS paper establishes as design-time verification results are, in Wadler's terminology, free theorems. They cost nothing beyond the type declarations that the programmer provides (or that the inference engine derives):

**Dimensional consistency of the chain rule.** If \(f\) maps values with dimension \(d_1\) to values with dimension \(d_2\), then the derivative \(df/dx\) carries dimension \(d_2 \cdot d_1^{-1}\). This is a free theorem: the chain rule's dimensional behavior follows from the polymorphic type of differentiation. The DTS verifies it without examining the computation's structure, because the type determines it.

**Cross-target transfer fidelity.** When a value crosses a hardware boundary (FPGA to CPU, NPU to GPU), the dimensional annotation determines whether the precision conversion is acceptable. The representation selection function operates on the dimensional range, which is a type-level property. The transfer fidelity analysis is a free theorem of the value's dimensional type and the target's representation profile.

**Coeffect propagation.** The escape classification system (StackScoped, ClosureCapture, ReturnEscape, ByRefEscape) is a coeffect discipline in the sense of Petricek et al. The propagation of coeffects through the compilation graph follows the same parametricity structure: a transformation that is parametric in the coeffect annotation cannot change the escape classification.

**Grade preservation in geometric algebra.** The PHG paper (arXiv:2603.17627) introduces grade as a dimension axis within the DTS abelian group framework. Grade preservation through training, the theorem that forward-mode autodiff with quire-exact accumulation preserves the structural zeros of the Cayley table, is a free theorem of the grade-annotated type. The grade variable is polymorphic; operations that are parametric in grade cannot introduce grade corruption.

## The Connection to Reynolds

Wadler's paper is explicitly an accessible reformulation of Reynolds' abstraction theorem. Reynolds proved in 1983 that types can be read as relations: a type denotes not just a set of values but a relation between different interpretations of the type. Polymorphic functions must preserve these relations. Wadler showed that this relational reading generates useful theorems for specific types.

Reynolds also independently discovered continuations (as documented in his 1993 survey "The Discoveries of Continuations"), which provide the formal basis for Clef's DCont mechanism. The two contributions, abstraction (parametricity) and continuations (DCont), are the two formal pillars of the porous loop's typed interface:

DCont provides the suspension and resumption mechanism. The recurrent model suspends mid-computation, passes its state as a delimited continuation to a domain actor, and resumes with the typed response.

Parametricity provides the guarantee that the suspension and resumption are dimensionally consistent. The continuation's type is polymorphic in the dimension; the domain actor's response must satisfy the same dimensional constraints regardless of which specific dimension is instantiated. This is a free theorem of the continuation's type.

The convergence of these two contributions in one researcher's body of work is not coincidental. Both are consequences of taking types seriously as specifications of program behavior: types determine what continuations can capture (Reynolds 1972), and types determine what theorems functions satisfy (Reynolds 1983, Wadler 1989).

## Implications for DTS

The [DTS paper](https://arxiv.org/abs/2603.16437)'s Section 2.2 (dimensional inference) derives its soundness from parametricity. The claim that dimensional annotations survive lowering (the persistence property) is a consequence of parametric polymorphism applied to compilation passes. The decidability result (polynomial time, complete, principal) establishes that the inference algorithm terminates; parametricity establishes that the inferred types mean what they claim to mean.

In short, the full summary of Clef's innovation stems from:

1. **Reynolds' abstraction theorem**: types are relations; polymorphic functions preserve relations.
2. **Wadler's free theorems**: specific types generate specific theorems about all functions of that type.
3. **DTS inference**: dimensional types generate dimensional consistency theorems about all functions whose types the DTS infers.
4. **Persistence**: compilation passes that are parametric in dimensional annotations preserve the inferred dimensional consistency, by the same reasoning that Wadler's map-commutation theorem follows from parametricity.

## The Free Theorem Boundary

The properties enumerated above (dimensional consistency, grade preservation, coeffect propagation, transfer fidelity) all share a structural feature that makes them genuinely free: each is a statement about an *abelian-group-valued annotation* whose preservation under polymorphic operations is forced by parametricity. The dimensional algebra is a free abelian group on the base units. Grade is an integer index in that same abelian setting. Escape classifications form a finite lattice that the compiler propagates without engineer intervention. In every case, the theorem follows from the type structure alone, and the engineer's annotation cost is zero.

This boundary is sharp, and it is worth being explicit about where it falls. Properties that involve *inequalities* over the integers (a buffer index lies within bounds, a temperature stays below a threshold, a sampled coefficient stays inside a norm bound) are not free theorems. They are not derivable from type structure by parametricity alone, because parametricity is silent about which specific values the variables take. Such properties live one tier up in the Fidelity framework's verification stack: the engineer declares a range, the compiler propagates it through the computation, and Z3 discharges the resulting QF_LIA obligations. The cost is modest, and the obligations are decidable, but the discharge requires *establishing* a precondition rather than deriving one from a type. Properties that involve probability distributions (rejection-sampling termination, support equality of uniform distributions over lattice cosets) and properties that involve pairs of program runs (the probabilistic relational reasoning at the heart of cryptographic indistinguishability proofs) live further still from "free," because the obligations are no longer about a single value at a single point but about a distribution or a relation between two computations.

The framework treats the four logical fragments (\(\mathbb{Z}^n\) equality, QF_LIA, the restricted probabilistic fragment, probabilistic relational Hoare logic) as distinct sheaves over a shared compilation poset, where the stalk category is what changes between tiers and the dual-pass discharge mechanism is what remains constant. The [compilation sheaf design document](/docs/design/categorical-foundations/the-compilation-sheaf/) makes that structure precise, and the [triangle without mystery](/blog/a-triangle-without-mystery/) post sketches why the same categorical scaffolding shows up in Tarau's combinatorial isomorphisms and in the recent cellular-sheaf literature on compositional information flow. The point worth carrying out of this post is the fence: parametricity does the work in the abelian fragment, and only in the abelian fragment.

## Lower Bounds Framing

A lower bound is rarely a property of a problem alone. It is a property of a (problem, technique) pair: a statement that the known techniques for solving the problem have not done better than the bound, established by a proof that exploits some structural assumption about how those techniques work. When the literature reports "problem P has lower bound B," the load-bearing content is "every technique we have tried so far inherits a reasoning step that forces B." The bound looks structural because the reasoning step is shared across all the techniques. It stops looking structural the moment someone finds a technique that does not depend on that step.

Engineering value follows directly. If a lower bound is a property of (problem, technique) pairs, then progress comes from finding techniques whose structural assumptions do not bind on the specific instance class you care about. The distinction between "the problem is intractable" and "the problem is intractable under reasoning step S" is the difference between a closed door and a door that has not yet been examined for an appropriate handle.

Two results from different fields make the distinction concrete. In type theory, the standard argument against fully automated verification runs: program correctness needs dependent types, type inference and proof search in dependent type systems are not fully automatable (the engineer must supply proof terms by hand), therefore complete annotation-free verification is impossible. The undecidability of inference and proof search in general dependent type systems is real. The reasoning step that fails is the implicit assumption that *every* useful program property requires the full expressive range of dependent types. Dimensional consistency does not. It lives in a free-abelian-group fragment whose obligations are systems of linear equations over the integers, and within that fragment inference is decidable, complete, and principal via Gaussian elimination. The engineer supplies no proof terms; the bound on proof search has nothing to bind on. The DTS escape route operates by observing that the dependent-type undecidability result's load-bearing assumption (every property needs the full machinery) does not hold for the property the framework actually verifies.

In algorithms, a 2025 paper from Tsinghua, Stanford, and Max Planck (arXiv:2504.17033) makes the same kind of move against the long-standing \(O(m + n \log n)\) bound for directed single-source shortest paths. The bound had stood since the Fibonacci heap result of 1987 and was widely believed to be tight, on the reasoning that any shortest-path algorithm establishes a distance order over the vertices and therefore inherits the \(\Omega(n \log n)\) lower bound that comparison sorting carries. The 2025 algorithm (BMSSP) defeats the bound by organizing the work as recursive divide-and-conquer over bounded vertex sets, compressing the search frontier in a way the comparison-sorting reduction cannot account for. The new bound is \(O(m \log^{2/3} n)\). The failure point in the older argument was the implicit transfer step: the comparison-sorting lower bound is a property of techniques that establish a total order through pairwise comparisons, and BMSSP does not work that way. The comparison-sorting bound holds for the techniques it was proved for, and it stops being a constraint the moment a technique appears that does not inherit the reasoning step it depends on.

The discipline that the (problem, technique) framing imposes is symmetric. Treating a lower bound as universal closes off research programs that turn out to be tractable in the structured subclass where the engineer actually works. Treating the structured subclass as broader than it is ships systems whose guarantees do not survive contact with workloads that fall outside the subclass. The honest position requires three things at once: identify the (problem, technique) pair the lower bound was actually proved for, identify the structural assumption in the original technique that fails on an instance class, and demonstrate a technique that exploits the failure constructively.

## The Deeper Pattern

Wadler's paper demonstrates a principle that recurs throughout the Fidelity framework's design: structure that is present in the type system generates properties of the compiled artifact ***for free***. Dimensional consistency, escape classification, grade preservation, coeffect propagation, and cross-target transfer fidelity are all instances of this principle. None requires runtime enforcement. None requires separate verification tooling. Each falls out of the type structure through parametricity.

This is the formal content of the claim that verification is a compilation byproduct: the types determine the theorems, the compiler infers the types, and the theorems follow. The cost is the type system's design. Once the design is in place, the theorems are "free" in the truest sense of its original meaning.

## References

[1] P. Wadler, "Theorems for free!" in *Proceedings of the Fourth International Conference on Functional Programming Languages and Computer Architecture*, pp. 347-359, ACM, 1989.

[2] J. C. Reynolds, "Types, abstraction and parametric polymorphism," in *Information Processing 83*, pp. 513-523, North-Holland, 1983.

[3] J. C. Reynolds, "The discoveries of continuations," *Lisp and Symbolic Computation*, vol. 6, pp. 233-248, 1993.

[4] H. Haynes, "Dimensional Type Systems and Deterministic Memory Management: Design-Time Semantic Preservation in Native Compilation," [arXiv:2603.16437](https://arxiv.org/abs/2603.16437), 2026.

[5] H. Haynes, "The Program Hypergraph: Multi-Way Relational Structure for Geometric Algebra, Spatial Compute, and Physics-Aware Compilation," [arXiv:2603.17627](https://arxiv.org/abs/2603.17627), 2026.

[6] T. Petricek, D. Orchard, and A. Mycroft, "Coeffects: A calculus of context-dependent computation," in *Proceedings of the 19th ACM SIGPLAN International Conference on Functional Programming*, pp. 123-135, 2014.
