---
title: "Adapting Inference on a Gradient"
linkTitle: "Adapting Inference on a Gradient"
description: "How an organization moves from a rented model to a purpose-built one in staged substitutions behind a stable interface"
date: 2026-06-08T00:07:00+00:00
weight: 8
authors:
  - SpeakEZ
tags: ["machine-learning", "adaptive-domain-models", "data-sovereignty", "compilation", "cloudflare"]
draft: false
---

Clef targets LLVM for CPU and native code today, and it does so deliberately and provisionally. LLVM is the pragmatic backend: mature, available now, and the fastest route to a real running artifact. It also carries baggage, an instruction-selection model and a set of assumptions Clef would not choose if it were designing the lowering from scratch, and the framework's longer arc contemplates novel backends that shed that baggage where doing so buys enough to justify the work. The point is not that LLVM is wrong. It is that LLVM is a *stage*: load-bearing now, deliberately temporary, used behind a stable interface so that the backend can be substituted underneath without disturbing everything above it.

The language model in an ADM constellation follows the same strategy exactly. A standing language model, whether a local open-weights model on your own GPU or CPU, or a frontier model reached through an API, is the LLVM of this story. It is the pragmatic backend: it works now, it gets you a running constellation today, and it carries baggage, opaque weights with no formal status, a parameter space that holds capability you do not need, and, on the API path, your data leaving the building. Each rung of the migration sheds more of that baggage, and the fully built model the later rungs reach is the novel backend, clean of all of it. The interface between the language node and the constellation is the stable boundary, designed once at the first rung and held constant while the model behind it matures.

Four rungs, one interface fixed across all of them, and two quantities traced up the ladder: latency, the speed to first useful token, and velocity, the compounding flywheel that each rung hands to the next. Underneath both runs the question the audience for this technology actually asks first, which is where their data goes.

## The interface is the invariant

Before the rungs, the boundary they share. The language node's job in the constellation is to take unstructured intent and route it to typed domain models that can satisfy it with guarantees. That routing is a tool-call surface, and it is defined in Clef against the domain models, not against any particular language model. The same surface is satisfied by a frontier API, a local open-weights model, a distilled model, or a from-scratch built model. Designing it first, at the rung where the model is least under your control, is what makes the later substitutions cheap.

The Clef in this article is illustrative of the idiom rather than a finalized API surface.

```fsharp
// The stable interface. Defined once, against the domain models,
// independent of which language model sits behind it. Every rung below
// satisfies this same surface; only the implementation of `propose` changes.

/// A typed domain model in the constellation, correct by construction in its domain.
type DomainModel<'Request, 'Response> =
    { name        : string
      /// The request type carries the domain's structure; an ill-formed
      /// request does not type-check and never reaches the model.
      invoke      : 'Request -> Result<'Response, DomainError>
      /// BAREWire schema for zero-copy interchange across the wire.
      wire        : BareSchema<'Request, 'Response> }

/// The language node's contract. It proposes Clef under the grammar; the
/// Olivier domain actors receive the request over BAREWire. The model behind
/// `propose` is the variable.
type LanguageNode =
    { /// Propose a Clef request for a goal. Grammar-constrained at decode time.
      propose : Goal -> Async<ClefRequest>
      /// Revise against a dimensional mismatch surfaced at the message fabric.
      revise  : Goal -> ClefRequest -> Mismatch -> Async<ClefRequest> }

/// The routing loop is identical at every rung. Propose under grammar, send the
/// request to the Olivier actors over BAREWire, revise on a contract mismatch.
/// The grammar holds syntax at decode; the BAREWire contract, known to both
/// sides by construction, holds dimensional fit; the loop works the same
/// whichever model `node` wraps.
let rec satisfy (node: LanguageNode) (registry: DomainActor list) (goal: Goal) =
    async {
        let! request = node.propose goal
        match registry |> send request with          // BAREWire structured contract
        | Mismatch m ->
            let! _ = node.revise goal request m
            return! satisfy node registry goal       // bounded retry elided
        | Satisfied result ->
            // The request reached an actor whose contract it honored;
            // each call is correct by construction.
            return result
    }
```

