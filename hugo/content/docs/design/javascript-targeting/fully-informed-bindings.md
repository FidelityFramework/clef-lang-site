---
title: "Fully Informed Bindings"
linkTitle: "Fully Informed Bindings"
description: "Combining TypeScript declarations, JavaScript implementation evidence and deferred Clef inference for bindings and owned supporting libraries."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Architecture", "Interop", "JavaScript", "Design"]
weight: 35
---

The destination for our Cloudflare work is a Clef source library covering Workers, agents, durable workflows, and their control plane, with an API a Fidelity.CloudEdge developer recognizes. This page describes a proposed binding-analysis pipeline, not a shipped verification facility. The [back-end transition](/docs/design/javascript-targeting/from-fable-to-jsir/) covers emission; this page covers how declarations and executable code could inform bindings together.

## Three Categories of Surface

"The SDK surface" covers three categories of artifact, and the binding mechanics differ for each.

The runtime API surface, `@cloudflare/workers-types` and the declaration packages around it, is pure declaration. No JavaScript ships, because the implementation is the workerd runtime itself, and the declarations are machine-derived from its source. Binding is the only relationship this category admits, through [Xantham's](/docs/design/interop/typescript-binding-via-xantham/) structural analysis.

The wrapper SDKs, the agents SDK, workflow helper classes, harness code, are TypeScript compiled to JavaScript that genuinely ships in a Worker's bundle: class hierarchies, RPC plumbing, scheduling helpers. Artifacts in this category carry behavior worth measuring and, in bounded cases, worth absorbing.

The control plane is machine-generated from Cloudflare's OpenAPI specification, and the specification is public. Its TypeScript client is a projection of that schema, so the principled Clef client generates from the schema directly, the path our OpenAPI ingestion already takes for backplane provisioning. Measuring or absorbing the projection would study a shadow whose source is published.

The rest of this page concerns the second category, a minority of the surface by volume and a majority of it by runtime behavior, release cadence, and drift risk.

## The Join

A TypeScript declaration and the JavaScript shipped with it provide different evidence. Xantham supplies declared signatures and structural types; the [JSIR](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) forward path exposes supported control flow and expressions in the implementation. Joining them requires a pinned package, entry-point resolution, and a justified mapping through exports, wrappers, and source maps. A symbol-name match is a candidate correspondence, not proof that the body implements the declaration. JSIR's placeholder value types do not restore TypeScript's erased semantic types.

JSHIR is a worked analysis candidate within a coordinated foreign-language frontend. Composer’s [tooling contribution map](https://forge.spkez.dev/FidelityFramework/Composer/src/branch/main/docs/javascript-targeting/02_jsir_tooling.md) also considers Bun’s structural/dependency evidence, Dafny’s contract and realization precedents, and Fable’s executable references. Their roles overlap; CCS owns semantic elaboration, and consistent unresolved facts remain pending until the relevant build or REPL commitment.

That join is what “fully informed” means here: declarations plus implementation evidence, with unknowns retained. Lifting JavaScript into JSHIR supplies an analysis surface. It does not make arbitrary JavaScript into typed Clef, and types shipped beside a body remain claims to check.

## Four Instruments

The join yields four instruments, in ascending ambition.

**Effect harvesting.** A proposed analysis would conservatively approximate network, storage, timer, and state effects reachable from each SDK entry point. Dynamic dispatch, callbacks, unresolved imports, and runtime-generated code must retain unknown effects unless justified summaries close them. A declaration alone cannot establish purity or determinism. The [ledger lowering](/docs/design/javascript-targeting/the-ledger-lowering/) may use accepted summaries; it must not omit journal entries merely because an analysis found no known effect.

**Conformance in IR space.** Lift Fable and Composer outputs through the same pinned JSIR path and compare explicitly normalized JSHIR as a regression instrument. Name normalization must respect binding and capture; option or object normalization must preserve observable behavior. Matching normalized IR is evidence under those normalization rules, not a general semantic-equivalence proof. The [contract-to-artifact acceptance path](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) separates model proofs, lowering obligations, and executable characterization.

**The disagreement channel.** Farscape's boundary taught that a C header under-declares, and the TypeScript boundary has the complementary vice: it over-declares, with rich types that are claims without enforcement. When the lifted body of an SDK function reaches `fetch` behind a signature that reads pure, or touches storage its types never mention, that divergence is a first-class finding. Regenerated per release, the channel is a lint on the vendor, and on a pre-1.0 surface that changes monthly it may be the single most valuable output of the pass, catching behavioral drift under stable signatures where a `.d.ts` diff sees nothing.

**The dependency-recovery workbench.** The design direction is owned Clef SDKs and supporting Clef libraries. Development proceeds through bounded replacements with declaration/body correspondence, deferred inference and human review. Retain a versioned complete package dependency graph independently of application reachability; pin changes invalidate affected correspondence and evidence. The libraries and accepted evidence become durable assets rather than restarting foreign conversion on every build. See Composer’s [dependency-recovery guide](https://forge.spkez.dev/FidelityFramework/Composer/src/branch/main/docs/javascript-targeting/09_contract_directed_dependency_recovery.md). Round-trip JSHIR comparison and differential execution would test those candidates. Graduation additionally needs stated behavioral contracts and justified correspondence for the supported operations; round-trip similarity alone does not prove equivalence or eliminate runtime dependencies. Only dependencies actually replaced by the accepted implementation are retired.

For JSX-producing UI bindings, compare structure before Solid compilation and supported ordinary JavaScript afterward. Upstream JSIR cannot lift JSX through its native bridge without extension, and two producers sharing Solid have a shared compiler assumption. The [toolchain chapter](../javascript-jsx-toolchain/) adds framework-transform and final-WebView acceptance obligations.

## One Discipline, Two Boundaries

The proposed discipline parallels [Farscape](/docs/design/interop/library-binding/): combine a declared surface, implementation analysis, explicit curation, and matched declarations and witnessing rules. Neither C headers nor TypeScript declarations fully specify effects or implementation behavior. Analysis can expose disagreements and discharge supported obligations; unresolved behavior remains a boundary premise. Atelier's Transcribe layer is the intended integration point for the per-language analysis substrates.

A per-package configuration would name entry points, accepted effect summaries and their evidence, fragments nominated for absorption, and known declaration/body disagreements. An override that reduces a conservative effect set needs justification; configuration alone cannot turn unknown behavior into purity. Pinning this configuration with the package makes regeneration and review reproducible.

## The Compatibility Contract

The F#/Fable surface supplies a useful API and executable reference over an explicit shared contract; Clef semantics and the selected host contract govern the Clef implementation. The first cut can retain familiar structure: module organization, record and discriminated-union message types, and computation-expression shapes carry across as cognates. The deliberate differences appear where Clef's integrity machinery pulls a construct upward, and their locations are predictable. Dynamic and `obj`-shaped surfaces become schema-directed narrowing returning `Result`, with [The Foreign Pair](/docs/design/javascript-targeting/the-foreign-pair/) supplying the boundary types and their grade discipline. Promise-shaped APIs become DCont-backed. Each supported interop operation needs a Clef boundary contract, portable witnessing and a matching target realization; replacing an `[<Emit>]` template does not itself establish that correspondence. Effectful call sites would carry conservative summaries and their evidence. The intended port preserves the application structure while making boundary assumptions explicit as obligations, generated checks, or accepted premises.

## The Sequence

The first step carries no Composer dependency: lift a pinned SDK and a Fable-compiled Worker with the reviewed JSIR tool to characterize the supported operations. Then build conservative effect analysis and declaration/body correspondence, followed by candidate Clef bindings and differential conformance checks. Absorption comes last, with explicit behavioral acceptance criteria. This sequence uses the TypeScript surface without treating its declarations as foreign-implementation proofs.