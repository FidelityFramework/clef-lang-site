---
title: "Why Adaptive Domain Models Change the Inference Problem"
linkTitle: "Why Adaptive Domain Models"
description: "The unique contribution that grounds this section: typed domain models that infer better, on simpler hardware, and relieve the monolithic language model of work it was never suited to carry"
date: 2026-06-08T00:00:00+00:00
weight: 1
authors:
  - SpeakEZ
tags: ["machine-learning", "adaptive-domain-models", "formal-verification", "architecture"]
draft: false
---

> Part of the Constrained Machine Learning section. This article establishes
> the contribution the section is built on, the Adaptive Domain Model, and the
> utility argument that motivates the effort. The full type-theoretic case is
> made in the ADM pre-print and is not re-prosecuted here; what follows is a
> highlight reel sufficient to ground the discussion, with the pre-print cited
> for the argument in full. The section as a whole is speculative: the
> constellation described is a research program, not a shipped system. The
> articles can be read in any order; this one is the natural place to start
> for the motivation.

## The contribution, stated plainly

The Adaptive Domain Model is a learned model whose weights carry the structure of their domain as a type-level invariant. Where a domain has structure known with certainty before any training example arrives, a conserved grade, a physical dimension, an equivariance under a known group, that structure is expressed in the type system, enforced during training, and discharged by a verifier, so that the trained model holds the structure exactly rather than approximately. This is the unique contribution the section rests on, and it is argued in full in the ADM pre-print. The purpose here is to recall enough of it to motivate what follows, not to make the case again.

The contrast that defines it is with the dominant paradigm. A monolithic transformer learns whatever structure it acquires from data alone, as a statistical regularity with no formal status, true on average over the training distribution and subject to drift everywhere else. An ADM does not learn the structure it is given; it is built to obey it, and learns only what remains once the known structure is fixed. The difference is between a model that might have discovered that physical quantities have consistent dimensions, and a model in which a dimensional inconsistency cannot be represented.

## The highlight reel

Four points from the pre-print carry the weight of everything downstream. Each is developed there in full; each is stated here only as far as the series needs.

**Structure becomes a type, and the type is exact.** The pre-print's central move is to express domain structure in a grade-typed algebra, geometric algebra for the physical domains, so that a quantity's grade, a scalar, a vector, a bivector, is a type-level fact. Operations that would violate the grade structure do not type-check, and the structure that survives is exact, not a learned approximation, because it is a property of the type rather than of the fitted weights.

**Exactness survives training, where statistical structure drifts.** A learned regularity degrades under the very process that produced it: continued training, fine-tuning, distribution shift all erode a structure that was only ever an average. A typed invariant does not, because training optimizes within the admissible space the type defines rather than toward a structure it might leave. The pre-print's forward-mode-plus-quire discipline is what holds the invariant exact through training in finite-precision arithmetic, closing the gap between exact-arithmetic structure and machine structure.

**The structural zeros are provable, not emergent.** A block-diagonal generator has a block-diagonal exponential, and the off-block entries are provably zero by the grade structure. Where a monolithic model would spend capacity learning that certain interactions are absent, and spend it imperfectly, an ADM has those absences as type-level facts. The model carries no parameters for interactions the domain forbids, which is the first hint of the hardware argument below.

**The obligations are tiered and discharged, not asserted.** Structure that is decidable is discharged automatically at the appropriate tier; structure that is not is carried as an explicit obligation to a relational backend, with the faithfulness of each result recorded rather than assumed. The model does not merely claim its invariants; it carries the evidence that they hold, into the running artifact.

That is the reel. The pre-print proves these; the section takes them as established and asks what they make possible.

## Why this produces better inference

The utility argument is the reason the section exists, and it has three parts that compound.

**More precise, because the structure cannot drift.** An ADM's domain guarantees hold exactly and hold under training, so an inference that depends on a conserved quantity, a dimensional consistency, an equivariance, is correct by construction rather than correct on average. A monolithic model can produce a physically impossible result, a dimensional category error, a violated conservation law, and nothing in its architecture forbids it; the error is simply off the training distribution. In an ADM the corresponding error is not improbable but unrepresentable. For any task where correctness of structure matters more than plausibility of output, this is the difference between a result you can rely on and one you must check.

