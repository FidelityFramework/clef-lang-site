---
title: "Design-Time Specification for Runtime Reliability"
linkTitle: "Design-Time Spec"
description: "How the Verified Properties in Clef's Middle-End Carry Through to JavaScript Emission via JSIR"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Design"]
---

## What the Middle-End Verifies

The [JSIR article](../jsir-javascript-as-mlir-backend/) establishes that JavaScript joins the same MLIR pipeline as every other Clef target. Both the native path and the JavaScript path go through Alex. Both consume the same dialect ops. Both are subject to the same pass infrastructure.

This article examines what that shared pipeline verifies, how each verified property behaves after emission to JavaScript, and where the compiler's jurisdiction ends and the runtime contract begins.

## Dimensional Consistency

The [DTS verification pipeline](/docs/internals/verification/decidability-sweet-spot/) assigns dimensional annotations to every numeric value and solves consistency constraints via Z3 in the `QF_LIA` fragment. The solution is polynomial-time, complete, and principal. Every dimensionally consistent program can be typed without annotation. The inferred type is the most general. The constraint system is finite and the solution algorithm terminates. This verification happens in Alex, before the pipeline forks to target-specific backends.

For JavaScript via JSIR, the dimensional verification that runs for the native path also runs for the JavaScript path, because both paths share the same middle-end. A Clef program that passes dimensional verification will not produce a dimensionally inconsistent computation on either target.

The dimensions are erased after verification. The emitted JavaScript carries no dimensional metadata. A dimensioned and undimensioned compilation of the same program produces identical instructions. This property holds for every target, including JavaScript. The verification happened at compile time. The runtime inherits the guarantee. The runtime does not check dimensions because there is nothing to check. Every program that could produce a dimensional inconsistency was rejected before emission.

## Memory and Escape Classification

The [coeffect algebra](/docs/internals/verification/memory-coeffect-algebra/) classifies every value's escape behavior: StackScoped, ClosureCapture, ReturnEscape, ByRefEscape. Each classification maps to an allocation strategy and lifetime bound. The classification interacts with a lifetime ordering (stack < arena < heap < static), and when any usage of a value demands a lifetime exceeding the value's tentative assignment, the value is promoted. The promotion is recorded in the PSG as a coeffect annotation. The compiler generates Z3 assertions verifying that no references to a value escape into a longer-lived scope than the value's allocation permits.

On native targets, these classifications drive concrete allocation decisions. Stack-scoped values stay on the stack. Arena-captured values go to arena allocation. The compiler eliminates garbage collection for verified allocations. Z3 proves the lifetime inequality mathematically. The resulting binary runs without a garbage collector, without reference counting, without runtime bounds checks for stack-scoped buffers with statically known sizes.

On JavaScript, the V8 garbage collector manages memory. The escape classifications still run in Alex, but the JavaScript backend cannot act on them the way LLVM can. A StackScoped classification does not prevent V8 from heap-allocating the value. The allocation strategy that the coeffect system derives is not actionable on this target.

The verification is not wasted. The escape analysis in Alex catches lifetime errors that would manifest as use-after-free or dangling reference bugs on the native target. Those same errors, caught in the shared middle-end, never reach either backend. The JavaScript target benefits from the analysis even though it cannot exploit the allocation strategy. A Clef program that passes escape analysis is free of lifetime errors on both targets. On the native target, the compiler also optimizes allocation. On the JavaScript target, V8 handles allocation its own way, but the lifetime correctness guarantee still holds.

## BAREWire Schema Derivation

When actors communicate across substrate boundaries, the BAREWire schema is derived from a verified discriminated union. The JSIR article describes how schema identity serves as a proxy for dimensional agreement. The key property for JavaScript targeting is that the schema derivation happens in Alex, before the pipeline forks.

The native serializer (via LLVM) and the JavaScript deserializer (via JSIR) both derive from the same BAREWire dialect ops. The byte layout is identical because both lowering paths consumed the same IR representation. A Worker receiving a BAREWire frame from a native container reads the same byte positions that the container wrote. Cross-substrate serialization compatibility is a structural property of the shared middle-end, not a property that must be tested across substrate pairs.

