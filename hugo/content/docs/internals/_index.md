---
title: Compiler Internals
weight: 4
sidebar:
  open: false
---

Composer is the Clef compiler — a nanopass architecture built on MLIR that lowers ML-family source through a series of small, composable transformations into native machine code.

These articles document the internal architecture: the frontend parse and type-check pipeline, the hypergraph-based intermediate representation, optimization passes including graph coloring and tree shaking, cache-aware code generation for both CPU and GPU targets, and backend lowering for embedded platforms like STM32. If you want to understand how Clef code becomes a native binary, this is where to look.
