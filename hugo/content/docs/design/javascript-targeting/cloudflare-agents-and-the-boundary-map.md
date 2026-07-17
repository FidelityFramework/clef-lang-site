---
title: "Cloudflare Agents and the Boundary Map"
linkTitle: "Cloudflare Agents"
description: "What a complete Clef implementation of Cloudflare's AI-agent surface looks like, and why the agent domain multiplies the value of compiler-generated wire narrowing"
date: 2026-06-28
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design", "Interop"]
weight: 40
---

[JSIR: JavaScript as an MLIR Backend](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) establishes the mechanism: Clef reaches JavaScript through the same MLIR middle-end as every native target, the compiler generates the boundary contract instead of asserting it, and a schema-directed narrowing validator checks inbound runtime values against the declared shape instead of trusting it. That document argues the mechanism in the general case. This one makes it concrete against the most demanding surface Cloudflare currently offers: the [Agents SDK](https://github.com/cloudflare/agents), where an AI agent is a Durable Object with its own SQLite database, AI model bindings, WebSocket channels, scheduling, and workflow orchestration.

This surface exercises the mechanism hardest. A plain Worker has one untrusted boundary, the inbound request. An AI agent has many, and each is a place where a shape can drift between the code that produced it and the code that consumes it, and where the standing toolchains (the TypeScript SDK, or a hypothetical OCaml-via-Melange port) carry a type that is real at design time and gone at runtime. Of the three approaches, generated narrowing is the only one whose safety guarantee extends to every one of those boundaries as the agent surface multiplies them.

We use Cloudflare's TypeScript SDK, and its recent AI orchestration layers, as the yardstick. A complete Clef implementation has three layers.

## Layer 1: Bindings, Generated and Regenerated

Cloudflare's `@cloudflare/workers-types` is large and machine-derived from the `workerd` runtime source; our [Fidelity.CloudEdge](/docs/design/javascript-targeting/) surface already covers 727 runtime types across Workers, Durable Objects, D1, R2, KV, Queues, Workers AI, Vectorize, and Containers. The Agents SDK adds more, and it does so on a pre-1.0 cadence: the surface is experimental, changes month to month, and is not accepting external contributions while it stabilizes. That cadence is the argument for *generated* bindings and against hand-written ones. A hand-authored binding is a declaration that silently rots when the surface shifts under it; a generated binding is regenerated when the surface shifts.

This is the role [Xantham](/docs/design/interop/typescript-binding-via-xantham/) already fills. Xantham ingests TypeScript via the compiler API and emits a target-neutral structural analysis; the Clef binding generator consumes that analysis the same way the F# generator does today. The binding layer for the Agents surface is the Xantham analysis fed to the Clef consumer, regenerated against each Cloudflare release, not a new hand-rolled `.d.ts` parser and not a wall of hand-written externals. The binding-rot problem the agent surface creates by changing monthly is solved upstream, by regeneration, rather than absorbed downstream as maintenance.

Xantham itself rides the TypeScript Compiler API, which is npm-resident, and deployment goes through Cloudflare's own tooling. These are build-time dependencies on Cloudflare's curated surface, not third-party code shipped into the running Worker, and they are unavoidable when the target platform is defined in TypeScript and deployed through npm tooling. The line this draws is deliberate: the binding surface and the deploy path take a bounded, build-time dependency on Cloudflare's own ecosystem; the shipped artifact takes none on anyone's. The [runtime-versus-build-time distinction](/docs/design/javascript-targeting/from-fable-to-jsir/) is drawn in full in the back-end transition document.

## Layer 2: The Agent as a Generated Durable Object

A Cloudflare Agent is a Durable Object. The class hierarchy is literally `DurableObject` → `Server` → `Agent`, and each agent instance is addressed by ID, owns isolated state, and holds its own embedded SQLite database accessed through `this.sql`. The instance-isolation requirement this imposes on the emission model, and how a Clef actor lowers to a per-instance Durable Object class rather than a single shared script, is the subject of the [Embodying a Durable Object](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#embodying-a-durable-object-per-instance-state) section of the JSIR document. The same embodiment carries to the Agent: a Clef actor lowers to the Agent class, its state cell instantiated per instance, its lifecycle methods (`onRequest`, `onConnect`, `onMessage`, `onStateChanged`, `schedule`, the `@callable` RPC methods) generated from the actor definition.

What the agent surface adds beyond a plain Durable Object is *boundaries*. Cloudflare's own SDK lets a developer write `this.sql<User>\`SELECT ...\`` and `env.AI.run(model, payload)`, and in TypeScript the `<User>` type argument and the inferred AI response type are both compile-time assertions that erase at runtime. The SQLite cursor returns whatever the database returns; the AI binding returns whatever the model emits. Neither is checked against the declared shape. This is the same erasure the JSIR document identifies at the request boundary, now repeated at the storage boundary and the inference boundary.

## Layer 3: The Boundary Map

Reaching this surface is not the contribution; Melange reaches it too, with cleaner module output, as the [Precedents discussion](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#the-precedents) covers. The contribution is that a Clef implementation generates and checks the boundary contract at *every* untrusted edge of the surface, where the standing approach asserts a type and erases it. Those edges form a map:

| Boundary | What crosses it | Drift risk | The generated check |
|---|---|---|---|
| Inbound request (`onRequest`) | client JSON | client sends a malformed or version-drifted body | `request.Json() : T` narrows to `Result<T, DeserializationError>` |
| WebSocket message (`onMessage`) | streamed frames | sender and receiver disagree on frame shape | BAREWire schema identity; tag mismatch is rejected at transport |
| AI inference (`env.AI.run`) | model output JSON | the model alters its output or tool-calling scheme | the response is a narrowing site, checked before it touches actor state |
| SQLite rows (`this.sql`) | column values from storage | schema migration or query change shifts row shape | the cursor rows narrow to the declared Clef record |
| State sync (`setState` / `onStateChanged`) | persisted and broadcast state | a peer or a prior version wrote an incompatible shape | the state shape is the actor's own type; reads narrow on load |
| RPC (`@callable`) | cross-actor method arguments | a caller compiled against a different definition | argument shapes derive from the shared schema |
| Workflow steps (`runWorkflow`) | step input and output payloads | a step's contract drifts from its caller | step payloads narrow at the step boundary |

Every row is a place where the TypeScript SDK carries an erased type and a hypothetical Melange port carries an unchecked `external`. The AI rows are the sharpest: a model's output is the most volatile value the agent handles, produced by a process that can change its emission shape between calls, and consumed by code that mutates agent state on the strength of it. The standing approach trusts that value. Generated narrowing parses and rejects it at the boundary, the same way it treats an untrusted inbound request. On the agent surface the cost of erasure is highest and the benefit of generation largest, which is why it maps the surface edge by edge.

## Why the Sounding Board Pointed Here

A useful way to find this map is to ask what a complete implementation in the closest ML-family ecosystem would look like, and to watch where it reaches for an escape hatch. An OCaml implementation built on Melange produces clean Agent classes and propagates types beautifully at design time, and then, at exactly the boundaries above, it binds `env.AI` and reads the model response and parses the SQL rows through unchecked `external` declarations or untyped coercions. The escape hatches cluster precisely on the AI response, the SQL rows, and the inbound payload. That clustering is the map. The boundaries where the ML-family ecosystem must drop to an assertion are the boundaries where compiler-generated narrowing earns its keep, and on the AI-agent surface there are more of them than anywhere else Cloudflare currently runs code.

## Status

The Agents SDK is pre-1.0 and changes frequently; the surface described here is a moving target, which is itself part of the argument for generated bindings. The Clef-via-JSIR back-end, the actor-lifecycle generation, and the per-boundary narrowing generation are design intent and upcoming work, the natural extension of the mechanism the [JSIR document](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) develops. What is settled is the shape of the target and the map of its boundaries. Building toward that map, with bindings regenerated by Xantham against each Cloudflare release and a generated narrowing check at every edge, is the path this investigation set out to chart.
