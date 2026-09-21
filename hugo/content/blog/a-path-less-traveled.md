---
title: "A Path Less Traveled"
linkTitle: "A Path Less Traveled"
description: "How Bidirectional Computation Can Lead to Higher Integrity Computation with a Quieter API"
date: 2026-09-21T08:30:00-04:00
lastmod: 2026-09-21
draft: false
authors: ["Houston Haynes"]
tags: ["Design", "Types", "Reversibility", "Architecture"]
---

I want to run a simulation forward, inspect a result, and retrace the computation without saving a copy of every state along the way. I also want the compiler to tell me when my chosen arithmetic makes that request impossible. The application code ought to express that intention without becoming a manual for managing its own recovery machinery.

A domain library could offer an interface as quiet as this:

```fsharp
let later = initial |> evolve model steps
let earlier = later |> retrace model steps
```

These are illustrative ordinary functions, with a proposed compiler contract behind them. A later request to retrace would constrain the earlier choice of implementation. If the selected update has a checked inverse, Composer could generate that inverse once for the region. Where recovery needs retained information, its cost would have to fit the declared budget. An unsupported request would produce an explanation at the boundary that needs it settled.

That is the direction I want to explore with negative and fractional types in Clef. The value for a developer is a supported operation with less bookkeeping. The additional type structure would let us check the relationship between the operation and its later use, then preserve that relationship through compilation.

## A library as a reference

Haskell's [Tardis](https://hackage.haskell.org/package/tardis) library gives us a useful case study. Its computation has a forward state channel and a backward state channel. An earlier stage can contribute information to a later stage while receiving information that the later stage supplies. Lazy evaluation makes certain recursive dependencies between those stages usable.

