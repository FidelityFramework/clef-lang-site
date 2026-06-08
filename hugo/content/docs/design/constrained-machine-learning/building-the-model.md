---
title: "Building the Constrained Language Model"
linkTitle: "Building the Model"
description: "Two tuning passes, a damping taxonomy that protects the compiler's own output, and a constraint layer that lives outside the weights"
date: 2026-06-08T00:02:00+00:00
weight: 3
authors:
  - SpeakEZ
tags: ["machine-learning", "language-model", "formal-verification", "compilation"]
draft: false
---

> Part of the Constrained Machine Learning section. The three commitments that
> carry a language component when the ADM type scaffold cannot apply are set
> out in [*A Scaffold for Constrained Models*]({{< ref "scaffold-for-constrained-models" >}});
> this article takes up the first of them in practice: how the model is
> actually built and tuned. The status remains speculative. Nothing here is
> built, and the open questions are marked as they arise.

## What "constrained" means, precisely

A model can be constrained in three senses, and the section's argument depends on keeping them apart. It can be constrained in its idiom, having learned to write Clef the way a fluent practitioner would. It can be constrained in its accent, having had the imperative and dynamically-typed reflexes of its training corpus suppressed. And it can be constrained in its output, held to syntactically valid, semantically elaborable Clef by machinery that does not depend on the weights at all. The first two live inside the model and are shaped by tuning. The third lives outside it and is guaranteed by the compiler. A useful constrained model needs all three, and the strongest sense, the one that carries an actual guarantee, is the one outside the weights.

This article works through the build in that order: the two tuning problems that shape idiom and accent, the taxonomy that keeps the damping from harming the framework's own machinery, and the deterministic layer that supplies the guarantee.

## Two tuning problems that must be kept apart

Instilling Clef and removing the inherited accent are different problems, and conflating them defeats both. Instilling is supervised learning: show the model idiomatic Clef and it learns the distribution. Removing the accent is a preference problem, because the base model already holds high probability mass on imperative loops, dynamic typing, exceptions as control flow, null, and class hierarchies, and adding Clef examples competes with that mass without removing it. A single combined objective makes the two gradients work against each other, the instilling gradient pushing toward Clef while the preference gradient pulls away from the accent, and they partially cancel. They belong in distinct passes.

The order is the subtle part. The conventional sequence is competence first, preference last, but that ordering lets the final preference pass re-import the accent it was meant to remove, because the pass that runs last shapes the final distribution. Running damping first introduces its own tension: a preference method suppresses a direction relative to a positive target, and a model with no Clef competence yet has no positive side to point at.

Both tensions resolve by reframing the first pass as generic functionalization rather than Clef-specific instilling. Pass one damps imperative and dynamically-typed reflexes toward the ML-family functional idiom, using F#, OCaml, and Haskell as the positive direction. That target is high-resource and exists independent of Clef, so the preference objective has something concrete to push toward. Pass one shapes the model's computational temperament:

```fsharp
// The temperament pass one instills, stated as the transformations it favors:
//   loops            ~>  folds and recursion
//   null             ~>  Option
//   exceptions       ~>  Result
//   class hierarchy  ~>  discriminated unions
//   in-place mutation ~> persistent structures and explicit regions
```

Pass two then specializes that functional substrate to Clef proper: the language's opinions, its grammar, and its tool calls. Two safeguards hold the order in place. A small slice of the pass-one contrastive pairs is replayed into pass two, so the final pass reinforces the damping instead of eroding it. And pass one is scoped to coding idiom only, leaving the model's tool-call and structured-output protocol intact, because pass two builds the compiler and language-server tool reflexes on top of that protocol.

## The damping taxonomy: do not teach the model to distrust its own compiler

JavaScript is the case that forces precision, and it is where a naive damping scheme does real harm. Clef emits JavaScript: it lowers through Alex to a JavaScript intermediate representation and produces JavaScript whose verification lives in the shared middle-end. A separate path reads TypeScript surfaces to produce Clef externs with witnessing rules. So JavaScript competence is load-bearing in two roles the framework depends on, reading it to bind it, and recognizing well-formed emitted output, and a damping pass that simply suppressed JavaScript would corrupt both.

The landscape therefore has to be stratified into three classes, and every JavaScript example in the contrastive catalog carries one of three labels set by the role it plays:

```fsharp
type JsExampleRole =
    | KeepAndRedirect   // reading JS/TS to bind it; recognizing emitted output
    | Damp              // authoring imperative/dynamic JS as source logic
    | Instill           // routing a JS need to Clef-under-grammar or a typed extern
```

*Keep and redirect* is the comprehension class: reading JavaScript and TypeScript to bind them, understanding what well-formed emitted JavaScript looks like, and the tagged-object heritage that underwrites schema-directed narrowing. The binding pipeline and the JavaScript backend both consume this competence, and it must survive.

*Damp* is the authoring-reflex class: writing imperative or dynamic JavaScript and Python as source logic, reaching for null and in-place mutation and exceptions as control flow, hand-rolling ad-hoc JSON shapes, and the supply-chain reflex of reaching for a package. These are the accents.

