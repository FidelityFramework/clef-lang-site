---
title: Categorical Foundations
weight: 60
---

Fidelity's type system and memory model were designed from engineering requirements: dimensional preservation through compilation, deterministic allocation without a garbage collector, and multi-target code generation from a single source. The categorical deep learning paper by Gavranović et al. provided the formal recognition that these properties instantiate a 2-categorical structure. Its adjoint correspondences place gradient computation, sensitivity analysis, and quantum evolution under a single algebraic framework.

These articles follow that correspondence into the compiler, where it does concrete work: choosing posit representations, closing the gradient memory wall through exact accumulation, moving inference and training onto spatial and neuromorphic hardware, and turning verification into a byproduct of compilation. The quantum entry marks the near edge of that reach, scoping what categorical compatibility provides today against the hardware maturity gap.

The algebraic substrate under all of this is abelian. Its boundary is the edge where the observable crossing order of concurrent work turns non-abelian, and the later entries reach for that edge and hold it coherent with the framework.

What binds together is a single interlocking construction. The formalisms carry meaning for certification, and they give efficient compilation its material support.