**Faster and simpler in hardware, because the model carries less.** A model that holds its structure as type-level fact does not spend parameters discovering that structure, and does not spend parameters representing interactions its domain forbids. The provable structural zeros are absent from the model rather than learned to be near zero. A typed domain model is therefore smaller than a monolithic model of comparable competence in its domain, and its computation runs over a known-sparse structure rather than a dense one. Smaller and known-sparse is exactly what simpler hardware wants: less memory, fewer operations, and a computation whose shape is fixed at compile time rather than discovered at runtime. The same property that makes the model precise, structure as fact rather than as fitted weight, makes it cheap to run.

**Faster in interaction, because the work is divided.** This is the part that reframes the monolithic model itself. A single large transformer is asked to be competent at everything at once: language, reasoning, arithmetic, physical intuition, domain after domain, all in one undifferentiated parameter space, all at the precision the hardest sub-task demands. A constellation divides that labor. The domain models handle the parts where correctness can be guaranteed, precisely and on simple hardware, and the language model is relieved of carrying competence it held only weakly and expensively. It no longer has to be the thing that gets the physics approximately right or the arithmetic usually right; it routes those to models that get them exactly right. The interaction is faster because each part runs on hardware sized to its actual job, and the monolithic model stops being the bottleneck for work it was never well suited to.

## Where the language model fits, and why it is secondary

The argument so far is about the typed models, because they are the contribution. The language model enters as the component the constellation relieves and, in turn, the component the constellation must still contain, because something has to interface with the unstructured world. Natural-language intent, an underspecified goal, a partial program: these have no domain type, and no ADM can accept them directly. The language model is the porous node that takes in the unstructured and routes it to the typed models that can satisfy it.

That node cannot wear an ADM type, because the prior structure of language admits no compact formal specification, the precise boundary the pre-print draws around its own method. So the language model is built by other means, which much of this section examines. The priority is conceptual, not a reading order: the typed models are the contribution and the source of the utility, and the language model is the necessary interface that the contribution makes lighter, faster, and less burdened. Whichever article a reader starts from, that is the relationship to keep in view.

## The rest of this section

With ADM and its utility argument in place, the rest of the section examines the language node that completes the constellation, from several independent angles that can be read in any order. [*A Scaffold for Constrained Models*]({{< ref "scaffold-for-constrained-models" >}}) names the three commitments that carry a language component when the ADM type scaffold cannot apply, and is the natural next step for the overall argument. [*Building the Constrained Language Model*]({{< ref "building-the-model" >}}) treats its tuning and the deterministic layer that bounds its output. [*Architecture and Arithmetic*]({{< ref "architecture-and-arithmetic" >}}) and [*Forward-Mode and Low-Rank Adaptation*]({{< ref "forward-mode-and-adaptation" >}}) treat the substrate that makes it precise and cheap. [*The Constellation*]({{< ref "the-constellation" >}}) returns to this article's central claim and shows the porous node and the typed models composed into one system. [*Reversible Cores and Inference-Time Recall*]({{< ref "reversible-cores" >}}) reframes the framework's negative and fractional types as a design theory of reversible computation inside a model. And [*Adapting Inference on a Gradient*]({{< ref "adapting-inference-on-a-gradient" >}}) is the adoption side, how an organization runs the constellation today and adapts toward a built model along a gradient. Each is speculative and marks its own open questions.

The through-line is the one stated here: the typed domain model is the contribution, its utility is precision and simplicity and a divided workload, and the language model is the interface that contribution relieves and contains. Everything downstream serves that claim.

## References

Internal: the Adaptive Domain Models pre-print for the full type-theoretic case; the rest of this section, which builds the language node the constellation requires.

External: the transformer architecture as the monolithic baseline the utility argument contrasts against; the geometric-algebra and equivariance literature the pre-print draws its typed structure from.
