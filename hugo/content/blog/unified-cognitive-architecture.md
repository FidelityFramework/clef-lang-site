---
title: "A Vision For Unified Cognitive Architecture"
description: "A design sketch for hypergraph-based reasoning that shares one substrate across training and inference"
date: 2025-10-15T00:00:00-04:00
tags: ["Architecture", "AI", "Design"]
authors: ["Houston Haynes"]
params:
  originally_published: 2025-10-15
  original_url: https://speakez.tech/blog/unified-cognitive-architecture/
  migration_date: 2026-03-29
---

## AI's Berlin Wall

In our exploration of [neuromorphic computing](/blog/advent-of-neuromorphic-ai/), we examined how specialized hardware might finally deliver on AI's efficiency promises. Hardware alone does not address a structural limitation in current AI systems: the wall between how systems learn and how they operate.

---

🔄 Updated October 22, 2025

This article now includes cross-references to related blog entries, connecting broader concepts presented here to detailed technical explorations elsewhere. These inline links serve as entry points for readers seeking deeper dives into various topics, while this blog entry illuminates our broader vision.

---

Every "AI" company today lives with a divide we've accepted as natural law: models are trained (expensive, slow, centralized) then deployed for inference (cheap, fast, distributed). This binary worldview has created a technological imbalance where accumulating and disseminating information exists in separate universes. Yes, modern models can incorporate corrections through in-context learning, but it's a workaround, not a solution. The knowledge lives in fragile prompt engineering and vanishes when context windows reset. A GPT model absorbs your correction only until the conversation ends. A vision model adapts to your specific product only within the bounds of few-shot examples, never truly updating its understanding. The working model is still trapped in the high, thick wall between training and inference.

The industry has built considerable infrastructure around this dichotomy: training clusters that consume megawatts, inference servers that strain to learn, and an entire industry devoted to managing frozen intelligence. Much like the [specialized LISP machines that seemed essential](/blog/hardware-lessons-from-lisp/) until economic forces rendered them obsolete, this barrier has come to seem permanent. We think a different architecture can move past it.

## Hypergraphs as a Shared Substrate

We've written before about [how hypergraphs naturally express complex relationships](/docs/internals/pipeline/hyping-hypergraphs/) in our compiler's Program Semantic Graph. The question we are working through is whether this same mathematical substrate can represent not just program structure, but knowledge, reasoning, and learning.

We theorize an AI system without frozen weights, a knowledge structure that grows with use, where compilation, knowledge representation, reasoning, and execution share one mathematical foundation. That is the direction hypergraph-based cognitive architectures point, extending our compiler work toward continuous intelligence.

#### The Universal Substrate

Traditional neural networks force everything through matrix multiplication: \[y = \sigma(Wx + b)\]

As we explored in our entry on [moving beyond transformers](/blog/beyond-transformers/), this matrix-centric view is increasingly recognized as a limitation rather than a law. Reasoning isn't matrix multiplication. Knowledge isn't tensors. Language isn't vectors. The hypergraph represents relationships as they actually exist:

\[\mathcal{H} = (V, E, \phi: E \to 2^V, \tau: E \to \text{Type}, w: E \to \mathbb{P})\]

Where vertices \(V\) are concepts, hyperedges \(E\) connect multiple concepts simultaneously, and weights \(w\) use posit arithmetic for tapered precision exactly where needed.

```fsharp
type UniversalHypergraph =
    | Knowledge of concepts: Set<Concept> * relations: Set<Relation>
    | Reasoning of states: Set<State> * transitions: Set<Inference>
    | Compilation of ast: Set<Node> * constraints: Set<Proof>
    | Execution of ops: Set<Operation> * dataflow: Set<Dependency>

    // one traversal over four cases, one structure
    member this.Traverse(query) =
        match this with
        | Knowledge kg -> reasonThroughKnowledge kg query
        | Reasoning rg -> executeReasoning rg query
        | Compilation cg -> optimizeViaProofs cg query
        | Execution eg -> runOnHardware eg query
```

## Fidelity's Library of Alexandria

### Knowledge as a Service, Not a Monument

The ancient Library of Alexandria didn't try to fit all knowledge into a single scroll. It organized specialized collections that scholars could access as needed.

Instead of monolithic models, we envision specialized knowledge hypergraphs that load on demand. A system loads the domains a given query requires, rather than holding all knowledge resident at once.

