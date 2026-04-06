---
title: "A Runtime Revolution, sort of..."
linkTitle: "Runtime Revolution"
description: "Google's JSIR Dialect Brings Cloud Edge Targeting and More to Fidelity Framework"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design"]
---

The Fidelity framework is primarily designed to target hardware in the most direct manner possible. You know the acronyms: CPUs, GPUs, FPGAs, MCUs, NPUs, and other accelerators too. Every compilation target goes through Alex (the "Library of Alexandria"), our MLIR middle-end, where dimensional verification, escape analysis, and BAREWire schema derivation all happen in one place. It is the heart of our framework. 

So why is a *JavaScript* target cause for excitement?

We have to admit, this *is* a special case. We have an affinity for Cloudflare's cloud edge model for its scale, utility, speed and security. Cloudflare Workers execute in V8 isolates, so JavaScript is the deployment format for edge workloads on their platform.

Until this development, we thought we'd be using a separate JavaScript path with F# and the Fable compiler. That would mean Composer for native targeting, and Fable for Cloudflare workloads; two compilers sharing some syntactic sympathies and diverging behind the scenes.

But recently, [Google published an RFC to upstream JSIR](https://github.com/google/jsir) (JavaScript Intermediate Representation) into MLIR. As mentioned above, using Multi-Level Intermeiate Representation is the central strata for the Fidelity framework's compilation path. So while we were looking at LLVM for certain 'legacy' targets and other back ends to carry to specific types of processors, the introduction of a JavaScript pathway thorugh MLIR was a pleasant surprise. For our purposes, JSIR places JavaScript inside the same lowering pathway that every other Clef target already uses. That means the JavaScript path can now go through Alex, through the same verification passes, through the same BAREWire schema derivation, and to a dedicated "BackEnd" via JavaScript to be packaged for various uses. 

The [technical details](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) are worth reading if you want to understand how JSIR's ops map to Alex's dialect infrastructure and where the trust boundaries fall. This post is about what the unification means in practice.

## Why This Matters for Cloudflare Workers

The Fidelity framework deploys actors to two substrates. Native actors run as OS processes with IPC and shared memory. Edge actors run as Cloudflare Workers, Durable Objects in V8 isolates. The [unified actor architecture](/blog/unified-actor-architecture/) describes how Prospero supervisors and Olivier workers communicate across this boundary using BAREWire.

The critical question has always been: can you trust that the native serializer and the JavaScript deserializer agree on the byte layout? In the Fable-based architecture, that agreement was verified by testing. If the two compilers disagreed about field order in a discriminated union, the mismatch showed up at runtime, in production.

With JSIR in Alex, both serializers derive from the same BAREWire dialect ops. The pipeline forks after the schema is derived, not before. Cross-substrate compatibility becomes a structural property of the compiler as opposed to after-build validation using standard testing. The [design-time specification article](/docs/design/javascript-targeting/design-time-spec-runtime-reliability/) walks through each verified property and how it behaves after emission to JavaScript.

## BAREWire at the Wire Boundary

[BAREWire](/blog/getting-the-signal-with-barewire/) was designed for exactly this kind of cross-substrate communication. The binary wire format encodes type structure in the layout of the bytes. No field names. No schema negotiation. The deserializer reads positions, not keys.

What JSIR adds is confidence that the JavaScript-side deserializer was produced by the same pipeline that produced the native-side serializer. The schema identity that BAREWire enforces at the wire level now has a compilation-level counterpart: both sides of the conversation were lowered from the same verified IR.

This is where the [dimensional type system](/blog/doubling-down-dmm-dts/) and BAREWire intersect in a new way. Schema identity serves as a proxy for dimensional agreement. If the dimensional structure of a type changes, the schema changes. If the schema changes, the tag changes. If the tag changes, the receiver rejects the frame. Dimensional disagreement surfaces as transport-level rejection, before any handler code executes. The JSIR article covers this mechanism in depth.

## Streaming Tokens from Containers

The unification also opens a path for inference streaming. When a BitNet ADM running in a container generates tokens, each token can become a BAREWire frame, streamed through a Worker to a client over WebSocket.

The conventional approach is Server-Sent Events with JSON. Every frame carries field names as strings. The schema is rediscovered from every message. A typical token payload is 60+ bytes of redundant structure.

With BAREWire, the same token is roughly 18 bytes. No field names. No JSON parsing. The frame is self-contained, independently parseable, and typed by the discriminated union that the compiler verified. The [streaming inference article](/docs/design/javascript-targeting/streaming-inference-through-the-actor-pipeline/) describes the full path from spatial dataflow inside the container through demand-driven frame emission to WebSocket relay at the edge.

Multiple concurrent inference streams multiplex over a single WebSocket, demultiplexed by correlation ID on the client. Each stream accumulates independently. With MoQ/QUIC in the future, each stream could become a separate QUIC stream with independent ordering and no head-of-line blocking.

## What Would Not Change

JSIR would affect the compilation pipeline. It would not affect the [actor model's design](/blog/the-case-for-actor-oriented-architecture/), the management API surface, or the deployment infrastructure. Olivier workers would still communicate via WebSocket. Prospero supervisors would still manage lifecycle. BAREWire would still carry the messages. The [RAII patterns for actors](/docs/design/memory/raii-in-olivier-and-prospero/) would be unchanged.

The Fidelity.CloudEdge management layer would still provision Workers, create D1 databases, and deploy scripts through the same REST clients. Whether the actual Worker runtime code was compiled by Fable or by Composer via JSIR would be invisible to the management layer.

## Where We See the Trust Boundary Moving

What excites us most is what JSIR could do for the trust boundary between native and edge actors. The JavaScript itself would still be dynamically typed and garbage collected. But the design-time contract between actors, enforced by BAREWire's schema identity, could extend further than any comparable framework offers today.

The closest prior art is Erlang/OTP. The BEAM VM provides runtime pattern matching on tagged tuples at the receive site, and OTP's supervision trees enforce structured concurrency across distributed nodes. Erlang gets closer to verified actor communication than anything else in production. But the verification is at runtime: a mismatched message crashes the receiving process, and the supervisor restarts it. The contract is enforced by convention and testing, not by the compiler. Elixir inherits the same model with improved ergonomics but the same runtime trust boundary.

Conventional edge deployment frameworks don't get even that far. gRPC-Web serializes to protobuf but relies on code generation that is disconnected from the application's type system. tRPC provides end-to-end TypeScript types but those types are erased at runtime and carry no verification beyond what TypeScript's structural checking provides. Remix and Next.js server actions cross the client/server boundary through JSON serialization with no schema enforcement at all.

With JSIR in the pipeline, we anticipate that the BAREWire schema governing actor communication would be derived from verified, well-structured types and records in a shared set of data specifications. The native serializer and the JavaScript deserializer would both lower from the same MLIR ops. Schema identity at the wire level would become a structural consequence of the compilation, not a property validated after the fact. The [verification internals](/docs/internals/verification/) describe what the pipeline is designed to guarantee: dimensional consistency, memory safety, representation fidelity, optimization correctness. The JavaScript binary would not be fully verified in the way a native binary is. But the contract between actors, the messages they exchange, the schemas they enforce, could be verified at design time and carried through to the wire format. We believe that would be a trust boundary no other edge framework provides.

## Looking Forward

The [posit arithmetic design](/docs/design/categorical-foundations/posit-arithmetic-dimensional-type-systems/) describes how the DTS would select numeric representations based on dimensional range analysis. On native targets, this means tapered precision concentrated where the computation operates. On JavaScript, it means IEEE 754 float64 every time. We're exploring how the compiler could surface this divergence as a design-time diagnostic so the developer can make informed decisions about what computation belongs at the edge and what belongs on native hardware.

JSIR also opens a path beyond Workers that we're eager to explore. The [WREN stack](/blog/wren-stack/) uses Partas.Solid components rendered in a system WebView, with native Clef logic communicating over BAREWire through a local WebSocket. Today, Partas.Solid compiles F# to JavaScript through Fable for the front-end layer. With JSIR in the pipeline, that front-end JavaScript could be produced by Composer through the same verified middle-end as the native backend. The WREN stack's WebView layer and its native layer would both compile from the same source, through the same passes, with the same BAREWire schema derivation governing the messages between them. The front-end/backend boundary would become another instance of the cross-substrate pattern that JSIR makes structural.

The three articles in the [JavaScript targeting section](/docs/design/javascript-targeting/) cover the technical foundations we're building toward: how JSIR welds to our pipeline, how verified properties could carry through to emission, and how BAREWire would enable streaming inference from containers through Workers to clients. This post is the invitation to read them.

For a framework designed around native compilation, the idea that JavaScript could become just another backend, reached through the same dialect infrastructure and subject to the same verification passes, is genuinely exciting. Not because JavaScript becomes something it is not. Because the compiler would stop treating it differently.
