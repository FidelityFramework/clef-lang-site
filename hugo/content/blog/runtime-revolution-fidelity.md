---
title: "A Runtime Revolution (sort of) for Fidelity"
linkTitle: "Runtime Revolution"
description: "Google's JSIR Dialect Brings Cloud Edge Targeting and More to Our Framework"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design"]
---

## The Day JavaScript Became a Backend

On April 6, 2026, Google published an RFC to upstream JSIR into MLIR. For most compiler projects this is an interesting research result. For the Fidelity framework, it changes the shape of the entire deployment story.

Until now, Clef's compilation to JavaScript has been the odd one out. Every other target (CPUs, GPUs, FPGAs, spatial accelerators) travels through Alex, our MLIR middle-end, where dimensional verification, escape analysis, and BAREWire schema derivation all happen in one place. JavaScript was the exception. It took a separate path, compiled through Fable, maintained independently, verified independently. Two compilers sharing a front-end and nothing else.

JSIR eliminates that split. JavaScript becomes a backend in the same sense that LLVM is a backend. One source, one middle-end, one set of verification passes, two (or more) output formats. The [technical details](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) are worth reading if you want to understand how JSIR's ops map to Alex's dialect infrastructure and where the trust boundaries fall. This post is about what the unification means in practice.

## Why This Matters for Cloudflare Workers

The Fidelity framework deploys actors to two substrates. Native actors run as OS processes with IPC and shared memory. Edge actors run as Cloudflare Workers, Durable Objects in V8 isolates. The [unified actor architecture](/blog/unified-actor-architecture/) describes how Prospero supervisors and Olivier workers communicate across this boundary using BAREWire.

The critical question has always been: can you trust that the native serializer and the JavaScript deserializer agree on the byte layout? In the Fable-based architecture, that agreement was verified by testing. If the two compilers disagreed about field order in a discriminated union, the mismatch showed up at runtime, in production.

With JSIR in Alex, both serializers derive from the same BAREWire dialect ops. The pipeline forks after the schema is derived, not before. Cross-substrate compatibility becomes a structural property of the compiler rather than a property discovered by testing. The [design-time specification article](/docs/design/javascript-targeting/design-time-spec-runtime-reliability/) walks through each verified property and how it behaves after emission to JavaScript.

## BAREWire at the Wire Boundary

[BAREWire](/blog/getting-the-signal-with-barewire/) was designed for exactly this kind of cross-substrate communication. The binary wire format encodes type structure in the layout of the bytes. No field names. No schema negotiation. The deserializer reads positions, not keys.

What JSIR adds is confidence that the JavaScript-side deserializer was produced by the same pipeline that produced the native-side serializer. The schema identity that BAREWire enforces at the wire level now has a compilation-level counterpart: both sides of the conversation were lowered from the same verified IR.

This is where the [dimensional type system](/blog/doubling-down-dmm-dts/) and BAREWire intersect in a new way. Schema identity serves as a proxy for dimensional agreement. If the dimensional structure of a type changes, the schema changes. If the schema changes, the tag changes. If the tag changes, the receiver rejects the frame. Dimensional disagreement surfaces as transport-level rejection, before any handler code executes. The JSIR article covers this mechanism in depth.

## Streaming Tokens from Containers

The unification also opens a path for inference streaming. When a BitNet ADM running in a container generates tokens, each token can become a BAREWire frame, streamed through a Worker to a client over WebSocket.

The conventional approach is Server-Sent Events with JSON. Every frame carries field names as strings. The schema is rediscovered from every message. A typical token payload is 60+ bytes of redundant structure.

With BAREWire, the same token is roughly 18 bytes. No field names. No JSON parsing. The frame is self-contained, independently parseable, and typed by the discriminated union that the compiler verified. The [streaming inference article](/docs/design/javascript-targeting/streaming-inference-through-the-actor-pipeline/) describes the full path from spatial dataflow inside the container through demand-driven frame emission to WebSocket relay at the edge.

Multiple concurrent inference streams multiplex over a single WebSocket, demultiplexed by correlation ID on the client. Each stream accumulates independently. With MoQ/QUIC in the future, each stream could become a separate QUIC stream with independent ordering and no head-of-line blocking.

## What Did Not Change

JSIR affects the compilation pipeline. It does not affect the [actor model's design](/blog/the-case-for-actor-oriented-architecture/), the management API surface, or the deployment infrastructure. Olivier workers still communicate via WebSocket. Prospero supervisors still manage lifecycle. BAREWire still carries the messages. The [RAII patterns for actors](/docs/design/memory/raii-in-olivier-and-prospero/) are unchanged.

The Fidelity.CloudEdge management layer still provisions Workers, creates D1 databases, and deploys scripts through the same REST clients. Whether the Worker code was compiled by Fable or by Composer via JSIR is invisible to the management layer.

What changed is confidence. The JavaScript that runs at the edge was produced by the same verified pipeline that produces native binaries. The [verification internals](/docs/internals/verification/) describe what that pipeline guarantees: dimensional consistency, memory safety, representation fidelity, optimization correctness. Those guarantees now extend to the JavaScript target, not because JavaScript became trustworthy, but because the pipeline that produces it is the same pipeline that produces everything else.

## Looking Forward

The [posit arithmetic design](/docs/design/categorical-foundations/posit-arithmetic-dimensional-type-systems/) describes how the DTS selects numeric representations based on dimensional range analysis. On native targets, this means tapered precision concentrated where the computation operates. On JavaScript, it means IEEE 754 float64 every time. The compiler surfaces this divergence as a design-time diagnostic so the developer can make informed decisions about what computation belongs at the edge and what belongs on native hardware.

The three articles in the [JavaScript targeting section](/docs/design/javascript-targeting/) cover the technical foundations: how JSIR unifies the pipeline, how verified properties carry through to emission, and how BAREWire enables streaming inference from containers through Workers to clients. This post is the invitation to read them.

For a framework designed around native compilation, the idea that JavaScript is just another backend, reached through the same dialect infrastructure and subject to the same verification passes, is a significant step forward. Not because JavaScript became something it is not. Because the compiler stopped treating it differently.
