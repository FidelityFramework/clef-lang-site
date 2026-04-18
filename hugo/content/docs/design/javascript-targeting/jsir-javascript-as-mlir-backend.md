---
title: "JSIR: JavaScript as an MLIR Backend"
linkTitle: "JSIR"
description: "How Google's JavaScript IR for MLIR changes the Clef compilation story"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design"]
---

The Fidelity framework is designed to target hardware natively. So why are we so cheerful about having a runtime target?

Because Cloudflare Workers run JavaScript, and Workers are how Fidelity deploys actors to the edge. Until now, the JavaScript compilation path was separate from the rest of the framework. On April 6, 2026, Google published an RFC to upstream JSIR (JavaScript Intermediate Representation) into MLIR. The announcement arrived as a single post on the LLVM Discourse, authored by Zhixun Tan of Google's compiler team. JSIR is an out-of-tree MLIR dialect that represents JavaScript with full AST fidelity, supporting lossless round-trip conversion between JavaScript source, Babel AST, and MLIR ops. Google has used it in production internally for Hermes bytecode decompilation, JavaScript deobfuscation, and malicious code detection.

For a framework that has been maintaining a separate JavaScript pipeline as a necessary compromise, this is not just an interesting research result. It is a structural inflection point.

## The Problem JSIR Solves

Clef compiles through the Composer middle-end, which is built on MLIR. The Program Semantic Graph lowers into Alex, a set of MLIR dialects that carry dimensional annotations, escape classifications, BAREWire schemas, and concurrency primitives through progressive lowering. At the end of the pipeline, Alex fans out to target-specific backends: LLVM for CPUs and GPUs, CIRCT for FPGA synthesis, MLIR-AIE for spatial accelerators. Every target is reached through MLIR's dialect infrastructure. Every optimization pass, every verification step, every analysis framework applies uniformly across targets.

JavaScript was the exception.

The Fidelity framework operates across two substrates. Native actors compile through LLVM and run as OS processes with IPC and shared memory. Edge actors compile to JavaScript and run as Cloudflare Workers, specifically Durable Objects executing in V8 isolates at the network edge. The same Clef source defines both. The same BAREWire protocol connects them. But the compilation paths diverged completely.

The original plan for JavaScript emission was to bypass the Alex middle-end entirely. Clef's PSG would lower to an Oak-like JavaScript AST (analogous to Fable's approach for F#) and emit JavaScript directly. This meant that every optimization and verification pass written against Alex would not apply to the JavaScript target. JavaScript would be a side door, separate from the MLIR pipeline, maintained independently, verified independently.

```
Previous architecture:

  Clef PSG ──▶ Alex MiddleEnd ──▶ LLVM ──▶ native binary
                                          (verified)

  Clef PSG ──▶ (bypass Alex) ──▶ Oak-like JS AST ──▶ JavaScript
                                          (separate pipeline, unverified)
```

This is the architectural equivalent of maintaining two compilers. One is the real compiler with the full verification story. The other is a translation layer that happens to produce JavaScript. The two share a front-end and nothing else.

## What JSIR Changes

JSIR places JavaScript inside MLIR as a first-class dialect. It is structurally the same kind of thing as EmitC, the MLIR dialect already upstream that lowers MLIR to C source code. EmitC established the pattern: an MLIR dialect can serve as a source language emission target, not just an analysis or optimization substrate. JSIR applies that pattern to JavaScript.

The architecture becomes:

```
With JSIR:

  Clef PSG ──▶ Alex MiddleEnd ──▶ LLVM ──▶ native binary

  Clef PSG ──▶ Alex MiddleEnd ──▶ JSIR ──▶ JavaScript source
```

Both paths go through Alex. Both consume the same dialect ops. Both are subject to the same pass infrastructure, the same verification framework, the same dataflow analysis. The JavaScript target is no longer a side door. It is a backend, in exactly the same sense that LLVM is a backend.

