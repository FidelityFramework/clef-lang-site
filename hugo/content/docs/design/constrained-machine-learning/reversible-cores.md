---
title: "Reversible Cores and Inference-Time Recall"
linkTitle: "Reversible Cores"
description: "Where the framework's own type machinery reaches back into the language component: negative types, verified-exact reconstruction, and a cache that is partly virtual"
date: 2026-06-08T00:06:00+00:00
weight: 7
authors:
  - SpeakEZ
tags: ["machine-learning", "language-model", "memory-model", "formal-verification"]
draft: false
---

> Part of the Constrained Machine Learning section, and the one that postulates
> a frontier rather than bounding a single language node. Where other articles build and bound
> a language node, this one asks a different kind of question: what happens
> when the framework's type-theoretic contribution, the
> [negative and fractional types](https://arxiv.org/abs/2606.04352) developed for the memory model, is turned on
> the internals of a model itself? The answer sketched here is a piece of
> design theory, not an engineering recipe. It proposes that a type long
> understood as a memory-management construct is also a statement about
> reversible computation inside a learned model, and it treats that proposal
> as a research opportunity worth pursuing precisely because the field has not
> yet asked type theory to do this work. Speculative throughout; the open
> questions are the point, not a caveat.

## A type with two readings

The negative and fractional types were developed for a concrete purpose: reasoning about memory, lifetimes, and reversible allocation in the Clef memory model. A negative type, in that setting, is the additive adjoint of a value, the formal trace of something given back, and the discipline lets the compiler reason about allocation and reclamation as exact inverses rather than as a runtime bookkeeping convention.

This article advances a claim that is larger than the engineering use that follows from it. A type that expresses exact reversibility is not only a memory-management construct. It is a statement about computation, that a step can be undone exactly, and a learned model is full of steps. The thesis of this article is that the negative type has a second reading, as a design-time assertion that a piece of a model's computation is reversible with proven exactness, and that this reading opens a line of work the field has not pursued: using type theory not to verify a model after it is built, but to shape what the model is, by making reversibility a property the type system carries and the compiler exploits.

This is design theory, and the section presents it as such. The engineering payoff below, a recurrent cache that is partly virtual, is real and worth having, but it is the first instance of the idea, not its boundary. The reason to take the idea seriously is that it points at a genuinely useful exertion of type theory into model building, a place where a type-theoretic contribution does work that statistical machine learning has no other way to do. The field has reversible architectures, built by convention and trusted by construction. It does not have a type discipline that makes reversibility a proven, compiler-exploitable property of a model's internals. That gap is the opportunity, and the rest of this article is the first probe into that opportunity.

## A storage problem at inference, not training

Recurrent and cache-augmented sequence models hold a memory of earlier hidden states so that a long context stays available for recall. As the sequence proceeds, checkpoints of hidden states accumulate, the effective memory grows with sequence length, and the cost being managed is storage: holding every hidden state across a long context is expensive, and the management question is which states to keep.

This is an inference-time concern, and the distinction from training-time activation checkpointing is load-bearing. The [forward-mode article]({{< ref "forward-mode-and-adaptation" >}}) discussed the activation tape that reverse-mode training stores and forward-mode avoids; that is a training concern, recovered in a structured backward sweep. Inference recall is different. There is no backward gradient pass at inference. The only requirement is to recover an earlier hidden state when the current computation needs to attend to it, and the access pattern is arbitrary: the current token may need a state from far back, or several scattered states, depending on what it attends to.

## Reversibility substitutes computation for storage

If the recurrent state transition is reversible, the earlier states need not be stored at all. The model holds the current state, and a state from some steps back is reconstructed by applying the inverse transition that many times. This is the established reversible-computing answer to a storage-versus-recomputation tradeoff, and the substitution itself is prior art from the reversible-residual and reversible-RNN literature. What the framework adds is a type discipline and an exactness guarantee.

The negative type is the additive adjoint that carries the backward transition as a first-class construct. The same machinery the memory model uses for inferring lifetimes and reasoning about reversible allocation supplies, here, an inverse transition that exists by the type discipline rather than by architectural convention:

```fsharp
// A reversible state transition carries its inverse as a typed adjoint.
// Reconstructing a state k steps back runs the adjoint k times,
// in place of storing k checkpoints.
let reconstruct (current: HiddenState) (k: int) (step: ReversibleTransition) : HiddenState =
    let inverse = Negative.adjoint step    // the backward transition, typed
    Seq.fold (fun s _ -> inverse s) current (Seq.replicate k ())
```

Because the access pattern is arbitrary, reversibility does not eliminate the cache; it changes the operating point. Recovering a state a few steps back is cheap; recovering one far back costs many inverse applications. The natural structure is hybrid: store sparse anchor checkpoints, and reconstruct the states between them by running the adjoint from the nearest anchor. This is the sparse-anchor-plus-reconstruction pattern of gradient checkpointing, transposed to inference recall and powered by the typed adjoint rather than by recomputation from a stored input.

## Verified-exact reconstruction is the load-bearing property

The reversible-architecture literature builds models reversible by construction and trusts the construction. The framework does more: the state transition is typed so that its adjoint exists and the round trip is the identity, and that obligation is discharged through the verification machinery. When an earlier state is reconstructed, the reconstruction is required by the type discipline to recover the exact state, not an approximation.

For inference recall this is exactly the property that matters, because the purpose of the cache is fidelity of recall. If a reconstructed state drifts from the true earlier state, the recall is silently corrupted, and the model continues producing plausible output from a wrong state, which is the kind of error that does not announce itself. A verified-exact reconstruction removes that failure mode by construction, and it is a stronger guarantee than architectural reversibility, because the property carried is the exactness of the round trip, not merely its existence.

The exactness depends on the arithmetic, which is where this article rejoins the [architecture article]({{< ref "architecture-and-arithmetic" >}}). A reversible transition is reversible in exact arithmetic and not under rounding, because the forward and inverse operations do not round to inverse results. The quire and b-posit discipline that keeps the rate objective sharp is the same discipline that makes the round trip exact rather than approximately reversible, because exact accumulation is what closes the gap between exact-arithmetic reversibility and machine reversibility. The reversibility guarantee is conditional on the same substrate the rest of the section already committed to.

## Store versus reconstruct as a compile-time placement decision

How much of the cache to materialize versus reconstruct on demand is not a runtime heuristic; it is a coeffect the compiler resolves, in exactly the way the memory model resolves where values live. On a memory-constrained inference target, an edge accelerator, the compiler stores sparse anchors and reconstructs aggressively through the adjoint, spending compute to save memory. On a memory-rich target it stores densely and reconstructs little, spending memory to save compute. The escape and lifetime discipline that already decides placement extends with a store-versus-recompute axis for the recurrent cache, resolved against the target's memory hierarchy and the model's recall-distance profile.

This is the layer the model-architecture literature does not reach. A published cache mechanism proposes a cache and ways to manage it at the model layer. The placement of that cache, the operating point between storage and reconstruction, and the per-target variation of that operating point are compilation concerns, and they are the framework's heterogeneous-targeting claim applied to the inference cache. A reversible model variant compiled through the framework has a cache that is partly virtual: reconstructed on demand with verified fidelity, and placed at the operating point each target's memory hierarchy dictates.

## The binding constraint, stated honestly

Only the reversible part of a transition can be reconstructed this way. A transition that discards information, a lossy nonlinearity or a dimension-reducing projection, has no adjoint, and the prior state cannot be recovered by running backward. This is the constraint the reversible-architecture literature lives under, and the framework does not relax it. Recurrent models compress context precisely through lossy operations, so a published cache-augmented model is not reversible, and this machinery does not extract reversibility from a model that was not built to have it.

The substitution applies to a model designed to be reversible in Clef, where the state transition is kept information-preserving so the adjoint exists, and whatever representational cost that constraint imposes is accepted in exchange for the virtual-cache and verified-recall benefits. Whether a usefully expressive recurrent model can be kept reversible enough for its recall to be reconstructable is the open question, and it is the same question the reversible-architecture literature wrestles with. The framework does not lower that price. It makes the reversibility a verified property and exploits it for placement once paid.

## Reversibility and the sub-quadratic form are the same generator

There is a connection here to the sub-quadratic thread the [constellation article]({{< ref "the-constellation" >}}) develops, and it is not a coincidence of vocabulary. A model is sub-quadratic precisely when it summarizes the past in a recurrent state instead of re-attending to every earlier token, and the summary is produced by a state transition that is the exponential of a generator. A model is reversible precisely when that state transition has an exact adjoint, the exponential of the negated generator. The two properties are read off the same object: a transition built from a graded one-parameter-group generator is sub-quadratic because it is a recurrence, and reversible because the generator negates. The decay-and-rotation generator that gives the linear-attention and state-space families their sub-quadratic cost is, when it is information-preserving, exactly the reversible transition this article reconstructs from.

This tightens the whole thread. The field's sub-quadratic models drift because their generator is untyped, as the constellation article argues; the same untyped generator is also not cleanly reversible, because floating-point forward and inverse steps do not compose to the identity. There is a current illustration of the stakes: [Mamba-3](https://arxiv.org/abs/2603.15569) reintroduced complex-valued state transitions precisely because the rotational dynamics they provide add expressivity, and a rotation is the one transition that is exactly invertible by negating its angle. The newest models are therefore moving toward generators that are, in principle, cleanly reversible, while still computing them in arithmetic that does not preserve the round trip. Typing the generator buys three things at once from one construction: the sub-quadratic cost, because it is a recurrence; the exact structure, because the grade decomposition holds through training; and the verified reversibility this article needs for inference-time recall, because the adjoint is exact. Sub-quadratic, structurally exact, and reversible are three consequences of one typed generator, and the hardware payoff would compound accordingly, a model that is designed to be linear in context, sparse in structure, and able to trade stored state for recomputed state against each target's memory hierarchy.

## The case for reversibility: limiting drift in recall

It is worth stepping out of the algebra to say plainly what reversibility buys, because the practical consequence is larger than the storage saving that motivated it. A sub-quadratic model works by compressing the entire past into a fixed-size state. When a later token needs something from earlier in the sequence, the model is relying on that state to still faithfully contain it. In the ordinary construction there is no way to check: the state is whatever the recurrence produced, recall is whatever the state yields, and if the state has quietly drifted from what the earlier tokens actually established, the model produces a confident answer from a corrupted memory and nothing flags it. This is the failure mode that makes long-context behavior in these models hard to trust, and it is invisible precisely because the model never reconstructs the earlier state to compare against.

A reversible, verified transition changes the kind of guarantee available. Because the transition has an exact adjoint, an earlier state is not merely *estimated* from the current one; it is *recovered*, and recovered to the exact value it held, with the round-trip identity discharged rather than assumed. That is the step change: it moves the reliability of long-range recall from a property one hopes the training instilled to a property the construction guarantees, and it limits drift not by training against it but by making the backward step exact.

### Two senses of reversible, kept apart

This is the point where the claim must be precise, because it has two senses that are easy to conflate and a careful reader will rightly separate them. There is *structural* reversibility, a static property carried in the type strata: the negative type is the adjoint of the transition, and the type system certifies that an inverse exists and that the round trip is the identity. And there is the *runtime traversal mechanism* itself: an attention head whose state actually steps backward, in real arithmetic on real hardware, to reconstruct an earlier value during inference. The first is a guarantee about the program; the second is a thing the program does. The framework's contribution is to make the second sound by discharging the first, but they are not the same claim, and the runtime mechanism deserves to be shown rather than asserted.

Structurally, the transition and its adjoint are a typed pair. The forward step advances the state; the backward step is the adjoint, and the type discipline certifies they compose to the identity:

```fsharp
// Structural reversibility: the type strata carry the adjoint as a first-class
// pair, and the round-trip obligation is discharged once, statically.
type ReversibleStep<'State> =
    { forward  : 'State -> 'State
      backward : 'State -> 'State          // the negative-type adjoint
      // Discharged at compile time: backward (forward s) = s, exactly.
      roundTrip : RoundTripWitness<'State> }

// For a generator-based head the pair is the exponential and its negation,
// which is why a rotational (complex) transition inverts cleanly: negate the angle.
let stepFromGenerator (g: GradedGenerator<Bivector>) : ReversibleStep<HeadState> =
    { forward   = applyExp g                 // exp(+g): advance one position
      backward  = applyExp (Generator.negate g)   // exp(-g): retreat one position
      roundTrip = Verifier.dischargeRoundTrip g }  // exp(-g) ∘ exp(+g) = id
```

The runtime traversal is where this becomes a mechanism rather than a guarantee. An attention head carrying a reversible recurrence holds its current state and can run the backward step to reach an earlier one, on demand, during inference. The traversal is the forward recurrence's mirror, and it is the same arithmetic substrate that makes it exact rather than approximately reversible:

```fsharp
// Runtime traversal: the head's state actually steps backward to reconstruct
// an earlier position. This runs at inference, in the head, on the b-posit and
// quire substrate that makes the backward arithmetic compose to the forward's
// inverse rather than merely close to it.
let recallAt (head: ReversibleHead) (current: HeadState) (stepsBack: int) : HeadState =
    // Each backward application is one position of the recurrence run in reverse.
    // Because the step is generator-based and the arithmetic is exact, the state
    // reached is the exact state the head held `stepsBack` positions ago, not a
    // reconstruction that has accumulated rounding.
    let step = head.reversibleStep
    let rec retreat state n =
        if n = 0 then state
        else retreat (step.backward state) (n - 1)
    retreat current stepsBack
    // The recalled state can now be compared against, or attended to, with the
    // certainty that it is what the head actually held, not a plausible estimate.
```

### How this behaves in the runtime strata of the heads

The mechanism's behavior at runtime is the part that determines whether the guarantee is worth its cost, and it is honest to lay it out concretely. A reversible head does not store the full history of its states; it stores the current state and, by the [placement decision]({{< ref "reversible-cores" >}}) the compiler makes, a sparse set of anchor states. Recall of an earlier position runs the backward step from the nearest anchor, so the runtime cost of a recall is the number of backward steps from that anchor, traded against the memory the anchors would otherwise occupy.

The traversal looks like this at runtime. The forward recurrence advances the head state position by position, materializing an anchor only at a chosen stride; when a later position needs to recall an earlier state, the head retreats from the nearest anchor by running the backward step, reconstructing the intervening states exactly rather than reading them from a store that was never kept:

```mermaid
graph LR
    subgraph Forward["Forward recurrence (inference pass)"]
        S0["state s0<br/>ANCHOR"] -->|"exp(+g)"| S1["s1"]
        S1 -->|"exp(+g)"| S2["s2"]
        S2 -->|"exp(+g)"| S3["s3"]
        S3 -->|"exp(+g)"| S4["state s4<br/>ANCHOR"]
        S4 -->|"exp(+g)"| S5["s5 current"]
    end

    subgraph Stored["What the head actually holds"]
        A0["anchor s0"]
        A4["anchor s4"]
        CUR["current s5"]
    end

    subgraph Recall["Recall of s2, on demand"]
        R4["from nearest<br/>anchor s4"] -->|"exp(-g)"| R3["s3 recovered"]
        R3 -->|"exp(-g)"| R2["s2 recovered<br/>EXACT"]
    end

    S0 -.materialized.-> A0
    S4 -.materialized.-> A4
    S5 -.materialized.-> CUR
    A4 ==>|"backward traversal"| R4
```

In the heads, this means a recall is not a memory lookup but a short backward recurrence, executed in the same head that runs the forward pass, on the same b-posit and quire arithmetic. The exactness is what the substrate is carrying: a backward step in IEEE-754 would not land on the forward step's inverse, and the reconstructed state would drift from the true one, which would defeat the entire purpose; the quire's exact accumulation is what makes `backward (forward s)` equal to `s` on the machine and not merely in the idealized algebra. The diagram's two anchors and three-step retreat are the operating point the compiler chose; a memory-richer target would place anchors more densely and retreat fewer steps, a memory-poorer one would place them sparsely and retreat more, and the recall is exact regardless of where on that spectrum the target sits.

There is a real cost and it should be named. A reversible head trades compute for trust: where an ordinary head reads whatever its single state yields, a reversible head may run several backward steps to reconstruct the state it needs, and it accepts the constraint that its transition be information-preserving, which the [binding constraint]({{< ref "reversible-cores" >}}) section above states is not free. What the trade buys is the thing the ordinary head cannot offer at any price: a recall that is provably the value the head held, on hardware, at inference time, rather than an estimate the model hopes is faithful. For an attention mechanism meant to carry information across a long context, the difference between "the state probably still contains this" and "the state provably still contains this" is the difference between a mechanism you must validate empirically and one whose recall is correct by the same discipline that bounds the rest of the constellation. Reliability stops being a thing measured after training and becomes a thing built into the traversal, which is the whole posture the framework takes everywhere else, now reaching the one place a sub-quadratic model is most likely to fail silently.

## The bookend: a graded generator on both sides

The reversible core connects to the positional-encoding structure the constellation article isolates. The [constellation article]({{< ref "the-constellation" >}}) showed that the admissible positional encodings are one-parameter subgroups, the exponential of a graded generator, with the antisymmetric part a rotor and the defective part a translator. A reversible state transition is itself a one-parameter-group action when it is the exponential of a generator, and its inverse is the exponential of the negated generator. The reversibility this article relies on is therefore the same group-theoretic object the positional-encoding analysis isolated: where the transition is built from graded generators, the adjoint is exact and grade-preserving by the same algebra that keeps the positional-encoding generator inside its admissible decomposition.

That is a strong hint that the typed positional encoding the constellation article wants and the reversible core this article wants are two applications of one structure. A transition built from graded one-parameter-group generators would be simultaneously the encoding the one wants and the reversible core the other wants. Whether the two coincide in a usable architecture is a discovery question, and it is the question this frontier turns on: the framework's type machinery, withheld from the language model's bulk by the scope rule of the scaffold article, reaches back in through the one door the algebra leaves open, the graded generator that is at once an encoding and an adjoint.

## The frontier, stated as such

The honest status of this article is that it proposes a design theory and works one instance through. The instance, a verified-reversible cache placed by the compiler against a target's memory hierarchy, is enough to show the idea is not vacuous: the work it would do is checkable. But the instance is not the contribution. The contribution is the reframing of a type, from a memory-management construct into a design-time assertion about reversible computation inside a model, and the claim that this reframing opens a useful and largely unexplored line of work.

The field has spent its type-theoretic energy on verification after the fact, checking that a trained artifact has properties it was hoped to have. The negative type points the other way, at construction: shaping what a model is by carrying a proven property, exact reversibility, through the type system and into the compiler's placement decisions. That is a different relationship between type theory and machine learning than the field currently has, and it is the relationship the whole [ADM program](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}) argues for, here pushed one step past the typed domain models into the internals of a learned transition. The reason to be excited about it is not that the cache trick saves memory. It is that a type theory built for memory turns out to have something exact and useful to say about the computation a model performs, which is the kind of result that suggests the type-theoretic contribution to this field has barely been started.

This is also the point where the framework's reading of [*Principles and Practice of Deep Representation Learning*](https://ma-lab-berkeley.github.io/deep-representation-learning-book/) reaches furthest from the common one. The book's §6, on consistent and self-consistent representations, is the closest the book comes to reversibility: closed-loop transcription and autoencoding both turn on a representation that can be carried forward and recovered. The common reading treats that recovery as an approximate reconstruction trained to be good enough, a reconstruction loss minimized. The framework reading asks for the round trip to be *exact by type*, the negative type's adjoint discharged to identity, which is a property §6's autoencoding objective approaches asymptotically and never asserts. Where the book trains toward self-consistency, the framework types it. That is the same alignment-and-divergence the rest of the section describes, carried into the one corner of the book that already gestures at reversibility, and it is why this frontier is continuous with the program rather than a departure from it.

## A research agenda, not a list of caveats

The open questions below are the shape of that frontier, and they are stated as an agenda to pursue rather than as risks to disclaim.

Whether a recurrent transition can be kept reversible enough to be reconstructable while remaining expressive enough to be useful, and what representational cost the constraint imposes, is the question that decides how far the design theory reaches into real architectures. It is the same question the reversible-architecture literature wrestles with, and the framework's contribution is to make the reversibility a proven property rather than to lower its price.

Whether the quire-and-b-posit discipline makes the round trip exact for the transitions of interest, or whether additional discipline is needed at the hidden-state arithmetic, is the substrate question continuous with the [architecture article's]({{< ref "architecture-and-arithmetic" >}}) posit-taper experiment. The two share an apparatus, because both ask how exactness behaves under the framework's numerics during real computation.

Whether the store-versus-reconstruct operating point can be derived from the target memory hierarchy and a recall-distance profile, or must be tuned empirically, is the compilation question, and it is where the design theory meets the heterogeneous-targeting claim the rest of the framework already makes.

And whether a transition built from graded one-parameter-group generators serves at once as positional encoding and reversible core is the question that would unify this article with the constellation keystone, and the one most worth chasing, because a positive answer would mean a single typed object is doing double duty as the structure attention needs and the structure reversibility needs. That would be the clearest evidence yet that the type-theoretic frame is not decoration on a model but a genuine account of its structure.

For the practical counterpart to this theoretical material, how an organization works with a model that exists today and adapts toward one built to fit the constellation, see [*Adapting Inference on a Gradient*]({{< ref "adapting-inference-on-a-gradient" >}}).
