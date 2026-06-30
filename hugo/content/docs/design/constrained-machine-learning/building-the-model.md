---
title: "Building the Constrained Language Model"
linkTitle: "Building the Model"
description: "How a language model is tuned to write Clef, with valid output enforced outside its weights"
date: 2026-06-08T00:02:00+00:00
weight: 3
authors:
  - SpeakEZ
tags: ["machine-learning", "language-model", "formal-verification", "compilation"]
draft: false
---

From our design perspective, we see that most of a language node's work is ordinary language: it reads natural-language intent, reasons in a domain's own vocabulary, a clinician's terminology, a statute's clauses, an analyst's terms of art, and answers in prose a person reads. One path through it is different. When the node hands work to a typed domain model, it has to produce structured Clef, and that Clef has to be well-formed. Teaching a model to emit Clef cleanly, while the rest of what it does stays conversation, is the subject here.

On that path the model is constrained in three senses. Its idiom, writing Clef the way a fluent practitioner would. Its accent, the imperative and dynamically-typed reflexes of its training corpus, suppressed. And its output, held to syntactically valid Clef by a grammar derived from Clef's own, a static artifact that stands at the boundary on its own. The first two are shaped by tuning, inside the model. The third is imposed by that grammar, which the build produces and the runtime carries forward.

## Instilling and Damping

Instilling Clef and removing the inherited accent are different problems, and conflating them defeats both. Instilling is supervised learning: show the model idiomatic Clef and it learns the distribution. Removing the accent is a preference problem, because the base model already holds high probability mass on imperative loops, dynamic typing, exceptions as control flow, null, and class hierarchies, and adding Clef examples competes with that mass without removing it. A single combined objective makes the two gradients work against each other, the instilling gradient pushing toward Clef while the preference gradient pulls away from the accent, and they partially cancel. They belong in distinct passes.

The order is the subtle part. The conventional sequence is competence first, preference last, but that ordering lets the final preference pass re-import the accent it was meant to remove, because the pass that runs last shapes the final distribution. Running damping first introduces its own tension: a preference method suppresses a direction relative to a positive target, and a model with no Clef competence yet has no positive side to point at.


## The damping taxonomy

JavaScript is the case that forces precision, and it is where a naive damping scheme does real harm. Clef emits JavaScript: it lowers through Alex to a JavaScript intermediate representation and produces JavaScript whose verification lives in the shared middle-end. A separate path reads TypeScript surfaces to produce Clef externs with witnessing rules. So JavaScript competence is load-bearing in two roles our framework depends on, reading it to bind it, and recognizing well-formed emitted output, and a damping pass that simply suppressed JavaScript would corrupt both.

The landscape therefore has to be stratified into three classes, and every JavaScript example in the contrastive catalog carries one of three labels set by the role it plays:

```fsharp
type JsExampleRole =
    | KeepAndRedirect   // reading JS/TS to bind it; recognizing emitted output
    | Damp              // authoring imperative/dynamic JS as source logic
    | Instill           // routing a JS need to Clef-under-grammar or a typed extern
```

*Keep and redirect* is the comprehension class: reading JavaScript and TypeScript to bind them, understanding what well-formed emitted JavaScript looks like, and the tagged-object heritage that underwrites schema-directed narrowing. The binding pipeline and the JavaScript backend both consume this competence, and it must survive.

*Damp* is the authoring-reflex class: writing imperative or dynamic JavaScript and Python as source logic, reaching for null and in-place mutation and exceptions as control flow, hand-rolling ad-hoc JSON shapes, and the supply-chain reflex of reaching for a package. These are the accents.

*Instill* is the routing class: the model learns that a JavaScript need is answered by authoring Clef under the grammar and letting the backend emit, or by binding a TypeScript surface into Clef externs. At interop boundaries it reaches for schema-directed narrowing returning Result, Option for absence, and structured handles, with the closed type system holding inside Clef proper, and wire interchange going through BAREWire.

The discriminating question for every example is whether the JavaScript is authored as logic, emitted as a target, or read as a surface to bind. Labeling target-side or boundary-side JavaScript as an accent would teach the model to distrust its own compiler's output and its own binding inputs, which is the opposite of the goal.

