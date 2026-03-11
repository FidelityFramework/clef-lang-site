---
title: Categorical Foundations
weight: 6
---

Fidelity's type system and memory model were designed from engineering requirements; dimensional preservation through compilation, deterministic allocation without a garbage collector, and multi-target code generation from a single source. The categorical deep learning paper by Gavranović et al. provided the formal recognition that these properties instantiate a 2-categorical structure: adjoint correspondences that unify gradient computation, sensitivity analysis, and quantum evolution under a single algebraic framework.

These articles trace that correspondence through its concrete implications: how dimensional types enable compiler-driven representation selection for posit arithmetic, how forward-mode AD and exact quire accumulation compose to eliminate the memory wall in gradient computation, how spatial dataflow and neuromorphic architectures reshape the inference/training boundary, and how the verification infrastructure falls out of compilation as a byproduct. The quantum entry scopes what categorical compatibility provides today and what it does not, given the hardware maturity gap.