```fsharp
type CognitiveLibrary = {
    Core: BaseKnowledge  // Always loaded: language, logic, common sense
    Catalog: DomainRegistry  // Available specializations
    Librarian: Alex  // Builds and indexes hypergraphs during compilation
    Cards: Map<User, Capabilities>  // What each user can access
}

let processQuery query library =
    let required = analyzeRequirements query
    let loaded =
        required
        |> Set.map (fun domain ->
            library.Load domain  // Memory-mapped, zero-copy
        )
    traverse loaded query
```

The economics shift with this structure: instead of training massive models, organizations build and trade specialized hypergraphs. A Bloomberg financial relationships graph. A PubMed molecular interactions graph. A proprietary manufacturing process graph. Knowledge becomes a modular, composable asset that can be loaded and exchanged.

We have since worked this vision into a concrete research program. The specialized models sketched here are developed as [Adaptive Domain Models](/docs/design/constrained-machine-learning/why-adaptive-domain-models/), and the arrangement in which many of them compose alongside a general model is [the constellation](/docs/design/constrained-machine-learning/the-constellation/).

## Fractal Reasoning

### System 1 vs System 2, Naturally

In this design, reasoning depth follows from the dynamics of the hypergraph traversal. We place the interesting behavior at the boundary between stable and chaotic regimes:

\[\lambda = \lim_{n \to \infty} \frac{1}{n} \sum_{i=0}^{n-1} \ln|f'(x_i)|\]

When \(\lambda < 0\): stable, fast, System 1 thinking. When \(\lambda > 0\): chaotic, exploratory, System 2 reasoning.

```fsharp
let adaptiveReasoning state query =
    let lyapunov = computeLyapunov state
    match lyapunov with
    | lambda when lambda < -0.5 ->
        // Stable: direct retrieval
        { Strategy = DirectPath; Depth = 1 }
    | lambda when lambda > 0.5 ->
        // Chaotic: deep exploration
        { Strategy = MonteCarloTree; Depth = 10..20 }
    | _ ->
        // Boundary: the interesting dynamics
        { Strategy = FractalSearch; Depth = variable }
```

### Variable Depth Across the Graph

Reasoning depth isn't global; it's local to the complexity encountered:

\[\text{depth}(v) = \begin{cases}
1 & \text{if simple lookup} \\
\log n & \text{if pattern matching} \\
\sqrt{n} & \text{if analogical reasoning} \\
n & \text{if proof construction}
\end{cases}\]

```fsharp
let variableDepthTraversal hypergraph start =
    let rec traverse vertex depth =
        let complexity = lambdaLocalComplexity vertex
        let newDepth =
            if complexity > threshold then
                depth * 2  // Double at complexity spikes
            else
                max 1 (depth - 1)  // Reduce in simple regions

        let neighbors = hypergraph.GetHyperedges vertex
        neighbors |> List.map (traverse newDepth)

    traverse start 1
```

## Hardware as Fluid Architecture

### CGRA: The Shape-Shifting Processor

As we've seen in our examination of [dataflow architectures emerging from the LISP era](/blog/hardware-lessons-from-lisp/), computation doesn't have to be instruction-driven. Coarse-Grained Reconfigurable Arrays represent this evolution: hardware that doesn't execute fixed instructions but reshapes itself to match the computation's dynamics.

```fsharp
let reconfigureBasedOnDynamics cgra lyapunov =
    match lyapunov with
    | Stable ->
        // Configure as pipeline
        cgra.Configure {
            Topology = LinearPipeline
            Tiles = [MAC; MAC; Add; Store]
            Routing = NearestNeighbor
        }
    | Chaotic ->
        // Configure as exploration mesh
        cgra.Configure {
            Topology = FullMesh
            Tiles = [Branch; Compare; Route; Cache]
            Routing = Crossbar
        }
    | Boundary ->
        // Hybrid configuration
        cgra.Configure {
            Topology = FractalClusters
            Tiles = mixed
            Routing = Adaptive
        }
```

### Neuromorphic Spike Patterns

Building on our work with neuromorphic architectures, hyperedges become synchronization patterns that map naturally to spike-based computation:

```fsharp
type NeuromorphicMapping = {
    Vertices: Map<Concept, NeuronGroup>
    Hyperedges: Map<Relation, SpikePattern>
    Reasoning: SpikeWave -> SpikeWave
}

let neuromorphicReasoning query mapping =
    let spikes = encodeAsSpikes query
    let rec propagate wave =
        let activated =
            mapping.Hyperedges
            |> Map.filter (matchesPattern wave)

        let next = fireNeurons activated
        if converged next then
            decode next
        else
            propagate next

    propagate spikes
```

## The Twilight of Discrete Training

### Every Query Teaches

In the architecture we are sketching, training and inference are not separate phases. Each interaction would strengthen the paths it used:

```fsharp
let continuousLearning hypergraph query =
    // traditional "inference"
    let result = traverse hypergraph query

    // strengthen used paths, traditional "training"
    let strengthened =
        result.Path
        |> List.map (fun edge ->
            { edge with Weight = edge.Weight * 1.01 })

    let newEdges =
        if result.Quality > threshold then
            createHyperedge result.Concepts
        else []

    { hypergraph with
        Edges = strengthened @ newEdges }
```

### Blue-Green Knowledge Deployment

We intend knowledge updates to land without disrupting active queries:

```fsharp
let updateKnowledge current update =
    // Load new knowledge in parallel
    let green = loadHypergraph update

    // Test compatibility
    let compatible =
        testQueries
        |> List.forall (fun q ->
            similarity (query current q) (query green q) > 0.95)

    if compatible then
        // Gradual transition
        async {
            for i in 0..100 do
                let weight = float i / 100.0
                activeGraph <- blend current green weight
                do! Async.Sleep 10
        }
    else
        // Keep current until resolved
        reportIncompatibility update
```

## Proof-Aware Reasoning

### Proofs Guide Optimization

Our [exploration of proof-aware compilation](/docs/internals/pipeline/proof-aware-compilation/) showed how verification properties carry information a compiler can act on. In cognitive architectures, we extend that principle: proofs establish correctness and also direct how reasoning unfolds.

The verifier's output feeds the optimizer here, the same properties that establish correctness also open up safe transformations:

```fsharp
type ProofGuidedOptimization = {
    Invariants: Set<Property>
    Transforms: Set<Optimization>
    Preserve: Property -> Optimization -> bool
}

let optimizeWithProofs hypergraph proofs =
    hypergraph.Edges
    |> List.map (fun edge ->
        let applicable =
            proofs.Transforms
            |> Set.filter (fun t ->
                proofs.Invariants
                |> Set.forall (fun inv ->
                    proofs.Preserve inv t))

        // Apply most aggressive safe optimization
        let optimal = selectBest applicable
        optimal edge)
```

## From Frozen Models to Living Intelligence

The transition to cognitive architectures can be incremental. Much as we've designed our Fidelity Framework to bridge traditional and emerging hardware, this path starts by augmenting existing systems before moving to native implementations.

### Phase 1: Hybrid Enhancement

Start with existing models, add hypergraph reasoning alongside:

- Keep your GPT/Claude/Gemini APIs
- Add hypergraph layers for specialized reasoning
- Target measurable gains in specific domains, reported with their bounds
- Learn which knowledge patterns benefit most from dynamic structures

### Phase 2: Knowledge Migration

Convert frozen weights to dynamic hypergraphs:

- Extract knowledge from trained models
- Recondition as hypergraph structures
- Deploy on neuromorphic hardware where available
- Establish continuous learning pipelines

### Phase 3: Native Intelligence

Pure hypergraph cognitive systems:

- No more massive training runs
- Continuous learning through use
- Knowledge as composable, tradeable assets
- Hardware that morphs with thought patterns

## Intelligence That Grows

We expect the advantage in the next era of AI to favor systems that update from use over systems that ship a fixed set of weights and freeze them. A hypergraph whose edges strengthen as queries traverse them is one way to build that.

Throughout this series, from [proof-aware compilation](/docs/internals/pipeline/proof-aware-compilation/) to [neuromorphic hardware](/blog/advent-of-neuromorphic-ai/), from [dataflow architectures](/blog/hardware-lessons-from-lisp/) to [post-transformer models](/blog/beyond-transformers/), we've been building toward one convergence. We treat the hypergraph as the mathematical substrate that lets compilation, reasoning, and learning share a single representation, rather than as one more compiler intermediate representation or neural network architecture sitting alongside the others.

The wall between training and inference is already under pressure from in-context methods and retrieval. Our interest lies in replacing the wall with shared structure: knowledge that grows, reasoning that adapts its depth, and hardware that reconfigures to the work. We will keep building toward that design as the rest of the framework comes into place.
