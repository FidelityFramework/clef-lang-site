---
title: Design
weight: 3
sidebar:
  open: false
---

These design documents are the informative companion to the [formal specification](/spec/draft/). The spec says what Clef is; these articles say why, the research, the trade-offs, and the decisions that shaped each part of the language.

The sections run from the inside out, starting at the language a developer writes and moving outward to the theory it rests on and the targets it reaches.

- **[Language Semantics](/docs/design/language/)**: the constructs a developer writes, quotations, active patterns, and computation expressions, treated as carriers that move semantic information through the pipeline.
- **[Type System](/docs/design/types/)**: the Native Type Universe, where ML-family types map directly to machine-native layouts, with dimensional safety and the negative and fractional duals that ride the same algebra.
- **[Concurrency](/docs/design/concurrency/)**: delimited continuations as the single primitive under async, actors, and structured concurrency, with deadlock freedom as a typing obligation.
- **[Memory Model](/docs/design/memory/)**: ownership, lifetimes, and region boundaries inferred from program structure, producing deterministic allocation and cleanup without a garbage collector.
- **[Structure and Performance](/docs/design/structure-and-performance/)**: why the shape of functional code is the shape that lowers well, the design decisions that make a program efficient before any compiler stage runs.
- **[Categorical Foundations](/docs/design/categorical-foundations/)**: the 2-categorical structure the type system and memory model turn out to instantiate, placing gradients, sensitivity, and quantum evolution under one algebraic framework.
- **[Constrained Machine Learning](/docs/design/constrained-machine-learning/)**: the Adaptive Domain Model, where a domain's conserved quantities, dimensions, and symmetries live in the types and inform the weights the model fits.
- **[JavaScript Targeting](/docs/design/javascript-targeting/)**: emission across the erasure boundary to the edge, where actors written once run on bare metal or Cloudflare's platform without code changes.
- **[FFI](/docs/design/interop/)**: foreign function interfaces and library binding at the boundary with non-Clef code.

For how the compiler carries out the lowering these articles motivate, the saturation engine, the nanopass pipeline, and the MLIR dialect path, see [Compiler Internals](/docs/internals/).
