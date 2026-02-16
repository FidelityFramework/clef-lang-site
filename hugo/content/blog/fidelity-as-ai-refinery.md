---
title: "Fidelity Framework as AI Refinery"
linkTitle: "Fidelity as AI Refinery"
description: "Clef Type Safety Will Revolutionize Model Inference"
date: 2023-01-03
authors:
  - SpeakEZ
tags: ["AI", "Design"]
params:
  originally_published: 2023-01-03
  original_url: "https://speakez.tech/blog/fidelity-as-ai-refinery/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

The AI industry is experiencing a profound shift in how computational resources are allocated and optimized. While the last decade saw rapid advances through massive pre-training efforts on repurposed GPUs, we're now entering an era where **test-time compute (TTC)** and custom accelerators are emerging as the next frontier of AI advancement. As highlighted in recent industry developments, DeepSeek AI lab disrupted the market with a model that delivers high performance at a fraction of competitors' costs, signaling two significant shifts: smaller labs producing state-of-the-art models and test-time compute becoming the next driver of AI progress.

## From Training to Test-Time Compute

This evolution is driven by a fundamental constraint: the scarcity of additional training data. The major AI labs have already trained their models on much of the available public data (including many copyrighted works to which they had no legal claim to use), making further pre-training improvements increasingly limited. Instead, the focus is shifting to reasoning capabilities at inference time, where models "think" before responding to a question rather than simply generating the statistically most likely response.

## Python is a **Dead End**, Not a Destination

The current dominant approach to AI model development, using dynamically typed frameworks like PyTorch, presents structural limitations to this pivot that become increasingly apparent as the AI landscape moves to a test-time compute paradigm. Python's inherent limitations prevent "baked in" awareness of units of measure and other constraints that would naturally govern many classes of model behavior to a grounded result.

This absence of strong type safety leads to overtraining as developers attempt to compensate through brute force, teaching models to reflect an image of disciplined behavior rather than embedding principles at the foundation. The result is inefficient, computationally expensive inference that lacks "zero-cost" safeguards of a more structured approach that SpeakEZ offers with the Fidelity Framework.

The core issue is that today's models are missing the structured understanding of the world and its physical constraints that humans develop naturally. Current AI approaches struggle to incorporate these constraints, resulting in systems that require enormous training datasets yet still fail to reliably perform tasks that humans learn quickly.

As Yann LeCun, Chief AI Scientist at Meta, noted: "A 17-year-old can learn to drive a car in about 20 hours of practice... And we have millions of hours of training data of people driving cars, but we still don't have self-driving cars. So that means we're missing something really, really big."

## A New Paradigm for Reliable AI

SpeakEZ's Fidelity Framework represents a groundbreaking approach that addresses these fundamental limitations. The framework would include:

1. **Composer compiler**: Would enable efficient native compilation through a direct Clef to MLIR/LLVM path
2. **Farscape CLI**: Would provide seamless integration with verified C/C++ libraries
3. **BAREWire interface**: Would deliver high-performance, low-burden binary communication
4. **Furnace auto-differentiation**: Would support reliable machine learning operations

This constellation of technologies would create a principled foundation for high-trust AI deployment that combines correctness with high performance. And for those organizations interested in advanced certification, Fidelity would also include high-assurance capabilities through a bridge to the F*(F-star) verification framework that will be detailed in future technical publications.

## Type Safety for Neural Representations

A cornerstone of SpeakEZ's approach is Clef's sophisticated type system, which provides compile-time safety without runtime overhead. This represents a fundamental shift from the current paradigm where models must learn constraints through extensive training rather than having these constraints built into their foundation. And it follows that even in model training the burden of runtime marshaling of tensor shapes conveys an outsized time and computational cost for current model building practices.

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
    // Implementation of convolution preserving physical unit meaning
    // Type system ensures operations maintain correct units
    // Returns image with same physical interpretation
