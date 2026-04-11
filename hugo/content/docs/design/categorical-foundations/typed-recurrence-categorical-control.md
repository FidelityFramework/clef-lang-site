---
title: "Typed Recurrence and Categorical Control of Inference"
linkTitle: "Typed Recurrence"
description: "From Fixed-Point Combinators to Porous Recurrent Loops: Typed Structure Around Neural Computation"
date: 2026-03-23T00:00:00-04:00
weight: 09
authors: ["Houston Haynes"]
tags: ["AI", "Architecture", "Innovation"]
params:
  originally_published: 2026-03-23
  original_url: "https://speakez.tech/research/typed-structural-control-of-neural-inference/"
---

## The Recurring Problem of Recurrence

Large language models have a recurrence problem. Not the recurrence of RNNs, which is architectural, but the recurrence of reasoning itself: any task that requires iterating over structure, decomposing a problem into subproblems, or maintaining state across an unbounded number of steps. Transformer attention is fixed-width. The context window is finite. When reasoning requires recursion, the model either fits the entire recursive trace into its context or it fails.

Two recent lines of work address this problem from different directions. They arrive at structurally similar solutions despite starting from different formal traditions. The convergence is instructive.

## λ-RLM: Fixed-Point Combinators as Inference Control

Roy et al.'s [λ-RLM framework](https://arxiv.org/abs/2603.20105) replaces free-form recursive code generation with a typed functional runtime grounded in λ-calculus. The core construction is the Y-combinator applied to LLM inference:

> λ-RLM ≡ fix(λf.λP. if |P| ≤ τ\* then M(P) else Reduce(⊕, Map(λpᵢ.f pᵢ, Split(P, k\*))))

The LLM M is invoked only at the leaves of a recursion tree, on subproblems bounded by τ\* tokens. The structural control, splitting, mapping, reducing, is deterministic and pre-verified. The neural component never manages its own recursion; the fixed-point combinator ties the knot externally.

This separation yields formal guarantees: termination (the recursion tree has bounded depth d = ⌈log\_k\*(n/τ\*)⌉), predictable cost (exactly (k\*)^{d+1} model invocations), and accuracy that degrades as a power law in input length, a substantial improvement over the exponential decay of direct long-context inference.

The key structural observation: λ-RLM factorizes inference into a *symbolic control path* (the combinator library: Split, Map, Filter, Reduce, Concat, Cross, Peek) and a *neural computation path* (the oracle M, invoked only on bounded subproblems). The combinators are deterministic and total. The LLM is the sole source of uncertainty, and its uncertainty is contained by the type discipline of the functional runtime.

## The HRM → RRM → Porous RRM Progression

The Typed Actor Constellations work develops a parallel factorization from the architecture side. The progression is:

**Hidden Recurrent Model (HRM).** A single recurrent loop (e.g., MLGRU) processes input tokens sequentially. The hidden state is opaque; the model's recurrence is closed. This is the baseline: the model handles everything, including domain reasoning, within its recurrent computation. The analogy to direct LLM inference is precise: the model must fit all reasoning into its state vector, and "context rot" is the degradation of early-step information across long sequences.

**Resonant Recurrent Model (RRM).** The recurrent loop operates at N resonant levels with dynamic coupling between levels. Information circulates at multiple timescales (Alpha, Beta, Gamma rates), and the coupling between levels is learned. This introduces structural recurrence: the model's computation is organized into interacting temporal scales, but the loop remains closed to external state.

**Porous Recurrent Model.** The recurrent loop is opened. At designated steps, the MLGRU suspends mid-recurrence, emits a typed query to a domain-specific actor (an Adaptive Domain Model), and integrates the typed response as intermediate state before resuming. The loop is porous: external, verified domain knowledge enters the recurrent computation through typed interfaces. The model no longer needs to encode all domain knowledge in its weights; it consults typed specialists and integrates their responses under dimensional and coeffect constraints.

The progression from HRM to Porous RRM mirrors the λ-RLM construction in a specific sense. Both separate neural computation from structural control. Both impose typed discipline on the boundary between the two. Both contain the neural component's uncertainty within bounded scope. The difference is the formal tradition: λ-RLM draws on the simply typed lambda calculus and fixed-point theory; the porous loop draws on the actor model, BAREWire typed protocols, and the PHG's hyperedge constraint system.

## The Categorical View

