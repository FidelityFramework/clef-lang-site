---
title: JavaScript Targeting
weight: 5
---

Clef is a native compiler. Its primary targets are CPUs, GPUs, FPGAs, and spatial accelerators, all reached through MLIR's dialect infrastructure and LLVM code generation. JavaScript is not a natural target for this architecture. It is dynamically typed, garbage collected, and structurally hostile to the properties that Clef's type system is designed to preserve.

And yet JavaScript is the execution environment for Cloudflare Workers, the substrate on which Fidelity.CloudEdge deploys actors to the edge. The question is not whether Clef should target JavaScript — the deployment topology requires it — but whether it can do so without abandoning the verification properties that justify the MLIR pipeline in the first place.

These articles examine how Google's JSIR dialect places JavaScript inside MLIR as a first-class target, what that means for a compiler designed around native emission, and where the trust boundaries actually fall when verified IR lowers to an untyped runtime.