## What Constrains the Output

Tuning shapes what the model prefers; the grammar guarantees the form it emits. In the runtime we envision, the grammar carries that guarantee: a grammar-constrained decoder, driven by an EBNF grammar derived from Clef's own, holds the sampler to syntactically valid Clef regardless of the model's habits. That grammar is a static artifact, built once and carried at the boundary on its own, so the node deploys on the grammar alone.

Semantic correctness is a separate matter, and the build settles it. During tuning, Composer is the teacher: the model proposes Clef, the compiler elaborates it or hands back diagnostics, the model revises, and producing elaborable Clef becomes a trained reflex the model carries into deployment.

```fsharp
// Build-time only: revise until Composer elaborates the attempt.
let rec trainAuthoring (model: Model) (goal: Spec) (attempt: ClefSource) : Program =
    match Composer.elaborate attempt with
    | Ok program  -> program                                             // accepted
    | Error diags -> trainAuthoring model goal (model.revise goal attempt diags)   // revise on the diagnostics
```

The loop runs during tuning, on trajectories where the grammar already guarantees a syntactically valid proposal, so the compiler's verdict is purely about meaning, and it lands on a model whose imperative accent is already gone, so the revisions are already in the right idiom. What deploys is the trained model and the static grammar. The [constellation article]({{< ref "the-constellation" >}}) takes up how the domain models around the node bound it at runtime, with the grammar the only constraint the compiler leaves behind.

## Where the Model Runs

The deployment target is CPU, which sets what the tuning operates on. Two routes reach it. The dense-small-then-quantize route takes a one-to-three-billion-parameter code-capable model and quantizes to four-bit, which runs at roughly ten to fifteen tokens per second on a modern CPU with eight to sixteen gigabytes of memory. These carry strong code priors, which is also why they carry the strongest accent to suppress. The native-ternary route takes a model whose weights are already in the integer-add-and-subtract regime, which aligns with the CPU and low-precision interests directly but reaches a working artifact later, since the tuning tooling around such models is thinner.

The scaffold article committed the architecture to precise arithmetic, and that commitment is in genuine tension with both CPU routes, because the rate-reduction operations the architecture depends on are worst-conditioned at low precision. The honest resolution, developed in the [architecture and arithmetic article]({{< ref "architecture-and-arithmetic" >}}), is that the foreign ternary format was always a borrowed terminal artifact, and a model built on the framework's own b-posit substrate is a candidate the borrowed format is not. The build path here, dense base, low-rank adaptation, quantize after tuning, is the route to a working artifact soonest; the substrate question is what determines whether that artifact is merely functional or sharp.

## Deployment as a constellation citizen

Both passes run as low-rank adaptation, which keeps the trainable dimension small, keeps tuning CPU-feasible, and keeps the forward-mode path of the [efficiency article]({{< ref "forward-mode-and-adaptation" >}}) tractable. Pass one is merged into the weights to produce a stable functional base, a model that thinks in ML-family terms and changes rarely. Pass two stays a swappable adapter carrying Clef idiom, grammar awareness, and the tool reflexes, and it is the artifact that iterates and warm-rotates as the language evolves. That boundary keeps the two passes from conflating across time, not merely within a single run.

The version-record discipline from [ADM](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}), carries over even though the language model holds no grade certificate. A signed record of base-checkpoint hash, adapter provenance, tuning recipe, and data provenance, with warm rotation swapping adapters, makes the tuned model a well-behaved citizen of the constellation and a clean prior source for distillation. It wears no ADM type, but it observes the same provenance discipline as everything that does, which is the first concrete sense in which it is adjacent to the constellation rather than foreign to it.

## Open questions

Whether the damping-first order holds in practice, or whether the pass-two replay is insufficient to prevent accent re-import, is an empirical question the contrastive catalog is designed to answer.

Whether a dense small base quantized to four-bit retains enough of the instilled idiom to be useful, or whether the substrate must move to b-posit before the model is sharp, is the question the architecture and arithmetic article takes up directly.

Whether the propose-check-revise training converges efficiently, or whether it needs too many compiler round-trips per accepted program during tuning, is measurable once the tool-trajectory dataset exists.