The contract at the boundary is what makes this surface stable. The grammar-constrained decoder holds the node's proposal to syntactically valid Clef regardless of which model proposes it. Across the boundary the request reaches the Olivier domain actors over BAREWire, a fixed-layout contract both ends were built to read, carrying its meaning in the layout itself. Structured binary records have moved between programs this way since long before machine learning, and BAREWire carries that settled discipline to the inter-actor boundary, with the domain's dimensional annotations in the contract, so a mismatch surfaces at the message fabric. The language model may be a rented black box or one you built; the contract around it holds either way, and that is the whole reason the substitution strategy works.

## Rung one: adapt what exists now

The first rung wraps a standing model and changes nothing inside it. All of the constraint lives outside the weights, in the interface above. This rung runs today, and it comes in two deployment variants that satisfy the identical interface.

The API variant reaches a frontier or hosted model through a gateway. Using the Cloudflare AI Gateway, through the Fidelity.CloudEdge bindings, the gateway gives caching, rate limiting, and observability over the model call, and the domain models run as Workers or Durable Objects reached over BAREWire on a websocket.

```fsharp
// Rung one, API variant. A hosted model behind the Cloudflare AI Gateway.
// The model is opaque; the grammar and Composer do all the constraining.

let apiNode (gateway: AiGateway) (grammar: ClefGrammar) : LanguageNode =
    { propose = fun goal -> async {
        // Grammar-constrained decoding is requested of the gateway where the
        // upstream supports it; where it does not, the grammar is enforced
        // client-side as a validating filter over streamed tokens.
        let request =
            AiGateway.chat gateway
            |> AiGateway.model "open-weights-instruct"
            |> AiGateway.grammar (Grammar.toGbnf grammar)
            |> AiGateway.prompt (Prompt.forGoal goal)
        let! raw = AiGateway.send request
        return ClefSource.ofConstrainedDecode raw }

      revise = fun goal source diags -> async {
        let request =
            AiGateway.chat gateway
            |> AiGateway.model "open-weights-instruct"
            |> AiGateway.grammar (Grammar.toGbnf grammar)
            |> AiGateway.prompt (Prompt.forRevision goal source diags)
        let! raw = AiGateway.send request
        return ClefSource.ofConstrainedDecode raw } }

// Domain models exposed as Workers, reached over BAREWire on a websocket.
// BAREWire carries the structured request with no JSON round-trip and no runtime tags.
let remoteDomainModel (ws: WebSocket) (schema: BareSchema<'Req,'Resp>) : DomainModel<'Req,'Resp> =
    { name   = schema.name
      wire   = schema
      invoke = fun req ->
        req
        |> BareWire.encode schema          // zero-copy, schema-directed
        |> WebSocket.requestResponse ws
        |> Result.map (BareWire.decode schema) }
```

The local variant swaps only the `propose` implementation. The model runs on your own GPU or CPU through a local inference server, and the domain models are in-process calls over BAREWire's shared-memory transport rather than a websocket. The interface above does not change.

```fsharp
// Rung one, local variant. Same interface, different backend.
// The model runs in-house; nothing leaves the machine.

let localNode (engine: LocalInference) (grammar: ClefGrammar) : LanguageNode =
    { propose = fun goal -> async {
        // Local engines (llama.cpp-family, vLLM) enforce the grammar in the
        // sampler directly, so syntactic validity is guaranteed at the source.
        let! raw =
            engine
            |> LocalInference.withGrammar (Grammar.toGbnf grammar)
            |> LocalInference.complete (Prompt.forGoal goal)
        return ClefSource.ofConstrainedDecode raw }

      revise = fun goal source diags -> async {
        let! raw =
            engine
            |> LocalInference.withGrammar (Grammar.toGbnf grammar)
            |> LocalInference.complete (Prompt.forRevision goal source diags)
        return ClefSource.ofConstrainedDecode raw } }

// In-process domain model over BAREWire shared memory: no network, no copy.
let localDomainModel (schema: BareSchema<'Req,'Resp>) (impl: 'Req -> Result<'Resp,DomainError>) =
    { name = schema.name; wire = schema; invoke = impl }
```

The worked example, end to end, is the same regardless of variant. A goal arrives, the node proposes grammar-valid Clef, Composer elaborates it, and the elaborated program dispatches structured calls to the domain models, which answer with guarantees that come from the domain models rather than the language node:

