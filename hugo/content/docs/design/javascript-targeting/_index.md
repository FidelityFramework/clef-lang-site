---
title: JavaScript Targeting
weight: 80
---

Fidelity.CloudEdge is the edge computing layer of the Fidelity Framework. It provides substrate-agnostic actor model support across bare metal and Cloudflare's edge platform: 727 runtime types covering the complete Workers surface (Durable Objects, D1, R2, KV, Queues, Workers AI, Vectorize, Containers, and more) plus 32 management service clients for infrastructure provisioning and orchestration. Actors written against `MailboxProcessor` run on either substrate without code changes, with Durable Objects providing the sequential execution context at the edge.

Cloudflare Workers run JavaScript. Today, Fidelity.CloudEdge compiles F# to JavaScript through Fable. But a recent publication of Google's JSIR (JavaScript Intermediate Representation) opens a path to compile through the same MLIR pipeline that native targets use, bringing the full Composer pass infrastructure to JavaScript-built workloads.

JavaScript becomes an ordinary MLIR backend under this path, subject to the same verification passes as native targets, with BAREWire carrying the type contract across the substrate boundary that JavaScript's own type system does not survive.

JavaScript is one of two pathways the framework takes to the web. [WebAssembly Targeting](/docs/design/wasm-targeting/) covers the other: a sealed, statically typed target with compiler-laid linear memory, close enough to native that the framework's existing machinery applies directly, compiled through the same MLIR pipeline as everything above.
