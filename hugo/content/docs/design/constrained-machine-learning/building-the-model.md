---
title: "Building a Constrained Language Model"
linkTitle: "Building a Constrained Language Model"
description: "An concept for a language model that could be efficiently tuned to write Clef"
date: 2026-06-08T00:02:00+00:00
weight: 3
authors:
  - SpeakEZ
tags: ["machine-learning", "language-model", "formal-verification", "compilation"]
draft: false
---

From our design perspective, we see that most of a language node's work is ordinary language: it reads natural-language intent, reasons in a domain's own vocabulary, and answers in prose a person reads. That vocabulary might be a clinician's terminology, a statute's clauses, or an analyst's terms of art. One path is unlike the rest. When the node hands work to a typed domain model, it has to produce structured Clef, and that Clef has to be well-formed. Emitting Clef cleanly while the rest of what the node does stays conversation is the problem this path poses.

On that path the model is constrained in three senses. Its idiom, writing Clef the way a fluent practitioner would. Its accent, the imperative and dynamically-typed reflexes of its training corpus, suppressed. And its output, held to syntactically valid Clef by a grammar derived from Clef's own, a static artifact applied at the boundary. Tuning shapes the first two, inside the model. The grammar imposes the third from outside it, produced once at build time and carried forward by the runtime.

## Instilling and Damping

Instilling Clef and removing the inherited accent are different problems, and conflating them defeats both. Instilling is supervised learning: show the model idiomatic Clef and it learns the distribution. Removing the accent is a preference problem. The base model already holds high probability mass on imperative loops, dynamic typing, exceptions as control flow, null, and class hierarchies, and adding Clef examples competes with that mass without removing it. A single combined objective can put the two updates in tension: where the imperative accent and idiomatic Clef share surface forms, the instilling term pulls one way and the preference term the other, and the net update is muddied. Separating the passes keeps each signal clean.

Which pass runs first decides the outcome. The conventional sequence is competence first, preference last, but that ordering lets the final preference pass re-import the accent it was meant to remove, because the pass that runs last shapes the final distribution. Running damping first introduces its own tension: a preference method learns by preferring a chosen response over a rejected one, and if the response we want it to prefer is idiomatic Clef, there is no such exemplar to point at before competence exists.


## The damping taxonomy

A naive damping scheme does real harm here, because JavaScript competence is load-bearing. Clef emits JavaScript: it lowers through Alex to a JavaScript intermediate representation and produces JavaScript whose verification lives in the shared middle-end. A separate path reads TypeScript surfaces to produce Clef externs with witnessing rules. So JavaScript competence does two jobs our framework depends on: reading JavaScript to bind it, and recognizing well-formed emitted output. A damping pass that simply suppressed JavaScript would corrupt both.

The landscape therefore has to be stratified into three classes, and every JavaScript example in the contrastive catalog carries one of three labels set by the role it plays:

```fsharp
type JsExampleRole =
    | KeepAndRedirect   // reading JS/TS to bind it; recognizing emitted output
    | Damp              // authoring imperative/dynamic JS as source logic
    | Instill           // routing a JS need to Clef-under-grammar or a typed extern
 
```

*Keep and redirect* is the comprehension class: reading JavaScript and TypeScript to bind them, understanding what well-formed emitted JavaScript looks like, and the tagged-object heritage that underwrites schema-directed narrowing. The binding pipeline and the JavaScript backend both consume this competence, and it must survive.

*Damp* is the authoring-reflex class: JavaScript and Python written imperatively or dynamically as source logic; null, in-place mutation, and exceptions used as control flow; ad-hoc JSON shapes hand-rolled at need. The supply-chain reflex of reaching for a package belongs here too. These are the accents.

