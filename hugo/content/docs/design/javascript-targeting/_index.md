---
title: JavaScript Targeting
weight: 5
---

Fidelity.CloudEdge is the edge computing layer of the Fidelity Framework. It provides substrate-agnostic actor model support across bare metal and Cloudflare's edge platform: 727 runtime types covering the complete Workers surface (Durable Objects, D1, R2, KV, Queues, Workers AI, Vectorize, Containers, and more) plus 32 management service clients for infrastructure provisioning and orchestration. Actors written against `MailboxProcessor` run on either substrate without code changes, with Durable Objects providing the sequential execution context at the edge.

Cloudflare Workers run JavaScript. Today, Fidelity.CloudEdge compiles F# to JavaScript through Fable. But a recent publication of Google's JSIR (JavaScript Intermediate Representation) opens a path to compile through the same MLIR pipeline that native targets use, bringing the full Composer pass infrastructure to JavaScript-built workloads.

These articles cover how that pipeline unification works, how BAREWire is a pivotal realtime verification layer between native and JavaScript substrates, and how the compilation path enables streaming from containers through Workers to clients, both for classical and inference workloads.
