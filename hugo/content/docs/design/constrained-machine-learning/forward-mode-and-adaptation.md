---
title: "Forward-Mode and Low-Rank Adaptation"
linkTitle: "Forward-Mode Efficiency"
description: "How forward-mode gradients over a low-rank derived structure make building and adapting cheaper"
date: 2026-06-08T00:04:00+00:00
weight: 5
authors:
  - SpeakEZ
tags: ["machine-learning", "automatic-differentiation", "posit-arithmetic", "efficiency"]
draft: false
---

Reverse-mode automatic differentiation, the backpropagation that trains essentially every large model, earns its dominance by amortizing a single backward pass over an arbitrary number of parameters. Compute the loss forward, sweep the gradient backward once, and every parameter receives its derivative from that one sweep. The cost is the activation tape: every intermediate value computed on the forward pass must be stored, because the backward pass needs it. For a large model over a long sequence the tape is the dominant memory cost of training.

That accounting rests on two assumptions. The parameter space is large and unstructured, so amortizing one backward pass over all of it is the efficient choice. And the arithmetic is cheap and noisy, so storing and reloading activations is acceptable and the gradient need only be approximately right. A derived architecture on precise arithmetic violates both assumptions, and violating them changes which mode of differentiation the accounting favors.

## Forward-mode over the directions that matter

