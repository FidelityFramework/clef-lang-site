---
title: "The Utility of Adaptive Domain Models"
linkTitle: "Utility of Adaptive Domain Models"
description: "Domain models correct by construction, holding their domain's structure as a verified invariant"
date: 2026-06-08T00:00:00+00:00
weight: 1
authors:
  - SpeakEZ
tags: ["machine-learning", "adaptive-domain-models", "formal-verification", "architecture"]
draft: false
aliases:
  - /docs/design/constrained-machine-learning/why-adaptive-domain-models/
---

In a [talk on how intelligence evolved](https://www.youtube.com/watch?v=Az9sfy3jWNU), Yi Ma articulates two kinds that are essentially distinct based on transferred and learned bases. In the framing he presents, *Phylogenetic* intelligence belongs to the species: it is evolved under selection and written into an inheritance, and the individual that carries it only reacts based on that provided frame. ***Ontogenetic*** intelligence belongs to the individual that builds its own memory after birth, through perception, feedback, and the correction of error. By extension, he asserts that a "monolithic model" (his exact turn of phrase) is phylogenetic. Its training run is the evolution and its benchmark is the selection pressure, and the weights it ships are fixed after training, and the model operates within them without revising them. 

Following his example, we see our Adaptive Domain Model as emerging with an ontogenetic form, and the advanced version of that illustrative characterization rather than the bare one. Ma notes that the most capable life pairs a rich inherited structure with a long life of learning, and the ADM is meant to do both. It is a learned model whose weights carry the domain's structure as a type-level invariant: a conserved grade, a physical dimension, an equivariance under a known group. That structure is expressed in the type system before any training example arrives, enforced during training, and discharged by a verifier. The model holds it exactly rather than approximately. That is the inherited prior. The adaptation is the other half: the model keeps updating its domain memory as the distribution shifts, past the point where training would otherwise end. The lifelong adaptation is the "Adaptive" in the name. Our ADM pre-print on arXiv argues this in full.

The contrast that defines our ADM shows up against the dominant paradigm, the same contrast we first drew in essay form in [A Vision For Unified Cognitive Architecture]({{< ref "/blog/unified-cognitive-architecture" >}}). A monolithic transformer learns whatever structure it acquires from data alone, as a statistical regularity with no formal status, true on average over the training distribution and subject to drift everywhere else. An Adaptive Domain Model does not form an emergent structure from over-parameterized data; it is informed within the shape of its provided domain, and learns only within that established bound. What's more, our [Resonant Recurrent Model]({{< ref "/docs/design/categorical-foundations/structured-recurrence" >}}) design lets these models be constrained at runtime, providing a scaffold for continuous learning.

## Foundational Points

Our ADM pre-print establishes four points that the rest of this section develops.

**Structure becomes a type, and the type is exact.** The pre-print's central move is to express domain structure in a grade-typed algebra, geometric algebra for the physical domains, so that a quantity's grade, a scalar, a vector, a bivector, is a type-level fact. Operations that would violate the grade structure do not type-check, and the structure that survives is exact, not a learned approximation, because the type governs it directly, fixing it in the weights the model fits.


**Precision survives training, where generic statistical structure can drift.** A learned regularity degrades under the very process that produced it: continued training, fine-tuning, distribution shift all erode a structure that was only ever an average. A typed invariant does not, because training optimizes within the admissible space the type defines rather than toward a structure it might leave. The pre-print's forward-mode-plus-quire discipline is what holds the invariant exact through training in finite-precision arithmetic, closing the gap between exact-arithmetic structure and machine structure.

**The structural zeros are provable, and useful.** A block-diagonal generator has a block-diagonal exponential, and the off-block entries are provably zero by the grade structure. Where a monolithic model would spend capacity learning that certain interactions are absent, and spend it imperfectly, an ADM has those absences as type-level facts. The model carries no parameters for interactions the domain forbids, so it computes less where a monolithic model would spend capacity representing those absences.

In our language the absent zero occupies no storage where the learned-near-zero occupies a parameter: The Clef here is illustrative of the idiom rather than a finalized API surface.

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

**The obligations are tiered and discharged, not asserted.** Structure that is decidable is discharged automatically at the appropriate tier; structure that is not is carried as an explicit obligation to a relational backend, with the faithfulness of each result recorded rather than assumed. The model does not merely claim its invariants. It carries the evidence that they hold into the running artifact.

## Why this would produce better inference

The utility gains compound: more precise structure permits simpler hardware, and simpler hardware permits a divided workload.

**More precise, because the structure cannot drift.** An ADM's domain boundaries hold structurally under training. So an inference rests on a conserved quantity, a dimensional consistency, an equivariance, each holding by construction rather than on average, even as the inference itself stays probabilistic. A monolithic model can produce a physically impossible result, a dimensional category error, a violated conservation law, and nothing in its architecture forbids it. The error is off the training distribution. In an ADM the corresponding error is unrepresentable. For any task where correctness of structure matters more than plausibility of text output, this is the difference between a result with a confidence interval you can rely on and an unstructured token output you must check by hand.

**Faster and simpler in hardware, because the model carries less.** A model that holds its structure as type-level fact does not spend parameters discovering that structure, and does not spend parameters representing interactions its domain forbids. The provable structural zeros are absent from the model rather than learned to be near zero. A typed domain model is therefore designed to be smaller than a monolithic model with advantaged competence in its domain, with its computation running over a known-sparse structure rather than a dense one. Smaller and known-sparse is exactly what simpler hardware benefits from: less memory, fewer operations, and a computation whose shape is intended to be bounded at compile time rather than emergent in a sea of over-parameterized weights. The same property that makes the model focused, structure carried as a type-level fact the weights inherit rather than learn, also makes it efficient to run.

**Faster in interaction, because the work is divided.** Dividing the work also changes the monolithic model's role. A single large transformer is asked to be competent at everything at once: language, reasoning, arithmetic, physical intuition, domain after domain, all in one undifferentiated parameter space, all at the precision the hardest sub-task demands. A constellation divides that labor. The domain models handle the parts where correctness can be guaranteed, precisely and on simple hardware, and the language model no longer needs parameters for those sub-tasks, which it handled poorly and at high cost. It no longer has to be the thing that gets the physics approximately right or the arithmetic usually right; it routes those to models that produced a bounded inference that has structured confidence. The interaction is designed to be faster because each part runs on hardware sized to its actual job, so the monolithic model need no longer be the bottleneck for work it was never well suited to.

Our reading is that Ma frames both generation and inference as constrained optimization for Bayesian inference on a low-dimensional distribution: recover the estimate consistent with an observation, under the constraint that it lies on the structured manifold the data occupies. Our Adaptive Domain Model realizes that picture by construction. The manifold is the typed domain, the constraint is a property the type discharges rather than a penalty the optimizer trades against, and the estimate returns as a Gaussian posterior, the confidence interval a domain model provides to its caller. We built that posterior because in our design a domain model should report how sure it is. Finding Ma describe the same operation from the representation-learning side told us the two lines likely converge from different disciplines. The posterior was part of the Adaptive Domain Model design from its own principled origins, in place before the Gaussian became a focus of representation learning.

That leaves our framework carrying Gaussian at two layers, and one of them aligns with Ma's thesis. The aligning instance estimates on the manifold: the posterior just described. The other certifies the manifold is the shape the types claim: Gaussian elimination, the polynomial [Tier-1 decision procedure]({{< ref "/docs/design/categorical-foundations/formal-verification-compilation-byproduct" >}}) our dimensional types inherit from Kennedy's units of measure, settling dimensional and grade consistency before an inference construct is built. Gaussian elimination is the verification the framework brings of its own, decidable and run before any estimate is drawn.

## Where the Language Model Fits

The preceding sections treat the domain models. The language model enters as the component the constellation relieves and, in turn, the component the constellation must still contain, because something has to interface with the unstructured world. Natural-language intent, an underspecified goal, a partial program: these have no domain type, and no ADM can accept them directly. The language model is the porous node that takes in the unstructured and routes it to the domain models that can satisfy it.

That node cannot wear an ADM type, because the prior structure of language admits no compact formal specification, the precise boundary the pre-print draws around its own method. So the language model is built by other means. The domain models are where the utility originates. The language model is the interface the constellation still needs, and offloading the structured work to the domain models leaves it smaller and faster to run.

## The rest of this section

With ADM and its utility argument in place, the rest of the section examines the language node that completes the constellation, from several independent angles that can be read in any order. [*A Scaffold for Constrained Models*]({{< ref "scaffold-for-constrained-models" >}}) names the three commitments that carry a language component when the ADM type scaffold cannot apply, and carries the argument from the domain models to the language node. [*Building a Constrained Language Model*]({{< ref "building-the-model" >}}) treats its tuning and the deterministic layer that bounds its output. [*Architecture and Arithmetic*]({{< ref "architecture-and-arithmetic" >}}) and [*Forward-Mode and Low-Rank Adaptation*]({{< ref "forward-mode-and-adaptation" >}}) treat the substrate that makes it precise and cheap. [*The Constellation*]({{< ref "the-constellation" >}}) returns to this article's central claim and shows the porous node and the domain models composed into one system. [*Reversible Cores and Inference-Time Recall*]({{< ref "reversible-cores" >}}) reframes the framework's [negative and fractional types](https://arxiv.org/abs/2606.04352) as a design theory of reversible computation inside a model. And [*Adapting Inference on a Gradient*]({{< ref "adapting-inference-on-a-gradient" >}}) is the adoption side, how an organization runs the constellation today and adapts toward a built model along a gradient. Each is speculative and marks its own open questions.
