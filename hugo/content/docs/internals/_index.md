---
title: Compiler Internals
weight: 4
sidebar:
  open: false
---

Composer is the Clef compiler. It uses a nanopass architecture built on MLIR, lowering ML-family source through a series of small, composable transformations into native machine code for CPUs, GPUs, FPGAs, and spatial accelerators.

The compilation path starts in Clef Compiler Services (CCS), which produces an AST and a fully typed tree. Baker correlates these into the Program Semantic Graph (PSG), a unified representation that preserves both structural and type information. The PSG is then saturated: dimensional annotations are verified via Z3, escape classifications are resolved through the coeffect algebra, and BAREWire schemas are derived from verified discriminated unions. Alex lowers the saturated PSG into MLIR dialects using XParsec, and from there the code flows to target-specific backends through LLVM, CIRCT, or JSIR depending on the deployment substrate.

The articles below document each stage of this process, from the front-end through to silicon.

### [Concepts](concepts/)

Foundational ideas that inform the Composer compiler's design: nanopass architecture, coeffect and codata systems, referential transparency analysis, and the sequence expression compilation model.

### [Compiler Pipeline](pipeline/)

The internal stages of Composer: Baker type resolution and PSG construction, hypergraph-based intermediate representation, and the optimization passes (tree shaking, graph coloring, proof-aware compilation) that shape the code before it reaches MLIR.

### [Transparent Verification](verification/)

How the DTS maps dimensional constraints to Z3's decidable `QF_LIA` fragment for microsecond-scale verification, how the coeffect algebra extends that verification model to memory safety through escape classification, and how the verified PSG produces cryptographic proof certificates that attest dimensional consistency, memory safety, representation fidelity, and optimization correctness in the release binary.

### [MLIR Integration](mlir/)

How Composer lowers the PSG into MLIR dialects, how Alex selects between `arith`, `memref`, `scf`, and `cf` dialects based on type information, how the MLIR test infrastructure validates generated IR, and how the pipeline produces native code through LLVM. Includes an introduction to MLIR for developers coming from other compiler backgrounds.

### [Hardware Targets](targets/)

Target-specific code generation. Cache-conscious memory management for both CPU and GPU architectures, embedded platform support for STM32 and Cortex-M targets, and AMD RDNA unified memory architecture for desktop GPU compute. Each article covers how Composer adapts its lowering strategy to the constraints and opportunities of a specific hardware platform.

### [Farscape Interop](farscape/)

Farscape provides C and C++ interoperability for Clef. These articles cover binding generation from C++ headers, modular entry points for mixed-language projects, and the design decisions that allow Clef to call into and be called from native C/C++ code without marshaling overhead.

### [Developer Tooling](tooling/)

Lattice is the Clef language server, built on the saturated PSG. It provides design-time diagnostics, escape classification visibility, dimensional verification feedback, and restructuring proposals directly in the editor. These articles also cover autocomplete integration with the Fidelity ecosystem and the Frosty test harness for compiler validation.

### [Hardware Architecture](hardware/)

Hardware-level concerns that inform Composer's code generation decisions. RDMA networking for low-latency actor communication, next-generation memory coherence models that shape how Composer reasons about shared state, and the silicon-level arithmetic architectures (ternary quantization, posit formats) that the DTS representation selection targets.
