---
title: "Design-Time Specification for Runtime Reliability"
linkTitle: "Design-Time Spec"
description: "Current BAREWire checks and the preservation obligations for a proposed Composer JavaScript backend."
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Type Systems", "Design"]
weight: 20
---

## What the Middle-End Can Carry

The [JSIR architecture](../jsir-javascript-as-mlir-backend/) proposes a common place to carry source facts toward native and JavaScript operations. A dimension, range or lifetime established in the PSG is useful only if the chosen lowering preserves it. This page explains that work, while keeping the working Fable/BAREWire implementation distinct from Composer's proposed JavaScript profile.

The normative rule is [Conformance §6](/spec/draft/conformance/#6-the-preservation-obligation-through-lowering): preserve a required property or re-check it at an affected lowering edge. The converse matters too: an emitted constant, buffer extent or behavior must have a declared origin in source, specification or platform facts. Shared IR is a convenient place to enforce those rules, not their proof.

## Dimensional Consistency

Dimensional relations can be solved before runtime, under the supported language and algebra. Their facts may come from an expression, a checked domain law or a boundary declaration. They remain available in PSG/codata for dependent reasoning, including numeric representation selection. A dimensionally valid expression does not by itself establish a numeric range, a lifetime or a message's origin.

Each proof obligation needs an appropriate decision procedure. Dimensional algebra, integer range arithmetic, bit-vector operations and effect constraints are not one universal polynomial-time SMT problem. A supported decidable fragment makes a question well-defined; it does not promise every query finishes within an interactive budget. Missing facts and solver timeouts stay unresolved, rather than becoming successful proofs.

Erasure is the final economy, not an early shortcut. Metadata stays available through the lowering edges that need it, then can be consumed or retained only as diagnostic evidence. The emitted payload need not carry dimensions. Whether two programs have identical instructions depends on their representation and operation choices; dimensional reasoning can inform those choices before the metadata disappears.

## Memory and Escape Classification

Escape and lifetime facts can guide native allocation and justify particular accesses. Eliminating a check or choosing stack storage requires the premises for that operation and the relevant target lowering. An analysis name alone does not establish that every allocation is safe or that an entire binary needs no memory management.

On JavaScript, the host manages ordinary objects. A native record's padding, alignment and byte size are not an object-layout contract for V8. The JavaScript backend accesses record properties structurally. Explicit byte storage, such as an `ArrayBuffer` view, has a separate extent and lifetime contract; sharing, mutation and detachment can affect that contract even in a garbage-collected runtime.

The useful common information is about what a computation needs, which captures it uses and when a fact remains valid. A target may realize that information differently. A native allocation decision cannot simply be relabeled as a JavaScript guarantee.

## BAREWire Across Memory, IPC and Network

BAREWire is the glue for these three roles. A declaration can guide a native layout, a shared-memory view or a network codec, with the applicable ABI, ownership and transport facts made explicit. These roles share reasoning material; they do not imply that a native structure is already a wire encoding or that an ordinary JavaScript object has a native footprint.

The final payload is untagged with respect to its types, schema, dimensions and proofs. Endpoint agreement supplies that interpretation. Union case indices and optional-value presence bits select alternatives inside the agreed contract; they do not identify its version or semantic meaning. Two incompatible contracts can assign different meanings to identical bytes, so deployment or session agreement has work to do outside the payload.

A common Composer codec derivation would choose field order and encoding once. Native and JavaScript operations would still need to implement that choice faithfully, including bounds and numeric conversions. The current BAREWire implementation already checks codec and framing behavior in JavaScript after Fable compilation, using exact vectors, round trips and selected rejection cases, including an out-of-bounds read and a truncated frame. These tests remain useful when another backend arrives.

The current proof inventory (`BAREWire/docs/12 Intersection Subset.md`, §5.1) also records JavaScript-generated SMT queries over declared platform facts. Those queries establish their modeled predicates. They are not a semantic proof of the JavaScript generator, codec or emitted application. The [worked example](../jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) identifies the additional operation and artifact correspondence required.

## Representation Fidelity

The joint analysis connects dimension, range, representation, width, footprint and allocation. Pending facts can survive elaboration; a concrete representation may be committed only when its required facts are established. The source need not name a width or attach a seal to each operation.

The JavaScript profile realizes ordinary reals as binary64 and respects the integer policy of [Width Inference §8](/spec/draft/width-inference/#8-target-lowering). Wider exact integers need a documented realization, such as `BigInt` or emulation, rather than silent truncation to a host number. Additional numeric capabilities depend on the target's declared implementation and emulation policy.

[Numeric Selection §10](/spec/draft/numeric-selection/#10-the-preservation-chain-and-the-quire-pass) requires transfer fidelity to be directional. Exact wire bytes preserve the selected encoding; they do not undo a lossy conversion made before encoding. Exact value transfer requires range coverage and representability at the destination. A lossy transfer needs a justified bound and an admissible boundary contract.

A quire can make accumulation exact for the admitted products and partial sums, provided both fit its finite representation. Input errors, surrounding arithmetic and final conversion still matter. A missing required capability or an empty coverage set is a diagnostic error, not a recommendation to use weaker arithmetic. The IDE can compare admissible choices and their costs without inventing quantitative accuracy claims or silently relaxing the requirement.

## From a Proof Result to a Build Result

A solver result needs the obligation it answered, the assumptions it used and the model in which it answered. Connecting that result to a build adds the target operation, relevant lowering edges and the emitted artifact. A source hash or successful MLIR verification binds neither numeric meaning nor observable behavior on its own.

The proposed [acceptance sequence](../jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) starts with one operation family, preserves or re-checks its obligations, tests its runtime behavior and records the exact tools, dependencies and outputs. This is a plan for the Composer path. The current JavaScript tests do not issue an artifact-bound semantic proof certificate, and the [JavaScript Substrate chapter](/spec/draft/javascript-boundary/) explicitly records that no conforming implementation exists yet.

## The Runtime Boundary

The specified boundary is productive work for generated code. Inbound objects and callback arguments are narrowed before entering the declared Clef shape. Host throws and awaited promise rejections become typed errors. Absence is converted according to the position's contract; interior `Option` representation has its own preservation obligation. These are requirements for the proposed profile, not a claim about every existing F# binding.

Byte decoding also retains checks for malformed, truncated or oversized inputs. An accepted case index is not evidence that every following byte exists. A valid payload is not evidence of peer authorization, session order, delivery or persistence. Each claim belongs to the contract that can establish it.

A dependency's TypeScript signature describes an interface. It cannot establish purity, storage durability or future callback behavior by itself. The compiler needs a justified implementation summary where possible and explicit external assumptions elsewhere. The host and any shipped SDK code remain part of the execution story even when the application has no package-manager directory at runtime.

## A Useful Result for the Developer

The goal is less work repeated by hand: one declaration informing a codec, one boundary policy generating its narrowing, one obligation traced through the operation that implements it. Some checks can disappear when their premises and preservation are established. Others belong at the boundary where a value first becomes known.

That combination lets BAREWire stay economical at final lowering while the compiler remains informed beforehand. JSIR offers a place to extend the discipline to JavaScript. The next step is to earn it one supported operation at a time, with the current tests and the specification both serving as concrete guides.

## UI Toolchain Preservation

For a Solid/WREN profile, [JSX generation](../javascript-jsx-toolchain/) is followed by Solid’s Babel-based transformation, bundling and native embedding. Preserve or re-check affected properties through those steps and bind evidence to the final assets, shipped dependencies and selected WebView. A successful JSX parse or source map does not establish reactive tracking, event multiplicity or cleanup behavior. The same proof-preservation principle applies; this is an additional realization path, not a replacement for the actor and numeric obligations above.
