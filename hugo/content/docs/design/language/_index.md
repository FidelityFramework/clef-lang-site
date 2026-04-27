---
title: "Language Semantics"
description: "Type universe, collections, metaprogramming, and managed mutability as compilation infrastructure"
weight: 6
---

Semantics and syntax differ in every language. This section offers some insights beyond notation. We centralize F# features such as quotations, active patterns, and computation expressions to be the carriers that move semantic information through the pipeline, anchoring Clef's ***concurrent* programming model**. Two other lineages contribute: F* for refinement types that carry proof obligations, and Scheme for the nanopass framework that organizes the compiler's passes.

This section highlights the path each commitment took to its current shape. Alloy began as a BCL-style standard library and was absorbed into the compiler once we understood that types belong to the compiler itself. Mutability was attempted in three different shapes before the current rule settled. Quotations evolved from a metaprogramming convenience into the structure that carries peripheral descriptors and memory constraints through our PSG construction. These are  language-level commitments that inform our four-tier proof architecture, which is our most significant contribution to ML-family languages.

These entries outline our motivations, the inspiration we took from various systems, and points of departure as we forge our own path.