This is the property that the Fable-based architecture could not guarantee. With Fable, two separate compilers each generated their own serializer from the same F# type definition. Agreement between the two serializers depended on both compilers making identical layout decisions. If they disagreed about field order, padding, or encoding for any discriminated union case, the mismatch was discovered at runtime. With JSIR in Alex, the layout decision is made once, in the shared middle-end, and both backends consume the same decision.

The enforcement extends to schema evolution. If a discriminated union gains a new case, the tag mapping changes. If the native side deploys the new schema before the edge side, the Worker receives frames with an unrecognized tag and rejects them at the transport level. The mismatch is immediate, explicit, and does not require version negotiation. The [BAREWire signal design](/blog/getting-the-signal-with-barewire/) describes the tell-first semantics that make this rejection safe: the sender does not wait for acknowledgment, so a rejected frame does not block the sender.

## Representation Fidelity

The DTS compilation chain links dimension to range, range to representation, representation to width, width to footprint, footprint and escape classification to allocation. Each step consumes the output of the preceding inference. On native targets, the representation selection step is the payoff. The compiler evaluates worst-case relative error across the dimensional range and selects the representation that minimizes it. A value with dimensional range `[1e-3, 1e6]` might be selected as `posit<32,2>` on an FPGA target, where tapered precision concentrates bits in the primary operating range, or `float64` on x86, where uniform precision is adequate. The [posit arithmetic design](/docs/design/categorical-foundations/posit-arithmetic-dimensional-type-systems/) covers this selection mechanism in detail.

On JavaScript via JSIR, the representation selection step runs and produces a trivial result. JavaScript's `Number` type is IEEE 754 float64. There is no posit arithmetic, no fixed-point, no tapered precision. The dimensional range analysis executes, and the answer is always the same. The representation selection machinery fires and has nothing to decide.

The range analysis is not wasted. The compiler knows the dimensional range of every value on the JavaScript path. It knows that float64 provides uniform precision across that range rather than the tapered precision that a posit format would provide. The JSIR article describes the representation fidelity diagnostic that surfaces when an actor deploys cross-substrate:

```
TorqueActor deploys to both native (x86_64) and edge (Cloudflare Worker):
  force<newtons> range [1e-3, 1e6]:
    native:  posit<32,2>   worst-case relative error 1.5e-9 in primary range
    edge:    float64        worst-case relative error 1.11e-16 (uniform)
  
  torque<newton-meters> range [1e-6, 1e12]:
    native:  posit<32,2>   precision concentrated in [0.01, 1e4]
    edge:    float64        precision uniform across range
  
  Note: cross-substrate comparison of computed values will diverge
  at the representation level. BAREWire preserves exact bit patterns
  in transport; the divergence originates in the arithmetic.
```

The developer sees, before deployment, where precision will diverge and by how much. The decision to deploy a computation to the edge is then informed, not blind.

This is not a limitation of JSIR. It is a limitation of the JavaScript numeric model. The compiler's contribution is making the limitation visible and quantified rather than silent. For computations that require grade preservation or exact accumulation, where posit arithmetic and quire accumulators provide properties that IEEE 754 cannot, the diagnostic can be stronger: an explicit note that the edge target does not support the numeric properties that the native target provides. This is not an error. It is a design-time constraint that the developer should see and decide about, the same way the DTS language server surfaces escape promotions and allocation strategy changes.

## The Release Certificate

The [verification internals](/docs/internals/verification/) describe the cryptographic release certificate generated by `clef build --release`. The PSG is locked. The Z3 assertions from every verification pass are aggregated into a global SMT problem. Z3 verifies the system. The resulting witness is cryptographically hashed alongside the compiled binary into a `.proofcert` artifact.

For a Clef project that targets both native and JavaScript, the certificate covers both compilation paths. The dimensional consistency assertions, the escape classification proofs, the BAREWire schema derivations all originate in the shared middle-end and apply to both backends. The certificate attests properties of the pipeline, not properties of a specific target.

