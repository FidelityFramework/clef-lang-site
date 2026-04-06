---
title: Design Rationale
weight: 3
sidebar:
  open: false
---

Design documents provide the informative companion layer to the [formal specification](/spec/draft/). These articles explain the _why_ behind language decisions — the research, trade-offs, and motivations that shaped Clef.

The articles are organized into focused sub-sections:

- **[Memory Model](/docs/design/memory/)** — region-based allocation, ownership, lifetime inference, and deterministic cleanup
- **[Type System](/docs/design/types/)** — the Native Type Universe, dimensional type safety, and type representation across execution models
- **[Concurrency](/docs/design/concurrency/)** — delimited continuations, interaction nets, and the async compilation strategy
- **[JavaScript Targeting](/docs/design/javascript-targeting/)** — JSIR, MLIR-based JavaScript emission, and type safety across the erasure boundary

General design articles covering topics like metaprogramming, verification, inlining, posit arithmetic, and the broader structural decisions behind Clef remain at this level.

Many of these documents were originally published on the [SpeakEZ Technologies blog](https://speakez.tech) during the early design phase of the Fidelity Framework and have been updated to reflect current naming and project structure.
