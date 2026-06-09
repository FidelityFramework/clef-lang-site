---
title: Type System
weight: 2
---

Clef's type system is grounded in our Native Type Universe (NTU), a unified representation that maps ML-family types directly to machine-native layouts without an intermediate managed runtime.

These articles take up three decisions. Clef's intermediate language lowers into the NTU. Dimensional type safety extends across CPU and GPU execution models. By-reference semantics are resolved at the type level rather than deferred to runtime convention. Across all three, Clef preserves the expressiveness of algebraic types and type inference while producing the compact, predictable representations that systems programming demands.
