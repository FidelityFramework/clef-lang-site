---
title: "Fidelity Framework as AI Refinery"
linkTitle: "Fidelity as AI Refinery"
description: "Clef Type Safety Applied to Model Inference"
date: 2023-01-03
authors:
  - SpeakEZ
tags: ["AI", "Design"]
params:
  originally_published: 2023-01-03
  original_url: "https://speakez.tech/blog/fidelity-as-ai-refinery/"
  migration_date: 2026-02-15
---

The AI industry is shifting in how computational resources are allocated and optimized. While the last decade saw rapid advances through massive pre-training efforts on repurposed GPUs, we're now entering an era where **test-time compute (TTC)** and custom accelerators are emerging as the next stage of AI advancement. As highlighted in recent industry developments, DeepSeek AI lab disrupted the market with a model that delivers high performance at a fraction of competitors' costs, signaling two significant shifts: smaller labs producing state-of-the-art models and test-time compute becoming the next driver of AI progress.

## From Training to Test-Time Compute

This evolution is driven by a hard constraint: the scarcity of additional training data. The major AI labs have already trained their models on much of the available public data (including many copyrighted works to which they had no legal claim to use), making further pre-training improvements increasingly limited. Instead, the focus is shifting to reasoning capabilities at inference time, where models "think" before responding to a question rather than simply generating the statistically most likely response.

## Where Python-Based Frameworks Run Out of Road

The current dominant approach to AI model development, using dynamically typed frameworks like PyTorch, presents structural limitations to this pivot that become more apparent as the AI landscape moves to a test-time compute paradigm. Python's dynamic typing prevents "baked in" awareness of units of measure and other constraints that would govern many classes of model behavior toward a grounded result.

This absence of strong type safety leads to overtraining as developers attempt to compensate through brute force, teaching models to reflect an image of disciplined behavior rather than embedding principles at the foundation. The result is inefficient, computationally expensive inference that lacks the "zero-cost" safeguards of the more structured approach we are building in our Fidelity Framework.

The core issue is that today's models are missing the structured understanding of the world and its physical constraints that humans develop naturally. Current AI approaches struggle to incorporate these constraints, resulting in systems that require enormous training datasets yet still fail to reliably perform tasks that humans learn quickly.

As Yann LeCun, Chief AI Scientist at Meta, noted: "A 17-year-old can learn to drive a car in about 20 hours of practice... And we have millions of hours of training data of people driving cars, but we still don't have self-driving cars. So that means we're missing something really, really big."

## A Structured Approach to Reliable AI

Our Fidelity Framework addresses these limitations through a different starting point. The framework would include:

1. **Composer compiler**: Would enable efficient native compilation through a direct Clef to MLIR/LLVM path
2. **Farscape CLI**: Would provide integration with verified C/C++ libraries
3. **BAREWire interface**: Would deliver high-performance, low-burden binary communication
4. **Furnace auto-differentiation**: Would support reliable machine learning operations

Together these would form a foundation for high-trust AI deployment that holds correctness and performance in the same path. For workloads that require formal certification, our Fidelity Framework would add high-assurance capabilities through a bridge to the F* (F-star) verification framework, which we will detail in future technical publications.

## Type Safety for Neural Representations

A cornerstone of our approach is Clef's type system, which provides compile-time safety without runtime overhead. This shifts away from the current paradigm where models must learn constraints through extensive training rather than having these constraints built into their foundation. And it follows that even in model training the burden of runtime marshaling of tensor shapes conveys an outsized time and computational cost for current model building practices.

Consider how neural networks process data through tensor operations, where shape inconsistencies and dimensional mismatches are common sources of errors that can only be detected at runtime in Python-based frameworks. Through the use of Clef and Furnace, those runtime considerations would be resolved before compilation even begins:

```fsharp
// Define physical units for medical imaging
[<Measure>] type mm        // millimeters for physical dimensions
[<Measure>] type voxel     // voxel/pixel units
[<Measure>] type HU        // Hounsfield Units for CT scans

// Type-safe medical image with embedded physical properties
type MedicalImage<[<Measure>] 'resolution, [<Measure>] 'intensity> = {
    Data: float32[,,]
    Resolution: float<'resolution>  // e.g., mm per voxel
    Dimensions: int * int * int     // dimensions in voxels
}

// A CNN layer that preserves physical units
let convolutionLayerCT
    (input: MedicalImage<mm/voxel, HU>)
    (kernelSize: int)
    (filters: int)
    : MedicalImage<mm/voxel, HU> =
    // convolution preserving the input's physical units
```

