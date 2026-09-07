---
title: "The Ledger Lowering"
linkTitle: "Ledger Lowering"
description: "Journal only what cannot be recomputed: a proposed compiler partition into reconstructible segments and retained observations, with recovery code for the JSIR back-end."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Architecture", "Reversibility", "JavaScript", "Design"]
weight: 45
---

Durable execution systems persist events and selected step results so execution can recover after interruption. Their exact replay and checkpoint policies differ; deterministic computation is not necessarily journaled instruction by instruction. The opportunity here is to use compiler-visible dependencies and effects to identify which observations must survive, and which state can be reconstructed.

This page proposes a write-side discipline for the [JSIR backend](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/): retain evidence needed for recovery, and recompute where the required laws hold. Negative and fractional types are proposed machinery, not a currently implemented partitioning proof. The [boundary map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/) covers the complementary read-side checks.

## The Compiled Partition

The [negative and fractional types](/docs/design/types/negative-fractional-types/) design motivates a forward/reverse pairing. A reverse-channel type expresses an intended interface; an inverse law requires separate justification. Deterministic replay from saved inputs, inversion from a final state, and compensation for an external effect are different contracts.

The proposed analysis would identify captured dependencies, accepted inverse or recomputation laws, and effect summaries for each segment. Explicit closure slots make those obligations inspectable; structural completeness alone does not prove invertibility, especially after information-losing arithmetic. Unknown calls and unresolved effects remain in the recovery model. A network response, model output, or externally observed write may require retained observations or an effect-specific recovery protocol. The journal policy follows the accepted evidence, not a claim that every unproved obligation can be solved by recording one entry.

For segments admitted as reconstructible, this design can avoid retaining every intermediate state. Its benefit is measured against the durable runtime's actual checkpoint policy, including the inputs and control state needed for recomputation.

## The Write-Side Boundary Map

At each admitted residue site, the proposed back-end would derive three artifacts from the typed source and recovery contract, alongside the [BAREWire](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#the-trust-chain) serializers.

The first artifact is a declared record containing the effect identity and observations needed for recovery. BAREWire lowers its agreed layout to bytes; payloads carry no self-describing type, schema, dimension, or proof tags. Application discriminants and presence bits may remain when the record requires them. The graph retains schema and obligation metadata until their lowering role is fulfilled. Recovering against what the run observed also requires an application/version contract for those recorded observations.

The second artifact pairs a journal entry with its effect. When both are writes in one supported transaction domain, they can commit atomically. An external service call is not made atomic by a local journal write: recovery requires its own idempotency, deduplication, or acknowledgement protocol. An acknowledged durable write can discharge a runtime premise only under the storage contract the [constructed witness](/docs/design/javascript-targeting/constructed-witnesses/) checks.

The third artifact would be generated recovery code beside the forward path. Segments with accepted inverse laws use those inverses; reconstructible segments replay from sufficient recorded inputs; irreversible effects use declared compensations where available. A numerical-method adjoint or differentiation pullback is not automatically an inverse. The [contract-to-artifact path](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) supplies the separation between graph obligations, JavaScript lowering, and runtime premises.

## Typed Checkpoints

A typed checkpoint would describe the live values, control position, version, and external observations required at a suspension. BAREWire can provide a common byte layout across JavaScript and native endpoints. Resuming a computation across them additionally requires corresponding continuation/state-machine semantics, compatible code versions, and valid target-local resources; sharing the frame definition alone does not migrate a V8 continuation. Checkpoint size depends on that live state and must be measured.

## Anchor Placement

Recovery obligations determine mandatory observations; optional anchors trade storage against recomputation. Checkpointing techniques can inform this policy once dependency, cost, and determinism premises are established. Numerically approximate reconstruction also needs a stated error tolerance: the [Lyapunov window](/docs/design/types/lyapunov-window/) motivates measuring its useful horizon, not an automatic inverse guarantee. A schedule must preserve the required recovery behavior across suspension and retry.

## Honest Framing

The emitted JavaScript and its runtime remain part of the acceptance argument. Compensation is a declared semantic action, not erasure of history. Exactly-once externally observed compensation requires an explicit transactional or idempotent protocol; generated code and a ledger alone do not establish it. Effect classification must conservatively retain unknown behavior, because TypeScript signatures do not enumerate all effects and analysis of dynamic calls may be incomplete.

The [back-end transition](/docs/design/javascript-targeting/from-fable-to-jsir/) is the proposed implementation home. Existing Fable applications provide characterization cases, not automatic effect proofs. Start with a bounded recoverable flow, identify its observations and laws, and exercise interruption and retry alongside the graph and lowering obligations. The aim is a smaller, justified recovery record, with the omitted state genuinely reconstructible.