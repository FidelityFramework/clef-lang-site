---
title: "Fully Informed Bindings"
linkTitle: "Fully Informed Bindings"
description: "The binding factory for the Cloudflare surface: a declared shape from TypeScript analysis, a measured body from the JSIR lift, and the join that makes a Clef binding derive from what a library is, never only from what its declarations admit."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Architecture", "Interop", "JavaScript", "Design"]
weight: 35
---

The destination for our Cloudflare work is a Clef source library covering the platform's full surface, Workers, agents, durable workflows, and the control plane behind them, with an API a Fidelity.CloudEdge developer recognizes on sight. The [back-end transition](/docs/design/javascript-targeting/from-fable-to-jsir/) describes how the emission side of that library reaches JavaScript through Composer and JSIR. This page describes the ingestion side: how the SDK surface becomes Clef bindings, and what it takes for those bindings to be fully informed, deriving from what the library is and does.

## Three Categories of Surface

"The SDK surface" covers three categories of artifact, and the binding mechanics differ for each.

The runtime API surface, `@cloudflare/workers-types` and the declaration packages around it, is pure declaration. No JavaScript ships, because the implementation is the workerd runtime itself, and the declarations are machine-derived from its source. Binding is the only relationship this category admits, through [Xantham's](/docs/design/interop/typescript-binding-via-xantham/) structural analysis.

The wrapper SDKs, the agents SDK, workflow helper classes, harness code, are TypeScript compiled to JavaScript that genuinely ships in a Worker's bundle: class hierarchies, RPC plumbing, scheduling helpers. Artifacts in this category carry behavior worth measuring and, in bounded cases, worth absorbing.

The control plane is machine-generated from Cloudflare's OpenAPI specification, and the specification is public. Its TypeScript client is a projection of that schema, so the principled Clef client generates from the schema directly, the path our OpenAPI ingestion already takes for backplane provisioning. Measuring or absorbing the projection would study a shadow whose source is published.

The rest of this page concerns the second category, a minority of the surface by volume and a majority of it by runtime behavior, release cadence, and drift risk.

## The Join

A TypeScript declaration and the JavaScript compiled from the same source are two projections of one artifact. Xantham reads the first projection and yields the declared shape: signatures, generics, structural types, the surface a consumer is invited to trust. The [JSIR](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) forward pipeline reads the second and yields the operational body: the actual control flow, captures, and runtime API reach of each function, lifted into JSHIR where MLIR's dataflow machinery can see it. JSIR carries no types, by its own design boundary, and the declaration carries no behavior. Joined by symbol, the two projections give the binding generator what neither analysis holds alone: a shape that is claimed and a body that is observed, per function, regenerated together on each SDK release.

That join is what "fully informed" means here. The backward direction of JSIR serves as the measurement plane of the binding factory, never as a decompiler. Raising untyped JavaScript into a typed ML is the hard direction of the lifting problem, and nothing below depends on solving it, because the types this JavaScript erased ship in the same package.

## Four Instruments

The join yields four instruments, in ascending ambition.

**Effect harvesting.** Dataflow over the lifted body computes what each SDK function reaches: network, storage, timers, the isolate's own state, and whether its result is a function of its inputs. That output is the residue classification the [ledger lowering](/docs/design/javascript-targeting/the-ledger-lowering/) depends on, measured from the SDK's own code and regenerated with it, in place of a hand-asserted table that rots. Capability reach over dynamic dispatch is a sound over-approximation, which errs in the direction of journaling more than strictly necessary.

**Conformance in IR space.** The transition document establishes Fable's deterministic output as the executable specification for what the JSIR pipeline must produce. The comparison instrument lives here: lift the Fable-emitted JavaScript and the Composer-emitted JavaScript through the same forward pipeline, normalize, and compare in JSHIR. Equivalence judged on the IR survives the formatting, naming, and pass-ordering differences that make textual comparison brittle, so witnessing-rule characterization becomes a harness run instead of eyeball work.

**The disagreement channel.** Farscape's boundary taught that a C header under-declares, and the TypeScript boundary has the complementary vice: it over-declares, with rich types that are claims without enforcement. When the lifted body of an SDK function reaches `fetch` behind a signature that reads pure, or touches storage its types never mention, that divergence is a first-class finding. Regenerated per release, the channel is a lint on the vendor, and on a pre-1.0 surface that changes monthly it may be the single most valuable output of the pass, catching behavioral drift under stable signatures where a `.d.ts` diff sees nothing.

**The absorption workbench.** The [JSIR back-end document](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#jsirs-design) registers whole-library absorption as conceptually coherent and declines it as work, and that verdict stands. What the join makes practical is the bounded form: small, stable, protocol-like fragments, an RPC envelope encoding, WebSocket framing, Durable Object lifecycle glue, lifted to JSHIR with their declared types joined from Xantham, raised with mechanical assistance into candidate Clef, and finished by a person. A graduated fragment is verified by round trip: the absorbed Clef compiles back through JSIR, and the conformance instrument checks the emitted JavaScript equivalent to the original in JSHIR space. The harness that characterizes witnessing rules also verifies each absorbed fragment, so absorption inherits the conformance instrument instead of needing verification machinery of its own. Each graduation retires a piece of shipped third-party JavaScript, concentrated first where BAREWire intends to own the framing anyway.

## One Discipline, Two Boundaries

The pattern above is the second instantiation of a discipline [Farscape](/docs/design/interop/library-binding/) established at the C boundary. A header's failure mode is omission, so Farscape's Pilot configuration drives clang passes past the declaration into the code, and the binding derives from what the library is. The TypeScript surface's failure mode is unenforced richness, so the measurement pass verifies claims and harvests what the declaration has no vocabulary for. One abstract shape covers both: a declared surface, a measured body, a configured curation, and matched declaration-plus-witness pairs coming out, with clang as the instrument at one boundary and JSIR at the other, and the Library of Alexandria as the rule store both feed. Atelier's Transcribe layer, which already consumes Farscape and Xantham as per-language analysis substrates, is where the unified discipline would operate.

The curation piece deserves its Pilot analog on this side. A per-package configuration would name the entry points to lift, the effect-class overrides where analysis lands too conservative, the fragments nominated for the workbench, and the declared-versus-measured disagreements accepted as known. That file makes the lift reproducible per release, and it gives graduated absorption its paper trail: nomination, verification, and adoption recorded in one place.

## The Compatibility Contract

The API of the resulting Clef library is constrained from the F# side. Today's Fidelity.CloudEdge surface serves as the normative contract for the first cut: module organization, record and discriminated-union message types, and computation-expression shapes carry across as cognates. The deliberate differences appear where Clef's integrity machinery pulls a construct upward, and their locations are predictable. Dynamic and `obj`-shaped surfaces become schema-directed narrowing returning `Result`, with [The Foreign Pair](/docs/design/javascript-targeting/the-foreign-pair/) supplying the boundary types and their grade discipline. Promise-shaped APIs become DCont-backed. Each `[<Emit>]` becomes a witnessing rule. Each effectful call site carries its measured residue class. A developer ports application code by translation, and the places where code declines to port unchanged are, by design, the places where the F# version trusted something the Clef version proves.

## The Sequence

The first step carries no Composer dependency: `jsir_gen` exists today, so the lift harness can stand up now, running the agents SDK and one Fable-compiled Worker through the forward pipeline to confirm the op set holds Worker-shaped JavaScript, which is also the immediate step the JSIR back-end document commits to. The effect-harvest pass over the lifted SDK follows, producing the measured-metadata store keyed by symbol against Xantham's analysis. The Clef extern generator consumes both and emits matched declaration-plus-witness pairs, the third and fourth waypoints of the transition arc. The conformance harness then runs Fable output against Composer output in JSHIR space, one binding shape at a time. The workbench comes last and graduates fragments only as round trips pass. The spine of the factory stays TypeScript to Clef, the control plane stays OpenAPI to Clef, and the backward direction of JSIR is the instrument that keeps both honest.
