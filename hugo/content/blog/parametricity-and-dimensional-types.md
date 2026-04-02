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

## Free Theorems for the Fidelity Framework

Several properties that the DTS paper establishes as design-time verification results are, in Wadler's terminology, free theorems. They cost nothing beyond the type declarations that the programmer provides (or that the inference engine derives):

**Dimensional consistency of the chain rule.** If \(f\) maps values with dimension \(d_1\) to values with dimension \(d_2\), then the derivative \(df/dx\) carries dimension \(d_2 \cdot d_1^{-1}\). This is a free theorem: the chain rule's dimensional behavior follows from the polymorphic type of differentiation. The DTS verifies it without examining the computation's structure, because the type determines it.

**Cross-target transfer fidelity.** When a value crosses a hardware boundary (FPGA to CPU, NPU to GPU), the dimensional annotation determines whether the precision conversion is acceptable. The representation selection function operates on the dimensional range, which is a type-level property. The transfer fidelity analysis is a free theorem of the value's dimensional type and the target's representation profile.

**Coeffect propagation.** The escape classification system (StackScoped, ClosureCapture, ReturnEscape, ByRefEscape) is a coeffect discipline in the sense of Petricek et al. The propagation of coeffects through the compilation graph follows the same parametricity structure: a transformation that is parametric in the coeffect annotation cannot change the escape classification.

**Grade preservation in geometric algebra.** The PHG paper (arXiv:2603.17627) introduces grade as a dimension axis within the DTS abelian group framework. Grade preservation through training, the theorem that forward-mode autodiff with quire-exact accumulation preserves the structural zeros of the Cayley table, is a free theorem of the grade-annotated type. The grade variable is polymorphic; operations that are parametric in grade cannot introduce grade corruption.

## The Relationship to Reynolds

Wadler's paper is explicitly an accessible reformulation of Reynolds' abstraction theorem. Reynolds proved in 1983 that types can be read as relations: a type denotes not just a set of values but a relation between different interpretations of the type. Polymorphic functions must preserve these relations. Wadler showed that this relational reading generates useful theorems for specific types.

Reynolds also independently discovered continuations (as documented in his 1993 survey "The Discoveries of Continuations"), which provide the formal basis for Clef's DCont mechanism. The two contributions, abstraction (parametricity) and continuations (DCont), are the two formal pillars of the porous loop's typed interface:

DCont provides the suspension and resumption mechanism. The recurrent model suspends mid-computation, passes its state as a delimited continuation to a domain actor, and resumes with the typed response.

Parametricity provides the guarantee that the suspension and resumption are dimensionally consistent. The continuation's type is polymorphic in the dimension; the domain actor's response must satisfy the same dimensional constraints regardless of which specific dimension is instantiated. This is a free theorem of the continuation's type.

The convergence of these two contributions in one researcher's body of work is not coincidental. Both are consequences of taking types seriously as specifications of program behavior: types determine what continuations can capture (Reynolds 1972), and types determine what theorems functions satisfy (Reynolds 1983, Wadler 1989).

## Implications for the DTS Paper

The DTS paper's Section 2.2 (dimensional inference) derives its soundness from parametricity without naming it directly. The claim that dimensional annotations survive lowering (the persistence property) is a consequence of parametric polymorphism applied to compilation passes. The decidability result (polynomial time, complete, principal) establishes that the inference algorithm terminates; parametricity establishes that the inferred types mean what they claim to mean.

In short, the full summary of Clef's innovation stems from:

1. **Reynolds' abstraction theorem**: types are relations; polymorphic functions preserve relations.
2. **Wadler's free theorems**: specific types generate specific theorems about all functions of that type.
3. **DTS inference**: dimensional types generate dimensional consistency theorems about all functions whose types the DTS infers.
4. **Persistence**: compilation passes that are parametric in dimensional annotations preserve the inferred dimensional consistency, by the same reasoning that Wadler's map-commutation theorem follows from parametricity.

This chain is implicit in the DTS paper. This entry makes it implicit while and update to the whitepaper on arXiv is in review.

## The Deeper Pattern

Wadler's paper demonstrates a principle that recurs throughout the Fidelity framework's design: structure that is present in the type system generates properties of the compiled artifact ***for free***. Dimensional consistency, escape classification, grade preservation, coeffect propagation, and cross-target transfer fidelity are all instances of this principle. None requires runtime enforcement. None requires separate verification tooling. Each falls out of the type structure through parametricity.

This is the formal content of the claim that verification is a compilation byproduct: the types determine the theorems, the compiler infers the types, and the theorems follow. The cost is the type system's design. Once the design is in place, the theorems are "free" in the truest sense of its original meaning.

## References

[1] P. Wadler, "Theorems for free!" in *Proceedings of the Fourth International Conference on Functional Programming Languages and Computer Architecture*, pp. 347-359, ACM, 1989.

[2] J. C. Reynolds, "Types, abstraction and parametric polymorphism," in *Information Processing 83*, pp. 513-523, North-Holland, 1983.

[3] J. C. Reynolds, "The discoveries of continuations," *Lisp and Symbolic Computation*, vol. 6, pp. 233-248, 1993.

[4] H. Haynes, "Dimensional Type Systems and Deterministic Memory Management: Design-Time Semantic Preservation in Native Compilation," arXiv:2603.16437, 2026.

[5] H. Haynes, "The Program Hypergraph: Multi-Way Relational Structure for Geometric Algebra, Spatial Compute, and Physics-Aware Compilation," arXiv:2603.17627, 2026.

[6] T. Petricek, D. Orchard, and A. Mycroft, "Coeffects: A calculus of context-dependent computation," in *Proceedings of the 19th ACM SIGPLAN International Conference on Functional Programming*, pp. 123-135, 2014.
