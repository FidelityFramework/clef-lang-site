---
title: "A Scaffold for Constrained Language Models"
linkTitle: "A Scaffold for Constrained Models"
description: "The three commitments that carry a language model where the ADM type scaffold does not reach"
date: 2026-06-08T00:01:00+00:00
weight: 2
authors:
  - SpeakEZ
tags: ["machine-learning", "adaptive-domain-models", "formal-verification", "architecture"]
draft: false
---

The [*Adaptive Domain Models*]({{< ref "why-adaptive-domain-models" >}}) article established the contribution and the division of labor: typed domain models carry their structure exactly and run on simple hardware, and the language model is the porous node that interfaces with the unstructured world and routes to them. That division leaves one node to account for. The domain models are correct by construction because their domains have structure known before training. The language node has no such structure to type, because the prior structure of language admits no compact formal specification, which is the precise boundary the [ADM pre-print](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}), draws around its own method.

That boundary is usually read as a limit on ADM. Read the other way, it is a specification for the language node. It says exactly what is missing in the language case, the formal prior the domain models rely on, and forces the question this article answers: with the type scaffold unavailable, what carries the weight instead?

The answer is not chosen freely. It is what our framework's existing commitments produce when they are pointed at a domain without a formal prior. Clef already insists that structure be carried through compilation, that arithmetic be precise, and that guarantees come from the toolchain rather than from trust in the source. We have taken that discipline into a model before: an earlier [CNN-to-TopOC transfer-learning design]({{< ref "/blog/cnn-to-topoc-transfer-learning" >}}) carried Units-of-Measure dimensions from a convolutional backbone through a topological transform and on to FPGA and ASIC targets, so a dimensional inconsistency could not survive compilation. Follow those three commitments into the territory of generative models and they land on a specific architecture: the structure is derived rather than searched, the arithmetic is precise rather than approximate, and the guarantees are moved outside the weights into deterministic machinery the framework already owns. None of these is a substitute for the ADM type discipline. Each is what that discipline's underlying commitments imply once the type-level scaffold itself is unavailable.

What this arrangement produces is a claim about cost. Building and adapting a model this way should be orders of magnitude cheaper than the standard deep-learning pipeline, and the saving is not a tuning trick. It is structural. A derived architecture has few enough meaningful degrees of freedom that the gradient can be obtained by propagating tangents forward through them, in place of storing an activation tape and sweeping a backward graph over a parameter cloud most of which does no identifiable work. The emergent loss is computed over the degrees of freedom that the architecture's own derivation exposes, and there are far fewer of them than an unstructured model carries. The efficiency is what falls out when the loss is computed over structure instead of over an undifferentiated parameter space.

## A derived architecture, not a searched one

The derivation introduced in the section index, set out in Buchanan, Pai, Wang, and Ma's [*Principles and Practice of Deep Representation Learning*](https://ma-lab-berkeley.github.io/deep-representation-learning-book/), treats a network layer not as an arbitrary function to be tuned but as one step of an optimization algorithm descending an information-theoretic objective. Attention emerges as a step that compresses a representation against a set of low-dimensional subspaces; the feed-forward block emerges as a sparsification step. The architecture is the unrolled optimizer, and each block has a closed-form reason to exist. Their [CRATE](https://github.com/Ma-Lab-Berkeley/CRATE) architecture is the worked result, now with a causal variant suited to sequence data.

This matters to us for a structural reason. A black-box transformer offers no principled account of which of its parameters do what, so making it smaller means pruning after the fact and hoping. A derived architecture inverts that: you instantiate only the blocks the objective requires, and the representation it converges toward is a union of low-dimensional, ideally non-interacting subspaces. That target geometry is the same block-sparse, structurally-separated geometry the ADM substrate enforces by construction. The difference is the mechanism. ADM types the structure and forbids the model from leaving it. The white-box objective makes the structure an attractor the model descends toward. Same geometry; one reached by construction, one by convergence.

For the language case, where construction is unavailable, convergence toward a derived structure is the right substitute. We adopt the white-box backbone for the architecture it gives us, one whose internal geometry is interpretable and whose blocks are accountable, which is the most a domain without a formal prior can offer.

## Why the arithmetic has to be precise

