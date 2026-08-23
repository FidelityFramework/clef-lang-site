---
title: "Constructed Witnesses"
linkTitle: "Constructed Witnesses"
description: "Why a compiler committed to static discharge still emits runtime checks at open boundaries, and why those checks strengthen the proof story: guards discharge transported premises, and the banked implication does the rest."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Architecture", "Verification", "Type Systems", "Design"]
weight: 25
---

This section of the docs holds two postures that deserve to be stated as one. [Design-Time Specification](/docs/design/javascript-targeting/design-time-spec-runtime-reliability/) describes a runtime with nothing to verify, because every property was discharged before emission. The [boundary map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/) describes seven edges where the compiler generates runtime checks, and the [ledger lowering](/docs/design/javascript-targeting/the-ledger-lowering/) adds journal writes and acknowledgments to the list. A reader could take the generated checks as an admission that the static story falls short at the edge. The opposite is true, and this page states the principle that makes both postures one discipline.

## The Shape of an Obligation

Every obligation the middle-end discharges has the form of an implication: premises about the world entail an invariant about behavior. Dimensional consistency, escape classification, schema agreement, and the reversibility of a computation each decompose this way, and the implication itself is always discharged statically, witnessed on the Program Semantic Graph, and carried through lowering.

What varies is the disposition of the premises. In the closed world, native code exchanging BAREWire frames with native code, the premises are themselves discharged statically: the layout is the layout the compiler emitted, the lifetime is the lifetime the coeffect proved, and nothing remains for the runtime to establish. This is the case Design-Time Specification describes, and on that path the guard does not exist because it would have no work. At an open boundary the situation differs in one respect only: some premises are about things no compiler can settle. The contents of a V8 value that arrived from a client. The durability of a write to a store the program does not own. The divergence rate of a physical system a simulation declares. These premises are transported across the boundary as carried proof obligations, and for each one the compiler constructs its discharge procedure: a small, decidable check over what the runtime can observe, derived from the same PSG facts the static half of the proof consumed, emitted as code beside the program it protects.

When the procedure passes, the premise is established, the implication was banked at compile time, and the invariant follows. The guard never tests the invariant, which is typically not expressible at runtime at all. It tests the premises, and its successful run is a runtime witness in the same sense the middle-end's discharges are compile-time ones. Crossing the boundary completes the proof: the guard supplies the premises the static half could not reach, and the banked implication does the rest.

## The Typed Exit

A guard has two outcomes, and both were enumerated before emission. On the passing side, execution proceeds under the proven invariant. On the failing side, control passes to a case the compiler generated together with the guard: a `Result` carrying which premise failed and where, or an unresolved fractional obligation surfacing through supervision. The failure path is ordinary typed code. This preserves the framework's integrity claim in its strict form: every state reachable at runtime, success or failure, was enumerated at compile time, and the guard's job is to keep boundary failures inside the enumeration.

The standing art for checks at typed boundaries is the contract discipline of [Findler and Felleisen](https://doi.org/10.1145/581478.581484), with [Wadler and Findler's](https://doi.org/10.1007/978-3-642-00590-9_1) blame theorem locating every contract failure on the untyped side of the boundary. Two refinements distinguish the construction here. The contract is derived from an implication the middle-end already discharged, with nothing declared by hand, so the check and the proof cannot drift apart. And blame is recorded structurally, in the frame the failure produces, as the specific premise in place of a boundary label.

## Four Instances

The instances already documented across this section are this one discipline applied to four kinds of premise.

| Premise | Constructed guard | Runtime witness | Documented in |
|---|---|---|---|
| An inbound value has the declared shape | schema-directed narrowing validator | a typed record, or `Error` with the failed path | [Boundary map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/) |
| Sender and receiver derive from the same type | BAREWire tag and layout identity | an accepted frame, or transport-level rejection | [JSIR back-end](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#schema-identity-as-a-proxy-for-dimensional-agreement) |
| An anchor is durable before its window advances | journal write paired with acknowledgment | the acknowledged frame | [Ledger lowering](/docs/design/javascript-targeting/the-ledger-lowering/) |
| The dynamics stay within the declared bound | running finite-time Lyapunov estimate | cadence held, or a typed tightening | [Lyapunov window](/docs/design/types/lyapunov-window/) |

The first two rows guard values crossing into the program. The third and fourth guard premises of the reversal proof, which is why the ledger lowering and the Lyapunov window belong to the same section of the design as the narrowing validators: reversibility at an open boundary is one more invariant whose premises partly live outside the compiler's reach.

## The Residency Rule

The third row generalizes into a placement discipline. An anchor is only as good as the failures it survives, and the storage a deployment offers forms a gradient of durability domains: the actor's own memory, a supervising sentinel that outlives the actor, an external store reached through a persistence connection. We imagine the required class being derived from one rule: the anchor's durability domain must dominate the failure domain of whatever may demand the reversal. A reversal demanded within an actor's lifetime is served from its own frame. A reversal that must survive the actor's restart requires the sentinel. A reversal demanded across processes, sessions, or substrates requires the store. The supervision hierarchy the program already declares carries exactly the information this rule consumes.

The sympathetic art is Akka.NET's persistence layer, where the journal and snapshot store are held behind a plugin interface and the sink is transparent to the actor. That separation of destination from policy is the right cut. The construction here types the sink, BAREWire frames in place of opaque blobs, and derives the policy, cadence and residency class, from the proof the reversal invariant depends on, leaving the destination as the deployment-time selection the plugin model already handles well.

## Guided Design

Everything this page derives is information a developer should see before anything deploys, and the natural surface is the same one our DTS diagnostics already use. The [representation fidelity display](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#representation-fidelity-across-substrates) shows precision per target at design time. The guard discipline extends that display with the facts it computes: the residue sites of a flow and their frame rate, the checkpoint cadence per target with its Lyapunov window where one is declared, and the residency class each reversal demand requires. Surfaced through the language server into [Lattice](/docs/tooling/leveling-up-with-lattice/), the display becomes guided design in a specific sense: the compiler prices the admissible configurations, and the developer chooses among them with the entropy bill, the storage class, and the per-substrate windows in view. The choice stays a design decision, made with the costs computed instead of guessed.