The certificate guarantees that the compiled artifact preserves the properties verified in the PSG: dimensional consistency, memory safety, representation fidelity, optimization correctness. A third party can verify the certificate independently without trusting the compiler. The Z3 witness is machine-checkable.

The JavaScript that JSIR emits is still dynamically typed. It still runs in a garbage-collected V8 isolate. It still depends on Cloudflare's runtime contract. The certificate does not change what JavaScript is. It attests what happened before the JavaScript was produced: the program was verified, the schemas were derived from verified types, and the emitted code faithfully represents the verified IR.

## What the Compiler Does Not Verify

The boundary of the compiler's jurisdiction is explicit. Claiming more than the type system can guarantee would undermine the credibility of the claims it does make. For JavaScript targeting, the boundaries are:

**External API contracts.** The emitted JavaScript calls Cloudflare's runtime APIs: `fetch()`, `WebSocket.send()`, `DurableObjectState.storage.put()`. Whether `storage.put()` persists the value or the V8 event loop schedules handlers in the expected order depends on Cloudflare's runtime, not on the compiler. The MLIR pipeline can verify that the call is structurally correct (right number of arguments, right SSA value flow) but it cannot verify that the runtime honors its contract. Cloudflare mitigates this with compatibility dates, the open-source workerd runtime, and the Workers runtime test suite. These are engineering practices, not formal guarantees.

**V8 execution semantics.** The V8 isolate is opaque to the compiler. JavaScript's execution model (event loop, microtask queue, garbage collection timing) is defined by the runtime, not by the IR that produced the code. The compiler can verify properties of the program. It cannot verify properties of the runtime that executes the program.

**Numeric behavior beyond representation.** The compiler selects float64 for JavaScript and surfaces the precision characteristics. It does not and cannot verify that IEEE 754 rounding behavior will produce acceptable results for a specific computation. The [companion post on range propagation](/blog/proofs-for-the-real-world/) describes how range analysis produces safety proofs for monotonic arithmetic over declared ranges. Those proofs hold regardless of representation. But the specific numeric values produced by float64 arithmetic may differ from the values produced by posit arithmetic on a native target, and the compiler cannot determine whether that difference matters for the application. That determination belongs to the developer, informed by the representation fidelity diagnostic.

**User experience.** Whether the client renders streaming tokens legibly, whether the UI handles a `StreamError` frame gracefully, whether the latency between container and Worker is acceptable for the application. These are design and operational concerns outside the compilation pipeline.

These concerns require testing, monitoring, and operational validation. The compiler's contribution is removing an entire category of failure from the space that testing must cover. Dimensional mismatches, schema disagreements, memory lifetime errors, and serialization format drift between substrates do not appear in production because they cannot survive compilation. Testing addresses the concerns that remain: behavior against external requirements, performance characteristics, and integration with systems outside the compiler's jurisdiction.

## What the Runtime Does Not Do

The practical consequence for a Clef developer targeting JavaScript through JSIR is a short list of things the runtime does not need to do, because the compiler already did them.

The runtime does not check dimensions. The runtime does not validate BAREWire schemas. The runtime does not negotiate numeric representations. The runtime does not inspect frame structure beyond reading the tag and payload at compiled positions. The runtime does not compare serialization formats between substrates.

The runtime runs the code the compiler emitted. The compiler verified the specification that the code implements. The [unified actor architecture](/blog/unified-actor-architecture/) describes the actor semantics that the emitted JavaScript implements. The [design-time specification](/docs/design/javascript-targeting/design-time-spec-runtime-reliability/) principle applies uniformly: the compiler specifies at design time what the runtime must obey, the runtime does not verify anything, and the wire format bridges the gap where the compiler's jurisdiction ends and the runtime's begins.

The DTS paper states the principle formally: "Decidability is not a property the framework discovers; it is a property the framework enforces through the structure of its type language." For JavaScript targeting through JSIR, that enforcement happens in the shared middle-end. The JavaScript inherits the result.
