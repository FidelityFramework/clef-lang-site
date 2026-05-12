---
title: "TypeScript Binding via Xantham: The Persistent Analysis Substrate"
linkTitle: "TypeScript Binding (Xantham)"
description: "How Xantham serves as the durable TypeScript analysis layer across both the current F# binding pipeline and the eventual Clef-via-JSIR pipeline"
date: 2026-05-04
authors: ["Houston Haynes"]
tags: ["Architecture", "Interop", "TypeScript", "Design"]
---

[Library Binding for C/C++](../library-binding/) describes how Farscape generates Clef bindings for native libraries. This document describes the analogous concern for TypeScript surfaces — the analysis layer that ingests `.d.ts` files and produces the structural type information consumer generators turn into bindings. Today that analysis layer is **Xantham**, a schema-driven extractor maintained by Shayan Habibi at [shayanhabibi/Xantham](https://github.com/shayanhabibi/Xantham). The SpeakEZ fork is at [speakeztech/Xantham](https://github.com/speakeztech/Xantham).

The point of this document is to establish what Xantham contributes that *persists* across changes in the rest of the toolchain. The current binding pipeline (Xantham → F# generator → Fable → JavaScript) and the eventual pipeline ([Xantham → Clef-via-JSIR](../../javascript-targeting/from-fable-to-jsir/)) consume the same analysis substrate. The downstream pieces change; Xantham's role does not.

## What Xantham Is — and What It Is Not

Xantham is a schema-driven TypeScript analyzer. It separates concerns into three phases:

1. **Extract** — A Fable-compiled F# program (`Xantham.Fable`) crawls TypeScript via the TypeScript Compiler API, walks every reference recursively across all referenced files, and emits JSON conforming to a common schema.
2. **Decode** — A .NET library (`Xantham.Decoder`) reads the JSON into strongly-typed F# structures (`ResolvedType`, lazy containers, an arena interner) and provides a utility layer for generator consumption.
3. **Common Schema** — A shared discriminated-union vocabulary (`Xantham.Common/Common.Types.fs`) that defines the contract between extractor and consumers. Included as source in both `Xantham.Fable` and `Xantham.Decoder`; not a separately published assembly.

What Xantham is *not*: a generator. The author has been explicit on this point — *"the generators are supposed to be the consumer libraries... I want to just give a reference generator. The encoder and decoder are what I want to try and maintain myself. Anyone can use the information how they want."* The reference generator (`Xantham.Generator`) exists as an example consumer; it produces F# bindings via Fabulous.AST. Other consumers can produce other targets.

This separation is the load-bearing architectural commitment. Xantham's encoder and decoder are designed to serve multiple consumer generators that all consume the same JSON schema and decoder API. Today's consumer is the F# binding pipeline. Tomorrow's consumer is the Clef binding pipeline ([described in From Fable to JSIR](../../javascript-targeting/from-fable-to-jsir/)). Both consume the same upstream.

## The Analysis Schema as Stable Contract

`Xantham.Common/Common.Types.fs` defines the vocabulary for representing TypeScript shapes in target-language-neutral form. The schema's discriminated unions are the **durable artifact** that consumers depend on.

| Schema construct | What it captures |
|:-----------------|:-----------------|
| `TsType` (22 cases) | Every TypeScript type construct — primitives, interfaces, classes, unions, intersections, generics, type aliases, conditional types, mapped types, indexed access, type literals, template literals, predicates, type queries, etc. |
| `TsAstNode` (32 cases) | Every TypeScript AST node kind that affects type information — declarations, references, members, parameters, signatures, etc. |
| `TypeKey` indirection | Integer references into a `TypeMap`, allowing cyclic graphs to be representable without physical nesting. |
| `LibEsExports` set | The set of TypeScript standard library exports, distinguishable from user types so consumers can route lib.es references to external resolvers rather than re-binding them. |
| Source path metadata | The originating `.d.ts` file and position, available for diagnostics and (eventually) explicit package-boundary identification. |

The schema's stability is what makes multi-consumer architecture viable. A consumer written today against the schema continues to work as long as the schema's compatibility commitments hold; consumers written for new target languages don't need to negotiate with the encoder, only against the same schema.

Two related concerns are tracked as open work in the upstream repo:

- **Decoder behavior on `MISSREF` keys** — the encoder logs `[MISSREF]` diagnostics during ingestion when it can't bind a referenced TypeKey to a type definition, then writes the key into the JSON anyway; the decoder's compression pass then crashes. Fixing this contract (whether by encoder stub-emission, decoder substitution, or refusing to write dangling JSON) is an open issue with consumer impact.
- **Explicit package-boundary information** — currently consumers parse `Source` paths to recover npm package identity; surfacing this explicitly in the schema would let consumer generators produce clean module names without ad hoc parsing.
- **Stable lib.es reference policy** — sometimes lib.es references survive cleanly to consumers; sometimes they become unresolvable `MISSREF` keys. A consistent policy ("lib.es references always emit by external name, never inlined") would let consumers wire a single resolver.

These concerns are filed upstream and informed by current consumer experience. They don't change Xantham's role; they refine the contract it provides.

## Multi-Consumer Architecture in Practice

Today's primary consumer is `Xantham.Generator`, which produces F# bindings rendered via Fabulous.AST and Fantomas. Fidelity.CloudEdge depends on this pipeline for its Cloudflare runtime SDK bindings (workers-types, agents, dynamic-workflows; see [Fidelity.CloudEdge docs/12](https://github.com/speakeztech/Fidelity.CloudEdge/blob/main/docs/12_xantham_glutinum_replacement_assessment.md) for the operational details).

The eventual second consumer — described in [From Fable to JSIR](../../javascript-targeting/from-fable-to-jsir/) — produces Clef extern declarations paired with Alex-side witnessing rules. Same JSON input from Xantham; different output artifacts.

The downstream contract from Xantham's perspective:

```
Xantham.Decoder.Runtime.create "input.json"
  └── XanthamTree
       ├── KeyExportMap        : exports keyed by TypeKey
       ├── ExportMap           : exports keyed by source module path
       ├── LibEsExports        : lib.es-marker set
       ├── GetArenaInterner()  : lazy resolved object graph
       └── (other accessors)
```

A consumer takes that `XanthamTree`, walks it through whatever rendering or emission strategy it implements, and produces its own artifacts. The consumer never modifies Xantham's analysis; it reads from it. Multiple consumers can read the same analysis output independently.

## What Persists Across Toolchain Transitions

The current Fidelity.CloudEdge work is in production. The eventual Clef-via-JSIR work is in design. Across that transition, three things about Xantham don't change:

**The analysis substrate**. The encoder reads TypeScript, walks the type graph, distinguishes lib.es from user types, handles cycles, merges overloads, captures brand symbols. The accumulated knowledge in this layer — the corner cases Shayan has worked through over the project's evolution — is hard-won and target-agnostic. None of it has to be redone for a new consumer.

**The schema vocabulary**. `Common.Types.fs`'s discriminated unions are how TypeScript shapes are represented for downstream consumption. The vocabulary is consumed by the F# generator today and would be consumed by a Clef generator tomorrow. Improvements to the vocabulary (the open issues mentioned above) compound for all consumers.

**The maintenance domain**. Shayan maintains the encoder/decoder; consumers maintain their generators. This separation is what makes a multi-consumer architecture viable long-term. The Fidelity Framework's Clef-targeting binding work doesn't need to take ownership of Xantham — it needs to consume Xantham reliably and contribute upstream where consumer experience reveals encoder/decoder concerns.

## Supply Chain Boundary

Xantham's encoder runs in Node and uses the TypeScript Compiler API to walk `.d.ts` files. That's the toolchain's only npm dependency on the binding-generation side. Everything downstream — the decoder, the F# generator (today), the Clef generator (tomorrow), Composer's MLIR pipeline, the deployed JavaScript artifact — operates outside the npm ecosystem.

The boundary is structural. `.d.ts` files are *data*, not executable code; the encoder reads them, but no npm package's runtime logic executes during ingestion in a way that affects downstream artifacts. The arbitrary-code-execution risk that npm supply chain attacks exploit is contained to the read-only ingestion step and bounded by the TypeScript Compiler API's surface (a Microsoft-maintained package with strong audit posture).

The .NET-hosted side of the toolchain (decoder, generator, Composer host) operates in the NuGet ecosystem, which has materially better supply-chain hygiene than npm. The MLIR/LLVM tooling is C++ in the LLVM ecosystem. None of these are npm-resident.

For comparison, the current full pipeline (Xantham + F# generator + Fable + npm bundlers) carries deep transitive npm dependencies through Fable and any bundling step (esbuild, vite, webpack, rollup) that follows. The eventual pipeline ([Xantham + Composer + JSIR](../../javascript-targeting/from-fable-to-jsir/)) reduces this surface to the single Xantham ingestion point. The structural property is: read TypeScript, never run TypeScript-ecosystem code in the build pipeline.

## Connection to the Library of Alexandria

The Clef-via-JSIR pipeline introduces a curated catalog of witnessing rules — what we informally call the "Library of Alexandria" inside Alex (Composer's middle-end). Each rule characterizes how a TypeScript shape category elides through the compilation pipeline to the right JSIR ops and ultimately the right JavaScript AST.

Xantham's analysis output is what those rules are characterized *from*. The encoder doesn't write witnessing rules — that's consumer territory — but the structural information it provides (a function returns `Promise<T>`; this class extends another generic class with type-arg passthrough; this interface has overloaded methods, etc.) is exactly what the consumer needs to identify which Pattern from the Library applies.

As more libraries are bound via the Clef-via-JSIR pipeline, the Library of Alexandria matures: D3's chained-method-builder pattern, SolidJS's reactive primitives, TanStack's hook-with-options pattern. The patterns recur across libraries; the Library accumulates them. Xantham provides the source material; the consumer maps source to pattern; the rule library grows with each new library characterized.

This is also why every binding shipped today via the F# pipeline has compounding value beyond shipping. The deterministic Fable output produced from a Xantham-analyzed TypeScript surface is the **executable specification** for what the JSIR pipeline must produce when that surface is bound through the Clef path. Each binding fix today characterizes a pattern that enters the Library tomorrow.

## Practical Considerations

For the medium-term horizon (months through end of year and beyond), Xantham's role is operational:

- **Track upstream stability**. The encoder/decoder API is published and consumed. Breaking changes need to be coordinated. Issues with consumer impact are filed in the upstream repo.
- **Pin versions per binding**. Each Fidelity.CloudEdge binding (workers-types, agents, dynamic-workflows) pins to a specific Cloudflare SDK version and is regenerated as that source changes. Xantham analysis is reproducible against pinned inputs.
- **Contribute back where the contract is being defined**. The decoder MISSREF behavior, package-boundary metadata, lib.es policy — all are best resolved upstream so consumers benefit uniformly. SpeakEZ's role is to surface consumer experience as filed issues; Shayan's role is to maintain the encoder/decoder; both depend on the discipline of the maintenance boundary.
- **The Fable+F# consumer continues**. Fidelity.CloudEdge ships against the F# binding pipeline today and through the JSIR transition. The eventual rename to FSharp.CloudEdge (when that lands) doesn't affect Xantham consumption; the F# binding pipeline serves F# users indefinitely.

## The Long Arc, Briefly

The analysis capability described in this document has further forms beyond the document's planning horizon. The Atelier IDE anticipates a polyglot ingestion feature called Transcribe (see [Atelier docs/10_transcribe.md](https://github.com/speakeztech/Atelier/blob/main/docs/10_transcribe.md)) that handles many source languages — F#, Python, Rust, Go, TypeScript / JavaScript, C / C++ — as a unified IDE workflow that produces matched (Clef binding + Alex lowering witness) pairs. Xantham is the analysis substrate Transcribe consumes for the TypeScript / JavaScript case; Farscape plays the same role for C / C++; other substrates plug in for other languages.

Naming Transcribe and Atelier here is **directional confirmation** that the analysis-substrate framing in this document points toward a coherent destination, not an attempt to map that destination in detail. The current plan — Xantham as standalone tool, multi-consumer architecture, supply-chain isolation, schema-as-stable-contract — sets up the long arc without committing to its specifics. Xantham continues being maintained as a standalone analysis tool by Shayan Habibi for the medium-term horizon; how the eventual IDE-side coordination layer consumes it (and how the parallel substrates for other languages do the equivalent thing) is Atelier's territory to design and document.

## Cross-references

- [JSIR: JavaScript as an MLIR Backend](../../javascript-targeting/jsir-javascript-as-mlir-backend/) — the back-end target Xantham analysis eventually feeds via Clef bindings
- [From Fable to JSIR](../../javascript-targeting/from-fable-to-jsir/) — the back-end transition arc; companion to this document
- [Design-Time Specification for Runtime Reliability](../../javascript-targeting/design-time-spec-runtime-reliability/) — what verification properties hold across both binding pipelines
- [Library Binding for C/C++](../library-binding/) — the Farscape pattern that Xantham parallels for TypeScript ingestion
- [Xantham upstream repo](https://github.com/shayanhabibi/Xantham) — encoder/decoder maintenance
- [SpeakEZ Xantham fork](https://github.com/speakeztech/Xantham) — consumer-side coordination
- [Fidelity.CloudEdge docs/12](https://github.com/speakeztech/Fidelity.CloudEdge/blob/main/docs/12_xantham_glutinum_replacement_assessment.md) — the operational binding migration that this document's strategic framing supports