This matters because of what lives in Alex. Dimensional annotations, carried as PSG codata through every lowering stage, are available at the point where JSIR emission occurs. Escape classifications, resolved during elaboration, inform the JavaScript code generator. BAREWire schema derivation, expressed as MLIR ops, produces both the native serializer (via LLVM) and the JavaScript serializer (via JSIR) from the same IR representation. The verification that both serializers produce byte-identical output is no longer a testing concern; it is a structural property of the pipeline.

## JSIR's Design

JSIR maintains a nearly 1:1 mapping with Babel AST nodes. It uses MLIR regions to model JavaScript control flow structures (`if`/`while`/logical short-circuit) and SSA values for expression results. It distinguishes l-values (`jsir.identifier_ref`) from r-values (`jsir.identifier`), achieving 99.9%+ fidelity on round-trip conversion across billions of samples at Google. JSIR ships as a single binary, `jsir_gen`, whose `--passes` argument names a pipeline among four representations: JavaScript source, Babel AST, JSHIR (region-based high-level IR), and JSIR (flat SSA-shaped low-level IR). The forward pipeline is `source2ast,ast2hir`; the reverse pipeline used for backend emission is `hir2ast,ast2source`.

The contributor list confirms the design pedigree. Jacques Pienaar is a core MLIR contributor who mentioned JavaScript-to-MLIR round-trip work "not yet fully open source" in a 2022 RFC discussion. Mehdi Amini, now at NVIDIA, is another core MLIR infrastructure engineer. Jeff Niu, now at OpenAI, contributed to IRDL and other MLIR infrastructure. JSIR was built by people who understand MLIR's architecture, not as a side project, but as a production system tested against billions of JavaScript samples.

What JSIR is not: a type system for JavaScript. JSIR has no type representation. It is structurally and syntactically faithful, not semantically typed. Type erasure is an explicit design boundary. This is the correct decision for the dialect's purpose of representing JavaScript at the AST level, and it is the reason that type safety in the Clef-to-JavaScript pipeline cannot come from JSIR itself. It must come from what happens before JSIR, in the shared middle-end, where types still exist.

## The Trust Chain

The Fidelity framework's actor model deploys Clef actors as Cloudflare Durable Objects, JavaScript classes running in V8 isolates with infrastructure-enforced single-concurrency. Actors communicate via WebSocket using BAREWire, a binary serialization protocol whose wire format encodes discriminated union structure directly in the byte layout. A message tag identifies the union case. The payload layout is fixed per tag. The serializer and deserializer are generated from the same type definition at compile time.

This architecture creates a specific trust question: how much can you trust JavaScript that was emitted by a compiler whose verification properties are defined at the MLIR level?

The answer requires distinguishing three boundaries.

### Boundary 1: Type Erasure

JavaScript has no type system at runtime. When Clef compiles to JavaScript, whether via Fable today or via JSIR in the future, the type information is erased. A discriminated union becomes a JavaScript object with an integer tag and an untyped fields array. JavaScript imposes no constraint on what appears in those fields. A malformed message, a version-drifted sender, or a compromised intermediary could deliver structurally invalid data, and the JavaScript runtime would not object.

BAREWire addresses this boundary for messages on the wire. The binary wire format encodes type structure in the layout of the bytes: tag as positional index, fixed payload layout per tag, no schema negotiation at runtime. The deserializer does not validate; it reads, because the layout is structurally guaranteed by the serializer on the other end. The wire format functions as the runtime type system in a context where JavaScript's own type system is absent. This is not a metaphor. The message tag associates data with a type, the layout constrains operations, and unrecognized tags are rejected before dispatch. These are the operations a runtime type system performs.