In both examples, the Clef type system ensures that operations maintain dimensional and physical consistency at compile time. If a developer attempts to use incompatible dimensions or units, the Clef tooling reports a compilation error at design time. This is a constructive constraint that supports correctness and safety, and we have found no other representative implementations of it for neural representations in the standing literature we have reviewed.

This collaborative approach between the tooling and developer would remove a class of errors that recurs in deep learning systems and currently requires extensive runtime checks, debugging, and trial-and-error. The efficiency gains we envision for our Fidelity Framework would also reach design-time. Many machine learning working groups currently plan for large staff allocations over long calendar windows into the assumptions of their work. The efficiency gains we expect over Python-based frameworks would shorten the half-life of model engineering cycles and reduce the fixed costs that come with them.

```fsharp
// Define tensor dimension types for compile-time safety
[<Measure>] type batchSize
[<Measure>] type embeddingDim
[<Measure>] type seqLength
[<Measure>] type hiddenDim

// Type-safe tensor representation
type Tensor<[<Measure>] 'batch, [<Measure>] 'seq, [<Measure>] 'dim> =
    { Data: float32[,,]; Shape: int<'batch> * int<'seq> * int<'dim> }

// A self-attention layer with compile-time dimension checking
let selfAttention
    (input: Tensor<batchSize, seqLength, embeddingDim>)
    (wq: Tensor<embeddingDim, hiddenDim, unit>)
    (wk: Tensor<embeddingDim, hiddenDim, unit>)
    (wv: Tensor<embeddingDim, hiddenDim, unit>)
    : Tensor<batchSize, seqLength, hiddenDim> =
    // Simplified implementation to demonstrate type safety
    let batchSize, seqLen, _ = input.Shape
    let _, hiddenDim, _ = wq.Shape

    // Create result tensor with correct dimensions
    { Data = Array3D.zeroCreate<float32> (int batchSize) (int seqLen) (int hiddenDim)
      Shape = (batchSize, seqLen, hiddenDim) }

// This compiles correctly because dimensions match
let validUsage() =
    let batch = 32<batchSize>
    let seq = 128<seqLength>
    let embed = 512<embeddingDim>
    let hidden = 64<hiddenDim>

    let input = { Data = Array3D.zeroCreate<float32> (int batch) (int seq) (int embed)
                  Shape = (batch, seq, embed) }
    let wq = { Data = Array3D.zeroCreate<float32> (int embed) (int hidden) 1
               Shape = (embed, hidden, 1<unit>) }
    let wk = { Data = Array3D.zeroCreate<float32> (int embed) (int hidden) 1
               Shape = (embed, hidden, 1<unit>) }
    let wv = { Data = Array3D.zeroCreate<float32> (int embed) (int hidden) 1
               Shape = (embed, hidden, 1<unit>) }

    let output = selfAttention input wq wk wv
    // output has type Tensor<batchSize, seqLength, hiddenDim>

// This would fail to compile - Clef catches the dimension mismatch at compile time!
// Uncommenting would cause error: "Type mismatch. Expecting: Tensor<embeddingDim,hiddenDim,unit>
//                                  But given: Tensor<seqLength,embeddingDim,unit>"
let invalidUsage() =
    let batch = 32<batchSize>
    let seq = 128<seqLength>
    let embed = 512<embeddingDim>
    let hidden = 64<hiddenDim>

    let input = { Data = Array3D.zeroCreate<float32> (int batch) (int seq) (int embed)
                  Shape = (batch, seq, embed) }
    // Incorrect: using seq and embed dimensions in wrong order
    let wq = { Data = Array3D.zeroCreate<float32> (int seq) (int embed) 1
               Shape = (seq, embed, 1<unit>) }
    let wk = { Data = Array3D.zeroCreate<float32> (int embed) (int hidden) 1
               Shape = (embed, hidden, 1<unit>) }
    let wv = { Data = Array3D.zeroCreate<float32> (int embed) (int hidden) 1
               Shape = (embed, hidden, 1<unit>) }

    let output = selfAttention input wq wk wv  // Type error caught here!
    ()
```

