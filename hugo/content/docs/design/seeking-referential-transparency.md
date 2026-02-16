---
title: "Seeking Referential Transparency"
linkTitle: "Referential Transparency"
description: "Balancing Interaction Nets and Delimited Continuations in the Composer PHG"
date: 2025-08-05
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
params:
  originally_published: 2025-08-05
  original_url: "https://speakez.tech/blog/seeking-referential-transparency/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

In the landscape of modern compiler design, a fundamental tension exists between preserving the elegance of high-level abstractions and generating efficient machine code. The Fidelity framework confronts this challenge head-on. By leveraging a powerful insight, referential transparency, the Composer compiler provides a natural decision point for compilation strategies.

At the heart of our Clef native compiler lies the Program Hypergraph (PHG), which analyzes code to identify any referentially transparent regions. This analysis drives a sophisticated compilation strategy that chooses between two powerful computational models: interaction nets for pure, concurrent computations and delimited continuations for effectful, sequential operations. This isn't merely an optimization technique - it's a fundamental rethinking of how functional programs should be compiled for modern heterogeneous hardware.

By automatically selecting the appropriate compilation strategy based on mathematical properties rather than heuristics, Composer achieves something remarkable: it preserves the high-level intent of Clef code while generating executables that rival hand-optimized implementations. This document explores how these two paradigms work together to create a compilation framework that respects both the functional programmer's intent and the realities of modern hardware architectures.

## Core Architecture

### The Alex Component: Purity Analysis

Alex serves as the semantic analyzer that identifies referentially transparent code:

```fsharp
// Alex identifies this as pure - suitable for interaction nets
let pureComputation data =
    data
    |> Array.map (fun x -> x * 2.0)
    |> Array.filter (fun x -> x > threshold)
    |> Array.reduce (+)

// Alex identifies effects - requires delimited continuations
let effectfulComputation data = async {
    let! result = externalService.process data
    return result
}
```

### Program Analysis Pipeline

The compilation strategy emerges from three key analyses:

1. **Program Hypergraph (PHG)** - Captures high-level program structure
2. **Control Flow Graph (CFG)** - Hypernodes in the PHG that identify control dependencies
3. **Data Flow Graph (DFG)** - Hypernodes in the PHG that tracks data dependencies

These analyses inform whether code should target:
- **Inet dialect** (interaction nets) for pure parallelism
- **DCont/Async dialects** for continuation-based execution

## Interaction Nets as Primary Representation

When Alex identifies pure code, interaction nets become the top-level MLIR representation:

```mlir
// Pure Clef function compiles to Inet dialect
func @pureMapReduce(%data: !inet.wire<tensor<f32>>) -> !inet.wire<f32> {
  // Duplicate for parallel processing
  %dup:2 = inet.duplicate %data

  // Apply transformations in parallel
  %mapped = inet.cap %dup#0, @mapper
  %filtered = inet.cap %dup#1, @filter

  // Merge results
  %result = inet.construct %mapped, %filtered
  return %result
}
```

### Why Interaction Nets Excel for Pure Code

1. **Natural Parallelism** - Reductions happen simultaneously wherever patterns match
2. **No Synchronization Overhead** - Pure functions need no coordination
3. **Optimal for GPUs** - Maps directly to SIMD/SIMT execution models

## Post-Transformer Revolution: When Inet Becomes Essential

While interaction nets might seem "rare" for traditional ML workloads dominated by matrix multiplication, post-transformer architectures fundamentally change this equation. MatMul-free and sub-quadratic models are *perfectly* suited for interaction net compilation.

### MatMul-Free Networks as Interaction Patterns

Post-transformer architectures replace matrix multiplication with simple arithmetic operations that map directly to interaction rules:

```fsharp
// Ternary operations from MatMul-free networks
let ternaryOperation (input: Vector<float>) (weights: TernaryMatrix) =
    // Only additions and subtractions - perfect for Inet!
    for i in 0..outputDim-1 do
        for j in 0..inputDim-1 do
            match weights.[i,j] with
            | 1y -> result.[i] <- result.[i] + input.[j]   // Simple addition
            | -1y -> result.[i] <- result.[i] - input.[j]  // Simple subtraction
            | 0y -> ()  // No operation

// Maps directly to interaction net rules
inet.rule @ternary_add : (!inet.wire<f32>, !inet.wire<f32>) -> !inet.wire<f32>
inet.rule @ternary_sub : (!inet.wire<f32>, !inet.wire<f32>) -> !inet.wire<f32>
```

### BitNet and Quantized Models

BitNet's 1.58-bit weights create ideal conditions for interaction nets:

```fsharp
// BitNet layer - 2 bits per weight, simple operations
[<CompileToSPIRV>]
let bitnetLayer (input: Tensor) (weights: PackedBitArray) (scale: float32) =
    inet {
        // Massively parallel ternary operations
        let! outputs = inet.parallel_map (fun i ->
            let sum = applyTernaryOps input weights.[i]
            sum * scale
        ) [0..outputSize-1]

        return outputs
    }

// Compiles to highly efficient SPIR-V with no tensor cores needed!
```

### State Space Models (Mamba, RWKV)

Linear-complexity models with local state updates are natural interaction net candidates:

```fsharp
// Mamba's linear recurrence - perfect for Inet concurrency
let mambaStep (state: State) (input: float) (A: Diagonal) (B: Vector) =
    // Each dimension updates independently
    inet {
        let! updates = inet.parallel_map (fun i ->
            A.[i] * state.[i] + B.[i] * input
        ) [0..stateSize-1]

        return updates
    }
```

### Memory Efficiency Revolution

Post-transformer models via Inet achieve dramatic efficiency gains:

| Architecture | Traditional GPU | Inet + SPIR-V | Improvement |
|-------------|----------------|---------------|-------------|
| Memory per param | 16-32 bits | 1.58-2 bits | 10-20x reduction |
| Ops per token | Billions (MatMul) | Millions (Add/Sub) | 1000x fewer |
| Memory bandwidth | Bottlenecked | Register-only | ∞ improvement |
| Hardware required | Tensor cores | Any GPU | Democratized |

## Delimited Continuations for Effects

When code involves effects, delimited continuations preserve control flow:

```fsharp
// Clef async with effects
let processWithEffects data = async {
    let! validated = validate data  // External effect
    let transformed = pure data     // Pure computation
    let! stored = save transformed  // External effect
    return stored
}

// Compiles to DCont dialect
dcont.func @processWithEffects(%data: !fidelity.data) {
  %cont1 = dcont.shift @validate
  %transformed = call @pure(%cont1)
  %cont2 = dcont.shift @save(%transformed)
  dcont.reset %cont2
}
```

## Hybrid Compilation Strategy

The real power comes from combining both approaches:

```fsharp
// Mixed pure and effectful code
let hybridProcessing datasets = async {
    // Effectful: read from external source
    let! data = DataSource.readAsync()

    // Pure: massively parallel processing via Inet
    let processed =
        data
        |> Array.Parallel.map complexTransform
        |> Array.reduce combine

    // Effectful: save results
    do! Storage.saveAsync processed
}
```

This compiles to:

```mlir
func @hybridProcessing() {
  // DCont for async boundaries
  %data = dcont.shift @readAsync

  // Switch to Inet for pure computation
  %inet_data = dcont.to_inet %data
  %processed = call @pureProcessingViaInet(%inet_data)

  // Back to DCont for effects
  %result = inet.to_dcont %processed
  dcont.shift @saveAsync(%result)
}
```

## SPIR-V Integration for Post-Transformer Architectures

SPIR-V's capabilities align perfectly with interaction net compilation, especially for post-transformer models:

### Reference Type Preservation
- **Interaction nets** → Direct mapping to GPU work items
- **Ternary weights** → 2-bit packed representations in shared memory
- **Zero-copy semantics** → BAREWire unified memory access

### Efficient SPIR-V Generation for MatMul-Free Models

```fsharp
// BitNet layer compiles to efficient SPIR-V
[<CompileToSPIRV>]
let matMulFreeLayer (input: Tensor<float32>) (weights: TernaryTensor) =
    inet {
        let! parallelOps = inet.parallel_map (fun outputIdx ->
            // Each GPU thread: just adds/subtracts in registers!
            let mutable sum = 0.0f
            for inputIdx in 0..inputSize-1 do
                match weights.[outputIdx, inputIdx] with
                | Pos -> sum <- sum + input.[inputIdx]
                | Neg -> sum <- sum - input.[inputIdx]
                | Zero -> ()
            sum
        ) [0..outputSize-1]

        return Tensor.ofArray parallelOps
    }

// Generates SPIR-V that:
// - Uses no tensor cores (works on any GPU)
// - Keeps weights in shared memory (2 bits each!)
// - Accumulates in registers (no memory bandwidth issues)
// - Achieves near-theoretical ALU utilization
```

### Hybrid CPU-GPU Execution

Post-transformer architectures enable efficient hybrid execution:

```fsharp
// CPU handles ternary ops efficiently with AVX-512
// GPU handles parallel decompression of compressed KV cache
[<HybridExecution>]
let hybridInference (model: HybridBitNet) (input: TokenSequence) =
    inet {
        // CPU: Sequential ternary operations (cache-friendly)
        let! cpuResult = inet.cpu {
            return processTernaryLayers model.BitNetLayers input
        }

        // GPU: Parallel KV cache decompression via SPIR-V
        let! gpuResult = inet.gpu {
            return decompressAndApplyAttention model.CompressedKV cpuResult
        }

        // Zero-copy result sharing via BAREWire
        return combine cpuResult gpuResult
    }
```