The white-box guarantees are real, and they are soft. The subspaces the objective separates are orthogonal at convergence in exact arithmetic. Trained in IEEE-754 floating point, they are approximately orthogonal, and the rate-reduction objective that drives the separation is built on log-determinant and covariance terms that are numerically delicate, the long accumulations where floating-point cancellation does its quiet damage. The structure does not fail; it blurs. Nothing breaks, because nothing was ever claimed to be exact.

For interpretability that blur is tolerable. For a component meant to sit adjacent to the ADM constellation it is the wrong tradeoff, and it is avoidable. The substrate the ADM work already uses, b-posit arithmetic with quire accumulation, is built for the operations the white-box objective stresses. The quire carries a long accumulation without intermediate rounding, which is what the log-det and covariance terms need. The choice is therefore not to tolerate the floating-point slack but to remove its cause: keep the derived architecture, and run its sensitive operations on arithmetic that makes the convergence sharp instead of loose.

This is the first place the language-model component rejoins the rest of the framework. It stops being the one piece that runs on a foreign numeric format and lives in the same b-posit world as everything else.

There is an honest hazard here. Posit precision is not uniform; it is densest near magnitude one and tapers toward the extremes. Whether that taper aligns with where the rate-reduction objective places its numerical stress during training is an empirical question about the interaction of two specific designs, and it is one of the experiments this program must run before the synthesis can be called real.

## Forward-mode over a low-rank structure

This is where the efficiency thesis stated at the outset is paid for. The derived architecture has few meaningful degrees of freedom, and the gradient of the emergent loss can be taken over exactly those, which is what makes the saving structural rather than incidental. Reverse-mode automatic differentiation earns its reputation by amortizing one backward pass over many parameters, but that accounting assumes a large, unstructured parameter space and cheap, noisy arithmetic. A derived architecture violates the first assumption in our favor: the directions the objective actually moves along are not the full ambient parameter space but the low-dimensional subspace structure the architecture is built around.

That is the setting where forward-mode with multiple tangents becomes competitive, and then preferable. A multi-tangent forward pass propagates a batch of directional derivatives alongside the primal computation, in a single pass, with no stored activation tape and no backward graph. Its classic weakness is that it costs one pass per direction, which loses badly when the directions are the whole parameter cloud. But over a derived low-rank structure the meaningful directions are few, and the tangent set spans only them. The architecture supplies the low rank that makes the tangent set affordable; the forward pass supplies the gradient without the reverse-mode storage. Each rescues the other from its worst case.

The shape of the step, as we currently lay it out, makes the bargain concrete: the tangent set is indexed by the derived rank, rather than using the more lenient ambient parameter count. 

```fsharp
// A dual number over b-posit: primal carried with a tangent, accumulated through the quire.
type Dual = { Primal: BPosit; Tangent: BPosit }

// One forward step of the derived block, propagating r tangents at once.
// r = the derived rank, not the parameter count.
let forwardStep
        (block: GradedSubspaceBasis<Bivector>[])   // the derived block, grade-typed
        (z: Dual[])                                 // primal field carrying r tangents
        : Dual[] =
    block
    |> Array.map (fun u ->
        z
        |> compressAgainst u            // the same rate-reduction step, run on duals
        |> Quire.accumulateDual)        // primal and tangent, one quire, no rounding
    |> SubspaceAggregation.byGrade      // off-block directions never enter the tangent

// gradient = the tangents of one forward pass.
let gradient (block: GradedSubspaceBasis<Bivector>[]) (x: Dual[]) : BPosit[] =
    forwardStep block x |> Array.map (fun d -> d.Tangent)
```

The payoff concentrates where this section's models are built and adapted. Tuning is planned as low-rank adaptation throughout: a stable functional base with a swappable Clef-specific adapter over it. A low-rank adapter is the ideal target for multi-tangent forward-mode, because the tangent set spans the adapter's rank, which is small by construction. Distillation and fine-tuning, the regimes this whole program operates in, are the regimes where a few storage-free forward passes cost less than reverse-mode with its activation tape. The precision argument and the efficiency argument turn out to be one argument seen from two ends: precise arithmetic lets the accumulated tangents be trusted, and the derived structure lets there be few enough of them to be cheap.