This foundation would open a path toward delivering neural networks that can reason with built-in dimensional awareness and structural constraints, reducing the need to rediscover these patterns from training data alone. The benefits would reach past catching errors. By embedding structural knowledge directly into the type system, models built with our Fidelity Framework could learn more efficiently from less data, since they would not need to rediscover basic constraints already encoded in their foundation.

This aligns with research into inductive biases in machine learning, where as noted in a Royal Society paper on higher-level cognition: "Our current state-of-the-art machine learning systems sometimes achieve good performance on a specific and narrow task, using very large quantities of labelled data" **while systems with appropriate inductive biases can generalize more efficiently with less training data**. These cumulative advances: reduced error rate, simplified model processing, reduced data needs all converge on challenging the current assumptions on the outsized time and compute costs of building models.

And in that process companies would get higher performance, greater reliability, and more options for deploying high-leverage models in a wider array of devices that meet the customer and their needs "where they are".

## Multi-Head Latent Attention

Our Fidelity Framework's envisioned use of [the Furnace library](https://github.com/fsprojects/Furnace) would show how to adapt recent techniques like DeepSeek's Multi-Head Latent Attention (MLA) while tuning models for performance. MLA reports efficiency improvements up to a 93.3% reduction in KV cache size while improving model throughput. [see the Appendix for an extended example]

This approach would include:

1. **Latent Compression Layer**: To compress the input hidden states
2. **Query Projection**: For computing query vectors
3. **Key/Value Decompression**: To expand latent vectors to full key/value representations
4. **RoPE Handling**: For decoupled positional encoding
5. **Forward Pass**: The main attention computation logic
6. **KV Cache Management**: For efficient caching of latent vectors

The primary benefit would be the significant reduction in memory footprint during inference while maintaining or improving quality compared to standard attention mechanisms. Through the Fidelity Framework, MLA could be deployed across diverse hardware architectures through a unified MLIR lowering pipeline, including NVIDIA, AMD, and Tenstorrent hardware. This approach would enable:

1. Dramatic reduction in memory requirements
2. Performance improvements over standard attention
3. Increased token output while maintaining context
4. Significantly expanded context windows without hardware upgrades

These benefits would hold while the compilation process maintains memory safety as a structural guarantee and keeps computational accuracy within bounds the types carry, which would be difficult to reach with conventional Python approaches.

## Optimizing Real World Inference

Our "AI Refinery" reframes how AI models are constructed for real-world performance. Like an oil refinery that transforms crude petroleum into refined end products, the AI Refinery would transform raw model capabilities into verified, efficient, and safe computational systems.

By focusing on inference optimization through compile-time verification and Clef's type safety, we aim to address the emerging challenges in test-time compute:

1. **Memory Efficiency**: Clef's units of measure and our framework's memory management would reduce the memory footprint of models during inference, enabling larger context windows and more complex reasoning on existing hardware.

2. **Computational Performance**: Direct compilation to MLIR/LLVM would create highly optimized execution paths that deliver superior inference speed compared to interpreted approaches.

3. **Verification Guarantees**: Unlike black-box models whose behaviors can only be validated through extensive testing, models built with the Fidelity Framework would carry type guarantees and even algorithmic proofs of their properties and constraints.

4. **Hardware Adaptability**: The framework's universal adaptation interface would enable targeting of diverse hardware architectures from a single verified codebase, maximizing performance across the computing ecosystem.

## Physics-Based Models for Reliable AI

The future of AI lies in models that incorporate real-world understanding and constraints at their foundation rather than attempting to approximate these principles through brute-force training. Current AI models, despite their capabilities in language and image generation, still approximate mathematical, financial, and physical reality from data rather than modeling and predicting it with the well-founded understanding that humans possess.

Our Fidelity Framework takes a different starting point: build models with an intrinsic understanding of the problem space's natural constraints through Clef's type system and formal verification. This approach would aim to create systems that can reason about the physical world with the same understanding that humans develop, rather than approximating it through massive datasets that continue to leave many models coming up short.

## Redefining AI Delivery for the Age of Inference

