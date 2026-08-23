---
title: "The Ledger Lowering"
linkTitle: "Ledger Lowering"
description: "Journal only what cannot be recomputed: how negative and fractional types partition a program into structurally reversible segments and a journaled residue, and what the JSIR back-end emits for each."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Architecture", "Reversibility", "JavaScript", "Design"]
weight: 45
---

Durable execution systems journal by default. Temporal pioneered the pattern, Cloudflare's Workflows and agent runtime carry it forward, and the mechanics are consistent across the family: every step's result is persisted before the next step runs, recovery is a replay of the journal, and history is an append-only log the runtime consults on every resumption. The pattern works, and its cost is structural. The runtime journals everything because it can prove nothing: with no visibility into which steps are deterministic recomputation and which touched the outside world, the safe policy is to write it all down. Storage, write amplification, and replay time then scale with the length of the computation, whatever the computation contains.

A compiler that carries reversibility in its types can prove what the runtime cannot, and that changes what needs to be written down. This page describes the write-side discipline of our JavaScript targeting: which parts of a Clef program the [JSIR back-end](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) would journal, which parts it would not, and what it would emit for each. The [boundary map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/) covers the read side, generated narrowing at every untrusted inbound edge. This is its complement.

## The Compiled Partition

The [negative and fractional types](/docs/design/types/negative-fractional-types/) page makes a strong claim: with the reversal information carried in the type, a prior state comes from re-running the reverse computation, and the event log that ordinary architectures maintain for retrace has no work left to do. That claim holds exactly as far as the type discipline reaches. Stated together with its boundary, it becomes an emission strategy.

The compiler verifies, per segment, that the reversal information is structurally complete: every piece of state the forward computation depends on has a corresponding piece of reversal information, checkable because dependencies are explicit in the flat closure representation. A segment that passes is structurally reversible. Its prior states are recomputed on demand, it writes nothing, and it needs nothing written. A segment that cannot pass is one where the pair cannot cancel inside the program, because the world intervened: a network call whose response is not a function of program state, a model inference, a write another party may observe, a payment. These sites are the residue, and the residue is what gets journaled. The journal's contents are then a computed property of the program: the image, under compilation, of exactly the obligations the type system could not discharge.

The blanket-journal engines write a record per step. This discipline writes a record per irreversible effect. For a data-transformation pipeline with one external call, that is the difference between a journal that grows with the pipeline and a journal with one entry.

## The Write-Side Boundary Map

At each residue site, the back-end has three artifacts to emit, and all three derive from the same typed source that already produces the [BAREWire](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#the-trust-chain) serializers.

The first is the journal entry itself, a BAREWire-framed record carrying the effect's identity, its class, and the observations the step consumed. Capturing the observations matters as much as capturing the effect: a later reversal or resumption must be computed against what the run saw, never against a world that has since moved on, so the frame holds the inputs the residue site read alongside what it did.

The second is the pairing between the entry and the effect it describes. A journal that can disagree with the store it journals is a liability, so where the target offers a transactional write, the entry and the effect commit together. On Cloudflare's Durable Objects, whose embedded SQLite is the natural hot store, the frame tables land in the same database as the actor's state, and the schema for those tables is derived from the frame type the way serializers are, one definition producing the record layout, the wire form, and the DDL. The acknowledged write discharges a durability premise of the reversal proof, in the pattern [Constructed Witnesses](/docs/design/javascript-targeting/constructed-witnesses/) sets out.

The third is the reversal path, emitted as code. For each flow, the back-end would generate the reverse function beside the forward one, compiled from the same Program Semantic Graph: structurally reversible segments run their adjoints, and residue entries resolve by class, an exact inverse where one exists, a compensating action where one does not, with the class recorded in the frame at the moment of the forward effect, never judged after the fact. Reversal by generated code replaces reversal by interpreting a history, and it inherits the verification the shared middle-end applies to everything else it lowers.

## Typed Checkpoints

Durable runtimes checkpoint executions so that a computation survives eviction, and the checkpoint is conventionally an opaque blob: whatever the engine needed to freeze. Under this discipline the checkpoint has a type. What must be preserved across a suspension is the undischarged residue, the open obligations and their observations, and that set serializes as a BAREWire frame like any other. The result is smaller than a frame-agnostic snapshot, inspectable, and, because the same layout compiles natively through LLVM, readable off the JavaScript substrate entirely. A computation checkpointed in a V8 isolate is designed to be resumable by a native process holding the same frame definition, which extends the [cross-substrate byte-identity argument](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#what-this-means-for-cross-substrate-actors) from messages at rest to computations in flight.

## Anchor Placement

Residue sites fix the journal's mandatory entries. A second, elective class of entries serves performance: anchors, stored states that bound how much recomputation a reversal or recovery pays. Placing them is a solved family of problems. Where the constraint is memory against recompute time, the checkpointing schedules from reverse-mode differentiation apply directly. Where the constraint is numerical, a computation whose reversibility decays at a rate the dynamics set, the cadence comes from the [Lyapunov window](/docs/design/types/lyapunov-window/). One principle covers all three cases: the journal meters irreversibility, at the rate it actually occurs, whether that rate is set by an external effect, a memory ceiling, or an entropy production. Everything else is recomputed.

## Honest Framing

Three boundaries keep the claim inside what the mechanism delivers. The emitted JavaScript remains JavaScript, and everything the [trust chain](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#the-trust-chain) says about the emission boundary and the runtime contract applies unchanged here. The journal discipline is established above that boundary, in the shared middle-end, and the artifact inherits it. Compensation is a semantic commitment, and where an effect admits no exact inverse the generated reversal performs the compensating action the developer supplied, so the guarantee for that class is that compensation runs, once, against the recorded observations, and never that history is undone. And the partition is only as sharp as the effect classification: an external call misdeclared as pure is invisible to the residue analysis, which is why the classification lives in the binding layer the [boundary map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/) generates, where it is regenerated with the surface it describes instead of asserted by hand.

The sequencing home for this work is the [back-end transition](/docs/design/javascript-targeting/from-fable-to-jsir/) already in motion, and the oracle doctrine carries over intact: every effectful binding shipped on the Fable path today characterizes a residue classification and a frame-emission rule for the pipeline that follows. The blanket journal is the sound answer for a runtime that cannot see inside the programs it persists. With a compiler that can, the ledger holds exactly the residue.
