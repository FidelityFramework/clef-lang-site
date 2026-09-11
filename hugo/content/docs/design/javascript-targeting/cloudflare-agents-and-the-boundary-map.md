---
title: "Cloudflare Agents and the Boundary Map"
linkTitle: "Cloudflare Agents"
description: "What a complete Clef implementation of Cloudflare's AI-agent surface looks like, and why the agent domain multiplies the value of compiler-generated wire narrowing"
date: 2026-06-28
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design", "Interop"]
weight: 40
---

[JSIR: JavaScript as an MLIR Backend](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) opens a path to intelligent agents whose Durable Objects hold persistent storage, call model bindings, stream over WebSockets, schedule work, and coordinate workflows. This page applies that architecture to the [Cloudflare Agents SDK](https://github.com/cloudflare/agents): generated boundaries would let Clef carry its declarations and obligations through each interaction. The Composer JavaScript backend and lifecycle generation described here are proposed work.

Agents expose many boundaries: requests, storage, model output, RPC, and lifecycle events. TypeScript and Melange applications can validate these values using generated or hand-written validators. Clef's proposed contribution is to derive and track such checks systematically from shared declarations and obligations, rather than to make runtime validation uniquely possible.

We use Cloudflare's TypeScript SDK, and its recent AI orchestration layers, as the yardstick. A complete Clef implementation has three layers.

## Layer 1: Bindings, Generated and Regenerated

Cloudflare's runtime declarations and wrapper SDKs evolve independently. Bindings should name the exact package version and coverage inventory used for generation, rather than carry a timeless type count or release-status claim. Regeneration exposes declaration changes; implementation changes under stable signatures need separate analysis and tests.

This is the role [Xantham](/docs/design/interop/typescript-binding-via-xantham/) already fills. Xantham ingests TypeScript via the compiler API and emits a target-neutral structural analysis; the Clef binding generator consumes that analysis the same way the F# generator does today. The binding layer for the Agents surface is the Xantham analysis fed to the Clef consumer, regenerated against each Cloudflare release, not a new hand-rolled `.d.ts` parser and not a wall of hand-written externals. The binding-rot problem the agent surface creates by changing monthly is solved upstream, by regeneration, rather than absorbed downstream as maintenance.

Xantham uses the TypeScript compiler API to analyze declarations. Build and deployment tools are separate choices from the Worker's runtime dependencies. Runtime declaration packages describe workerd APIs, while wrapper SDKs may ship JavaScript in the bundle. Invoking tools without an npm launcher does not remove their libraries, and generating bindings does not absorb the SDK implementation. The [fully informed binding design](/docs/design/javascript-targeting/fully-informed-bindings/) distinguishes these categories.

## Layer 2: The Agent as a Generated Durable Object

A Cloudflare Agent is a Durable Object. The class hierarchy is literally `DurableObject` → `Server` → `Agent`, and each agent instance is addressed by ID, owns isolated state, and holds its own embedded SQLite database accessed through `this.sql`. The instance-isolation requirement this imposes on the emission model, and how a Clef actor lowers to a per-instance Durable Object class rather than a single shared script, is the subject of the [Embodying a Durable Object](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#embodying-a-durable-object-per-instance-state) section of the JSIR document. The same embodiment extends to the Agent: a Clef actor lowers to the Agent class, its state cell instantiated per instance, its lifecycle methods (`onRequest`, `onConnect`, `onMessage`, `onStateChanged`, `schedule`, the `@callable` RPC methods) generated from the actor definition.

What the agent surface adds beyond a plain Durable Object is *boundaries*. Cloudflare's own SDK lets a developer write `this.sql<User>\`SELECT ...\`` and `env.AI.run(model, payload)`, and in TypeScript the `<User>` type argument and the inferred AI response type are both compile-time assertions that erase at runtime. The SQLite cursor returns whatever the database returns; the AI binding returns whatever the model emits. Neither is checked against the declared shape. This is the same erasure the JSIR document identifies at the request boundary, now repeated at the storage boundary and the inference boundary.

## Layer 3: The Boundary Map

The intended contribution is systematic generation and checking of boundary contracts. An erased type annotation is insufficient on its own in any of these toolchains; TypeScript and Melange can also pair declarations with runtime validators. The proposed Clef boundary map is:

| Boundary | What crosses it | Drift risk | The generated check |
|---|---|---|---|
| Inbound request (`onRequest`) | client JSON | client sends a malformed or version-drifted body | `request.Json() : T` narrows to `Result<T, DeserializationError>` |
| WebSocket message (`onMessage`) | streamed frames | sender and receiver disagree on frame shape | Declared schema agreement plus framing/bounds checks; case-index rejection alone does not establish schema identity |
| AI inference (`env.AI.run`) | model output JSON | the model alters its output or tool-calling scheme | the response is a narrowing site, checked before it touches actor state |
| SQLite rows (`this.sql`) | column values from storage | schema migration or query change shifts row shape | the cursor rows narrow to the declared Clef record, SQL NULL arriving as `Option` cases |
| State sync (`setState` / `onStateChanged`) | persisted and broadcast state | a peer or a prior version wrote an incompatible shape | the state shape is the actor's own type; reads narrow on load |
| RPC (`@callable`) | cross-actor method arguments | a caller compiled against a different definition | argument shapes derive from the shared schema |
| Workflow steps (`runWorkflow`) | step input and output payloads | a step's contract drifts from its caller | step payloads narrow at the step boundary |

Each row needs either discharged closed-world premises or runtime evidence for the observations the compiler cannot establish. An AI response is a particularly clear open boundary: it must be narrowed before use. A TypeScript signature can supply the requested shape, but proving a JavaScript-generated SMT model of that shape does not prove that the model service or SDK implements it. The [worked acceptance path](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) separates these layers.

## Why the Sounding Board Pointed Here

A useful sounding-board exercise is to map the same application in an ML-family ecosystem such as Melange. Foreign declarations identify where assumptions enter; validators and explicit results can enforce open-boundary checks there too. Clef aims to generate those checks and preserve their obligations through lowering, making the map systematic and reviewable.

## Status

Boundary shape checks are one part of the actor contract. [Proof Preservation Across Actors and Workflows](../proof-preservation-across-actors-and-workflows/) adds logical job and actor-incarnation correspondence, validity across suspension, permitted numerical merges, and durable acceptance of retried contributions. A well-shaped reply does not establish those behavioral properties. [Carrying Proofs into JavaScript](/blog/carrying-proofs-into-javascript/) develops the application-facing motivation.

The SDK surface must be reviewed against pinned releases. Clef-via-JSIR emission, actor-lifecycle generation, and per-boundary narrowing are design intent. The next evidence is a bounded, reproducible contract-to-artifact example, followed by bindings and checks generated for explicitly inventoried SDK entry points.
