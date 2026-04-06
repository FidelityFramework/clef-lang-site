---
title: "Design-Time Specification for Runtime Reliability"
linkTitle: "Design-Time Spec"
description: "How Clef's Compiler Produces Artifacts That Are Correct by Construction, Not by Testing"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Design", "Innovation"]
---

## The Core Principle

The Fidelity framework inverts a decades-old assumption in systems programming. Conventional compilers produce artifacts that must be tested to determine whether they are correct. The Fidelity compiler produces artifacts that are correct by construction, because the compiler verified the specification that the code implements before any code was emitted.

This is not a claim about testing being unnecessary. Testing verifies behavior against external requirements that the type system cannot express: user experience, integration with third-party services, performance under load. What the compiler eliminates is the class of errors that arise from internal inconsistency: dimensional mismatches, memory lifetime errors, representation precision failures, schema disagreements across process boundaries.

The DTS paper states the principle formally: "Decidability is not a property the framework discovers; it is a property the framework enforces through the structure of its type language." This article traces how that principle manifests across the framework's major subsystems.

## Dimensional Consistency

The [Dimensional Type System](/docs/internals/verification/decidability-sweet-spot/) assigns to each numeric value a dimension drawn from a finitely generated free abelian group. Dimensional consistency of an arithmetic expression reduces to linear algebra over the integers. Addition requires operand dimensions to be equal. Multiplication adds exponent vectors. Division subtracts them.

The compiler derives dimensional constraints from the program's arithmetic structure and solves them via Z3 in the `QF_LIA` fragment. The solution is polynomial-time, complete, and principal. Every dimensionally consistent program can be typed without annotation. The inferred type is the most general. The constraint system is finite and the solution algorithm terminates.

The runtime does not check dimensions. It does not carry dimensional metadata. Dimensions are erased after verification, exactly as the DTS paper specifies. A dimensioned and undimensioned compilation of the same program produces identical instructions. The verification happened at compile time. The runtime inherits the guarantee.

## Memory Safety

The [coeffect algebra](/docs/internals/verification/memory-coeffect-algebra/) classifies every value's escape behavior: StackScoped, ClosureCapture, ReturnEscape, ByRefEscape. Each classification maps to an allocation strategy and lifetime bound. The classification interacts with a lifetime ordering (stack < arena < heap < static), and when any usage of a value demands a lifetime exceeding the value's tentative assignment, the value is promoted.

The promotion is recorded in the PSG as a coeffect annotation. The compiler generates Z3 assertions verifying that no references to a value escape into a longer-lived scope than the value's allocation permits. Z3 proves the lifetime inequality mathematically.

The runtime does not perform garbage collection for verified allocations. It does not use reference counting. It does not insert runtime bounds checks for stack-scoped buffers with statically known sizes. The memory strategy was decided at compile time, verified by the coeffect system, and baked into the emitted code.

## BAREWire Schema Identity

When actors communicate across process boundaries, whether between native processes over IPC or between containers and Cloudflare Workers over WebSocket, the message format is BAREWire. The binary wire format encodes type structure in the layout of the bytes: tag as positional index, fixed payload layout per tag, no schema negotiation at runtime.

The [JSIR article](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) describes how schema identity serves as a proxy for dimensional agreement. The BAREWire schema is derived from a verified discriminated union. If the dimensional structure of the source type changes, the schema changes. If the schema changes, the tag changes. If the tag changes, the receiver rejects the frame at the transport level. Dimensional disagreement surfaces as schema disagreement, which surfaces as tag mismatch, which surfaces as transport-level rejection.

The runtime does not validate schemas. It does not compare field names. It does not negotiate encodings. The deserializer reads positions determined by the compiled schema. A tag that does not exist in the receiver's schema is rejected before any handler code executes. The wire format enforces the type structure that the compiler verified.

## Representation Selection

The DTS compilation chain links dimension to range, range to representation, representation to width, width to footprint, footprint and escape classification to allocation. Each step consumes the output of the preceding inference. The [companion post on range propagation](/blog/proofs-for-the-real-world/) describes how this chain extends from representation selection into physical safety constraints.

On native targets, the compiler evaluates worst-case relative error across the dimensional range and selects the representation that minimizes it. On JavaScript targets via JSIR, the representation is always IEEE 754 float64. The compiler surfaces this divergence as a design-time diagnostic, not a runtime error.

The runtime does not negotiate representations. It does not perform precision checks. The numeric format was selected at compile time from the dimensional range analysis and baked into the emitted code.

