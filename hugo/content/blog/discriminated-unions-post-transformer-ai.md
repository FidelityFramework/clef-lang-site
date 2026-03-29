---
title: "Discriminated Unions In Post-Transformer AI"
linkTitle: "DUs in Post-Transformer AI"
description: "Exploring BAREWire's Key Role in the Future of High Efficiency Machine Learning"
date: 2025-06-17
authors: ["Houston Haynes"]
tags: ["Innovation", "Architecture", "AI"]
params:
  originally_published: 2025-06-17
  original_url: "https://speakez.tech/blog/discriminated-unions-in-post-transformer-ai/"
  migration_date: 2026-03-12
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

The AI industry stands at an inflection point. As detailed in our ["Beyond Transformers"](/blog/beyond-transformers/) analysis, the convergence of matmul-free architectures and sub-quadratic models will lead a fundamental shift in how we build and deploy AI systems. While the research community has demonstrated these approaches can match or exceed transformer performance with dramatically lower computational requirements, our investigation at SpeakEZ has uncovered an intriguing gap:

> Current tensor-only representations may not optimally capture the heterogeneous computational patterns these models require.

This insight emerged from our practical work on [CNN to Topological Object Classification (TopOC) transfer learning](/blog/cnn-to-topoc-transfer-learning/), where we recently discovered that heterogeneous representations were essential for maintaining dimensional integrity across different mathematical domains. This article explores our early investigation into how BAREWire's discriminated union-aware memory layout could address this gap more broadly, though we emphasize this remains a theoretical direction requiring substantial research to verify.

## The Post-Transformer Memory Challenge

Our consideration began with examining the building blocks of emerging post-transformer architectures:

**Matmul-free models** use ternary weights (-1, 0, +1) and replace matrix multiplication with simple addition and subtraction. The UC Santa Cruz research showed these can process billion-parameter models at just 13 watts on FPGAs.

**Sub-quadratic architectures** like Mamba employ state space models that maintain different computational states across sequence positions. Linear attention variants use kernel approximations that require heterogeneous data structures.

**Hybrid architectures** combine neural and symbolic components, mixing continuous computations with discrete reasoning steps.

Traditional tensor frameworks force all these patterns into homogeneous arrays, creating potential inefficiency at every level:

```python
# PyTorch's tensor-only approach to representing a Mamba layer
class MambaLayer:
    def __init__(self, d_model, d_state):
        # Everything becomes a tensor, regardless of actual structure
        self.A = torch.zeros(d_state)  # Diagonal matrix, wastes memory
        self.B = torch.zeros(d_state, d_model)
        self.C = torch.zeros(d_model, d_state)
        self.D = torch.zeros(d_model)  # Single parameter per channel
        self.delta = torch.zeros(d_model)  # Time step parameter

        # State has to be maintained as tensor
        self.state = torch.zeros(batch_size, d_state)
```

This homogeneous representation misses an opportunity:

> Post-transformer models are inherently heterogeneous, combining different types of computations that might benefit from different memory layouts.

## Genesis: CNN to TopOC Transfer Learning