The [CDL paper](https://arxiv.org/abs/2402.15332) (Gavranović et al.) established that neural network architectures are morphisms in a 2-category, with backpropagation as the canonical 2-cell. [Entry 01](../categorical-deep-learning-adjoint-correspondence/) in this series traced how the Fidelity framework's PSG instantiates this structure through its coeffect pairs and dimensional propagation.

The factorization that both λ-RLM and the porous loop perform has a natural expression in this categorical vocabulary. In the parameterized category Para(C):

- A neural computation M: A → B is a 1-morphism (a learner, in CDL's terminology)
- The control structure (whether a combinator tree or a porous recurrence schedule) is a *diagram* in the category, specifying how 1-morphisms compose
- The typed interfaces (λ-RLM's combinator signatures; the porous loop's BAREWire protocol) are the *constraints on composition*, the conditions that must hold at each boundary for the diagram to be well-formed

The Y-combinator is a fixed-point construction on endofunctors. The porous loop is a fixed-point construction on the recurrence operator, modified by typed external state injection. Both are instances of the same categorical pattern: a recursive structure whose well-formedness is guaranteed by type constraints that propagate through the diagram.

Both λ-RLM and the porous loop exhibit compositional structure over a tree-shaped diagram of subcomputations. In λ-RLM, the forward pass through the combinator tree produces subproblem results, and the Reduce operation is a catamorphism that folds those results back to a single output at the root. In the porous loop, the forward recurrence produces queries, and the ADM response is a typed structured fact that re-enters the recurrence as grounded state. Both patterns inhabit a neighborhood of the CDL framework: the tree-shaped composition, the typed boundaries, the compositionality condition at each interface. The precise CDL Fwd ⊣ Bwd adjunction specializes to the differentiable case where the upward arrow carries gradient information, and the upward arrows in λ-RLM and the porous loop carry computed results or grounded state instead. What both do instantiate is the more general pattern of verified composition over a diagram, which is the property the Fidelity framework's type discipline actually depends on.

## What λ-RLM Validates and What It Misses

λ-RLM provides empirical validation for the core thesis: typed structural control around neural inference produces measurable improvements. The framework outperforms standard recursive LLM approaches in 29 of 36 model-task comparisons, with accuracy gains up to +21.9 points and latency reductions up to 4.1x. These results show that the factorization is practically effective at the workloads λ-RLM measured against.

What λ-RLM does not address, and what the porous loop is designed for, is the *domain grounding* problem. λ-RLM's combinators are structural: Split, Map, Reduce. They decompose problems by size, not by domain semantics. The neural oracle M operates on text subproblems without access to typed domain state. When the task requires consulting a physics model, a financial model, or a medical knowledge base, λ-RLM has no mechanism for integrating domain-specific posterior distributions into the inference loop.

The porous loop's typed actor consultation addresses this directly. The ADM is not a generic oracle but a domain-specialized model with Bayesian posterior state, dimensional annotations, and PHG-certified structural correctness. The consultation is not "process this text" but "given the current recurrent state with these dimensional properties, what is the posterior distribution over this domain query?" The response enters the loop as a StructuredFact with typed Value, Dimension, Confidence, and Certificate fields, bypassing the tokenization-embedding-attention path that would destroy its typed structure.

The synthesis is the claim that both factorizations, the combinator tree and the porous loop, are instances of the same categorical construction, but the porous loop operates at a higher level of the type hierarchy. λ-RLM's type discipline governs structural composition of text processing. The porous loop's type discipline governs semantic composition of domain knowledge.

## The Gradient Connection

Forward-mode automatic differentiation, as developed in [Entry 03](../forward-gradients-exact-accumulation/) of this series, provides an additional link. λ-RLM's cost analysis shows that the number of oracle invocations is predictable: (k\*)^{d+1}. The porous loop's consultation frequency is governed by the relevance signal from the MLGRU's gating mechanism. Both are, in effect, *schedules for neural computation invocation* embedded in a typed control structure.

When the porous loop incorporates forward-mode training (the [continuous learning](../continuous-learning-inference-training-boundary/) configuration from Entry 07), the ADM consultation becomes not just an inference event but a potential learning event. The boundary tension between the MLGRU's expectation and the ADM's posterior is a loss signal. The forward-mode gradient of that loss, with its O(1) memory signature and StackScoped coeffect classification, can drive a scoped weight update within the current interaction. This is the "consultation-driven micro-update" identified in the synthesis dialectic: the porous loop triggers not just a grounding event but a learning event, with the same formal guarantees that λ-RLM provides for its structural recursion.

## The Sheaf Reading

The constructions described above (typed actor constellation, BAREWire consultation, gossip convergence, warm rotation, boundary tension as loss signal) compose into a single sheaf-theoretic picture. Naming the picture explicitly connects the porous loop to the cohomological framework developed for the rest of the framework and to the substantial literature on cellular sheaves over finite posets (arXiv:2502.15476).

**The actor constellation is a sheaf on the actor topology poset.** The poset is the reachability relation between actors in the constellation; the stalks are actor states (MLGRU recurrent state, ADM Bayesian posterior, certificate metadata); the structure maps are the BAREWire typed consultation interfaces, which specify how information flows from one actor's state to another. The compositionality equation holds because BAREWire enforces typed contracts at every consultation: a value flowing from actor \(A\) through actor \(B\) to actor \(C\) must arrive at \(C\) with the same type and dimension it would have arrived with under a direct \(A \to C\) consultation. Without this property, the constellation would not be a sheaf and the convergence guarantees below would not exist.

**The gossip convergence protocol is sheaf diffusion.** The sheaf diffusion operator \(\mathrm{sd}_D\) from the cellular sheaf paper is a form of message passing that propagates local states toward global coherence via the sheaf Laplacian. The porous RRM's convergence condition (boundary tensions below threshold across the constellation) is precisely the energy function \(E(x) = \|d_0 x\|^2\) reaching its minimum, which is zero at a global section. The MLGRU's expectation and the ADM's posterior are stalk values at adjacent nodes; the boundary tension is the difference between them mapped through the structure map; the consultation-driven micro-update is a gradient step on the energy function that moves the constellation toward the global section. The convergence follows the standard convergence of sheaf diffusion to its fixed point, subject to two conditions the architecture has to enforce rather than assume. The first is that the sheaf Laplacian has a spectral gap bounded away from zero, which for the porous RRM depends on the BAREWire consultation topology being well-connected and the structure maps being well-conditioned. The second is that the initial stalk values sit in the correct cohomology class, which the BAREWire typed contracts enforce by construction: a consultation that arrives with incompatible types never enters the diffusion process in the first place. Under both conditions, convergence is exponential in the spectral gap. The analog in quantum error correction is the inductive bias that makes geometry-aware neural decoders work on BB codes (locality and translation equivariance on the factor graph guarantee a well-conditioned Laplacian), and the porous loop's typed consultation topology plays the same structural role for the constellation.

**The warm rotation protocol is a global section check.** When an incoming model configuration arrives for rotation into the active pathway, it must pass PHG elaboration before it can rotate. The elaboration check is exactly the global section condition on the configuration sheaf: do the incoming weights define a coherent assignment of annotations across all PHG nodes that is compatible with all structure maps? A configuration that fails elaboration has no global section, meaning there is no consistent annotation assignment that satisfies all the PHG's joint constraints simultaneously. The warm rotation succeeds only when the elaboration confirms that the incoming configuration witnesses a global section of the configuration sheaf. A failed elaboration is the absence of a coherent state for the constellation to rotate into, which is what makes the safety check load-bearing rather than advisory.

**λ-RLM convergence guarantees are Hoare triples over the combinator tree.** The termination guarantee (depth \(d = \lceil \log_{k^*}(n / \tau^*) \rceil\)) is a Hoare triple of the form \(\{|P| > \tau^*\}\, \text{Split}\, \{|P_i| < |P|\}\) iterated through the recursion tree, with the loop invariant being "every leaf is bounded by \(\tau^*\)." The Y-combinator's fixed point is the invariant that the combinator structure maintains across recursion levels; the cost guarantee is a Hoare triple over the combinator tree that bounds the number of model invocations by counting the leaves; the power-law accuracy result is a Hoare triple over the Reduce composition that bounds the error in the aggregated result. The combinators' typed signatures are what make these triples decidable: the type discipline restricts the combinator language to one in which the relevant Hoare obligations live in QF_LIA over the abelian fragment, which means Z3 can discharge them at compile time. λ-RLM is, in this reading, a Tier 1 / Tier 2 verification target whose obligations the framework's existing dual-pass machinery already supports.

The convergence of these four readings is not coincidental. Both λ-RLM and the porous loop are constructions over finite posets (the recursion tree, the actor constellation) with stalks (subproblem states, actor states) and structure maps (combinator composition, BAREWire consultation). Both require a global section condition for their guarantees to hold (every leaf below \(\tau^*\) for λ-RLM; convergent boundary tensions for the porous loop). Both compose with the framework's existing verification stack at exactly the layer where the sheaf vocabulary makes the composition precise.

## Implications

The convergence between λ-RLM's functional approach and the porous loop's actor-based approach is not coincidental. Both are responses to the same structural limitation: neural models that lack formal control over their own recurrence. Both impose typed discipline from outside the neural component. Both contain uncertainty within bounded scope.

The categorical framework developed in this series provides the formal vocabulary for understanding why both approaches work and how they relate. The practical implication for the Fidelity framework is that the porous loop's typed actor consultation is a generalization of the fixed-point combinator pattern: it provides the same structural control guarantees while additionally supporting domain-grounded inference through typed, verified, dimensionally-annotated channels.

This positions the HRM → RRM → Porous RRM progression not as a speculative architectural direction but as a principled extension of a pattern that is independently validated: typed structural control of inference produces formally verifiable, empirically measurable improvements over unstructured neural recursion.

## References

[1] A. Roy, R. Tutunov, X. Ji, M. Zimmer, H. Bou-Ammar, "The Y-Combinator for LLMs: Solving Long-Context Rot with λ-Calculus," arXiv:2603.20105, 2026.

[2] B. Gavranović, P. Lessard, A. Dudzik, et al., "Categorical Deep Learning is an Algebraic Theory of All Architectures," arXiv:2402.15332, 2024.

[3] M. Zhu, W. A. Tali, R. D. Lange, et al., "Scalable MatMul-free Language Modeling," arXiv:2406.02528, 2024.