## Cross-Substrate Compatibility

In a hybrid actor network where native actors run as OS processes and edge actors run as Cloudflare Workers, the [JSIR compilation pipeline](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) ensures both substrates consume the same BAREWire dialect ops in the shared MLIR middle-end. The pipeline forks after the schema is derived, not before. The native path lowers BAREWire ops to memory-mapped struct writes via LLVM. The Worker path lowers the same ops to `DataView`/`ArrayBuffer` operations emitted as JSIR ops.

Byte-identical serialization output is a structural property of the compiler, not a property verified by testing. If the two serializers produced different byte layouts for the same discriminated union, the divergence would originate in the MLIR lowering pass, not in an uncaught runtime mismatch.

## Streaming as a Special Case

The [streaming inference article](/docs/design/javascript-targeting/streaming-inference-through-the-actor-pipeline/) demonstrates how these properties compose for the specific case of autoregressive model output. A BitNet ADM in a container generates tokens. Each token becomes a BAREWire frame. The Worker relays frames over WebSocket. The client deserializes each frame as it arrives.

Every frame in the stream carries the same type guarantee as a single request/response message. A stream of 500 token frames is 500 independently typed, independently parseable messages. The correlation ID ties them to the original request. Multiple concurrent inference streams multiplex over the same WebSocket, demultiplexed by correlation ID on the client.

The frame structure is fixed at design time by the discriminated union definition:

```fsharp
type InferenceResponse =
    | Token of text: string * index: int     // tag 0
    | StreamEnd of totalTokens: int          // tag 1
    | StreamError of message: string         // tag 2
```

Adding a new case produces a new tag. Removing a case changes the tag mapping. Renaming a field changes nothing on the wire because fields are positional. Every structural change is visible at the type level, verified at compile time, and enforced at the wire level. The [BAREWire signal design](/blog/getting-the-signal-with-barewire/) and the [unified actor architecture](/blog/unified-actor-architecture/) describe the tell-first semantics that make this streaming model efficient: each frame is fire-and-forget from the sender's perspective.

## The Cryptographic Release Certificate

All of these properties converge in the release certificate. When `clef build --release` executes, the PSG is locked, the Z3 assertions from every verification pass are aggregated into a global SMT problem, Z3 verifies the system, and the resulting witness is cryptographically hashed alongside the compiled binary. The [verification internals](/docs/internals/verification/) document this pipeline in full.

The certificate attests that the compiled artifact preserves the properties verified in the PSG: dimensional consistency, memory safety, representation fidelity, optimization correctness. A third party can verify the certificate independently without trusting the compiler.

## What the Compiler Does Not Verify

The boundary of the compiler's jurisdiction is explicit. The compiler does not verify:

**Clinical correctness.** Whether the right drug was selected for the right indication is a domain decision, not a type system property. The [range propagation analysis](/blog/proofs-for-the-real-world/) can verify that a dosage stays within a declared therapeutic window, but it cannot verify that the therapeutic window was declared correctly.

**External API contracts.** Whether `storage.put()` persists the value or the V8 event loop schedules handlers in the expected order depends on Cloudflare's runtime, not on the compiler.

**User experience.** Whether the client renders the streaming tokens legibly is a design concern outside the compilation pipeline.

**Performance under load.** Whether the system meets latency targets at a given request rate depends on the deployment infrastructure, not on the type system.

These concerns require testing, monitoring, and operational validation. The compiler's contribution is removing an entire category of failure from the space that testing must cover. Dimensional mismatches, schema disagreements, memory lifetime errors, and representation precision failures do not appear in production because they cannot survive compilation. Testing addresses the concerns that remain: behavior against external requirements, performance characteristics, and integration with systems outside the compiler's jurisdiction.

## The Practical Consequence

An engineer working in Lattice sees dimensional consistency verified as they type. They see escape classifications and allocation strategies surfaced in the editor. They see range propagation findings with exact confidence levels. They see representation fidelity diagnostics showing precision divergence across targets. Every finding corresponds to a Z3-verified assertion that will appear in the release certificate.

The engineer's confidence in the deployed artifact does not rest on test coverage. It rests on the mathematical properties of the type system and the compiler's verification pipeline. Testing validates the things the compiler cannot see. The compiler guarantees the things testing cannot prove.

This is the design-time specification that produces runtime reliability. The compiler specifies. The runtime obeys. The wire format bridges the gap. The certificate attests the whole chain.
