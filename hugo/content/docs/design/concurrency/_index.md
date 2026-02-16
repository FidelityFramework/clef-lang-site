---
title: Concurrency
weight: 3
---

Clef's concurrency model is built on delimited continuations — a composable, formally grounded primitive that replaces the colored-function problem of traditional async/await with a unified mechanism for suspension, resumption, and structured concurrency.

These articles explore how that foundation was chosen, how it interacts with interaction nets to support fine-grained parallelism, and how the compiler lowers async Clef code to native continuations without a managed runtime. If you're familiar with .NET's async model, the migration article traces the specific design choices that diverge from that lineage and why.
