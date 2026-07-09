---
title: Concurrency
weight: 30
---

Clef's concurrency model is built on delimited continuations. This primitive replaces the colored-function problem of traditional async/await with a single mechanism for suspension, resumption, and structured concurrency.

The dependency structure of a region decides how it lowers. Sequential effects become delimited continuations. Independent work parallelizes, taking the tensor path for dense rectangular work and interaction nets for irregular graph-rewrite reduction. That choice is a [Library of Alexandria](/docs/internals/mlir/hello-world-goes-native/) decision in Alex, our Composer middle-end. A witnessing rule reads a region's structure and selects the lane it elides to, one CCS abstraction fanning out to the pathway the region's shape calls for.

These articles trace how that foundation was chosen and how the compiler lowers async Clef code to native continuations without a managed runtime, from the divergence with .NET's async lineage to liveness across actor boundaries carried as a visible compile-time obligation. The same reading of a region's structure decides whether its concurrent strands merely reorder or cross in an order the runtime can observe, the distinction the braid work carries into the type system.