*Instill* is the routing class: the model learns that a JavaScript need is answered by authoring Clef under the grammar and letting the backend emit, or by binding a TypeScript surface into Clef externs. At interop boundaries it reaches for schema-directed narrowing that returns Result, Option for absence, and structured handles. The closed type system holds inside Clef proper, and wire interchange goes through [BAREWire](/spec/draft/discriminated-union-representation/#70-the-contract-model-barewire-and-bare).

The discriminating question for every example is whether the JavaScript is authored as logic, emitted as a target, or read as a surface to bind. Labeling target-side or boundary-side JavaScript as an accent would teach the model to distrust its own compiler's output and its own binding inputs.

## What Constrains the Output

Tuning shapes what the model prefers; the grammar guarantees the form it emits. In the runtime we envision, the grammar carries that guarantee: a grammar-constrained decoder, driven by a grammar derived from [Clef's own grammar](/spec/draft/basic-grammar-elements/), holds the sampler to syntactically valid Clef regardless of the model's habits. That grammar is a static artifact, built once and applied at the boundary, so the node would deploy on the grammar alone.

Semantic correctness is a separate matter, and the build settles it. During tuning, Composer would serve as the teacher: the model proposes Clef, the compiler elaborates it or hands back diagnostics, the model revises, and producing elaborable Clef becomes a trained reflex it carries into deployment.

```fsharp
// Build-time only: revise until Composer elaborates the attempt.
let rec trainAuthoring (model: Model) (goal: Spec) (attempt: ClefSource) : Program =
    match Composer.elaborate attempt with
    | Ok program  -> program                                             // accepted
    | Error diags -> trainAuthoring model goal (model.revise goal attempt diags)   // revise on the diagnostics
 
```

The loop runs during tuning, on trajectories where the grammar already guarantees a syntactically valid proposal, so the compiler's verdict is purely about meaning, and the model it evaluates already has its imperative accent removed, so the revisions come out in the right idiom. What would deploy is the trained model and the static grammar. The [constellation article]({{< ref "the-constellation" >}}) takes up how the domain models around the node bound it at runtime, with the grammar the only constraint the compiler leaves behind.

## Where the Model Runs

The deployment target is CPU, which sets what the tuning operates on. Two routes reach it. The dense-small-then-quantize route takes a one-to-three-billion-parameter code-capable model and quantizes to four-bit. The quantized model runs at roughly ten to fifteen tokens per second on a modern CPU with eight to sixteen gigabytes of memory. These carry strong code priors, which is also why they carry the strongest accent to suppress. The native-ternary route takes a model whose weights are already in the integer-add-and-subtract regime, which aligns with the CPU and low-precision interests directly but reaches a working artifact later, since the tuning tooling around such models is thinner.

The scaffold article committed the architecture to precise arithmetic, and that commitment is in genuine tension with both CPU routes, because the rate-reduction operations the architecture depends on are worst-conditioned at low precision. The [architecture and arithmetic article]({{< ref "architecture-and-arithmetic" >}}) resolves this: the foreign ternary format was always a borrowed terminal artifact, and a model built on the framework's own [b-posit substrate](https://arxiv.org/abs/2603.01615) is a candidate the borrowed format is not. The build path here, dense base, low-rank adaptation, quantize after tuning, is the route to a working artifact soonest; the substrate question is what determines whether that artifact is merely functional or reaches the arithmetic precision the architecture requires.

## Deployment as a constellation citizen

Both passes run as low-rank adaptation, which holds the trainable dimension small, keeps tuning CPU-feasible, and leaves the forward-mode path of the [efficiency article]({{< ref "forward-mode-and-adaptation" >}}) tractable. Pass one is merged into the weights to produce a stable functional base, a model that thinks in ML-family terms and changes rarely. Pass two stays a swappable adapter holding Clef idiom, grammar awareness, and the tool reflexes. It is the artifact that iterates and warm-rotates as the language evolves. That boundary keeps the two passes from conflating across time, not merely within a single run.

The version-record discipline from [ADM](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}), carries over even though the language model holds no grade certificate. A signed record would list base-checkpoint hash, adapter provenance, tuning recipe, and data provenance. With warm rotation swapping adapters, that record would let the tuned model integrate into the constellation on the same terms as everything else and serve as a clean prior source for distillation. It carries no ADM type, but it follows the same provenance discipline as everything that does, which is the first concrete sense in which it belongs to the constellation rather than sitting outside it.

## Open questions

Whether the damping-first order holds in practice, or whether the pass-two replay is insufficient to prevent accent re-import, is an empirical question the contrastive catalog is designed to answer.

Whether a dense small base quantized to four-bit retains enough of the instilled idiom to be useful, or whether the substrate must move to b-posit before the model is sharp, is the question the architecture and arithmetic article takes up directly.

Whether the propose-check-revise training converges efficiently, or whether it needs too many compiler round-trips per accepted program during tuning, is measurable once the tool-trajectory dataset exists.
