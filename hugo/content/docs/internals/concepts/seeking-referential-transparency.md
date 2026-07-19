---
title: "Seeking Referential Transparency"
linkTitle: "Referential Transparency"
description: "Balancing Interaction Nets and Delimited Continuations in the Composer PHG"
date: 2025-08-05
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation", "Concurrency"]
params:
  originally_published: 2025-08-05
  migration_date: 2026-02-15
---

Compiler design carries a standing tension between keeping high-level abstractions and generating efficient machine code. In our design, referential transparency is the decision point for the Composer compiler's compilation strategy: whether a region is pure is a mathematical property to branch on, rather than a heuristic.

Our Clef native compiler is organized around the Program Hypergraph (PHG), the representation Composer analyzes to identify referentially transparent regions. From that analysis, Composer chooses between two computational models: interaction nets for pure, concurrent computations, and delimited continuations for effectful, sequential operations.

Selecting the compilation strategy from the purity of a region, rather than from optimization heuristics, lets Composer preserve the high-level intent of Clef code while targeting the appropriate execution model. The two models are designed to work together across heterogeneous hardware.

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

The compilation strategy derives from three analyses:

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

### Why Interaction Nets Suit Pure Code

1. **Natural Parallelism** - Reductions happen simultaneously wherever patterns match
2. **No Synchronization Overhead** - Pure functions need no coordination
3. **Optimal for GPUs** - Maps directly to SIMD/SIMT execution models

## Interaction Nets and Post-Transformer Architectures

Interaction nets might seem a poor fit for traditional ML workloads dominated by matrix multiplication, but [MatMul-free](https://arxiv.org/abs/2406.02528) and sub-quadratic post-transformer models map well onto interaction net compilation.

### MatMul-Free Networks as Interaction Patterns

Post-transformer architectures replace matrix multiplication with simple arithmetic operations that map directly to interaction rules:

```fsharp
// Ternary operations from MatMul-free networks
let ternaryOperation (input: Vector<float>) (weights: TernaryMatrix) =
    // Only additions and subtractions, mapping to interaction rules
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

BitNet's 1.58-bit weights are ternary, so each layer reduces to additions and subtractions, the operations interaction rules express directly:

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

// Compiles to SPIR-V with no tensor cores required
 
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

### Memory Characteristics

Compiling post-transformer models through the Inet path changes the memory and arithmetic profile:

| Architecture | Traditional GPU | Inet + SPIR-V | Direction |
|-------------|----------------|---------------|-------------|
| Memory per param | 16-32 bits | 1.58-2 bits | Fewer bits per weight |
| Ops per token | MatMul-dominated | Add/Sub-dominated | Simpler arithmetic |
| Memory bandwidth | A bottleneck | Register/cache-resident | Less DRAM traffic |
| Hardware required | Tensor cores | Any GPU | No tensor cores |

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

Mixed workloads combine both strategies in a single function:

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

SPIR-V's capabilities align with interaction net compilation, especially for post-transformer models:

### Reference Type Preservation
- **Interaction nets** → Direct mapping to GPU work items
- **Ternary weights** → 2-bit packed representations in shared memory
- **Zero-copy semantics** → BAREWire unified memory access

### SPIR-V Generation for MatMul-Free Models

```fsharp
// BitNet layer compiles to efficient SPIR-V
[<CompileToSPIRV>]
let matMulFreeLayer (input: Tensor<float32>) (weights: TernaryTensor) =
    inet {
        let! parallelOps = inet.parallel_map (fun outputIdx ->
            // Each GPU thread: adds/subtracts in registers
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
// - Keeps weights in shared memory (2 bits each)
// - Accumulates in registers (avoids DRAM bandwidth)
// - Targets high ALU utilization
 
```

### Hybrid CPU-GPU Execution

In post-transformer architectures, the work splits between CPU and GPU:

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

| Pattern | Compilation Strategy | Target Hardware | Characteristic |
|---------|---------------------|-----------------|-------------|
| Pure map/reduce | Inet dialect | GPU/SIMD | Parallel reduction |
| Async I/O | DCont dialect | CPU | Deterministic memory |
| Mixed workload | Hybrid | Heterogeneous | Adaptive |
| **MatMul-free layers** | **Inet dialect** | **Any GPU** | **Reduced memory footprint** |
| **Ternary networks** | **Inet dialect** | **CPU SIMD** | **Add/sub in place of MatMul** |
| **State space models** | **Inet dialect** | **GPU** | **Linear complexity** |

### Post-Transformer Specific Benefits

The Inet compilation path becomes the dominant strategy for post-transformer architectures:

1. **BitNet/Ternary Networks**
   - 2 bits per weight → entire layers fit in L2 cache
   - Add/subtract ops → no tensor cores needed
   - Parallel reductions → concurrent interaction net rewrites

2. **Linear Attention/State Space Models**
   - O(n) complexity instead of O(n²)
   - Local state updates → independent, coordination-free updates
   - No attention matrices → massive memory savings

3. **Hybrid Architectures**
   - CPU for sequential ternary ops (cache-efficient)
   - GPU for parallel decompression (bandwidth-efficient)
   - Zero-copy via BAREWire (no transfer overhead)

## Design Principles

1. **Preserve Until Necessary** - Keep high-level abstractions as long as they provide value
2. **Purity as the Decision Point** - Referential transparency determines compilation strategy
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

## Property-Driven Compilation

Fidelity selects a compilation strategy per computational pattern, preserving functional abstractions while targeting code competitive with hand-optimized implementations.

The purity analysis (via Alex) can automatically determine whether interaction nets or delimited continuations will provide better performance. The effect is largest with post-transformer architectures, where simple arithmetic operations (additions, subtractions, bit manipulations) make interaction nets the primary compilation target.

Post-transformer models fundamentally change the GPU programming landscape:

- **No tensor cores required** - Addition and subtraction run on any GPU
- **Minimal memory bandwidth** - Ternary weights fit in cache/registers
- **Massive parallelism** - Every operation can execute simultaneously
- **Consumer hardware** - Efficient inference without specialized accelerators

What began as an exploration of referential transparency as a compilation heuristic became a design commitment: the mathematical properties of our code determine how we compile it.

Developers write idiomatic functional code, and the compiler selects the execution strategy from the purity of each region. A BitNet model that would traditionally require specialized kernels and manual optimization can compile directly from straightforward Clef expressions to SPIR-V code that may outperform hand-tuned implementations.

By making post-transformer architectures accessible through familiar functional abstractions, Composer will enable a new generation of AI applications:

- **Edge AI devices** running full language models with megabytes, not gigabytes, of memory
- **Real-time inference** on consumer GPUs without specialized hardware
- **Energy-efficient deployment** reducing computational costs by orders of magnitude
- **Compositional AI systems** where functional guarantees enable safe, predictable model composition

As post-transformer architectures mature, our approach to compilation, grounded in the mathematical properties of the program, is designed to extend to whatever computational paradigms emerge next.