The open question is the tangent count itself. The cost scales with the number of tangents, so the synthesis is efficient only if the structured subspace stays genuinely low-dimensional throughout training, not merely at convergence. A derived structure is an emergent attractor, so early training may demand a wider tangent set than the converged rank. Whether the effective rank stays low enough, early as well as late, is measurable, and it is the companion experiment to the posit-taper question above. Both ask the same underlying thing: how the rate-reduction objective behaves under our numerics while training, not just at the fixed point.

## The guarantees live outside the weights

Everything above improves the probabilistic substrate. The guarantee lives outside the weights entirely, in deterministic machinery the framework already owns.

A grammar-constrained decoder, driven by a grammar derived from Clef's own, holds the sampler to syntactically valid Clef regardless of what the weights prefer. This is a deterministic guard over a probabilistic model, and it splits the labor cleanly:

```fsharp
// Grammar filters the sampler; Composer decides meaning.
let decode (model: Model) (grammar: ClefGrammar) (prompt: Tokens) : ClefSource =
    model
    |> sampleUnder grammar      // syntactic validity is guaranteed here
    |> Composer.elaborate       // semantic validity is decided here
    |> function
       | Ok program  -> program
       | Error diags -> reviseAgainst diags   // propose, check, revise
```

The grammar guarantees syntax. The model's tuning shapes idiom. And Composer, the Clef compiler itself, becomes the final acceptance test: a generated program that does not elaborate is rejected, and its diagnostics feed a revision step. The compiler is the verifier the weights cannot be. This is the same posture the rest of Clef takes, where correctness is a property the toolchain establishes rather than a property the source is trusted to have, applied now to source the model wrote instead of source a person wrote.

So the three layers stay cleanly separated, each carrying what it is suited to carry. Inside the weights, structure is emergent and white-box-derived, made sharp by precise arithmetic. Outside the weights, guarantees are deterministic and Composer-supplied. The type scaffold is withheld, by the same rule that mandates it elsewhere in the framework, and what replaces it is not nothing but this layered arrangement.

## Where this sits in the constellation

The ADM paper describes a constellation of domain models, each typed by the structure of its domain. A language component cannot be one of those in the strict sense, because it has no such structure to type. What this section argues is that it can be an adjacent citizen of the same constellation: built on the same b-posit and quire substrate, trained through the same forward-mode discipline, recorded with the same version-record provenance, and bounded by the same compiler that elaborates everything else. It contributes to the constellation not by wearing the ADM types but by sharing everything underneath them and accepting deterministic bounds in place of the types it cannot wear.

That adjacency is the paradigm this section means to sketch: domain models that are correct by construction, and a language component that is precise by construction and bounded by the compiler, sharing one substrate. The wager underneath it is that computing a model's loss over the degrees of freedom a derived architecture exposes, rather than over an undifferentiated parameter space, is both more honest about what the model is doing and orders of magnitude cheaper to build and adapt. That wager is where the framework leads, and it is held lightly: it rests on experiments not yet run, and the open questions marked above are the ones that decide it. Whether the pieces compose into a working artifact is what remains to be built.

## Related reading in this section

The commitments named here are developed independently elsewhere in the section, and the articles do not need to be read in order. [*Building the Constrained Language Model*]({{< ref "building-the-model" >}}) works through the two-pass tuning, the damping taxonomy that protects the compiler's own output, and the constraint layer in practice. [*Architecture and Arithmetic*]({{< ref "architecture-and-arithmetic" >}}) examines the derived backbone and the b-posit and quire treatment of the rate-reduction objective. [*Forward-Mode and Low-Rank Adaptation*]({{< ref "forward-mode-and-adaptation" >}}) develops the multi-tangent forward-mode path and the effective-rank measurement behind the efficiency claim. [*The Constellation*]({{< ref "the-constellation" >}}) treats the language model as a porous node among domain models. [*Reversible Cores and Inference-Time Recall*]({{< ref "reversible-cores" >}}) turns the framework's [negative and fractional types](https://arxiv.org/abs/2606.04352) on a model's internals. And [*Adapting Inference on a Gradient*]({{< ref "adapting-inference-on-a-gradient" >}}) is the practical adoption side. Each is speculative and marks its own open questions.
