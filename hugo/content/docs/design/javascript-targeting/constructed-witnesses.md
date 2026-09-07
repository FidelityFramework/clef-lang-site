---
title: "Constructed Witnesses"
linkTitle: "Constructed Witnesses"
description: "How static implications, generated boundary checks and explicit external assumptions can work together through lowering."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Architecture", "Verification", "Type Systems", "Design"]
weight: 25
---

Static reasoning and boundary checks can work together. A compiler can establish an implication about a computation while generating a check for an input premise that will become known only when a message arrives. The useful question is which premise the check establishes, and how that fact reaches the operation that relies on it.

This is the proposed discipline connecting [Design-Time Specification](/docs/design/javascript-targeting/design-time-spec-runtime-reliability/), the [boundary map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/) and the [ledger lowering](/docs/design/javascript-targeting/the-ledger-lowering/). It follows the specification's [preservation obligation](/spec/draft/conformance/#6-the-preservation-obligation-through-lowering). It is not a claim that the current JavaScript implementation has discharged every obligation those designs describe.

## The Shape of an Obligation

An obligation relates premises to an invariant: an admitted range to a representation's coverage, a valid byte span to an access, or an input shape to the operations a record supports. The compiler needs the premises' provenance, the supported reasoning fragment and a disposition for the result. A successful proof, a counterexample, an unresolved obligation and a timeout are distinct outcomes. Pending facts can remain in the PSG while the program and platform context are elaborated; a lowering cannot silently choose a representation that needs facts still missing at commitment.

Some premises are established from source, checked library laws or platform declarations. Others can be checked when a value arrives. The proposed JavaScript profile requires generated narrowing at every inbound declared-shape position, including callbacks, and typed interception of host throws and awaited rejections. A check must establish the property it claims, remain valid until the use, and have its own lowering behavior justified. A getter that throws or a buffer that becomes detached is part of that work.

Not every external premise has a small runtime decision procedure. A storage acknowledgment reports a result under the store's durability contract; it does not prove the implementation survives every failure. A finite-time Lyapunov estimate measures a trajectory interval; it does not establish a future global bound. Such facts need explicit assumptions or conditional monitors, with claims scoped to what they establish.

When a sound guard establishes a premise and the relevant implication and lowering are justified, the invariant follows for that use. This is the sense in which the guard constructs a witness. It need not send a proof with the application payload, and it does not make unrelated external assumptions disappear.

## The Typed Exit

The JavaScript profile requires total narrowing: a successful conversion or a `Result` error identifying the failed path, with no partially converted value admitted. That makes a boundary failure ordinary typed control flow. Enumerating those outcomes does not enumerate every reachable program state or prove termination, delivery or recovery.

The contract tradition of [Findler and Felleisen](https://doi.org/10.1145/581478.581484) and [Wadler and Findler](https://doi.org/10.1007/978-3-642-00590-9_1) supplies a useful precedent, especially for higher-order crossings. A blame theorem depends on its language and contract semantics; it is not a blanket claim that every failure belongs to foreign code. Here the engineering goal is to derive checks and their failure provenance from the same obligations that the middle-end tracks, and to preserve that relation through emission.

## Four Instances

These examples share a method while requiring different evidence:

| Premise | Check or evidence | What it establishes |
|---|---|---|
| An inbound value has the declared shape | Generated narrowing under the JavaScript boundary rules | A converted value or a typed rejection; foreign behavior beyond the conversion remains external |
| A BAREWire access follows the endpoint contract | Build/session contract agreement plus codec bounds and encoding checks | A valid access and decoded value under that agreed contract; no type/schema/dimension tag is required in the final payload |
| An anchor is durable before its window advances | Acknowledgment under a named store and failure-domain contract | The declared acknowledgment condition; continued durability depends on the external contract |
| Dynamics remain inside an admitted numerical envelope | Checked bounds where available, plus a scoped runtime monitor | The proved interval or monitored observation; a future trajectory claim needs further assumptions |

The BAREWire row is deliberately split. A union case index selects a case within an agreed contract, and a presence bit selects an optional value. Neither identifies a schema or proves dimensional agreement. The metadata can remain in PSG/codata through the reasoning that needs it and disappear from the final payload once the affected obligations are fulfilled.

The [worked example and acceptance sequence](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) show how to connect a declared extent to an operation, a query and an emitted artifact. The current BAREWire JavaScript gate exercises codecs and emits declaration-level SMT queries; this wider preservation chain is work to build, not evidence already supplied by those tests.

## The Residency Rule

An anchor is only as useful as the failures it survives. Actor memory, a supervising process and an external store offer different durability domains. A useful proposed placement rule is that the anchor's durability domain must cover the failure domain against which reversal is promised. Reversal within an actor's lifetime may use its own frame; recovery after a restart needs an anchor that survives that restart. Cross-session recovery also needs compatible code, contracts and a recovery protocol.

Akka.NET's persistence plugin model is a useful precedent for separating storage destination from policy. BAREWire can encode the checkpoint under an agreed contract. It does not supply the store's durability, exactly-once effects or a portable continuation merely by preserving bytes. The compiler would need declared failure domains and effect semantics to derive placement and cadence; unknown external effects remain explicit.

## Guided Design

This information belongs where a developer can use it. The [representation display](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#representation-fidelity-across-substrates) could show admissible carriers, unresolved premises and justified transfer bounds. The same interface could show which boundary checks are generated, what recovery promises depend on a store, and which observations would trigger a tighter checkpoint policy.

The aim for [Lattice](/docs/tooling/leveling-up-with-lattice/) is guided design with traceable evidence: explain an admissible choice and its costs, show a pending obligation while its context is incomplete, and locate an error when a required premise cannot be established. It should be clear which figures were computed, which facts were declared and which behavior remains a host assumption.