```fsharp
// One worked request, identical across both deployment variants.
// A business goal that touches a domain where correctness must be guaranteed.

let registry =
    [ localDomainModel financeSchema  DimensionalFinance.invoke   // domain, exact
      remoteDomainModel ws kinematicsSchema ]                     // domain, exact

let goal = Goal.ofText "Price this FX-denominated option book and flag any \
                        position whose currency dimensions are inconsistent."

// The language node proposes Clef that calls the finance domain model.
// If it proposes a currency-mismatched call, the finance model's request type
// rejects it: the dimensional error is unrepresentable, not merely unlikely.
let result = satisfy node registry goal |> Async.RunSynchronously
```

This is the rung that establishes the data-sovereignty position, and the two variants are precisely the two sovereignty postures. The API variant is the weakest: your prompts, and whatever context they carry, transit a third party, and even with a gateway in front the data has left the building. It is the right rung to start on for the same reason LLVM is the right first backend: it works now and proves the constellation. The local variant keeps everything in-house from the first day, at the cost of running the model yourself. Both satisfy the same interface, so an organization can begin on the API variant to validate the constellation and move to local without touching the routing, the tool surface, or the domain models. The migration that matters most for sovereignty is available at the very first rung, and it is a one-line substitution.

## Rung two: fine-tune a standing model

The second rung stops treating the model as fixed. The two-pass tuning of the [building article]({{< ref "building-the-model" >}}) is applied to a standing open-weights model: pass one damps the imperative and dynamically-typed accent toward functional idiom, pass two instills Clef and the tool reflexes against this very interface. The architecture is still conventional, but the model now proposes better Clef on the first attempt and revises more competently, which shows up directly in the two tracked quantities.

Latency improves because fewer propose-check-revise round trips are needed per satisfied goal. A model fluent in Clef and trained against the tool surface proposes elaborable programs more often, so the Composer-rejection loop runs fewer times, and the speed to a *useful* token, one that survives elaboration and dispatches a real domain call, drops even though the per-token speed of the model is unchanged. Velocity improves because the fine-tuning is low-rank adaptation, a swappable adapter over the base, so the organization can iterate the adapter as its own domain models and conventions evolve without retraining anything. This rung is where the flywheel starts turning: each adapter revision is cheap, and each makes the constellation fit the organization's work more closely.

Sovereignty improves on this rung as well. A fine-tuned open-weights model is yours, and the adapter encodes your conventions and your domain vocabulary. That adapter is trained on your data and stays in your control; it is not a contribution to a hyperscaler's next base model. The fine-tuning itself can run in-house or in a sovereign environment, and the artifact, the adapter, never leaves.

## Rung three: distill toward the edge

The third rung compresses the fine-tuned model toward a size that runs resident on modest hardware, and it is where the constellation does something a conventional distillation cannot. Ordinary distillation trains a small student to imitate a large teacher's output distribution. Here the domain models supply a *verifiable* signal the teacher's raw output does not: a proposed program either elaborates and dispatches correct structured calls or it does not, so the distillation target is intended to be filtered to trajectories that satisfied the constellation, with Composer as the judge.

The techniques, in outline. Sequence-level distillation on the propose-check-revise trajectories, keeping only the trajectories that terminated in an elaborable program that dispatched correct domain calls, so the student learns the routing behavior that worked rather than the teacher's unfiltered habits. On-policy distillation where the student proposes, Composer and the domain models judge, and the student is corrected toward the judged-correct proposal, which keeps the student's learning grounded in the same acceptance test that bounds it at inference. And low-rank adaptation throughout, with the [forward-mode path]({{< ref "forward-mode-and-adaptation" >}}) making the gradient cheap over the adapter's small rank, so distillation is a fast inner loop rather than a major training run.

Latency is designed to take its largest single step here. A distilled model resident on a CPU or a modest GPU has a far shorter time to first token than an API round trip, because the network hop is gone and the model is small, and the constellation's habit of offloading the parts that need guarantees to fast domain models means the small model is asked to do less. The speed to a useful token is now bounded by local inference plus a few BAREWire calls, not by a hosted model's queue and the public internet. Velocity compounds because the verifiable distillation signal means each cycle of fine-tune-then-distill is intended to produce a better small model from a better teacher, and the domain models that supply the signal are themselves improving, so the flywheel turns faster at every revolution.

