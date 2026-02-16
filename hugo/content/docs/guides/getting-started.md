---
title: Getting Started
weight: 1
---

{{< callout type="info" >}}
Clef is in active development. The toolchain is not yet available for public use, but the language specification, design rationale, and compiler architecture are progressing in the open. This page describes where things stand and what to expect.
{{< /callout >}}

## What is Clef?

Clef is a concurrent systems language in the ML family, designed for heterogeneous compute. It targets CPU, GPU, NPU, FPGA, and other accelerators through a single coherent type system — with proof-carrying capabilities for safe realtime systems.

If you're familiar with F#, Clef will feel immediately recognizable. The syntax and type-checking behavior are preserved; what changes is the compilation target. Clef produces standalone native binaries without runtime dependencies, using MLIR as its primary backend infrastructure.

## Current Status

The Clef toolchain comprises several components at different stages of maturity:

| Component | Purpose | Status |
|-----------|---------|--------|
| **Composer** | Compiler and build orchestration | In development |
| **ClefPak** | Source-based package manager | In development |
| **Lattice** | Language server (LSP) | In development |
| **Atelier** | IDE extensions (VS Code, Vim) | Planned |

The [language specification](/spec) is the most mature artifact and is published for review. The compiler frontend (CCS) is operational for a growing subset of the language. Backend code generation through MLIR is progressing, with initial targets focused on CPU-native output before expanding to GPU and accelerator targets.

## What You Can Do Now

While the toolchain is being assembled, there are several ways to engage with the project:

- **Read the specification** — the [draft spec](/spec/draft/) defines Clef's type semantics, memory model, and native type universe in normative detail
- **Explore the design rationale** — the [design documents](/docs/design/) explain the research and trade-offs behind language decisions
- **Follow development** — the [Fidelity Framework](https://github.com/FidelityFramework) organization on GitHub hosts the compiler, specification, and supporting libraries

## What's Ahead

The path to a public toolchain involves several infrastructure milestones that are actively being worked through: MLIR dialect stabilization, native memory management validation, and cross-target build orchestration. These are not small problems, and we are taking the time to get them right rather than shipping a partial experience.

This guide will be updated with installation instructions, project setup, and a first-program walkthrough as those capabilities come online.