[Forward-mode differentiation](https://arxiv.org/abs/2202.08587) propagates a directional derivative alongside the primal computation, in the same forward pass, with no tape and no backward sweep. Its textbook weakness is that one forward pass yields the derivative along one direction, so recovering a full gradient costs one pass per input dimension, which is hopeless when the input dimension is the whole parameter space. This is why reverse-mode dominates: for a model with millions of parameters, millions of forward passes is absurd and one backward pass is not.

Our derived architecture removes the premise of that weakness. The directions along which the objective actually moves are not the full ambient parameter space; they are the low-dimensional subspace structure the architecture is built around. The coding-rate objective operates on a union of low-dimensional subspaces, and the meaningful gradient directions live in that union, not in the undifferentiated cloud of all parameters. So the number of tangent directions that need propagating is set by the rank of the derived structure, not by the raw parameter count.

```fsharp
// Multi-tangent forward-mode: propagate a batch of directional derivatives
// alongside the primal, one pass, no stored activation tape. The tangent set
// spans the derived structure's rank, not the full parameter space.
let forwardGradient
        (model: DerivedModel)
        (tangents: TangentBasis)   // spans the low-rank structure, |tangents| small
        (input: Batch) : Gradient =
    tangents
    |> propagateAlongside model input   // primal and all tangents in one pass
    |> assembleGradient                  // gradient over the structured subspace
```

Multiple tangents are what make this competitive. A multi-tangent forward pass carries a batch of directional derivatives at once, and if that batch spans the derived structure's rank, a single structured forward pass would yield the gradient over the directions that matter, with no tape. Our architecture supplies the low rank that makes the tangent set small; the forward pass supplies the gradient without the reverse-mode storage. Each rescues the other from its worst case, and neither rescue is available to a black-box model, which has no principled small set of directions, or to a floating-point model, whose accumulated tangents would be too noisy to trust at the bit-widths in play.

One designated primitive of that pass, written out. A dual value carries the primal and the whole batch of tangents together, and a single op advances both in lockstep, so the derivative travels alongside the value with no tape recording it:

```fsharp
// A dual value: the primal and the batch of directional derivatives that ride
// with it. The tangent width is the derived structure's rank r, so the batch is small for a structural reason.
type Dual<[<Measure>] 'Dim> =
    { Primal   : BPosit<'Dim>
      Tangents : BPosit<'Dim>[] }          // length r, one per basis direction

// One forward-mode step: advance the primal and all r tangents in a single pass.
let stepDual (u: GradedSubspaceBasis<Bivector>) (z: Dual<1>[]) : Dual<1>[] =
    z
    |> Array.map (fun d ->
        let p  = compressAgainst u d.Primal |> Quire.round   // primal advances
        let dt =
            d.Tangents
            |> Array.map (fun t ->
                jvp (compressAgainst u) d.Primal t           // J·t, the directional derivative
                |> Quire.accumulate)                         // no intermediate rounding
            |> Array.map Quire.round
        { Primal = p; Tangents = dt })
// The gradient over the r directions is read off the Tangents fields.
```

## Precise arithmetic is what makes the tangents trustworthy

The forward-propagated tangent is itself a long accumulation, and its quality is the same accumulation-precision question the [architecture article]({{< ref "architecture-and-arithmetic" >}}) raised for the rate objective. The precision pillar and the efficiency pillar are not two separate choices. The quire that keeps the coding-rate separation sharp is the same quire that keeps the forward tangent accurate, because both are long accumulations carried without intermediate rounding. Committing to [b-posit](https://arxiv.org/abs/2603.01615) and the quire for the architecture's sake is intended to give the trustworthy tangents the efficiency mechanism needs, at no additional cost. The two pillars share one substrate decision.

## Where the Saving Concentrates

The payoff is sharpest where this section's models are built and rebuilt. The [building article]({{< ref "building-the-model" >}}) commits the tuning to low-rank adaptation throughout: a stable functional base with a swappable Clef adapter over it, the adapter warm-rotating as the language evolves. A low-rank adapter is a natural fit for multi-tangent forward-mode, because the adapter's trainable space is already low-rank by construction, so the tangent set that spans it is small for a reason independent of the architecture's own rank. Adapting the model means taking gradients over the adapter's handful of dimensions, in a few storage-free forward passes, instead of taping activations through the entire base for a backward sweep.

The adapter's rank is carried in its type, which is what makes the tangent set the right size by construction rather than by choice. The Clef here is illustrative of the idiom rather than a finalized API surface:

```fsharp
// A low-rank adapter, update A·Bᵀ, with rank r in the type and the base frozen.
type LoraAdapter<[<Measure>] 'Dim, 'Rank> =
    { Down : Matrix<BPosit<'Dim>, 'Rank>     // B: full dim down to rank r
      Up   : Matrix<BPosit<'Dim>, 'Rank> }   // A: rank r back up to full dim

// The tangent basis is the adapter's 2·r columns, derived from the type.
// The forward pass needs no more tangents than this.
let tangentBasis (adapter: LoraAdapter<'Dim, 'Rank>) : TangentBasis =
    TangentBasis.spanning [ Param.columns adapter.Down
                            Param.columns adapter.Up ]   // |basis| = 2·r
```

Distillation has the same shape. Distilling from a teacher into a student adapter, or refreshing the Clef adapter against an evolved grammar, is a low-rank fine-tuning operation, the one performed most often in practice. These are the regimes where a few storage-free forward passes cost less than a full reverse-mode pass with its tape, and they are the regimes the constellation lives in, because a constellation of domain models plus a language component is rebuilt and re-adapted far more often than it is trained from scratch.

The over-parameterization the scaffold article flagged is partly an artifact of reverse-mode itself. Reverse-mode's amortization rewards piling on parameters, since more parameters cost nothing extra per backward pass, so the incentive runs toward larger models. Forward-mode over a derived low-rank structure removes that incentive from the other side: there is no amortization bonus for extra parameters, and the cost scales with the structure's rank, so the economic pressure runs toward keeping the structure tight. The efficiency argument and the precision argument and the parameter-economy argument are three views of one decision.

This is also the sharpest illustration of the two readings of the [book](https://ma-lab-berkeley.github.io/deep-representation-learning-book/) named in [*Architecture and Arithmetic*]({{< ref "architecture-and-arithmetic" >}}). The book's §3.3 characterizes, in coding-rate terms, the conditions under which training generalizes rather than memorizes, and a derived architecture is supposed to generalize because its structure is low-rank by construction. The common reading absorbs that result and then trains the model with the reverse-mode-plus-dense-parameters toolkit that *creates the incentive to over-parameterize*, working against the very low-rank structure §3.3 says is the point. The model carries far more capacity than its derived structure needs, and the surplus is spent memorizing what the structure was meant to let it generalize past. The framework-informed reading takes the low-rank structure as real and matches the training method to it: forward-mode over the rank the architecture actually has, so the model stays as small as its derivation implies.

## The open question that decides it

The cost of multi-tangent forward-mode scales with the number of tangents, so the entire claim rests on the structured subspace being genuinely low-dimensional throughout training, not merely at the optimum. A derived structure is an emergent attractor, and early in training, before the model has converged toward the separated subspaces, the set of meaningful directions may be wider than the converged rank. If the effective rank starts high and only narrows late, the tangent budget early in training could erase the advantage.

This is measurable, and it is the companion to the posit-taper experiment from the [architecture article]({{< ref "architecture-and-arithmetic" >}}). Both ask how the rate-reduction objective behaves dynamically under the framework's numerics, during training, not at the fixed point. The measurement is the effective rank of the meaningful-direction set as a function of training step. If it stays low throughout, the orders-of-magnitude claim holds for the build-and-adapt regime as stated. If it spikes early, the claim narrows to late-stage adaptation, which is still the most common operation but a smaller prize. The honest version of the efficiency thesis is conditional on this curve, and the article states it as conditional.

## Scope of the claim

The efficiency thesis is a claim about build time and adaptation, and the article holds it there deliberately. Taking gradients cheaply over a low-rank derived structure makes training and fine-tuning cheaper. It is not a claim about inference-time cost against a frontier black-box model, which has its own efficiency regime, nor about total training FLOPs in a from-scratch comparison where the derived architecture's own forward cost must be counted. Scoped to building and adapting, where the constellation does most of its work, the mechanism is what the claim rests on. Widened beyond that, it would need evidence the mechanism does not by itself provide.

## Open questions

Whether the effective rank of the meaningful-direction set stays low throughout training, or spikes early, is the measurement that decides whether the efficiency claim holds for the full build or only for late-stage adaptation.

Whether multi-tangent forward-mode's per-tangent cost, in the framework's arithmetic on the CPU target, is low enough that the storage saving translates into a wall-clock saving, is an implementation question the bench answers.

Whether the parameter-economy pressure forward-mode creates produces models small enough to deploy on the CPU target without quantization, or whether quantization is still required, connects this article back to the deployment friction of the [building article]({{< ref "building-the-model" >}}). The formal treatment is the [ADM pre-print](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}).