For values that arrive as JavaScript objects rather than as BAREWire frames — Cloudflare API responses, KV-stored values parsed from JSON, third-party library return values — the complementary mechanism is schema-directed narrowing. Clef has no `obj` type and no `null`; the type system is closed. But every JavaScript value arriving at the interop boundary carries runtime tags that a disciplined compiler can read: property-name tags (the object's keys, accessible through standard reflection) and value type-tags (`typeof`, `Array.isArray`, `instanceof`, strict null checks). When a Clef expression declares a target type at the boundary — `response.Json() : UserProfile` — the compiler inspects the PSG to see what `UserProfile` is structurally and emits a validator that walks the runtime's tags according to the declared shape. The return type is `Result<UserProfile, DeserializationError>`, not a raw record. Missing required field? `Error` with path information. Wrong type for a field? `Error`. `null` or `undefined` for an `Option<T>` field? `None`. The programmer pattern-matches on `Result`; the validator is compiler-generated code derived from the type annotation. Rust's `serde` plus `serde-wasm-bindgen` is the production-proven version of this exact pattern; Clef's schema-directed narrowing is the same mechanism generated by the compiler rather than declared via derive macros.

### The Academic Twist: JavaScript's Tagged-Structure Heritage

The reason this works at all is historical and worth naming. JavaScript was designed in 1995 with Scheme as one of Brendan Eich's explicit references. JavaScript's `Object`, in its essential structure, is a direct descendant of LISP's association lists and hash tables: a tagged associative structure where keys are observable at runtime and values carry their own type tags. V8's hidden classes, which specialize access for stable-shape objects, are the modern rediscovery of Common Lisp's `DEFSTRUCT` with declared slots. The same pattern, different vocabulary, forty years apart.

Clef's schema-directed narrowing is consequently not a new idea; it is the technique LISP-family languages with static-typed refinements (Typed Racket most clearly) have been refining for two decades. The tags that JavaScript carries at runtime — property names and `typeof` results — are exactly the handles Clef's compiler-generated validators need. If JavaScript were genuinely untyped at runtime, the approach would not work. Because JavaScript's `Object` is structurally a tagged associative value in the LISP tradition, the mature LISP-family techniques for narrowing dynamic tagged data to static records transfer directly.

This is the shape of the erasure argument: Clef has no obj in the .NET sense (no universal root, no implicit boxing, no runtime reflection over arbitrary values) anywhere in the language. Clef has obj in the LISP sense only at the JavaScript/WASM interop boundary, through specific typed constructs (`JsValue`, `JsRef<'T>`, schema-directed narrowing). Everywhere else — the FFI boundary with C, the wire boundary with BAREWire, every native target — the type system is closed. The fun twist on a real-world production system is that the runtime is actually able to carry the freight: V8's tag system is what makes the narrowing work.

### Boundary 2: Lowering Fidelity

JSIR's formality is real. It is an MLIR dialect with ODS-defined ops, region semantics, and SSA values. A `jsir.call_expression` has operands and results with defined structure. The MLIR pass infrastructure verifies that these ops are well-formed. Within the MLIR pipeline, the JavaScript representation is as formally rigorous as any other dialect.

The formality ends at emission. JSIR lifts to readable JavaScript source. That source is syntactically faithful to the IR, with round-trip fidelity above 99.9%, but the fidelity is syntactic, not semantic. If a lowering pass emits the wrong byte offset for a BAREWire payload field, the emitted JavaScript will faithfully reproduce that error. The MLIR pipeline can verify that the call structure is correct, but it cannot verify that the emitted `DataView.setUint8(offset, value)` call uses the right offset. That correctness depends on the lowering pass, which is C++ code in the MLIR infrastructure, trusted by convention and testing, not verified by construction.

This is the same boundary every compiler exhibits. LLVM verifies IR properties and emits machine code that depends on the CPU honoring its ISA. CIRCT verifies hardware descriptions and emits Verilog that depends on Vivado synthesizing correctly. The question is never whether the entire stack is verified. It is where the verified region ends.

For Clef targeting JSIR, the verified region ends at JSIR emission. The trust chain is:

```
Clef types ──── decidable (Z^n, polynomial, principal)
BAREWire ops ── structural (same MLIR ops, both targets)
JSIR ops ────── well-formed (ODS-defined, pass-verified)

────── emission boundary ──────

JavaScript ──── syntactically faithful, semantically dependent
V8 isolate ──── opaque
workerd ──────── opaque
```

Each link is weaker than the one before it. The formality degrades gracefully from decidable verification to contractual assumption. This degradation is not a flaw; it is the universal structure of compilation. What matters is that the degradation is explicit and that each boundary is identified.

### Boundary 3: Runtime Contract

The emitted JavaScript calls Cloudflare's runtime APIs: `fetch()`, `WebSocket.send()`, `DurableObjectState.storage.put()`. These APIs are defined by Cloudflare's documentation, implemented in workerd's C++ runtime, and versioned with compatibility dates. The MLIR pipeline can verify that the call is structurally correct (right number of arguments, right SSA value flow) but it cannot verify that `storage.put()` persists the value or that the V8 event loop schedules handlers in the order the actor model assumes.

This is an external contract, taken on faith, the same way LLVM-emitted code takes the x86 ISA on faith. Cloudflare mitigates it with compatibility dates (no silent behavioral changes), the open-source workerd runtime (the implementation is inspectable), and the Workers runtime test suite. These are not formal guarantees. They are engineering practice.

## Schema Identity as a Proxy for Dimensional Agreement

Dimensional types are erased through compilation. This is by design. The DTS paper specifies that dimensional annotations persist through multi-stage lowering as PSG codata, inform representation selection and memory placement, and are then consumed. At Stage 5, dimensions are lowered to debug metadata. They do not affect operational semantics. A dimensioned and undimensioned compilation of the same program produces identical instructions. This property holds for every target, including JavaScript via JSIR.

But the message boundary introduces a subtlety that pure compilation does not. When an actor sends a BAREWire frame, the frame's schema (which fields exist, in what order, with what encoding) was derived from a discriminated union whose fields carried dimensional annotations. Consider:

```fsharp
type TorqueInput =
    | ComputeTorque of force: float<newtons> * distance: float<meters>
```

The Clef compiler verifies during elaboration that `force * distance` produces `float<newton-meters>`, not `float<newtons * seconds>` or any other dimensionally inconsistent result. The BAREWire schema is then derived from this verified type: tag 0, payload is two float64 values in field order. The dimensional annotations are not present in the schema. There is no "this field is in newtons" metadata in the wire format. But the schema's *structure* is a consequence of the dimensional verification having passed.

Now consider what happens if the sender's type definition were different:

```fsharp
type TorqueInput =
    | ComputeTorque of force: float<newtons> * time: float<seconds>
```

The compiler rejects this because the dimensional types are inconsistent. But set that aside and consider the structural consequence: a different type definition produces a different BAREWire schema. Different schemas produce different tags. Different tags are rejected at the transport level, before the receiving actor's `Handle` method is ever called.

The enforcement is indirect but real. BAREWire does not check dimensions. It checks schema identity: the tag matches, the payload layout matches, the frame is well-formed. But schema identity is causally downstream of dimensional verification. If the dimensional structure of the source type changes, the schema changes. If the schema changes, the tag changes. If the tag changes, the receiver rejects the frame. Dimensional disagreement between sender and receiver surfaces as schema disagreement, which surfaces as tag mismatch, which surfaces as transport-level rejection.

This is a property that silent erasure through compilation does not provide on its own. In F#, dimensions are erased and the runtime has no mechanism, not even an indirect one, to detect that a sender and receiver disagree about what the fields mean semantically. Two `float` values arrive; the receiver trusts that the first is force and the second is distance. If the sender's code was refactored and the field order changed, or if the sender's type was modified to pass time instead of distance, the F# runtime accepts the message and the computation proceeds with wrong inputs.

BAREWire closes this gap. Not by carrying dimensional metadata, but by carrying schema structure that is a projection of a dimensionally verified type. Schema identity serves as a proxy for dimensional agreement. The proxy is imperfect. Two dimensionally distinct types could in principle produce structurally identical schemas (same number of fields, same encodings, different dimensional meaning). But in practice, dimensional changes alter field types, field counts, or case structure, and these changes propagate to the schema. The proxy is not a formal guarantee. It is a structural consequence that holds for the common case and fails loudly in the uncommon case.

## Representation Fidelity Across Substrates

Dimensional erasure is not the concern. The concern is representation selection under target constraint.

The DTS compilation chain is: dimension determines range, range determines representation, representation determines width, width determines footprint, footprint and escape classification determine allocation. Each step consumes the output of the preceding inference. On native targets, the representation selection step is the payoff. The compiler evaluates worst-case relative error across the dimensional range and selects the representation that minimizes it. A value with dimensional range `[1e-3, 1e6]` might be selected as `posit<32,2>` on an FPGA target, where tapered precision concentrates bits in the primary operating range, or `float64` on x86, where uniform precision is adequate.

On JavaScript via JSIR, the representation selection step runs and produces a trivial result. JavaScript's `Number` type is IEEE 754 float64. There is no posit arithmetic, no fixed-point, no tapered precision. The dimensional range analysis executes, and the answer is always the same. The representation selection machinery fires and has nothing to decide.

This creates a concrete concern in a heterogeneous actor network. A native actor computing torque with `posit<32,2>` and an edge actor computing the same torque with `float64` perform the same arithmetic over the same dimensional types but produce results that diverge at the bit level. Not wrong results, since both are within their respective precision envelopes, but different results. If a Prospero supervisor compares outputs from native and edge Oliviers, the comparison fails at the bit level even though both computations are dimensionally correct.

The BAREWire wire format is not the source of this divergence. BAREWire faithfully serializes the sender's computed value, every bit preserved, and faithfully delivers it to the receiver. The divergence originates in the arithmetic itself, because the two substrates performed the arithmetic with different numeric representations. The wire format carries the *result* of a computation, not the *precision characteristics* of the computation that produced it.

A second concern is more subtle. The decidable-by-construction paper identifies a specific failure mode of IEEE 754 arithmetic in geometric algebra computations: "IEEE 754 rounding can corrupt structural zeros across training steps. A component that is algebraically zero in grade-1 acquires a numerically non-zero value through accumulated rounding error." The b-posit quire provides exact accumulation that prevents this drift. JavaScript has no quire. An edge actor performing iterated Clifford algebra operations accumulates IEEE 754 rounding errors that a native actor with quire accumulation does not. Over many iterations, the native actor preserves grade structure. The edge actor drifts. Both actors are dimensionally correct. The dimensional types are identical. The numeric behavior is not.

The right design response is not to disallow dimensional types on edge targets. That would discard the dimensional verification, which is valuable regardless of representation constraints. Nor is it to warn generically about "type erasure reducing efficacy." The erasure is working as designed, and the warning would be imprecise about what is actually lost. The right response is a representation fidelity diagnostic that surfaces when an actor deploys cross-substrate:

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

This diagnostic is a natural extension of the representation selection display described in the DTS paper: the same information, shown comparatively across targets, surfaced at design time through the language server. The developer sees, before deployment, where precision will diverge and by how much. The decision to deploy a computation to the edge is then informed, not blind.

For computations that require grade preservation or exact accumulation, where posit arithmetic and quire accumulators provide properties that IEEE 754 cannot, the diagnostic can be stronger: an explicit note that the edge target does not support the numeric properties that the native target provides. This is not an error. It is a design-time constraint that the developer should see and decide about, the same way the DTS language server surfaces escape promotions and allocation strategy changes. The compiler provides information. The developer makes the architectural decision.

## What This Means for Cross-Substrate Actors

The endgame for Fidelity.CloudEdge is a hybrid actor network where one stratum runs as native processes and the other runs as Cloudflare Workers, with BAREWire contracts governing communication between them. A Prospero supervisor running on bare metal supervises Olivier workers running at the edge, and vice versa. The message protocol is byte-identical across substrates because both serializers derive from the same Clef discriminated union definition.

In the current architecture, where F# compiles to JavaScript via Fable and to native code via .NET or Fidelity, the byte-identical guarantee depends on two separate compilers agreeing on the BAREWire wire format. This is verified by testing. If the Fable-compiled serializer and the native serializer produce different byte layouts for the same discriminated union, the mismatch is discovered at runtime, in production, under load.

With JSIR in Alex, both serializers are lowered from the same BAREWire dialect ops in the shared MLIR middle-end. The pipeline forks after the schema is derived, not before. The native path lowers BAREWire ops to memory-mapped struct writes via LLVM. The Worker path lowers the same BAREWire ops to `DataView`/`ArrayBuffer` operations emitted as JSIR ops. The byte layout is identical because both lowering paths consumed the same IR representation. Cross-substrate compatibility is a structural property of the compiler, not a property verified by testing.

This is where JSIR delivers its most significant value for Fidelity. Not in making the JavaScript "trustworthy." The JavaScript is still untyped, still garbage-collected, still running in a runtime whose behavior is an external contract. The value is in making the compilation pipeline *unified*. One source, one middle-end, one set of verification passes, two backends. The JavaScript happens to be the output format for one of those backends. The trust lives in the pipeline, not in the artifact.

## The Precedents

JSIR does not arrive in isolation. It follows a pattern that MLIR has been establishing for several years.

**EmitC** is an MLIR dialect already upstream in MLIR core, designed for lowering MLIR to C source code. It established that source language emission is a valid MLIR use case. JSIR is structurally the same pattern applied to JavaScript.

**WAMI** (WebAssembly through MLIR) demonstrated compilation to WebAssembly through MLIR dialects without going through LLVM IR. Their paper explicitly mentions future integration with a JavaScript MLIR dialect. The research community anticipated this direction.

**The 2022 emitjs RFC** proposed an `emitjs` dialect modeled on EmitC, with the pipeline: ONNX model / C / DSL to MLIR dialects to MLIR js-Dialect to JavaScript. The community asked for prototypes. Four years later, JSIR delivers from the opposite direction: analysis-first, but with full round-trip capability enabling the emission use case.

**js_of_ocaml** is the closer architectural precedent and deserves more than one sentence. Jérôme Vouillon and Vincent Balat built js_of_ocaml starting in 2010 at PPS/CNRS Paris Diderot. The design commits to compiling from OCaml bytecode (the output of `ocamlc`) rather than from source AST: bytecode is lifted into an internal SSA-style IR (`Code.program`), optimization passes run over that IR (dead code elimination, tail-call optimization, flow analysis, effect-handler CPS transformation, closure generation), and JavaScript is produced through a conventional compiler back end. The commitment is, in the project's own words, that "the bytecode provides a very stable API" — making the compiler easier to maintain than one that retargeted OCaml's source-level AST.

In 2024 the same project shipped `wasm_of_ocaml`, a WebAssembly backend. The critical detail: `wasm_of_ocaml` shares `Code.program` with `js_of_ocaml`. Both backends sit atop the same IR and consume the same upstream optimization passes. This is the multi-target-from-shared-IR pattern in miniature — one frontend, one middle-end, two backends — and it is structurally what Composer generalizes over MLIR's dialect infrastructure to reach four backends (LLVM, CIRCT, MLIR-AIE, JSIR) from one Clef source. The architecture Composer is adopting is not novel in concept; it is the pattern js_of_ocaml has been proving at production scale for fifteen years, adapted to MLIR's substrate and extended to more targets.

**Fable** took a different tactical route than js_of_ocaml, and the reason is architectural rather than preferential. Fable was built for F#, and F#'s post-compile representation is .NET CLR IL — an IR designed around C#'s semantics (reified generics, sealed class hierarchies, boxing rules, structural variance). By the time F# source has been lowered to IL, F# idioms (pipes, curried functions, discriminated unions as sealed types, pattern matching expanded into cascading type tests) have been translated into CLR-shaped constructs. A hypothetical IL-to-JavaScript compiler would be compiling the CLR, not F#. The F# source AST is the last representation where F# idioms are still visible as F# idioms, which is why Fable walks the source AST — not because AST-walking was preferred over IR-walking, but because F# has no analogue to the stable, ML-preserving bytecode that OCaml provides. Fable remains the right answer for F# for this reason, and it is not going anywhere. Composer's JSIR path is the js_of_ocaml-shaped approach applied to Clef, which, like OCaml, owns its post-frontend representation end-to-end.

The transformations that both js_of_ocaml and Fable perform — pattern matching to switch/if chains, algebraic data types to object construction, tail calls, currying — are thoroughly characterized. These serve as direct blueprints for MLIR lowering passes that target JSIR.

## What Does Not Change

JSIR affects the compilation pipeline. It does not affect the actor model's design, the management API surface, or the deployment infrastructure.

Fable continues as the F#-to-JavaScript path for everything already built on it: Partas.Solid frontends, WrenHello's Fable-rendered WebView layer, the F# Worker bindings in Fidelity.CloudEdge. Two coexisting models reach JavaScript through different routes — the F#/.NET path through Fable (forced to source-AST walking because the CLR's IL erases F# idioms before any IR stage could recover them) and the Clef path through Composer and JSIR (free to use a compiler IR because Clef owns its post-frontend representation end-to-end). Neither replaces the other; each serves the source language it was built for.

Related to this, Fidelity.CloudEdge itself will likely transition to **FSharp.CloudEdge** at some point, distinguishing itself as a direct F# implementation for the F#/.NET ecosystem. The current naming conflates the F# binding layer with the broader Fidelity framework; the rename better reflects what the package actually is — the F# community's Cloudflare SDK, independent of what Clef and Composer are doing. Details on timing and community maintenance (likely through fsprojects) will be worked out over time. The technical content of this document is unaffected by the rename.

The Fidelity.CloudEdge management layer (currently 40 Management-tier REST clients and 2 Tenancy-tier clients, generated from Cloudflare's OpenAPI specification, with runtime types covering Workers, Durable Objects, Containers, and Facets) runs externally, not inside Workers. It provisions Queues, creates D1 databases, configures tunnels, deploys Worker scripts. Whether the Worker code was compiled by Fable or by Composer via JSIR is invisible to the management layer. The deployment pipeline uploads JavaScript either way.

The actor model's semantic design (Olivier workers, Prospero supervisors, WebSocket transport, BAREWire serialization, elastic scaling with Queue pivot, event-sourced persistence) is unchanged. These are runtime patterns, not compilation patterns. They exist at the Clef source level and in the Cloudflare runtime contract. JSIR changes how the compiler produces the JavaScript that implements them. It does not change what they are.

Firetower, the monitoring tool, is similarly unaffected. It consumes management APIs and runtime WebSocket data. Its own compilation path (Avalonia for desktop, Fable for web) is independent of how Worker code is generated.

## Practical Next Steps

JSIR is available today as an out-of-tree MLIR dialect. The RFC was posted on April 6, 2026. Upstream status is pending; inclusion alongside dialects like WasmSSA is proposed but not yet decided. This does not block evaluation.

**Immediate.** Clone `google/jsir`, build via Docker, feed it JavaScript resembling Fable output for a Cloudflare Worker: a `fetch` handler with `async`/`await` and `Response` construction. Inspect the MLIR. This validates whether the op set maps naturally to Worker-shaped JavaScript.

**Short-term.** Extract the TableGen dialect definitions and integrate them into the Composer MLIR build. The dialect is build-system agnostic; the Bazel dependency is for Google's parser tooling, not for the dialect itself. Write a single lowering pass from an Alex dialect op to JSIR ops (a let-binding or function definition) and walk the resulting JSIR to emit JavaScript.

**Medium-term.** Use the existing Fidelity.CloudEdge patterns, every Worker instrumentation pattern already built in F# and compiled by Fable, as lowering pass specifications. The Fable-compiled JavaScript is the expected output. The MLIR pipeline is the mechanism that produces it. Every Worker that runs today validates what the compiler must produce tomorrow.

## The Adjacent Capability: WASM and Stack Switching

JavaScript is not the only target that benefits from the unified middle-end. Composer's MLIR pipeline also reaches WebAssembly — through two pathways that parallel the situation JSIR creates for JavaScript. The **LLVM WASM pathway** lowers Alex's dialects through the conventional MLIR-to-LLVM IR path and emits WASM via LLVM's WASM backend; this is production-ready today for any runtime that supports standard WASM. The **WAMI pathway** (defined in the "WebAssembly through MLIR" research project) stays within MLIR's infrastructure end-to-end, using `SsaWasm` and `Wasm` dialects to emit WASM directly without touching LLVM; this is horizon-2 engineering, but it is the pathway that preserves delimited-continuation semantics through to WASM's stack-switching primitives rather than reifying them into state machines.

The WebAssembly Stack Switching proposal — Phase 2 as of August 2024, advancing toward Phase 3, with Wasmtime shipping a production-grade implementation showing 6x micro-benchmark improvements over CPS-transformation fallbacks — enumerates its own use cases as *coroutines, async/await, generators, lightweight threads, and other advanced non-local control flow idioms*. That list is Fidelity's DCont unification almost verbatim. The WebAssembly community and Fidelity are converging on the same abstraction from different directions, and once Stack Switching reaches broad availability, delimited continuations become a runtime-native capability everywhere WASM runs.

For Cloudflare specifically, WASM sits in Tier 2 of a step-graded compute model that spans pure JavaScript Workers (Tier 1), WASM-in-Workers (Tier 2), and Containers (Tier 3). WASM is not a separate deployable unit on Cloudflare — it is a library bundled inside a Worker's deployment, instantiated by a JavaScript shim that handles the runtime API binding. The Cloudflare Workers runtime supports WebAssembly SIMD (enabling data parallelism within the single-threaded isolate), JSPI (a shipping subset of Stack Switching that allows suspension at Promise boundaries), and mature tooling via `workers-rs` for Rust Workers that demonstrate the pattern. Clef WASM deployments adopt the same pattern: the LLVM or WAMI pathway emits a `.wasm` module; a Fidelity bundler generates the JS shim that instantiates it and exposes Cloudflare bindings as imports; `cfs` uploads the bundle through the Management API.

The point for this document: JSIR solves the JavaScript path. It does not solve the WASM path, but the WASM path uses the same Alex middle-end, the same verification passes, the same BAREWire schema derivation. The schema identity argument and the type-erasure boundary analysis in this document apply to WASM targets with the same structure, modified only by WASM's different runtime model (typed linear memory rather than tagged JavaScript objects; BAREWire codecs implemented through `memref` ops rather than `DataView` ops; Component Model WIT types for cross-module boundaries rather than JavaScript's property-bag convention). The technical content here generalizes; a forthcoming companion document will cover the WASM-specific build, bundling, and execution-model details, including the unikernel-style execution pattern that the DCont scheduler enables within a single WASM instance and the fan-out model that coordinates multiple instances via BAREWire-framed messages when compute demands exceed a single-core ceiling.

## Honest Framing

JSIR is not "JavaScript you can trust." It is not a type system for JavaScript. It is not a formal verification framework for browser code. It is an MLIR dialect that represents JavaScript's AST with high fidelity, built by core MLIR engineers, tested at production scale inside Google.

What it provides for Clef is architectural unification. The JavaScript target joins the same pipeline as every other target. The verification properties that hold in the shared middle-end (dimensional consistency, BAREWire schema agreement, escape classification, concurrency primitive validation) apply to the JavaScript path because the JavaScript path goes through the same middle-end. Those properties do not survive emission. The JavaScript that comes out is still JavaScript: dynamically typed, garbage-collected, dependent on Cloudflare's runtime contract. But the pipeline that produced it is the same pipeline that produces native binaries. And BAREWire's binary protocol carries the type structure through the erasure boundary at runtime, substituting for the type system that JavaScript lacks.

The result is not JavaScript you can trust. It is JavaScript that won't keep you awake at night, because the things that would keep you awake (message type mismatches, cross-substrate serialization drift, verification gaps between native and edge code) are addressed in the compiler, before any JavaScript is emitted. The rest is Cloudflare's contract, and they've been keeping that contract for millions of Workers in production.

For a compiler designed to target CPUs, GPUs, FPGAs, and spatial accelerators through MLIR, the idea that JavaScript is just another backend, reached through the same dialect infrastructure, subject to the same pass pipeline, verified by the same middle-end, is a quiet revolution. Not because JavaScript becomes trustworthy. Because the compiler doesn't have to care that it isn't.
