---
title: "Architecture and Arithmetic"
linkTitle: "Architecture and Arithmetic"
description: "How a derived backbone and precise b-posit arithmetic keep a model's structure sharp through training"
date: 2026-06-08T00:03:00+00:00
weight: 4
authors:
  - SpeakEZ
tags: ["machine-learning", "adaptive-domain-models", "posit-arithmetic", "architecture"]
draft: false
---

The section index attributes the architectural half of this program to Buchanan, Pai, Wang, and Ma's [*Principles and Practice of Deep Representation Learning*](https://ma-lab-berkeley.github.io/deep-representation-learning-book/). One idea grounds it: a network layer is one step of an optimization algorithm. The algorithm climbs an information-theoretic objective, the coding-rate reduction, whose maximization drives a representation toward a union of low-dimensional, mutually incoherent subspaces. The attention block emerges as the step that compresses the representation against those subspaces; the feed-forward block emerges as the step that sparsifies. The architecture is the unrolled optimizer, and the resulting [CRATE](https://github.com/Ma-Lab-Berkeley/CRATE) family, now with a causal variant for sequences, has a closed-form reason for every block it contains.

The objective has two terms, and the step moves opposite ways on them: it raises the coding rate of the whole representation \(R(\mathbf{Z})\) and lowers the coding rate of each class \(R^{c}\). The quantity it climbs is their difference, the coding-rate reduction \(\Delta R\):

\[
\Delta R(\mathbf{Z}\mid\mathbf{\Pi}) = \underbrace{\tfrac{1}{2}\log\det\!\left(\mathbf{I} + \tfrac{d}{\varepsilon^{2}}\tfrac{\mathbf{Z}\mathbf{Z}^{\top}}{m}\right)}_{R(\mathbf{Z})} - \underbrace{\sum_{k=1}^{K}\tfrac{\operatorname{tr}(\mathbf{\Pi}_k)}{2m}\log\det\!\left(\mathbf{I} + \tfrac{d}{\varepsilon^{2}}\tfrac{\mathbf{Z}\mathbf{\Pi}_k\mathbf{Z}^{\top}}{\operatorname{tr}(\mathbf{\Pi}_k)}\right)}_{R^{c}(\mathbf{Z}\mid\mathbf{\Pi})}
\]

Each layer is one gradient-ascent step on it, \(\mathbf{Z}^{\ell+1} = \mathbf{Z}^{\ell} + \eta\,\partial\Delta R/\partial\mathbf{Z}\), and the attractor is the union of incoherent subspaces the derivation turns on.

The consequence that matters here is accountability of parameters. A black-box transformer offers no principled account of which weights do what, so the only way to make it smaller is to prune after training and measure what broke. A derived architecture inverts this. You instantiate the blocks the objective requires and no others, and the representation the trained model carries is, by the derivation, a set of separated subspaces. There is a known answer to the question "what is this part of the model doing," and the answer is the same for every model of the family.