```

In both examples, the Clef type system ensures that operations maintain dimensional and physical consistency at compile time. If a developer attempts to use incompatible dimensions or units, the Clef tooling will notify the developer with a compilation error at design time. This provides a constructive constraint that supports correctness and safety that no other tooling stack can match.

This collaborative approach between the tooling and developer would eliminate an entire class of errors that plague deep learning systems and currently require extensive runtime checks, debugging, and trial-and-error. From this perspective, the efficiency gains of SpeakEZ's Fidelity Framework would also impact design-time. Many machine learning working groups currently plan for large staff allocations over long calendar windows into the assumptions of their work. The efficiency gains that SpeakEZ envisions over Python-based frameworks are not just a "flex". They would be a material advance in transforming the half-life of model engineering cycles and the outsized fixed costs that come with it.

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

This foundation would create a path toward rapidly delivering neural networks that can reason with built-in dimensional awareness and structural constraints, reducing the need to rediscover these patterns from training data alone. The benefits would extend beyond just catching errors. By embedding structural knowledge directly into the type system, models built with the Fidelity Framework could potentially learn more efficiently from less data, as they wouldn't need to rediscover basic constraints that are already encoded in their foundation.

This aligns with research into inductive biases in machine learning, where as noted in a Royal Society paper on higher-level cognition: "Our current state-of-the-art machine learning systems sometimes achieve good performance on a specific and narrow task, using very large quantities of labelled data" **while systems with appropriate inductive biases can generalize more efficiently with less training data**. These cumulative advances: reduced error rate, simplified model processing, reduced data needs all converge on challenging the current assumptions on the outsized time and compute costs of building models.

And in that process companies would get higher performance, greater reliability, and more options for deploying high-leverage models in a wider array of devices that meet the customer and their needs "where they are".

## Multi-Head Latent Attention

The Fidelity Framework's envisioned use of [the Furnace library](https://github.com/fsprojects/Furnace) would demonstrate how to adapt cutting-edge techniques like DeepSeek's Multi-Head Latent Attention (MLA) while tuning models for performance. MLA achieves remarkable efficiency improvements, up to a 93.3% reduction in KV cache size while simultaneously improving model throughput. [see the Appendix for an extended example]

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

These benefits would be delivered while maintaining memory safety and computational accuracy guarantees throughout the compilation process, something that would be difficult or impossible with conventional Python approaches.

## Optimizing Real World Inference

SpeakEZ's vision of an "AI Refinery" represents a fundamental rethinking of how AI models should be constructed for optimal real-world performance. Like an oil refinery that transforms crude petroleum into valuable end products, the AI Refinery would transform raw model capabilities into verified, efficient, and safe computational systems.

By focusing on inference optimization through compile-time verification and Clef's inherent type safety, SpeakEZ aims to address the emerging challenges in test-time compute:

1. **Memory Efficiency**: Clef's units of measure and the framework's optimized memory management would significantly reduce the memory footprint of models during inference, enabling larger context windows and more complex reasoning on existing hardware.

2. **Computational Performance**: Direct compilation to MLIR/LLVM would create highly optimized execution paths that deliver superior inference speed compared to interpreted approaches.

3. **Verification Guarantees**: Unlike black-box models whose behaviors can only be validated through extensive testing, models built with the Fidelity Framework would carry type guarantees and even algorithmic proofs of their properties and constraints.

4. **Hardware Adaptability**: The framework's universal adaptation interface would enable targeting of diverse hardware architectures from a single verified codebase, maximizing performance across the computing ecosystem.

## Physics-Based Models for AI You Can Bank On

The future of AI lies in models that incorporate real-world understanding and constraints at their foundation rather than attempting to approximate these principles through brute-force training. Current AI models, despite their impressive capabilities in language and image generation, still fundamentally lack the ability to model and predict mathematical, financial and physical reality with the well-founded understanding that humans possess.

SpeakEZ's Fidelity Framework embraces a vision for building models with intrinsic understanding of the problem space's natural constraints through Clef's type system and formal verification. This approach would aim to create systems that can reason about the physical world with the same natural understanding that humans develop, rather than approximating this understanding through massive datasets that continue to leave many models coming up short.

## Redefining AI Delivery for the Age of Inference

As the AI landscape evolves from massive pre-training to the age of test-time compute, SpeakEZ's Fidelity Framework would offer a trusted solution that addresses the fundamental limitations of current approaches. By leveraging Clef's powerful type system and direct compilation pathways, SpeakEZ is envisioning an "AI Refinery" that would transform how models are built, verified, and deployed.

The result would be a new generation of AI systems that combine advanced reasoning capabilities with formal safety and efficiency guarantees, delivering trusted, high-performance computation for mission-critical applications. Building on the F# ecosystem's 20-year foundation of balancing computational efficiency with a supportive developer experience, SpeakEZ's Fidelity Framework would extend this paradigm into the next wave of AI innovation. As test-time inference increasingly drives AI advancement, SpeakEZ's approach to safe and trustworthy compute could transform the dream of next-gen intelligent systems into commercial reality.

## Looking Forward: Neuromorphic Oracle Architecture

Building on the foundation of Clef's type safety and the Fidelity Framework's compilation architecture, our research is exploring how the Composer compiler's Program Semantic Graph (PSG) can evolve into something unprecedented: a **knowledge-aware compilation substrate** that bridges symbolic reasoning with neuromorphic hardware execution.

The PSG already represents computational relationships as a rich graph structure, but we're discovering that this same graph can encode semantic knowledge, proof obligations, and even target neuromorphic spike patterns. When combined with emerging spiking neural chips from companies like Infineon, this creates a fascinating architectural possibility: **AI systems that consult structured knowledge graphs as oracle memory** during inference.

Unlike current approaches that try to encode all knowledge in neural weights, this neuromorphic oracle architecture separates learned patterns (optimized for pattern recognition) from structured knowledge (optimized for logical reasoning). When faced with questions requiring physical laws or mathematical constraints, the neuromorphic circuit generates query patterns that traverse the knowledge hypergraph, performing deductive closure over verified relationships.

The Composer compiler's zipper-based traversal of the PSG becomes more than just a compilation technique; it becomes a **learning agent** that discovers optimal paths through both computational and knowledge graphs. As the system compiles more code and processes more queries, it learns which knowledge patterns are most valuable and how to structure the graph for efficient access.

This points toward a future where the compiler itself participates in the intelligence of the systems it builds, where proof obligations become optimization opportunities, and where the boundary between compilation and reasoning begins to blur. The neuromorphic substrate provides ultra-low-power execution (10-50mW vs 100-300W for GPUs), while the oracle architecture ensures that reasoning remains grounded in verifiable knowledge.

We're just beginning to explore these possibilities, but early investigations suggest that this convergence of Clef type safety, neuromorphic hardware, and knowledge hypergraphs could represent a fundamental shift in how intelligent systems are constructed. Rather than the current paradigm of "training everything from scratch," we envision systems that combine rapid pattern learning with structured knowledge consultation, achieving the efficiency and reliability that production AI applications demand.

This represents the next frontier for the Fidelity Framework: not just refining conventional AI approaches, but pioneering entirely new architectures for intelligence that bridge the gap between symbolic and subsymbolic computation. The details of this research will be the subject of future publications as we continue to push the boundaries of what's possible when type safety meets neuromorphic innovation.

## References

1. Microsoft Learn. "Units of Measure - F#." 2024. https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure

2. RCR Wireless. "The convergence of test-time inference scaling and edge AI." February 2025. https://www.rcrwireless.com/20250210/ai-infrastructure/convergence-of-test-time-inference-scaling-and-edge-ai

3. NVIDIA Developer Blog. "Physics-Informed Machine Learning Platform NVIDIA PhysicsNeMo Is Now Open Source." March 2023. https://developer.nvidia.com/blog/physics-ml-platform-modulus-is-now-open-source/

4. F# for Fun and Profit. "Units of measure." 2024. https://swlaschin.gitbook.io/fsharpforfunandprofit/understanding-f/understanding-fsharp-types/units-of-measure

5. DZone. "Understanding Inference Time Compute." January 2025. https://dzone.com/articles/understanding-inference-time-compute

6. VE3 Global. "Inference-Time Scaling: The Next Frontier in AI Performance." January 2025. https://www.ve3.global/inference-time-scaling-the-next-frontier-in-ai-performance/

7. Bengio, Y., et al. "Inductive biases for deep learning of higher-level cognition." Proceedings of the Royal Society A: Mathematical, Physical and Engineering Sciences, 2021. https://royalsocietypublishing.org/doi/10.1098/rspa.2021.0068
