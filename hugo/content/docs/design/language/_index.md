---
title: "Language Semantics"
description: "Type universe, collections, metaprogramming, and managed mutability as compilation infrastructure"
weight: 6
---

Clef's language-level commitments are designed as compilation infrastructure. The native type universe lives in the compiler rather than in a standard library. Collection operations carry their purity as lambda calculus the compiler can recognize and parallelize. Mutability is admitted under a managed-mutability rule that makes the failure surface visible at compile time. Centralizing F# features such as quotations, active patterns, and computation expressions are carriers that move semantic information through the pipeline. Two further lineages sit alongside F#: F* for refinement types that carry proof obligations, and Scheme for the nanopass framework that organizes the compiler's passes.

These articles highlight the path each commitment took to its current shape. Alloy began as a BCL-style standard library and was absorbed into the compiler once we understood that types belong to the compiler itself. Mutability was attempted in three different shapes before the current rule settled. Quotations evolved from a metaprogramming convenience into the structure that carries peripheral descriptors and memory constraints through our PSG construction. If you're arriving from the proof-architecture material, these are the language-level commitments the four-tier proof architecture depends on. If you're arriving from the .NET ecosystem, the section traces what changes once the standard-library pattern is replaced by an in-compiler type universe.
