---
title: "From Fable to JSIR: The Back-End Transition for JavaScript Targeting"
linkTitle: "Fable to JSIR"
description: "How Composer's JavaScript-emission back-end transitions from Fable + npm bundlers to JSIR + MLIR, while the .NET-hosted compiler persists across the change"
date: 2026-05-04
authors: ["Houston Haynes"]
tags: ["Architecture", "Compilation", "JavaScript", "JSIR", "Design"]
---

[JSIR: JavaScript as an MLIR Backend](../jsir-javascript-as-mlir-backend/) establishes that Composer's JavaScript-targeting pipeline joins the same MLIR architecture as every other Clef target. [Design-Time Specification for Runtime Reliability](../design-time-spec-runtime-reliability/) covers what verification properties hold across that unified pipeline. This document covers the **transition arc**: how the back-end emission moves from Fable + npm bundlers (today) to JSIR + MLIR (eventually), while the rest of the toolchain — particularly the .NET-hosted Composer host — persists across the change.

The companion document [TypeScript Binding via Xantham](../../interop/typescript-binding-via-xantham/) covers the analysis-substrate side of the same transition. The two documents together describe the full pipeline arc: ingestion (Xantham, durable) and emission (Fable → JSIR, transitional).

## The Two-Path Reality

Two paths reach JavaScript from Composer's broader ecosystem. Both will continue to exist long-term. They serve different source languages.

**The Fable path** serves F# users. F# source compiles through Fable, walking F#'s source AST, producing JavaScript via string-template emission with `[<Emit>]` attributes for binding-specific shapes. Fable is a Node-resident tool with deep transitive npm dependencies (the Fable compiler itself, plus whatever bundler the user chooses, plus that bundler's plugin ecosystem). The path is mature, production-tested, and the right answer for F# code today and indefinitely.

**The JSIR path** serves Clef users. Clef source compiles through Composer's MLIR pipeline, with Alex (the middle-end) witnessing PSG nodes against verification rules and emitting JSIR ops that lower to JavaScript via JSIR's `hir2ast,ast2source` reverse pipeline. JSIR is an out-of-tree MLIR dialect from Google (RFC April 2026), with full Babel AST round-trip fidelity and production scale at Google for adjacent uses (Hermes bytecode decompilation, deobfuscation, malicious code analysis).