## Practical Implications

### Memory Management

- **Pure computations** (Inet) - Can use zero-copy shared memory
- **Effectful computations** (DCont) - Align with continuation boundaries for RAII

### Concurrency Strategies

- **Data parallelism** - Inet reductions across GPU warps
- **Task concurrency** - DCont for coordinating async operations
- **Pipeline concurrency** - Hybrid approach for streaming

### Performance Characteristics

| Pattern | Compilation Strategy | Target Hardware | Performance |
|---------|---------------------|-----------------|-------------|
| Pure map/reduce | Inet dialect | GPU/SIMD | Near-optimal |
| Async I/O | DCont dialect | CPU | Deterministic memory |
| Mixed workload | Hybrid | Heterogeneous | Adaptive |
| **MatMul-free layers** | **Inet dialect** | **Any GPU** | **10-100x memory reduction** |
| **Ternary networks** | **Inet dialect** | **CPU SIMD** | **8x throughput vs MatMul** |
| **State space models** | **Inet dialect** | **GPU** | **Linear complexity** |

### Post-Transformer Specific Benefits

The Inet compilation path, initially considered "rare" for ML workloads, becomes dominant for post-transformer architectures:

1. **BitNet/Ternary Networks**
   - 2 bits per weight → entire layers fit in L2 cache
   - Simple add/subtract ops → no tensor cores needed
   - Parallel reductions → perfect for interaction nets

2. **Linear Attention/State Space Models**
   - O(n) complexity instead of O(n²)
   - Local state updates → ideal for concurrent execution
   - No attention matrices → massive memory savings

3. **Hybrid Architectures**
   - CPU for sequential ternary ops (cache-efficient)
   - GPU for parallel decompression (bandwidth-efficient)
   - Zero-copy via BAREWire (no transfer overhead)

## Design Principles

1. **Preserve Until Necessary** - Keep high-level abstractions as long as they provide value
2. **Let Purity Guide** - Referential transparency determines compilation strategy
3. **No Forced Model** - Choose interaction nets or continuations based on code semantics
4. **Hardware Awareness** - Target appropriate hardware based on computation patterns

## Future Directions

### Advanced Patterns

- **Distributed interaction nets** - Cross-process/machine reductions
- **Persistent continuations** - Checkpoint/restore for long-running computations
- **Adaptive recompilation** - Switch strategies based on runtime behavior

### Hardware Targets

- **Quantum backends** - Interaction nets for quantum circuit optimization
- **Neuromorphic chips** - Event-driven computation via continuations
- **Custom ASICs** - Domain-specific interaction patterns

## Conclusion

By recognizing that different computational patterns benefit from different compilation strategies, Fidelity achieves something unique: it preserves the elegance of functional programming while generating code competitive with hand-optimized implementations.

The purity analysis (via Alex) can automatically determine whether interaction nets or delimited continuations will provide better performance. This becomes especially powerful with post-transformer architectures, where simple arithmetic operations (additions, subtractions, bit manipulations) make interaction nets the *optimal* compilation target, not a rare edge case.

Post-transformer models fundamentally change the GPU programming landscape:

- **No tensor cores required** - Simple arithmetic ops work on any GPU
- **Minimal memory bandwidth** - Ternary weights fit in cache/registers
- **Massive parallelism** - Every operation can execute simultaneously
- **Democratized AI** - Efficient inference on consumer hardware

The convergence of functional programming principles, interaction net theory, and post-transformer architectures creates unprecedented opportunities. What began as an exploration of referential transparency as a compilation heuristic has revealed a deeper truth: the mathematical properties of our code should drive how we compile it, as opposed to being bound by the limitations of previous patterns.

This approach transforms Clef from a high-level language making performance compromises into a precision tool for modern computing. Developers write idiomatic functional code; the compiler automatically identifies optimal execution strategies. A BitNet model that would traditionally require specialized kernels and manual optimization can now compile directly from straightforward Clef expressions to efficient SPIR-V code that may outperform hand-tuned implementations.

The implications extend beyond performance metrics. By making post-transformer architectures accessible through familiar functional abstractions, Composer will enable a new generation of AI applications:

- **Edge AI devices** running full language models with megabytes, not gigabytes, of memory
- **Real-time inference** on consumer GPUs without specialized hardware
- **Energy-efficient deployment** reducing computational costs by orders of magnitude
- **Compositional AI systems** where functional guarantees enable safe, predictable model composition

As we move beyond the transformer era, the Fidelity framework stands ready. Its principled approach to compilation, grounded in mathematical properties and guided principled analysis, provides the foundation for whatever computational paradigms emerge next. The future of functional programming isn't about choosing between elegance and efficiency. With Composer and the Fidelity framework, we show that they can be intertwined with the right foundations.
