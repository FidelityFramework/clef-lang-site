---
title: "How Adaptive Domain Models Could Change the Game"
linkTitle: "Utility of Adaptive Domain Models"
description: "Typed domain models that hold their domain's structure as a verified, type-level invariant"
date: 2026-06-08T00:00:00+00:00
weight: 1
authors:
  - SpeakEZ
tags: ["machine-learning", "adaptive-domain-models", "formal-verification", "architecture"]
draft: false
---

In a [talk on how intelligence evolved](https://www.youtube.com/watch?v=Az9sfy3jWNU), Yi Ma articulates two kinds that are essentially distinct based on transferred and learned bases. In the framing he presents, *Phylogenetic* intelligence belongs to the species: it is evolved under selection and written into an inheritance, and the individual that carries it only reacts based on that provided frame. ***Ontogenetic*** intelligence belongs to the individual that builds its own memory after birth, through perception, feedback, and the correction of error. By extension, he asserts that a "monolithic model" (his exact turn of phrase) is phylogenetic. Its training run is the evolution and its benchmark is the selection pressure, and the weights it ships are an inheritance it spends the rest of its existence in a reactive form. 

Following his example, we see Adaptive Domain Model as emerging with an ontogenetic form, and the advanced version of that illustrative characterization rather than the bare one. Ma notes that the most capable life pairs a rich inherited structure with a long life of learning, and the ADM is meant to do both. It is a learned model whose weights carry the domain's structure as a type-level invariant, a conserved grade, a physical dimension, an equivariance under a known group, expressed in the type system before any training example arrives, enforced during training, and discharged by a verifier, so the model holds that structure exactly rather than approximately. That is the inherited prior. The adaptation is the other half: the model goes on learning inside the prior, revising its own domain memory as the distribution shifts instead of freezing when training ends. The inherited structure is what most of this article treats; the lifelong adaptation is the "Adaptive" in the name. This is the contribution the section rests on, argued in full in our ADM pre-print on arXiv.

The contrast that defines it is when considering the dominant paradigm. A monolithic transformer learns whatever structure it acquires from data alone, as a statistical regularity with no formal status, true on average over the training distribution and subject to drift everywhere else. An Adaptive Domain Model does not form an emergent structure from over-parameterized data; it is informed within the shape of its provided domain, and learns only within that established bound. What's more, our [Resonant Recurrent Model]({{< ref "/docs/design/categorical-foundations/typed-recurrence-categorical-control" >}}) design lets these models be constrained at runtime, providing a scaffold for continuous learning.

## The highlight reel

Four points from our ADM pre-print carry the weight of everything downstream.

**Structure becomes a type, and the type is exact.** The pre-print's central move is to express domain structure in a grade-typed algebra, geometric algebra for the physical domains, so that a quantity's grade, a scalar, a vector, a bivector, is a type-level fact. Operations that would violate the grade structure do not type-check, and the structure that survives is exact, not a learned approximation, because the type governs it directly, fixing it in the weights the model fits.


**Precision survives training, where generic statistical structure can drift.** A learned regularity degrades under the very process that produced it: continued training, fine-tuning, distribution shift all erode a structure that was only ever an average. A typed invariant does not, because training optimizes within the admissible space the type defines rather than toward a structure it might leave. The pre-print's forward-mode-plus-quire discipline is what holds the invariant exact through training in finite-precision arithmetic, closing the gap between exact-arithmetic structure and machine structure.

**The structural zeros are provable, and useful.** A block-diagonal generator has a block-diagonal exponential, and the off-block entries are provably zero by the grade structure. Where a monolithic model would spend capacity learning that certain interactions are absent, and spend it imperfectly, an ADM has those absences as type-level facts. The model carries no parameters for interactions the domain forbids, which is the first hint of the hardware argument below.

The distinction the paragraph draws, the absent zero against the learned-near-zero, in the idiom. The Clef here is illustrative of the idiom rather than a finalized API surface.

```fsharp
// Dense: every interaction is a parameter.
type DenseGenerator = float<1>[,]               // n*n entries, all representable

// Structured: block-diagonal by type; off-block entries have no storage.
type BlockGenerator =
    { blocks : GradedBlock<Bivector>[] }        // only on-block entries exist

let exponential (g: BlockGenerator) : BlockTransform =
    g.blocks
    |> Array.map expBlock                       // block-diagonal in, block-diagonal out
    |> BlockTransform.assemble                  // off-block zeros never enter the sum
```

**The obligations are tiered and discharged, not asserted.** Structure that is decidable is discharged automatically at the appropriate tier; structure that is not is carried as an explicit obligation to a relational backend, with the faithfulness of each result recorded rather than assumed. The model does not merely claim its invariants; it carries the evidence that they hold, into the running artifact.

That is the reel, with the pre-print establishing the foundation. We expand on that design approach here.

## Why this produces better inference

The utility argument has three parts that compound.

**More precise, because the structure cannot drift.** An ADM's domain guarantees hold exactly and hold under training, so an inference rests on a conserved quantity, a dimensional consistency, an equivariance, each holding by construction rather than on average, even as the inference itself stays probabilistic. A monolithic model can produce a physically impossible result, a dimensional category error, a violated conservation law, and nothing in its architecture forbids it; the error is simply off the training distribution. In an ADM the corresponding error is not improbable but unrepresentable. For any task where correctness of structure matters more than plausibility of output, this is the difference between a result you can rely on and one you must check.

**Faster and simpler in hardware, because the model carries less.** A model that holds its structure as type-level fact does not spend parameters discovering that structure, and does not spend parameters representing interactions its domain forbids. The provable structural zeros are absent from the model rather than learned to be near zero. A typed domain model is therefore designed to be smaller than a monolithic model of comparable competence in its domain, with its computation running over a known-sparse structure rather than a dense one. Smaller and known-sparse is exactly what simpler hardware wants: less memory, fewer operations, and a computation whose shape is intended to be fixed at compile time rather than discovered at runtime. The same property that makes the model precise, structure carried as a type-level fact the weights inherit rather than learn, makes it cheap to run.

**Faster in interaction, because the work is divided.** This is the part that reframes the monolithic model itself. A single large transformer is asked to be competent at everything at once: language, reasoning, arithmetic, physical intuition, domain after domain, all in one undifferentiated parameter space, all at the precision the hardest sub-task demands. A constellation divides that labor. The domain models handle the parts where correctness can be guaranteed, precisely and on simple hardware, and the language model is relieved of carrying competence it held only weakly and expensively. It no longer has to be the thing that gets the physics approximately right or the arithmetic usually right; it routes those to models that get them exactly right. The interaction is designed to be faster because each part runs on hardware sized to its actual job, so the monolithic model need no longer be the bottleneck for work it was never well suited to.

## Where the language model fits, and why it is secondary

The argument so far is about the domain models, because they are the contribution. The language model enters as the component the constellation relieves and, in turn, the component the constellation must still contain, because something has to interface with the unstructured world. Natural-language intent, an underspecified goal, a partial program: these have no domain type, and no ADM can accept them directly. The language model is the porous node that takes in the unstructured and routes it to the domain models that can satisfy it.

That node cannot wear an ADM type, because the prior structure of language admits no compact formal specification, the precise boundary the pre-print draws around its own method. So the language model is built by other means. The domain models are the contribution and the source of the utility; the language model is the necessary interface that the contribution makes lighter, faster, and less burdened.

## The rest of this section

With ADM and its utility argument in place, the rest of the section examines the language node that completes the constellation, from several independent angles that can be read in any order. [*A Scaffold for Constrained Models*]({{< ref "scaffold-for-constrained-models" >}}) names the three commitments that carry a language component when the ADM type scaffold cannot apply, and carries the argument from the domain models to the language node. [*Building the Constrained Language Model*]({{< ref "building-the-model" >}}) treats its tuning and the deterministic layer that bounds its output. [*Architecture and Arithmetic*]({{< ref "architecture-and-arithmetic" >}}) and [*Forward-Mode and Low-Rank Adaptation*]({{< ref "forward-mode-and-adaptation" >}}) treat the substrate that makes it precise and cheap. [*The Constellation*]({{< ref "the-constellation" >}}) returns to this article's central claim and shows the porous node and the domain models composed into one system. [*Reversible Cores and Inference-Time Recall*]({{< ref "reversible-cores" >}}) reframes the framework's [negative and fractional types](https://arxiv.org/abs/2606.04352) as a design theory of reversible computation inside a model. And [*Adapting Inference on a Gradient*]({{< ref "adapting-inference-on-a-gradient" >}}) is the adoption side, how an organization runs the constellation today and adapts toward a built model along a gradient. Each is speculative and marks its own open questions.

The through-line: the typed domain model is the contribution, its utility is precision and simplicity and a divided workload, and the language model is the interface that contribution relieves and contains.
