---
title: JavaScript Targeting
weight: 5
---

Clef is a native compiler. Its primary targets are CPUs, GPUs, FPGAs, and spatial accelerators, all reached through MLIR's dialect infrastructure and LLVM code generation. JavaScript enters the picture through Cloudflare Workers, the substrate on which Fidelity.CloudEdge deploys actors to the edge.

Google's JSIR dialect places JavaScript inside MLIR as a first-class backend, which means the JavaScript target joins the same compilation pipeline as every other target. The verification properties that hold in the shared middle-end apply to the JavaScript path because the JavaScript path goes through the same middle-end.

These articles examine how JSIR unifies the compilation pipeline, how BAREWire's frame format enables token-level streaming from container inference to edge delivery, and where the trust boundaries fall when verified IR lowers to a dynamically typed runtime.