Sovereignty is near-complete on this rung. The resident distilled model runs entirely in-house, on hardware the organization controls, with no inference traffic leaving the building. The teacher it was distilled from may have been a standing model, but the deployed artifact is small, owned, and local.

## Rung four: build from the ground up

The final rung is the novel backend. A model built from scratch to be structurally compatible with the ADM constellation: the [derived CRATE backbone]({{< ref "architecture-and-arithmetic" >}}) on the [b-posit and quire substrate]({{< ref "architecture-and-arithmetic" >}}), trained through the [multi-tangent forward-mode path]({{< ref "forward-mode-and-adaptation" >}}), with the [reversible cores]({{< ref "reversible-cores" >}}) where the architecture admits them. This is the model the rest of the section described, and the migration path is what makes building it a justified investment rather than a leap of faith, because by this rung the organization already has a working constellation, a verifiable distillation pipeline, and a fine-tuning flywheel; the built model slots in behind the same interface and replaces the last rented component.

An open, reproducible build methodology is the right vehicle. The OLMo approach, fully open training data, code, and checkpoints, is the model to follow, because a from-scratch model meant to be a sovereign asset must be reproducible and auditable by the organization that owns it. A model whose provenance is a signed record of data, recipe, and checkpoints, the version-record discipline the [building article]({{< ref "building-the-model" >}}) carries over from [ADM](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}), is an asset an organization can stand behind, which an opaque downloaded checkpoint is not.

This rung is where latency and velocity reach the regime the constellation is built for. The model is small because it is derived rather than over-parameterized, it runs on the b-posit substrate the rest of the framework uses, and it is trained by a forward-mode loop that makes adaptation cheap. It is also sub-quadratic in context, by the typed-generator construction the [constellation article]({{< ref "the-constellation" >}}) describes, so its cost grows linearly rather than quadratically as the context lengthens, which matters most exactly where a business workload has long documents or long histories to attend to. Small, sparse, and sub-quadratic compound: time to first useful token would be local inference on a compact model whose attention cost scales gently with context and which offloads aggressively to domain models. And the flywheel is fully closed: the built model is adapted by the same cheap forward-mode loop, distilled by the same verifiable signal, and improved alongside the domain models it routes to, with every part of the loop in-house. This is the velocity that justifies the build. A rented model gives capability; a built model gives a compounding asset, where each improvement to the domain models, the adapters, and the base reinforces the others.

Sovereignty is total. The data never leaves, the model is owned and reproducible, the training and adaptation run in-house, and nothing about the organization's work is mined for anyone else's model. For the business audience this technology is for, the audience that needs to keep its data in-house and out of a hyperscaler's training set, this rung is the destination the first rung was already pointed at.

## The ladder, read as one strategy

The four rungs are not alternatives; they are stages of one substitution, the same shape as the compiler's path from LLVM to novel backends. Commit to a working artifact now, on the pragmatic backend, behind a stable interface. Then migrate the substrate underneath that interface as the value justifies it: damp the accent and instill the idiom, distill toward the edge against a verifiable signal, and finally build the structurally-compatible model that carries none of the rented backend's baggage. At every rung the interface holds, the domain models stay correct by construction, and the data-sovereignty posture strengthens. Latency falls as the model moves in-house and shrinks, and velocity rises as each rung makes the next cheaper, until the built model closes the flywheel.

An organization does not have to choose its rung in advance. It starts where it can start today, often the API variant of rung one, and climbs as far as the value warrants, with each step a substitution behind the interface rather than a rebuild. That is the practical claim the whole section rests on: the constellation is something you can run now with what exists, and the path from there to the fully built, fully sovereign model is a graded migration, not a cliff.

## Open questions

Whether grammar-constrained decoding is available or efficient enough on a given API path, or must be enforced client-side as a validating filter, is a per-provider question that affects the rung-one API variant's cost.

Whether the verifiable distillation signal, trajectories filtered by Composer and the domain models, is rich enough to train a competitive small student, or whether it must be supplemented with conventional distillation, is the central open question of rung three.

Whether the from-scratch model of rung four can be built at a scale an individual organization can afford, or whether it remains a shared-investment artifact that several organizations fund and then adapt sovereignly, is the economic question that decides how widely rung four is reached.

How much of the latency improvement comes from model shrinkage versus from offloading to domain models is measurable, and the split matters, because it tells an organization how much benefit it captures at each rung before reaching the built model.