As the AI landscape evolves from massive pre-training to the age of test-time compute, our Fidelity Framework would address the limitations of current approaches. Through Clef's type system and direct compilation pathways, we are designing an "AI Refinery" that would change how models are built, verified, and deployed.

The result would be AI systems that combine reasoning capabilities with formal safety and, for the structural layer, verification guarantees, delivering high-performance computation for mission-critical applications. Clef descends from F#, whose two decades in the .NET ecosystem balanced computational efficiency with a supportive developer experience, and our framework carries that lineage into the next wave of AI work. As test-time inference drives more of AI advancement, we see our approach to safe and trustworthy compute moving next-gen intelligent systems toward commercial reality.

## Looking Forward: Neuromorphic Oracle Architecture

Building on Clef's type safety and our Fidelity Framework's compilation architecture, our research is exploring how our Composer compiler's Program Semantic Graph (PSG) can evolve into a **knowledge-aware compilation substrate** that bridges symbolic reasoning with neuromorphic hardware execution. We have found no other representative implementations of this substrate in the standing literature we have reviewed.

The PSG already represents computational relationships as a graph structure, and our current work suggests this same graph can encode semantic knowledge, proof obligations, and even target neuromorphic spike patterns. Pairing the PSG with emerging spiking neural chips from companies like Infineon opens an architectural possibility we are pursuing: **AI systems that consult structured knowledge graphs as oracle memory** during inference.

Where current approaches try to encode all knowledge in neural weights, our neuromorphic oracle architecture separates learned patterns (optimized for pattern recognition) from structured knowledge (optimized for logical reasoning). As we currently conceive it, when faced with questions requiring physical laws or mathematical constraints, the neuromorphic circuit would generate query patterns that traverse the knowledge hypergraph and perform deductive closure over verified relationships.

In this design, our Composer compiler's zipper-based traversal of the PSG would carry a second role beyond compilation: a **learning agent** that discovers paths through both computational and knowledge graphs. As the system compiled more code and processed more queries, it would learn which knowledge patterns are most valuable and how to structure the graph for efficient access.

This points toward a future where the compiler itself participates in the intelligence of the systems it builds, where proof obligations become optimization opportunities, and where the boundary between compilation and reasoning begins to blur. The neuromorphic substrate provides ultra-low-power execution (10-50mW vs 100-300W for GPUs), while the oracle architecture ensures that reasoning remains grounded in verifiable knowledge.

We're early in exploring these possibilities, and our initial investigations suggest that this convergence of Clef type safety, neuromorphic hardware, and knowledge hypergraphs could shift how intelligent systems are constructed. Rather than the current paradigm of "training everything from scratch," we envision systems that combine rapid pattern learning with structured knowledge consultation, achieving the efficiency and reliability that production AI applications demand.

This is where our Fidelity Framework's research is headed next: architectures for intelligence that connect symbolic and subsymbolic computation. We will set out the details in future publications, and we will keep building toward this design as the work on Clef, Composer, and the neuromorphic substrate continues.

## References

1. Microsoft Learn. "Units of Measure - F#." 2024. https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure

2. RCR Wireless. "The convergence of test-time inference scaling and edge AI." February 2025. https://www.rcrwireless.com/20250210/ai-infrastructure/convergence-of-test-time-inference-scaling-and-edge-ai

3. NVIDIA Developer Blog. "Physics-Informed Machine Learning Platform NVIDIA PhysicsNeMo Is Now Open Source." March 2023. https://developer.nvidia.com/blog/physics-ml-platform-modulus-is-now-open-source/

4. F# for Fun and Profit. "Units of measure." 2024. https://swlaschin.gitbook.io/fsharpforfunandprofit/understanding-f/understanding-fsharp-types/units-of-measure

5. DZone. "Understanding Inference Time Compute." January 2025. https://dzone.com/articles/understanding-inference-time-compute

6. VE3 Global. "Inference-Time Scaling: The Next Frontier in AI Performance." January 2025. https://www.ve3.global/inference-time-scaling-the-next-frontier-in-ai-performance/

7. Bengio, Y., et al. "Inductive biases for deep learning of higher-level cognition." Proceedings of the Royal Society A: Mathematical, Physical and Engineering Sciences, 2021. https://royalsocietypublishing.org/doi/10.1098/rspa.2021.0068
