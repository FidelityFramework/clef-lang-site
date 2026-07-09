---
title: Type System
weight: 20
---

Clef's type system is grounded in our Native Type Universe (NTU), a unified representation that maps ML-family types directly to machine-native layouts without an intermediate managed runtime.

The NTU rests on an abelian-group algebra: dimensional units, grades, and the memory coeffects all unify over the same finitely-generated free abelian structure, which keeps type checking free at the wide end. These articles work outward from that substrate: how the intermediate language lowers into the NTU, how dimensional safety holds across CPU and GPU execution, how by-reference semantics resolve at the type level, and where the algebra turns non-abelian and a domain reaches for the depth past it. Throughout, Clef preserves the expressiveness of algebraic types and type inference while producing the compact, predictable representations systems programming demands.