Our exploration of discriminated unions for AI architectures emerged from practical challenges we encountered in our work on CNN to Topological Object Classification (TopOC) transfer learning. In that research, we developed dimensionally-constrained models using [the Clef language](https://clef-lang.com)'s Units of Measure system to maintain representational integrity when bridging fundamentally different mathematical domains, convolutional feature spaces and topological invariants.

This work revealed a critical insight: **the difficulty of transfer learning between different representational domains often stems from forcing heterogeneous information through homogeneous tensor pipelines**. Our TopOC architecture already employs discriminated union-like patterns out of necessity:

```fsharp
// From our TopOC work - implicit discriminated unions
type SensorData =
    | AccelerometerReading of Tensor<mps2 * Channel * Time>
    | GyroscopeReading of Tensor<radps * Channel * Time>
    | MagnetometerReading of Tensor<T * Channel * Time>

type RepresentationSpace =
    | ConvolutionalFeatures of FeatureTensor
    | TopologicalInvariants of TopologicalTensor
    | PhysicalState of NavigationState
```

The success of this approach in maintaining both safety and efficiency while translating between radically different mathematical structures suggested a broader principle might be at work. If heterogeneous representations proved essential for CNN-to-TopOC transfer, might they also address the inefficiencies we observe in post-transformer architectures?

## Discriminated Unions as Natural Representations

Building on our TopOC insights, we consider that Clef discriminated unions could provide a more natural representation for these heterogeneous computational patterns:

```fsharp
// Potential representation of matmul-free components
type MatmulFreeWeight =
    | Ternary of value: sbyte * scale: float32  // -1, 0, +1 with learned scale
    | Binary of bit: bool * scale: float32      // 0, 1 with scale
    | Quantized of bits: int * value: int * scale: float32

// Possible state space model components with natural structure
type SSMComponent =
    | DiagonalA of values: float32[]  // Diagonal transition matrix
    | InputProjection of B: float32[,]
    | OutputProjection of C: float32[,]
    | DeltaParameter of dt: float32   // Single time-step parameter

// Potential sub-quadratic attention variants
type EfficientAttention =
    | LinearAttention of kernel: KernelFunction * features: int
    | FlashAttention of blockSize: int * numBlocks: int
    | SlidingWindow of windowSize: int * stride: int
    | Performer of randomFeatures: float32[,] * orthogonal: bool
```

We see these representations may serve to match the mathematical structure of post-transformer models more directly, and therefore represent an advancement in machine learning architecture.

## BAREWire's Key: Zero-Copy Operations

Our work in developing BAREWire for general use suggests it might serve well to transform these type representations into efficient memory layouts using the BARE protocol's ability to natively handle discriminated unions. This could be particularly interesting for the mixed computational patterns in post-transformer architectures:

### Theoretical Matmul-Free Memory Layout

For matmul-free models, our initial explorations suggest BAREWire might enable direct memory representation of ternary weights with minimal overhead:

```fsharp
// Conceptual BAREWire schema for matmul-free layer
let matmulFreeSchema =
    schema "MatmulFreeLayer"
    |> withType "TernaryWeight" (struct' [
        field "packed_values" (array uint64)  // 32 weights per uint64
        field "scale" float32
    ])
    |> withType "Layer" (union [
        variant "Dense" (struct' [
            field "weights" (userType "TernaryWeight")
            field "bias" (array float32)
        ])
        variant "Attention" (struct' [
            field "q_weights" (userType "TernaryWeight")
            field "k_weights" (userType "TernaryWeight")
            field "v_weights" (userType "TernaryWeight")
        ])
    ])
```

This schema could enable packing 32 ternary values into a single 64-bit word (2 bits per value), achieving significant compression over float32 representation while maintaining zero-copy access. It should be noted that whether this theoretical advantage translates to real-world performance gains remains an open research question, but the implications are intriguing.

### Exploring State Space Model Optimization

For sub-quadratic models like Mamba, discriminated unions could also enable more efficient representation of heterogeneous components:

```fsharp
type MambaLayer =
    | StateUpdate of A: DiagonalMatrix * B: Tensor * state: MutableState
    | OutputProjection of C: Tensor * D: float32
    | GateComputation of input_proj: Linear * gate_proj: Linear

// Conceptual zero-copy state updates
let processSequence (layer: Region<MambaLayer, 'r>) (input: Tensor) =
    match BAREWire.readVariant layer with
    | StateUpdate(A, B, state) ->
        // Potential in-place state update without allocation
        for t in 0 .. seqLength - 1 do
            // s_t = A * s_{t-1} + B * x_t
            let x_t = input.[t]

            // Diagonal multiplication could be O(n) not O(n^2)
            DiagonalOps.multiplyInPlace state A
            TensorOps.addScaledInPlace state B x_t

            // Output y_t = C * s_t
            yield computeOutput state

    | _ -> failwith "Invalid layer type"
```

We're exploring is that state space models have natural sparsity patterns and sequential dependencies that discriminated unions might represent more directly than tensor-only frameworks. This speculation arises from examining the mathematical structure of SSMs like Mamba. These models fundamentally differ from transformers in several ways:

**Structural Sparsity**: The transition matrix A in SSMs is typically diagonal or block-diagonal, meaning only O(n) non-zero elements in what tensor frameworks represent as an n*n matrix. Current implementations waste memory storing zeros or use complex indexing schemes to avoid it.

**Selective Mechanisms**: Mamba's key innovation is input-dependent selection - certain computations are gated based on the input content. This creates a branching computational structure where different tokens might follow different computational paths, naturally suited to variant type representation.

**Heterogeneous Parameters**: SSMs combine fundamentally different types of parameters:
- Diagonal matrices (sparse, fixed structure)
- Dense projection matrices (full computation needed)
- Scalar time-step parameters (single values per channel)
- Binary gates (on/off selection mechanisms)

Current tensor frameworks force all these into the same representation, likely missing significant optimization opportunities.

**Sequential State Evolution**: Unlike transformers that can process all positions in parallel, SSMs must process sequences step-by-step, with each state depending on the previous. This creates a fundamentally different computational pattern - more like a state machine than a parallel matrix operation. Discriminated unions naturally represent state machines with different transition types.

Our hypothesis is that representing these diverse computational patterns as explicit variants could enable compilers and hardware to optimize each pattern separately, rather than forcing everything through generic tensor operations. For instance, diagonal multiplication could be a distinct variant from dense multiplication, allowing specialized hardware paths for each.

## Heterogeneous Memory

A particularly intriguing direction in our research involves the potential for discriminated unions to better preserve contextual information in continual learning scenarios. Current approaches force all context into homogeneous tensor representations, in theory losing structural information in the process.

### The Signal Preservation Hypothesis

We hypothesize that heterogeneous CPU-GPU memory splits with type-aware layouts could serve to preserve contextual signals that current approaches lose:

```fsharp
// Conceptual context-aware memory representation
type ContextMemory =
    | WorkingMemory of tokens: GPU.Tensor * attention: GPU.Cache
    | EpisodicMemory of events: CPU.SparseGraph * timestamps: Timeline
    | SemanticMemory of facts: CPU.KnowledgeGraph * confidence: float32[]
    | ProceduralMemory of skills: CPU.ProgramSynthesis * usage: Stats

// Potential zero-copy transitions at semantic boundaries
let updateContext (memory: Region<ContextMemory, 'rw>) (newInfo: Information) =
    match newInfo with
    | ImmediateRelevance data ->
        // Hot path: GPU processing
        GPU.updateWorkingMemory memory data

    | LongTermPattern pattern ->
        // Cold path: CPU storage with rich structure
        CPU.integrateEpisodicMemory memory pattern

    | FactualKnowledge fact ->
        // Structured storage: Graph operations
        CPU.updateSemanticGraph memory fact
```

This approach could enable models to maintain richer representations across different memory hierarchies, though validating this hypothesis would require extensive empirical research.

## Advancements in Auto-differentiation

This question represents perhaps the most significant technical challenge in our research direction. Current autodiff systems like PyTorch's autograd and JAX's grad are built on the assumption of homogeneous tensor operations with uniform gradient flow. However, our work on Furnace, a component of the Fidelity framework, provides a unique foundation for exploring heterogeneous auto-differentiation.

### A Foundation for Heterogeneous Gradients

Furnace brings several key capabilities that position it uniquely for post-transformer architectures:

- **Nested and mixed-mode differentiation**: Unlike conventional frameworks, Furnace supports arbitrarily nested differentiation operations, essential for meta-learning and higher-order optimization in heterogeneous architectures
- **Clef programming foundation**: Built in Clef, Furnace naturally supports the algebraic structures needed for variant type differentiation
- **PyTorch-familiar idioms with guarantees**: While maintaining familiar APIs, Furnace's core enables reasoning about gradient flow through variant types

Our speculation on extending Furnace for discriminated unions draws from several theoretical directions:

### Tagged Gradient Representations

Just as forward computation uses discriminated unions, Furnace could be extended to support tagged gradient variants:

```fsharp
type GradientFlow =
    | DenseGradient of grad: Tensor<float32>
    | SparseGradient of indices: int[] * values: float32[]
    | TernaryGradient of accumulated: float32 * threshold: float32
    | StructuredGradient of pattern: GradientPattern * data: CompactRepresentation
```

### Variant-Aware Chain Rule

The chain rule of calculus remains valid regardless of representation. Furnace's existing differentiation engine could be extended so each variant type defines its own local gradient computation and composition rules:

- **Ternary weights**: Gradients accumulate until crossing quantization thresholds
- **Sparse operations**: Gradients only flow through non-zero paths
- **Selective mechanisms**: Gradients gate based on forward-pass decisions

### Compile-Time Gradient Synthesis

Clef's type system, combined with Furnace's architecture, could enable compile-time generation of efficient gradient paths. Since the structure of discriminated unions is known statically, we could potentially generate specialized backprop code for each variant combination, avoiding runtime dispatch overhead while maintaining Furnace's mathematical correctness guarantees.

### Heterogeneous Adjoints

Drawing from automatic differentiation theory and Furnace's support for nested differentiation, each variant could maintain its own adjoint representation:

- Dense layers: Traditional tensor adjoints (Furnace's current strength)
- Ternary layers: Accumulated gradient buffers with quantization-aware updates
- Sparse layers: Coordinate-format gradient storage
- Gated layers: Boolean path traces for gradient routing

### Lazy Gradient Materialization

Not all gradients need to be materialized as full tensors. Furnace's foundation could enable lazy evaluation where gradients remain in their most efficient representation until needed for weight updates, a natural extension of Clef's lazy evaluation capabilities. This concept shares philosophical similarities with DeepSeek's multi-head latent attention (MLA), which maintains key-value pairs in compressed latent representations until needed for attention computation. Just as MLA achieves memory efficiency by deferring full materialization of attention matrices, our approach could defer full gradient tensor materialization until the actual weight update step.

An intriguing consideration in this approach involves the precision requirements of heterogeneous architectures. Post-transformer models with ternary weights and sparse operations create more complex, discrete loss landscapes that may require higher numerical precision to navigate effectively. This presents both a challenge and an opportunity:

**The Precision-Performance Balance**: While maintaining higher precision might seem to inhibit performance advantages, our research suggests that Clef's direct access to extended precision floating-point operations, unencumbered by Python's C library limitations, could provide a crucial advantage. Where PyTorch and TensorFlow are constrained to 32-bit or 64-bit operations by their underlying BLAS libraries, Furnace could selectively apply 80-bit extended precision (on x86) or 128-bit quad precision where the loss landscape demands it, while maintaining efficient representations elsewhere.

```fsharp
// Conceptual precision-aware gradient accumulation
type PrecisionGradient =
    | StandardPrecision of grad: Tensor<float32>
    | ExtendedPrecision of grad: ExtendedFloat80  // For critical paths
    | LazyAccumulated of thunks: (unit -> float32) list  // Deferred computation

// Selective precision based on gradient magnitude and variance
let accumulateWithAdaptivePrecision (gradients: GradientFlow list) =
    match analyzeGradientStability gradients with
    | Stable -> LazyAccumulated (gradients |> List.map toThunk)
    | CriticalRegion -> ExtendedPrecision (accumulateExtended gradients)
    | Standard -> StandardPrecision (accumulate gradients)
```

The theoretical foundation exists in the community's work on algebraic automatic differentiation, where sum types (coproducts) have well-defined differentiation rules. The challenge is efficiently implementing these in the context of large-scale neural networks, particularly when balancing precision requirements against computational efficiency. Our hypothesis is that the same type information that enables efficient forward computation could also guide both optimized backward computation and adaptive precision selection, using high precision only where the discrete nature of post-transformer architectures demands it, while maintaining efficiency elsewhere. This selective approach could provide more accurate gradient estimates in the challenging optimization landscapes of quantized networks without uniformly sacrificing performance, but this requires significant research to validate and likely some additional engineering effort to optimize.

## Research Directions and Open Questions

Our investigation has identified several key research questions that beg further assessment:

1. **Performance validation**: Can discriminated union representations match or exceed the performance of current tensor-only implementations?

2. **Automatic differentiation**: How would backpropagation work efficiently across heterogeneous type representations?

3. **Hardware integration**: Can existing accelerators be adapted to benefit from variant type dispatch, or would new architectures be required?

4. **Framework compatibility**: How could this approach integrate with existing ML ecosystems while providing migration paths?

5. **Memory hierarchy optimization**: Can type-aware layouts actually preserve more contextual information across CPU-GPU boundaries?

## Beyond Current Architectures

As we look even further forward toward neuromorphic and quantum-classical hybrid models, the potential value of discriminated unions may become even more apparent:

```fsharp
// Potential future neuromorphic representation
type SpikingNeuron =
    | LIF of threshold: float32 * decay: float32 * refractory: TimeSpan
    | Izhikevich of a: float32 * b: float32 * c: float32 * d: float32
    | SpikeResponse of kernel: ResponseKernel * adaptation: AdaptationCurve

// Possible quantum-classical hybrid
type QuantumLayer =
    | ParameterizedGate of angles: float32[] * qubits: int[]
    | Measurement of basis: PauliBasis * classical_post: ClassicalNetwork
    | VariationalCircuit of ansatz: Ansatz * parameters: float32[]
```

These future architectures may inherently require heterogeneous representations that current tensor-only frameworks cannot express.

## A Research Path Worth Exploring

Our investigation at SpeakEZ has identified discriminated union-aware memory layouts as a potentially valuable research direction for post-transformer AI architectures. This exploration emerged naturally from our practical experience with CNN to TopOC transfer learning and investigation of sub-quadratic and matmul-free architectures, where heterogeneous representations proved essential for maintaining dimensional integrity across mathematical domains. While **this broader application remains speculative**, the convergence of several trends makes this exploration timely:

- The shift to heterogeneous post-transformer architectures
- The need for more memory-efficient representations
- The opportunity for better context preservation in continual learning
- The potential for custom hardware optimization
- The availability of Furnace's advanced auto-differentiation capabilities

We emphasize that this article presents **early-stage exploration**, not validated results. The ideas presented here require rigorous empirical validation, performance benchmarking, and peer review before any significant claims can be made. Our goal is to contribute to the broader research conversation about how the industry might better represent the increasingly heterogeneous computational patterns emerging in modern AI, building on our practical insights.

As the industry moves beyond transformers, we believe exploring alternative memory representations is a valuable research direction. Whether this particular approach proves fruitful remains to be seen, but the investigation itself helps illuminate the assumptions and constraints built into our current frameworks. The combination of BAREWire's memory layout capabilities and Furnace's advanced differentiation engine provides a unique platform for this exploration. We look forward to the Fidelity framework's contributions to this growing field.

*The technology concepts described here relate to U.S. Patent Application No. 63/786,247 "System and Method for Zero-Copy Inter-Process Communication Using BARE Protocol", though we note that the specific application to AI architectures will likely remain a research topic of continuing interest.*
