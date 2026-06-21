---
title: Concurrency
weight: 3
---

Clef's concurrency model is built on delimited continuations. This primitive replaces the colored-function problem of traditional async/await with a single mechanism for suspension, resumption, and structured concurrency.

The dependency structure of a region decides how it lowers. Sequential effects become delimited continuations; independent work parallelizes, dense rectangular work on the tensor path and irregular reduction on interaction nets for fine-grained graph-rewrite parallelism. That choice is a [Library of Alexandria](/docs/design/hello-world-goes-native/) decision in Alex, our Composer middle-end: when the zipper witnesses the PSG, the witnessing rule for a region reads its structure and selects the lane it elides to, one CCS abstraction fanning out to the pathway the region's shape calls for. These articles trace how that foundation was chosen and how the compiler lowers async Clef code to native continuations without a managed runtime. The migration article traces the specific design choices that diverge from .NET's async lineage, and the deadlock-freedom article shows how liveness across actor boundaries becomes a visible compile-time obligation.
