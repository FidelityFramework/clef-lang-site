---
title: Transparent Verification
weight: 15
sidebar:
  open: false
params:
---

The Fidelity Framework includes tooling in development designed to eliminate the annotation burden that has historically made formal verification impractical for systems programming. Instead of requiring developers to write theorems, the Clef Compiler Service (CCS) is designed to derive proof obligations directly from the code's dimensional constraints and memory topologies, transparently, at design time, backed by the cvc5 SMT solver.

These articles trace the planned verification pipeline: from the double-annotation friction that led to the algebraic discovery behind the Dimensional Type System, through its decidable foundations, the coeffect-based memory safety model, and finally to the MLIR translation validation and cryptographic release certificates designed to verify dimensional and memory properties statically, imposing no runtime overhead for the covered property classes.

[Proof Composition and Tooling](/docs/internals/verification/proof-composition-and-tooling/) connects these local results to reusable concurrent, distributed, relational and device-level foundations. It explains automatic lemma instantiation, checked mode shifts, explicit trust dependencies and the proposed consolidated Rocq toolchain. [Mode Shifts](/docs/internals/verification/mode-shifts/) develops the judgment interfaces. These designs distinguish existing compiler slices from integrations still to be built.