*Instill* is the routing class: the model learns that a JavaScript need is answered by authoring Clef under the grammar and letting the backend emit, or by binding a TypeScript surface into Clef externs. At interop boundaries it reaches for schema-directed narrowing returning Result, Option for absence, and typed handles, with the closed type system holding inside Clef proper, and wire interchange going through BAREWire.

The discriminating question for every example is whether the JavaScript is authored as logic, emitted as a target, or read as a surface to bind. Labeling target-side or boundary-side JavaScript as an accent would teach the model to distrust its own compiler's output and its own binding inputs, which is the precise opposite of the goal. This taxonomy is the part of the build most specific to a compiler-aware model, and it has no analog in general code tuning.

## The constraint layer, outside the weights

Everything above shapes what the model prefers. None of it guarantees what the model emits, and for a language model none of it can. The guarantee comes from a deterministic layer that sits outside the weights entirely, exactly as the scaffold article described.

A grammar-constrained decoder, driven by an EBNF grammar derived from Clef's own grammar, would hold the sampler to syntactically valid Clef regardless of the model's habits. The grammar would guarantee syntax; tuning would shape idiom; preference tuning would remove the accent; and the labor is split cleanly across the three. Composer, the Clef compiler, then extends the guard in two distinct roles:

```fsharp
// Role one: Composer as decoding filter. A sample that does not elaborate
// is rejected before it is ever returned. The compiler is the acceptance test.
let accept (sample: ClefSource) : Result<Program, Diagnostics> =
    Composer.elaborate sample

// Role two: Composer as tool call. The model invokes it, reads diagnostics,
// and revises. This is the propose-check-revise loop pass two trains.
let rec authorUnderCheck (model: Model) (goal: Spec) (attempt: ClefSource) : Program =
    match Composer.elaborate attempt with
    | Ok program  -> program
    | Error diags -> authorUnderCheck model goal (model.revise goal attempt diags)
```

The second role is what pass two trains: the propose-check-revise reflex, on trajectories where the grammar guarantees a syntactically valid proposal and the compiler or language server supplies the semantic verdict the model acts on. This is the agentic extension of compiler-as-constraint, and it lands on a model whose imperative accent is already gone, so the revisions it proposes are already in the right idiom. The constellation article returns to this loop as the mechanism by which the language component is bounded by the typed domain models around it.

## Where the model runs, and the honest friction

The deployment target is CPU, which sets what the tuning operates on. Two routes reach it. The dense-small-then-quantize route takes a one-to-three-billion-parameter code-capable model and quantizes to four-bit, which runs at roughly ten to fifteen tokens per second on a modern CPU with eight to sixteen gigabytes of memory. These carry strong code priors, which is also why they carry the strongest accent to suppress. The native-ternary route takes a model whose weights are already in the integer-add-and-subtract regime, which aligns with the CPU and low-precision interests directly but reaches a working artifact later, since the tuning tooling around such models is thinner.

The scaffold article committed the architecture to precise arithmetic, and that commitment is in genuine tension with both CPU routes, because the rate-reduction operations the architecture depends on are worst-conditioned at low precision. The honest resolution, developed in the [architecture and arithmetic article]({{< ref "architecture-and-arithmetic" >}}), is that the foreign ternary format was always a borrowed terminal artifact, and a model built on the framework's own b-posit substrate is a candidate the borrowed format is not. The build path here, dense base, low-rank adaptation, quantize after tuning, is the route to a working artifact soonest; the substrate question is what determines whether that artifact is merely functional or actually sharp.

## Deployment as a constellation citizen

Both tuning passes run as low-rank adaptation, which keeps the trainable dimension small, keeps tuning CPU-feasible, and keeps the forward-mode path of the [efficiency article]({{< ref "forward-mode-and-adaptation" >}}) tractable. Pass one is merged into the weights to produce a stable functional base, a model that thinks in ML-family terms and changes rarely. Pass two stays a swappable adapter carrying Clef idiom, grammar awareness, and the tool reflexes, and it is the artifact that iterates and warm-rotates as the language evolves. That boundary keeps the two passes from conflating across time, not merely within a single run.

The version-record discipline from [ADM](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}), carries over even though the language model holds no grade certificate. A signed record of base-checkpoint hash, adapter provenance, tuning recipe, and data provenance, with warm rotation swapping adapters, makes the tuned model a well-behaved citizen of the constellation and a clean prior source for distillation. It wears no ADM type, but it observes the same provenance discipline as everything that does, which is the first concrete sense in which it is adjacent to the constellation rather than foreign to it.

## Open questions

Whether the damping-first order holds in practice, or whether the pass-two replay is insufficient to prevent accent re-import, is an empirical question the contrastive catalog is designed to answer.

Whether a dense small base quantized to four-bit retains enough of the instilled idiom to be useful, or whether the substrate must move to b-posit before the model is sharp, is the question the architecture and arithmetic article takes up directly.

Whether the propose-check-revise loop converges efficiently, or whether the model spends too many compiler round-trips per accepted program, is measurable once the tool-trajectory dataset exists.
