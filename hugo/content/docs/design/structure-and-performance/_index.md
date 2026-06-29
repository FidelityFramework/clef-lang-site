---
title: "Structure and Performance"
linkTitle: "Structure and Performance"
description: "How the structure of functional code becomes efficient native execution: the design decisions that make Clef lower well, before any compiler stage runs."
weight: 50
---

Performance in Clef is a property of how code is structured, not a phase that recovers it afterward. The same functional discipline that makes a program tractable to reason about, explicit data flow, bounded arity, controlled laziness, cohesive modules, is what makes it tractable to lower into fast native code. These articles are about that connection: why the shape of the language is the shape that compiles well.

This section is the *why*. It covers the design decisions that determine how efficiently a program can be lowered: why Clef fits MLIR, why laziness is the hard case and how it is bounded, how coupling and cohesion translate into cache behavior, what flow-loss analysis measures, and what arity and inlining buy at the boundary. The mechanism that carries out the lowering, Baker, the saturation engine, the nanopass pipeline, the dialect lowerings, lives in [Compiler Internals](/docs/internals/).