Consider annotating every element of a sequence with the sum of the elements before it and the sum of the elements after it. Prefix information flows forward; suffix information flows backward. A programmer can build two traversals and join the answers. The library provides a composition rule that makes those relationships part of one expression. Its [bind implementation](https://raw.githubusercontent.com/DanBurton/tardis/master/src/Control/Monad/Trans/Tardis.hs) is a precise reference for how the intermediate states connect.

Clef gives us a different place to express that rule. Baker can retain the operation's meaning while constructing the Program Hypergraph, or PHG. The source can use domain operations, with their state dependencies represented structurally. We would keep the semantics of the library construction while moving its repetitive wiring into elaboration. Tardis is the reference case, not the name of a proposed Clef feature.

We have made a related architectural choice for deferred work. [The Cold Half of Concurrency](/blog/cold-half-of-concurrency/) follows the library lineage of cold and incremental computation into Clef's native model. In that account, execution can wait for activation, and a demanded incremental value can reuse a valid cached result. [Native Reactivity in Clef](/blog/native-reactivity-in-clef/) describes the corresponding source-level experience. Each construct still needs its own evaluation and resource rules.

## Two directions, different guarantees

Bidirectional dependency means that information needed at one stage can come from a stage on either side of it. Reversibility means that a specified inverse can recover an earlier state. The sequence annotation example supplies the first property. It makes no promise to undo an arbitrary calculation of a prefix sum.

This distinction lets us connect the ideas without requiring them to mean the same thing. A future use can impose a recovery requirement on an earlier computation. The compiler then needs a law that justifies the chosen recovery strategy. Two-way propagation makes the requirement available; the inverse law establishes whether the strategy works.

The library's two states also have ordinary state semantics. Calling the forward state a negative type, or the backward state a fractional type, would add a claim that its definition does not establish. We need an explicit interpretation before resource duality can be used to check that composition.

The mapping into a native design looks like this:

| In the library encoding | In the proposed Clef interpretation |
|---|---|
| Bind connects the two state channels | Baker elaborates typed composition into ordered graph relationships |
| Lazy recursive bindings permit feedback | Demand rules determine which dependencies can produce a result |
| Wrappers and accessors expose the channels | Domain operations use the structure without repeating its wiring |
| Monad laws justify composition | The admitted operations retain those laws and their premises |
| An application supplies recovery bookkeeping | A region contract determines whether an inverse, replay or retained information is required |

This is more than removing syntax. A forced cyclic dependency can still fail to produce a value. An effect still needs an order. Finding a satisfying assignment for a constraint does not prove a monad law, and finishing a compiler analysis does not prove that a recursive program will terminate. The native design has to preserve these distinctions as carefully as the useful composition rule.

## Resources with a particular value

The proposed [negative and fractional type discipline](/docs/design/types/negative-fractional-types/) supplies additional information about directed evaluation and resource use. A negative type belongs to a calculus in which evaluation can change direction. It does not require a stored history of everything evaluated before it.

A fractional resource has a particularly useful constraint: it is associated with a specific value. A resource indexed by `v` cannot be discharged merely by producing some other value of the same type. The compiler needs evidence of the required match, or a runtime comparison with the specified failure behavior. Repeated execution also matters. Two calls to the same creation site produce distinct resource instances even when their values compare equal.

That structure could let a library hide temporary-resource bookkeeping behind an ordinary operation. Suppose a temporary workspace must be restored to its designated state before release. The operation's contract can retain that relationship through composition. An optimization that releases the wrong instance, duplicates a consumable resource, or loses the necessary equality check would violate the contract. The useful result is an application interface whose implementation has fewer unchecked ways to go wrong.

A notation such as `Recip<A>` would leave some of that information implicit in the source. Internally, we would still need the value index and scoped instance identity, together with the permitted uses. A cached resource-bearing result cannot become freely duplicable simply because it is stored in a lazy value.

This fractional meaning is separate from a physical unit such as inverse seconds. Rational exponents in dimensional analysis are another separate extension. The [2021 reference calculus](https://homes.luddy.indiana.edu/sabry/files/2021/02/popl.pdf) by Chen and Sabry uses value indexing. The earlier logic-variable interpretation has its own unification and search semantics. Choosing between those accounts changes what a program does, so Clef's engineering design keeps them distinct.

## The graph and its obligations

My useful starting point for the graph is an ordinary computation with requirements attached to it. A local requirement might say that a value has a particular dimension. A joint requirement might connect the operation that creates a resource with the indexed value and the operation permitted to consume it.

Clef's PHG gives those participants a shared semantic representation. The executable operations form the computation's structure. Coeffects record requirements; ordered relations connect facts that must hold together. Evidence belongs with the facts it justifies. A proof relation in that graph does not mean the program runs another computation beside the first one.

The familiar lattice picture helps with one part of this account: an analysis can order partial information by how much is known. It needs its own rules for combination and convergence. That order describes the analysis domain, while the hypergraph describes which participants are related. We can have several analyses over the same program without claiming that they all use the same algebra.

[Coeffects and Codata](/docs/internals/concepts/coeffects-and-codata/) develops that representation, and [Proof Composition and Tooling](/docs/internals/verification/proof-composition-and-tooling/) explains the intended use of reusable laws. A library author should be able to establish an operation's law once. At a use site, Baker would check the applicable premises and preserve the result for the selected target.

There is a practical connection to [The Gift of Deferred Inference](/blog/deferred-inference/). Keeping a requirement unresolved while useful information arrives can lead to a better implementation choice. The compiler must still settle the requirement at the boundary that relies on it. For reconstruction, that decision could involve the arithmetic representation and the allowed memory together.

## Reversal without a second trajectory

A small exact example makes the memory question concrete. Suppose a model already has two state components, `q` and `p`. Its forward update adds a function of one component to the other:

```text
forward:                    reverse:
    p := p + F(q)               q := q - G(p)
    q := q + G(p)               p := p - F(q)
```

The reverse update restores `q` first. It can then recompute the increment that was added to `p`. Under declared modular arithmetic, with total pure `F` and `G` and unchanged parameters, this recovers the exact starting state. The helpers can themselves be many-to-one: the full state update retains the information needed for its inverse.

Both components already belong to the model. There is no growing sequence of earlier `(q,p)` pairs. We need the current state and the workspace used to recompute the increments. A compiler can keep the inverse recipe with the region's code, independent of how many times the region executes. [Jos Stam's bitwise reversible integrator](https://research.nvidia.com/labs/prl/stam2023reversible/reversible2022.pdf) provides a numerical reference for this kind of exact discrete retracing.

The arithmetic contract matters. Replacing the exact additions with ordinary rounded floating-point additions changes the argument. A finite-state bijection can be traversed backward repeatedly with bounded working state, but eventually its states repeat. Exact retracing of that discrete path is a different guarantee from indefinite accuracy against a continuous physical model. A fixed-width step counter also bounds the requested retrace length. An unbounded exact count needs storage proportional to its logarithm, as does an unbounded absolute timestamp.

Lossy operations need another recovery strategy. A remainder modulo 13, for example, does not identify which integer produced it. The missing distinctions must be retained or reproducible under the program's input contract. The compiler could choose a compact residual instead of a full predecessor, but it would have to account for that storage. [Reversible learning](https://proceedings.mlr.press/v37/maclaurin15.pdf) gives a concrete example in which finite-precision optimization needs retained residual information.

## Tangents and the horizon

Forward differentiation has a separate storage advantage. For a streaming state with `n` components and a fixed `k` tangent directions, current primal and tangent storage can be proportional to `n(1+k)`. It need not grow with every elapsed step. Parameters, requested outputs and temporary workspace still add to the budget. A derivative tangent describes sensitivity; it is not an inverse operation or a fractional resource.

The connection is useful because a numerical region can have both differentiation and recovery requirements. The compiler could check them against the same arithmetic and placement decisions. [Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/) considers related obligations for numerical execution, while our [arithmetic construction design](/docs/internals/numerics/arithmetic-construction-and-placement/) places numeric representation and target realization together.

A [Lyapunov window](/docs/design/types/lyapunov-window/) would add a bound on how reconstruction error grows over an admitted interval. Such a bound needs justified amplification and local-error estimates for the chosen model and precision. It can determine when approximate recovery needs a checkpoint. It cannot recreate discarded bits.

For an arbitrarily long retrace with bounded live memory, the decisive property is an exact inverse or an explicitly costed reproducible replay strategy. Keeping a fixed number of checkpoints alone does not preserve arbitrary history. The useful role for types is to make the chosen guarantee and its resource cost part of the same checked construction.

## The corrected claim

The theory behind this proposal has a specific correction we need to retain. Chen and Sabry's [February 7, 2021 erratum](https://homes.luddy.indiana.edu/sabry/files/2021/02/errata.pdf) withdraws Theorem 25's claim that the constructed category is an inverse category. The omitted condition was uniqueness of generalized inverses, and the authors give a counterexample. The paper's compact-closed result is not withdrawn. This is an erratum to Chen and Sabry, not to the Tardis library.

For Clef, the consequence is concrete: name the selected reversal and establish its laws for the operations we admit. A general duality structure does not prove that every ordinary program has a unique inverse. Our [Negative and Fractional Types paper](https://arxiv.org/abs/2606.04352) develops the operational proposal. [The Program Hypergraph](https://arxiv.org/abs/2603.17627) describes the joint relationships, and [Fixed-Point Scaffolding](https://arxiv.org/abs/2606.02854) addresses their preservation through compilation. The corrections discussed here are in the working Markdown manuscripts. The linked arXiv editions predate these revisions; updating their generated editions requires a separate comprehensive publication review.

## An implementation we can examine

We can make the proposal testable in small steps. First, a finite bidirectional example should exhibit the intended demand behavior, including a circular demand that cannot produce a result. A finite indexed-resource example should then distinguish a matching value from a wrong value, and one valid use from a duplicated use. A native reversible region should demonstrate its inverse on the chosen machine representation while peak live memory stays independent of elapsed steps under the stated budget.

The engineering record for those gates is `Composer/docs/Bidirectional_Composition_Plan.md`, shared across the working repositories. The [NFT design page](/docs/design/types/negative-fractional-types/) provides the public technical account and related documentation. The native extension remains planned. The new structure earns its place when an application can express an ordinary operation, the compiler can explain why its realization is valid, and the generated program meets the promised recovery and storage contract.