The transition this document describes is **for Clef code**: from "Clef source bypasses Alex and emits JS through a Fable-like Oak-like AST" (the original architectural plan, prior to JSIR's release) to "Clef source flows through the same Alex middle-end as native targets, with JSIR as the JS-emission back-end." Fable continues unchanged for F# users; the change is in Composer's own JS-emission strategy for Clef.

## What Fable Does Well, What It Structurally Constrains

Fable is the right answer for F# for a specific architectural reason. F#'s post-compile representation is .NET CLR IL — an IR designed around C#'s semantics (reified generics, sealed class hierarchies, boxing rules, structural variance). By the time F# source has been lowered to IL, F# idioms — pipes, curried functions, discriminated unions as sealed types, pattern matching expanded into cascading type tests — have been translated into CLR-shaped constructs. A hypothetical IL-to-JavaScript compiler would be compiling the CLR, not F#. The F# source AST is the last representation where F# idioms are still visible as F# idioms, which is why Fable walks the source AST.

This is not a preference; it is forced by F#'s representation pipeline. Fable's architectural choices follow from where F# lives: it has no analogue to the stable, ML-preserving bytecode that OCaml provides for `js_of_ocaml`. Fable cannot adopt the IR-walking approach without first solving the problem of preserving F# idioms through some lower-level IR — which is not a small undertaking and is not what Fable was built for.

What Fable does well in consequence:

- **Source-AST fidelity**. F# constructs survive as F# constructs into the JS emission step. Pipes lower to JS chains; pattern matching lowers to switch chains; discriminated unions lower to tagged objects. The transformations are direct because the source AST is the input.
- **Tactical escape via `[<Emit>]`**. When the JS shape doesn't have a clean F# analog, the developer writes a string template and Fable substitutes during compilation. This is a pragmatic accommodation that keeps Fable's architecture simple.
- **Ecosystem maturity**. Fable has been production-shipping for years. Partas.Solid, WrenHello's WebView layer, every existing F# Worker on Cloudflare via Fidelity.CloudEdge — these all run on Fable today. None of that needs to change.

What Fable structurally constrains for the *Clef* targeting case:

- **No shared middle-end**. Fable's pipeline is separate from Composer's MLIR pipeline. Verification passes written against Alex don't apply to Fable output. Dimensional consistency, escape classification, BAREWire schema derivation, and other middle-end properties either need to be reproduced in Fable's pipeline or accepted as not applying to Fable-compiled code.
- **String-template emission for bindings**. Each `[<Emit>]` is a per-binding decision, written by hand, untyped at the JS-shape level. Fine for F# bindings; not the right model for the durable witnessing rules we want for the broader Clef binding effort.
- **npm-resident**. Fable runs in Node. Bundlers that follow it (esbuild, vite, webpack, rollup) all run in Node with their own transitive dep trees. The npm supply-chain surface is large.

These constraints are not Fable's fault. They are consequences of Fable serving F# in F#'s representation environment. For Clef, which has its own post-frontend representation under Composer, the right architecture is different — and JSIR makes that different architecture available.

## What JSIR Brings

JSIR places JavaScript inside MLIR as a first-class dialect. The pipeline becomes:

```
Clef source ──▶ PSG ──▶ Alex (MLIR middle-end) ──▶ JSIR ──▶ JavaScript
                          │
                          └──▶ LLVM ──▶ native binary
                          └──▶ CIRCT ──▶ FPGA
                          └──▶ MLIR-AIE ──▶ spatial accelerator
```

JavaScript joins the same back-end fan-out as every other Clef target. The shared middle-end (Alex) carries dimensional annotations as PSG codata, escape classifications, BAREWire schema derivation, concurrency primitive validation, and other verification properties through to the point of JSIR emission. The JS path inherits whatever properties hold at the middle-end.

What JSIR specifically provides:

- **First-class witnessing rules**. Alex emits JSIR ops by characterized rules, not string templates. A binding declaration's elision is encoded as a witnessing rule that produces specific JSIR ops; the rule is verified by MLIR's pass infrastructure; the same rule applies uniformly across every call site for that binding shape. `[<Emit>]`'s per-binding escape-hatch becomes per-shape-category systematic emission.
- **Round-trip Babel AST fidelity**. JSIR maintains a nearly 1:1 mapping with Babel AST nodes, with 99.9%+ fidelity on round-trip conversion across billions of samples at Google. The JS that comes out parses identically to JS written by hand against the same shape.
- **Shared verification with native targets**. Properties verified in Alex apply to the JS path because the JS path goes through Alex. BAREWire schema derivation produces both the native serializer (via LLVM) and the JS deserializer (via JSIR) from the same IR representation. Cross-substrate compatibility becomes a structural property of the compiler rather than a testing concern.
- **No npm in the emission pipeline**. JSIR's emitter (`jsir_gen`) is a single binary. Composer invokes it as part of its MLIR back-end fan-out. No bundler chain, no plugin ecosystem, no transitive npm deps for the emission step. (Ingestion-side TypeScript Compiler API, used by Xantham, remains the only npm dependency in the binding-generation toolchain.)

The witnessing-rule model is the load-bearing change. It enables the Library of Alexandria — the curated catalog of TS-shape-to-JSIR-op witnessing rules — to grow as more libraries are bound, with each rule verified once and applied uniformly thereafter. Fable's `[<Emit>]` doesn't compose this way; each binding stands alone.

## Composer as .NET Host Across the Transition

Composer is a .NET-hosted compiler. JSIR integrates into Composer as an MLIR dialect Composer's back-end fan-out invokes. The .NET host persists across the transition described in this document. The transition is *within* the .NET-hosted Composer environment, not an exit from it.

Concretely:

| Component | Today | Mid-transition | Mature |
|:----------|:------|:---------------|:-------|
| Composer compiler host | .NET | .NET | .NET |
| Front-end (Clef parser, type system) | Composer (.NET) | Composer (.NET) | Composer (.NET) |
| Middle-end (PSG, Alex, MLIR pipeline) | Composer (.NET hosting MLIR via interop) | Same | Same |
| JS back-end | (Clef paths bypass Alex, emit Oak-like JS AST directly; or Fable for F# code) | Both: existing path remains; first JSIR lowering passes added | JSIR via MLIR; Oak-like bypass retired for Clef |
| F# JS back-end | Fable (Node, npm) | Fable (unchanged for F# users) | Fable (continues for F# users) |
| Bundlers | npm bundlers (esbuild/vite/webpack/rollup) | npm bundlers (for Fable output); Composer-emitted JS is bundle-ready | Composer emits self-contained or directly bundleable JS; npm bundlers used only for Fable F# output |

The .NET host is constant. NuGet's curated package ecosystem remains the dependency model for Composer itself. MLIR/LLVM tooling (C++) continues to be invoked from the .NET host through Composer's existing MLIR integration. The npm-resident pieces (Fable + bundlers) phase out for the Clef-emission path; they continue to serve F# users on the parallel Fable path.

This is the persistent shape for the planning horizon — months through end of year and beyond. Self-hosting Composer in Clef itself is a far-longer-term consideration that is **out of scope for this document.** The transition arc described here ends at "Composer is .NET-hosted, JSIR is the canonical Clef JS back-end, Fable continues for F# users."

## The Transition Arc as Waypoints

Five waypoints, each with a clear state, a clear what's-true, and a clear what's-next:

### Waypoint 1: Today (May 2026)

**State.** Composer ships with the original JS-emission plan (PSG bypasses Alex, emits via an Oak-like JS AST). Fable serves F# users via a separate pipeline. Fidelity.CloudEdge ships F# bindings produced by Xantham + Xantham.Generator, compiled to JS by Fable.

**What's true.** Production F# Worker code runs on Cloudflare today via this path. The Xantham analysis substrate is in active development; encoder and decoder are stabilizing toward 1.0 (see [TypeScript Binding via Xantham](../../interop/typescript-binding-via-xantham/) for the analysis-side details). Composer's JS-emission path for Clef code is not a focus of current investment.

**What's next.** JSIR's MLIR upstream merge progresses. The first JSIR-targeted lowering pass from Alex is prototyped. The deterministic Fable output produced by today's F# bindings becomes the **executable specification** for what the eventual JSIR pipeline must produce — every shipping binding seeds the witnessing-rule library for the future path.

### Waypoint 2: Stabilized current path

**State.** Xantham 1.0 published. Open issues (decoder MISSREF behavior, package-boundary metadata, lib.es policy) closed upstream. Fidelity.CloudEdge regenerates bindings against multiple Cloudflare SDK packages mechanically. Fable output is deterministic across regenerations.

**What's true.** The F#/Fable path is fully stable. Bindings are in production. Pattern characterization data accumulates as a side-effect of normal binding work — every TS shape that gets bound is documented in Fable's deterministic output, which serves as the oracle for the rule library.

**What's next.** JSIR upstream in MLIR (or Composer absorbs the dialect from Google's repo in advance of upstream merge). Composer adds MLIR build infrastructure to consume JSIR. First Alex → JSIR lowering pass written for a single binding shape (a let-binding, a function definition).

### Waypoint 3: JSIR pipeline standup

**State.** Composer's MLIR back-end fan-out includes JSIR as a target alongside LLVM/CIRCT/MLIR-AIE. Initial witnessing rules characterized empirically against the Fable-compiled JS oracle for foundational shapes (Promise-returning function, async/await, basic class declaration, simple discriminated union). The Library of Alexandria starts as a small set of these foundational patterns.

**What's true.** A simple Clef program targeting JS now compiles through the unified pipeline — PSG → Alex → JSIR → JavaScript — with the same MLIR pass infrastructure that produces native code for other targets. The verification properties documented in [design-time-spec-runtime-reliability.md](../design-time-spec-runtime-reliability/) start applying to Clef-emitted JS.

**What's next.** The TypeScript-binding generator that produces Clef extern declarations + matching Alex witnessing metadata begins. This consumes Xantham's analysis output (no change required to Xantham itself) and produces the matched-pair artifacts that the JSIR pipeline knows how to elide. The first Cloudflare SDK gets bound through this new path as a proof-of-concept (likely the smallest surface — `dynamic-workflows` — given its size).

### Waypoint 4: First Clef-native bindings

**State.** Both binding pipelines are functional. F# bindings continue via Xantham → Xantham.Generator → Fable for existing F# users. New Clef code uses Xantham → Clef-binding generator → matched (Clef declaration + Alex witness) pairs → Composer's JSIR back-end → JavaScript. Fidelity.CloudEdge stays on the F# path; new Clef-native projects (or new modules in evolving projects) adopt the JSIR path.

**What's true.** The JSIR path produces JavaScript that runs on Cloudflare Workers identically to Fable's output for the same TS surface. Cross-substrate guarantees (BAREWire byte-identity between native and edge actors) hold structurally because both serializers derive from the same Alex IR. The witnessing rule library covers the shape categories the bound Cloudflare SDKs use.

**What's next.** Additional libraries get bound via the new path. D3 (chained-method-builder pattern), SolidJS (reactive primitives), TanStack Solid family (factory-with-options pattern). Each library characterizes new patterns or reuses existing ones. The Library of Alexandria matures with each round.

### Waypoint 5: JSIR canonical for new Clef code; Fable continues for F# code

**State.** Composer's JSIR back-end is the default JS-emission path for Clef. Witnessing rule library is sufficiently mature that binding new TypeScript libraries is mechanical pattern-classification rather than per-library design. Fable continues to serve F# code, including the existing Fidelity.CloudEdge surface (which transitions name to FSharp.CloudEdge under fsprojects community maintenance, per the rename anticipated in [JSIR doc § "What Does Not Change"](../jsir-javascript-as-mlir-backend/#what-does-not-change)).

**What's true.** Two ecosystems coexist permanently. F# source through Fable to JS for the F# community. Clef source through Composer + JSIR to JS for the Clef community. Both produce V8-acceptable JavaScript that runs on the same Cloudflare runtime. They share Xantham as the analysis substrate when binding TypeScript surfaces (one consumer each); they share BAREWire as the wire format when actors communicate cross-substrate; they don't compete.

**What's next.** Further consolidation as the rule library asymptotes. New JS libraries get bound by classification. The IDE-side coordination layer (Atelier's [Transcribe](https://github.com/speakeztech/Atelier/blob/main/docs/10_transcribe.md)) extends binding generation into interactive design-time workflows. That's the territory beyond this document's planning horizon.

## What Each Path Owns at Maturity

At Waypoint 5, the responsibility split is clean:

- **Fable owns**: F# source language, F# idiom translation, F# user community, existing `[<Emit>]`-based binding patterns, integration with the F# / .NET tooling ecosystem.
- **Composer + JSIR owns**: Clef source language, Clef → MLIR lowering through Alex, the witnessing rule library (the Library of Alexandria), JSIR-based JS emission, integration with the broader MLIR ecosystem (LLVM/CIRCT/MLIR-AIE for non-JS targets).
- **Xantham owns**: TypeScript surface analysis (consumed by both paths' binding generators).
- **Cloudflare owns**: the runtime contract for V8 isolate execution. Both paths produce JS that targets that contract; Cloudflare's stability commitments apply uniformly.

Neither Fable nor JSIR is "better" at the source-language level. They are *suited to different source languages*. The architectural decision was forced by F#'s representation environment for Fable and enabled by Clef's representation environment for JSIR.

## The Fable Output as Executable Specification

A concrete consequence of the transition arc that's worth naming explicitly:

The current Fidelity.CloudEdge work — every binding shipped, every bug fixed in Xantham, every encoder/decoder issue closed — produces deterministic Fable-compiled JavaScript output. That output is the **specification** for what the JSIR pipeline must produce when binding the same TypeScript surface through the Clef path.

This means the current production work is not transitional infrastructure to be discarded. It is the executable oracle that the JSIR pipeline characterizes its witnessing rules against. A Clef-emitted JS file produced by JSIR + the witnessing rule library should match the corresponding Fable-emitted JS file (or differ in well-understood ways: cleaner syntax from MLIR's pass infrastructure, but semantically equivalent).

The pattern is familiar from compiler bootstrapping. js_of_ocaml's test corpus serves the same role for OCaml's JS compilation. LLVM's reference test suite serves the same role for native compilation. Composer's JSIR pipeline gets the same kind of corpus by virtue of Fidelity.CloudEdge shipping F# bindings on the existing path.

This reframes Fidelity.CloudEdge work strategically: every binding shipped today both delivers production value *and* characterizes a pattern for tomorrow's pipeline. The two roles are simultaneous, not sequential.

## Cross-references

- [JSIR: JavaScript as an MLIR Backend](../jsir-javascript-as-mlir-backend/) — the underlying MLIR architecture
- [Design-Time Specification for Runtime Reliability](../design-time-spec-runtime-reliability/) — what verification properties hold across both paths
- [TypeScript Binding via Xantham](../../interop/typescript-binding-via-xantham/) — the analysis substrate consumed by both binding pipelines (companion to this document)
- [Library Binding for C/C++](../../interop/library-binding/) — Farscape's pattern; structurally analogous to what TS binding does for the JS target
- [Atelier docs/10_transcribe.md](https://github.com/speakeztech/Atelier/blob/main/docs/10_transcribe.md) — the polyglot IDE-side ingestion layer that consumes per-language analysis substrates (Xantham for TS, Farscape for C/C++, others)
- [Fidelity.CloudEdge docs/12](https://github.com/speakeztech/Fidelity.CloudEdge/blob/main/docs/12_xantham_glutinum_replacement_assessment.md) — the operational binding migration providing the executable specification for tomorrow's JSIR pipeline
