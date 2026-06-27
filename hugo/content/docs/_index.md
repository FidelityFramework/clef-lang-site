---
title: Documentation
cascade:
  type: docs
---

Clef is a concurrent systems language in the ML family, targeting heterogeneous compute — CPU, GPU, NPU, FPGA — through a single coherent type system. These documents cover the language from first principles through compiler implementation.

**[Guides](/docs/guides/)** are the starting point. They describe the current state of the project and will expand into installation, setup, and tutorial content as the toolchain matures.

**[Design Rationale](/docs/design/)** explains _why_ Clef works the way it does — the research, trade-offs, and prior art behind decisions in the memory model, type system, and concurrency architecture.

**[Compiler Internals](/docs/internals/)** documents how Composer is built: the nanopass pipeline, MLIR integration, hypergraph-based optimization, and backend code generation for native and embedded targets.

**[Tooling](/docs/tooling/)** covers the developer-facing surface of the Clef ecosystem: the Lattice language server, autocomplete, the ClefPak package manager, and source-level dependency resolution.

The [language specification](/spec/draft/) is maintained separately and defines Clef's semantics in normative detail.