That separated-subspace target is the point of contact with ADM. The [ADM substrate](https://arxiv.org/abs/2603.18104), collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}), enforces block-separated structure by construction: a block-diagonal generator has a block-diagonal exponential, and the off-block entries are provably zero by the grade type system. The derived architecture reaches the same geometry by a different road, as the attractor of an optimization rather than as a typed invariant. The [positional-encoding analysis]({{< ref "the-constellation" >}}) elsewhere in this section shows the two roads meeting on one concrete subsystem; here the claim is the general one. Same target geometry, construction in one case, convergence in the other.

That construction is a prior on weights. For a Clifford layer between grades \(k\) and \(k'\), the admissible support \(\mathcal{W}_{\mathrm{adm}}\) is fixed by type, and the type system induces a prior whose indicator is exact rather than a penalty:

\[
p(W) \propto \mathbf{1}\!\left[\,W \in \mathcal{W}_{\mathrm{adm}}\,\right]\,\tilde{p}(W)
\]

The orthogonal projector \(\Pi_{\mathrm{adm}}\) onto \(\mathcal{W}_{\mathrm{adm}}\) is a deterministic function of the layer's type signature, so configurations at forbidden grade pairs hold zero probability and leave the support. That is the difference between a filter and a regularizer: the coding-rate ascent admits incompatible structure at an energy cost it then pays down, where the type system drops it from the support outright.

## Two readings of the same book

There is one reading on which the program turns. *Principles and Practice of Deep Representation Learning* admits two materially different readings, and which one a reader arrives at is determined by the substrate they bring to it. Neither reading is wrong, and the second is offered here as an invitation rather than a correction: the book's own framing is that it describes what the structure *is*, and a reader equipped differently can build that structure differently.

The common reading takes the derivation as an interpretability result. On this reading the achievement is that CRATE explains, after the fact, what a transformer's blocks are doing: attention compresses against subspaces, the MLP then makes sparse, and the coding-rate objective gives a principled account of why the architecture works. The natural next step, given the tooling the field has standardized on, is to implement CRATE as a dense tensor program and train it the usual way. This is entirely reasonable, and it is what the published implementation does. The dense-tensor substrate, the tile-and-tensor MLIR lineage that machine-learning toolchains lower to, has no place to *record* the structure the derivation proves, so the structure exists in the mathematics and then thins out in the artifact. The book presumes the subspaces are incoherent; the substrate gives you no way to hold them so. This substrate was built principally for tensor tiling in machine learning, and a framework willing to use it as a general compilation target, rather than only for the workloads it was shaped around, has more of its structure available to preserve.

The second reading is available to a substrate that can carry structure as a typed, discharged invariant, which is the *starting point* of our Fidelity Framework. To make this concrete rather than abstract, consider the published CRATE forward pass. The model's reference implementation is open, at [github.com/Ma-Lab-Berkeley/CRATE](https://github.com/Ma-Lab-Berkeley/CRATE), and the papers give it in PyTorch-style pseudocode; the shape below follows that reference:

```python
# CRATE forward pass, as published (PyTorch-style pseudocode).
# Each layer: a subspace self-attention step (compression) with a skip,
# then an ISTA step (sparsification).
class CRATE:
    def forward(self, x):
        for ln1, attn, ln2, ff in self.layers:
            x_ = attn(ln1(x)) + ln1(x)   # MSSA: gradient step on the coding rate
            x  = ff(ln2(x_))             # ISTA: soft-thresholding toward sparsity
        return x
```

The structure the derivation turns on lives inside `attn`: the Multi-head Subspace Self-Attention operator compresses the tokens against a set of subspace bases, written `U_k` in the paper, one per head. The book's argument is that these bases should span *incoherent* subspaces, the off-subspace interactions vanishing, because that incoherence is what makes the representation a compact memory. In the published implementation the `U_k` are ordinary learnable tensors. Their incoherence is a property the training is expected to approach, not a property the code carries, and under finite-precision training it is approached imperfectly and drifts thereafter. This is not a flaw in the implementation; it is the most a dense-tensor substrate can express.

A Fidelity reframing of the same operator does not change the mathematics; it changes what carries the structure. The subspace bases become graded elements whose incoherence is a type-level fact, and the compression step is the same gradient step on the coding rate, now computed over quantities whose block structure cannot drift because the off-block interactions are not representable:

```fsharp
// Each head's basis is a graded element; its incoherence is a type property, discharged once.
let mssaStep (heads: GradedSubspaceBasis<Bivector>[]) (z: TokenField) : TokenField =
    heads
    |> Array.map (fun u ->
        // compression against this head's subspace, the coding-rate gradient step
        z |> compressAgainst u |> Quire.accumulate)
    |> SubspaceAggregation.byGrade        // head aggregation, structure-preserving
    |> skipConnection z                   // the "+ ln1(x)" of the published step

```

The published operator and the reframed example above descend from the same objective, but the published one trains then drifts. The reframing is designed to carry something the original sample cannot: a subspace structure that survives training and lowering while maintaining precision of its bound. It does so by reading §4 (the rate-reduction principle) and §5 (the unrolled derivation) as a specification the type system enforces rather than a behavior the optimizer approximates. Four of our framework's constructs each make one of the book's descriptive claims constructive in this way:

- **Dimensional and grade types** turn the book's "union of incoherent subspaces" from the optimum of a functional into a property the type system carries, as the `U_k` reframing above shows. (See [*A Scaffold for Constrained Models*]({{< ref "scaffold-for-constrained-models" >}}) for the scope rule that licenses this where structure is known in advance.)
- **Geometric algebra** turns subspace incoherence into the rotor and generator structure the [positional-encoding analysis]({{< ref "the-constellation" >}}) makes explicit, where the off-block zeros are algebraically forced rather than learned-small.
- **[The Program Hypergraph](https://arxiv.org/abs/2603.17627)** turns the book's layered computation, which the dense-tensor substrate flattens into matrix multiplies, back into the multi-way relationships it actually holds. A transformer's attention is a multi-way relationship among tokens; the dense lowering decomposes it into pairwise operations and loses the structure, exactly the join/split decomposition cruft the PHG will not admit. The PHG carries the relationship intrinsically, so the provably-absent interactions are absent from the lowered program rather than small within it, and the [graph-coloring parallelization]({{< ref "/docs/internals/pipeline/speed-and-safety-with-graph-coloring" >}}) our framework already performs operates on the true structure rather than a flattened shadow of it.
- **b-posit and the quire** turn the book's convergence-time, exact-arithmetic guarantee into one that holds under finite-precision training, which is the gap the rest of this article addresses.

The two readings produce different artifacts from the same text, and the difference is the substrate. The book itself does not choose between them; it is, in its own framing, a theory of what the structure *is*. A substrate that can hold structure as a typed, discharged, precisely-accumulated invariant can take the second reading. Where our framework engages the book by name from here on, it is this second reading that is meant, offered as an invitation to anyone whose substrate can support it. The structure the book theorizes is a mixture of K Gaussians the rate objective drives apart, the same mixture our [domain models]({{< ref "why-adaptive-domain-models" >}}) draw inference from; here it supplies the architecture.

The layer's other operator reframes the same way: CRATE pairs each compression step with a sparsification step, the ISTA block from `model/crate.py`. 

```fsharp
// D is the learned dictionary; one proximal-gradient step toward sparsity.
let istaStep (d: Dictionary<BPosit>) (lambda: BPosit) (step: BPosit) (x: TokenField) : TokenField =
    let dx   = d * x                          // D x
    let dtdx = Dictionary.adjoint d * dx      // Dᵀ (D x)
    let dtx  = Dictionary.adjoint d * x       // Dᵀ x
    let grad = step * (dtx - dtdx) - step * lambda    // negative-gradient update, in the quire
    x + grad |> TokenField.map (max BPosit.zero)      // ReLU: soft-threshold toward sparsity
```


## Why convergence is not enough on its own

The white-box guarantees are real and they are soft, and that softness is the problem to solve. The subspaces the coding-rate objective separates are orthogonal at the optimum in exact arithmetic. Trained in IEEE-754 floating point, they are approximately orthogonal, and the gap between "orthogonal" and "approximately orthogonal" is filled by the numerics. The objective is built on log-determinant and covariance terms, which are long accumulations, and long accumulations in floating point are exactly where catastrophic cancellation does its quiet work. The structure does not collapse. It blurs, and nothing announces that it has blurred, because the theory never claimed exactness in the first place.

For interpretability research that blur is acceptable; an approximately separated representation is still interpretable. For a component meant to sit adjacent to the ADM constellation, where the neighboring domain models carry exact, SMT-discharged invariants, an approximate substrate is the wrong tradeoff, and it is the same failure mode our framework already identified for learned positional-encoding generators, where a data-dependent generator drifts under floating-point training and cross-block contamination accumulates the way grade corruption does.

The blur is not intrinsic to the architecture. It is intrinsic to the arithmetic the architecture is conventionally trained in. Change the arithmetic and the convergence sharpens.

## b-posit and the quire close the gap

The substrate the ADM work already uses, b-posit arithmetic with quire accumulation, is built for the operations the coding-rate objective stresses. A quire is a wide fixed-point accumulator that carries a long sum or a dot product without rounding at each intermediate step, rounding only once at the end. The log-determinant and covariance computations that the rate objective depends on are exactly such accumulations, so they are what the quire protects.

```fsharp
// The rate term's long accumulation, carried through the quire and rounded once at the end.
let logDetThroughQuire (cov: Matrix<BPosit>) : BPosit =
    cov
    |> choleskyDiagonal          // the diagonal whose log-sum is the log-det
    |> Quire.sumOfLogs           // accumulated without intermediate rounding
    |> Quire.round               // a single rounding, at the end
```

The contrast with the common reading is the same operation built two ways. The dense-substrate reading computes the rate term as a dense floating-point reduction, correct in expectation and quietly lossy in practice; the Fidelity reading computes it as a quire accumulation over grade-carrying quantities whose grade structure is known before the reduction runs:

```fsharp
// The common reading: a dense float reduction, lossy in the tails.
let logDetDense (cov: float32[,]) : float32 =
    let mutable acc = 0.0f
    for i in 0 .. dim - 1 do
        acc <- acc + log (choleskyDiag cov i)   // rounds every iteration
    acc

// The Fidelity reading: the covariance is typed by its grade structure, so the
// block-diagonal form the derivation promises is a property of the type
let logDetFidelity (cov: GradedCovariance<Bivector>) : BPosit =
    cov
    |> GradedCovariance.blockDiagonal     // off-block zeros are type-level facts
    |> Quire.sumOfLogs                    // exact accumulation over the blocks
    |> Quire.round
```

The difference is not micro-optimization. In the dense version the block structure is an aspiration about the values that finite-precision training erodes; in the Fidelity version it is a fact about the type that training cannot touch, because the off-block interactions the book's derivation says should vanish are not small, they are unrepresentable. This is the §4 rate-reduction principle read as a specification rather than a target.

The move is therefore not to tolerate the floating-point slack but to remove its cause. Keep the derived architecture exactly as the white-box derivation gives it, and run its sensitive operations on arithmetic whose accumulation discipline makes the convergence sharp. This is the first point at which the language-model component stops being the one piece of the framework that runs on a foreign numeric format. It rejoins the b-posit world that the domain models, the [dimensional types](https://arxiv.org/abs/2603.16437), and the rest of the substrate already inhabit, which is a precondition for the adjacency the [constellation article]({{< ref "the-constellation" >}}) describes.

## The friction this resolves, and the one it does not

The [building article]({{< ref "building-the-model" >}}) named a real tension: the CPU deployment target wants four-bit or ternary weights, and those are the regimes where the rate-reduction operations are worst-conditioned. The b-posit substrate is the resolution, because it offers dynamic range that fixed low-bit integer formats cannot, and the borrowed ternary format was never more than a terminal artifact someone else's pipeline produced. Building the model on our framework's own arithmetic makes the deployment numeric format a free variable chosen for the framework's reasons rather than inherited from an external recipe.

One friction is not resolved, and the article states it as the open question it is. Posit precision is not uniform. It is densest near magnitude one and tapers toward the very large and very small. Whether that taper aligns with where the coding-rate objective concentrates its numerical stress during training is an empirical question about the interaction of two specific designs, Gustafson's tapered precision and Ma's rate objective. If the objective's stress falls near magnitude one, where posit is densest, the synthesis is clean. If it falls in the tapered tails, the quire-mediated accumulation has to carry it, which is what the quire is for, so even the unfavorable case has a designed answer rather than a dead end. The favorable case gives sharp convergence at low parameter count; the unfavorable case gives sharp convergence at the cost of more quire-mediated work. Distinguishing them is one bench experiment, and it is the one that decides whether b-posit is the right substrate for this architecture or merely a defensible one.

## What this buys the rest of the section

A derived architecture on precise arithmetic is the foundation the remaining articles stand on. The [forward-mode article]({{< ref "forward-mode-and-adaptation" >}}) depends on the derived structure being low-rank, so that the gradient can be taken over few directions, and on the arithmetic being precise, so that the accumulated tangents can be trusted. The [constellation article]({{< ref "the-constellation" >}}) depends on the shared b-posit substrate, because that shared substrate is what lets a non-typed language component and a grade-carrying domain model exchange values without a numeric impedance mismatch. And the [reversibility article]({{< ref "reversible-cores" >}}) depends on the quire making a state transition's round trip exact rather than approximately reversible. The architecture and its arithmetic are chosen once, here, and the rest of the section is what they make possible.

It also sets up the section's sharpest efficiency contrast, developed in the [constellation article]({{< ref "the-constellation" >}}). The two readings of the book diverge most consequentially on sub-quadratic attention. The dense-tensor reading that flattens attention into all-pairs matrix multiplies is the quadratic cost; the field's escape from it, the linear-attention and state-space families whose current frontier is [Mamba-3](https://arxiv.org/abs/2603.15569), replaces all-pairs attention with a learned data-dependent generator. Mamba-3 has converged on exactly the complex-valued rotational generator our framework types, bridging it to RoPE, while still listing as open the two problems the framework's substrate addresses: state tracking, and the gap between linear-in-theory and efficient-in-hardware inference. The structured reading set out in the constellation article gets the sub-quadratic cost from the same generator the field has converged on, and gets the exactness the field still reaches for by experiment, because the generator whose decomposition the grade types hold exact is the same generator that makes the recurrence sub-quadratic. The structural-zeros argument and the sub-quadratic argument are the same argument: the interactions the derivation proves absent are the interactions a quadratic model spends time computing and a drifting sub-quadratic model spends capacity suppressing, and a structured model does not represent.

## Open questions

Whether posit's tapered precision aligns with the rate objective's numerical stress, or whether the quire must carry the tails, is the bench experiment named above.

Whether a derived architecture trained on b-posit reaches target representation quality at lower parameter count than the same architecture on floating point, as the noise-hedge argument predicts, is measurable on the same bench.

Whether the causal CRATE variant's rate operations remain well-conditioned under the framework's arithmetic across the full sequence length, or degrade with context, is a conditioning question specific to the sequence case.
