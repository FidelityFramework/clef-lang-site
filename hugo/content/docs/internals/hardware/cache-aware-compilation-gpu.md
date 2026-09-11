---
title: "GPU Cache-Aware Compilation"
linkTitle: "GPU Cache-Aware Compilation"
description: "How Composer and Alex Will Extend Memory Optimization to Parallel Architectures"
weight: 40
date: 2025-09-24T00:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Architecture", "Performance", "GPU"]
params:
  originally_published: 2025-09-24
  migration_date: 2026-02-15
---

GPU optimization needs explicit execution, memory-space, and synchronization contracts. Fidelity's design carries those requirements through the compiler so a backend can choose an appropriate layout and work decomposition. Performance still depends on the workload and target.

As reviewed on September 9, 2026, Composer contains an AMD GPU backend path from portable MLIR through GPU lowering to a `.hsaco` code object. That implementation is narrower than the automatic coalescing, heterogeneous actor placement, multi-vendor lowering, and profiling feedback envisioned here. A generated code object also needs a host dispatch path and numerical device acceptance. HelloWayland's recorded CPU rendering and presentation are not evidence of a GPU compute kernel running.

## The GPU Memory Challenge

A GPU schedules many threads over execution units sharing registers, caches, and local memory resources. The exact hierarchy and capacities depend on the device. Dividing a cache's capacity by its maximum resident thread count does not describe a per-thread allocation or predict achievable locality.

Shared memory on CUDA targets, or LDS on AMD targets, is explicitly managed storage with a defined scope. It can reduce repeated global loads, at the cost of staging, synchronization, bank conflicts, and occupancy. Some architectures share physical capacity between local storage and cache; the target configuration matters.

### A Different Coherency Model

SIMT does not prevent two threads or warps from accessing the same address. GPU programs can race. NVIDIA warps contain 32 threads, and Volta and later devices support independent thread scheduling; algorithms must not rely on implicit lockstep for synchronization. AMD wave size and execution semantics depend on the target. See the [CUDA programming guide](https://docs.nvidia.com/cuda/archive/13.0.0/cuda-c-programming-guide/index.html#simt-architecture) and [LLVM's AMDGPU memory model](https://llvm.org/docs/AMDGPUMemoryModel.html).

CPU cache-coherence intuitions do not transfer unchanged to every GPU memory space. Describe the actual scope and visibility rules, then analyze transactions, contention, bank conflicts, and races. Neither separate actors nor separate logical elements guarantee physical memory isolation.

## The Coalescing Imperative

Neighboring lanes accessing neighboring elements can reduce the memory transactions needed by a warp or wave. Transaction size and count depend on instruction width, address alignment, active lanes, and architecture. “32 adjacent floats means one 128-byte transaction” is not a universal hardware rule. The [CUDA best practices guide](https://docs.nvidia.com/cuda/cuda-c-best-practices-guide/index.html#coalesced-access-to-global-memory) describes the target-dependent behavior.

An array-of-structures to structure-of-arrays transformation can help when lanes read one field across many records. It is not automatically beneficial, and a fixed external layout may forbid changing the original representation. A separate compute layout introduces conversion and storage costs.

Composer's proposed analysis should report the assumptions behind transaction estimates, preserve ABI and aliasing requirements, and compare the complete workload against its baseline.

## Shared Memory Programming Model

A tiled convolution is a useful candidate: cooperating threads can stage an input tile and its halo, then reuse it for neighboring outputs. A correct implementation must load the entire tile, handle boundary elements, synchronize participating threads, and avoid reading uninitialized storage. One load per thread is insufficient when the tile plus halo is larger than the block.

The optimization is worthwhile only if saved global traffic outweighs staging, barriers, and reduced occupancy. Shared memory is not a fixed multiplier such as “100 times faster” for the whole kernel.

### GPU Memory Ordering

Both CPU and GPU programs need a memory model. A fence orders specified operations at its scope; it does not make another thread wait, announce completion, or by itself make a racy publication protocol correct. A block barrier synchronizes participating threads within that block, not the whole device. Inter-block and host/device communication require the appropriate atomics, completion events, or other supported synchronization. See [CUDA memory fences](https://docs.nvidia.com/cuda/archive/13.0.0/cuda-c-programming-guide/index.html#memory-fence-functions) and the [AMDGPU synchronization model](https://llvm.org/docs/AMDGPUMemoryModel.html).

The compiler must preserve the declared communication contract. Known field offsets alone cannot determine all happens-before relationships. General automatic fence insertion and portable race rejection are design objectives here, not established features of the current backend.

## CPU-GPU Cooperation

A host dispatch contract includes allocation, address-space validity, transfer or mapping, launch arguments, completion, and reclamation. A successful launch does not mean the result is ready for CPU access.

Unified addressing, managed migration, and physical cache coherence are different capabilities. Their availability and guarantees vary by device and deployment. A shared address does not imply a shared physical allocation, free migration, or automatic synchronization. See [CUDA unified memory](https://docs.nvidia.com/cuda/archive/13.0.0/cuda-c-programming-guide/index.html#unified-memory-programming).

### The Actor Model Advantage

Actors offer a useful ownership boundary for heterogeneous work. A proposed device actor can receive a bounded job and return a completion while hiding the transport details behind a supported platform contract.

That design still needs the runtime to track in-flight device work, prevent premature reuse, and honor the memory-space and visibility rules. Exclusive logical ownership does not prove cache-line separation, transfer completion, or physical residency. A general CPU/GPU actor implementation is not established by hosted CPU carrier acceptance.

## The MLIR Abstraction Layer

Composer's current GPU pipeline returns an AMD code object. MLIR provides infrastructure for additional device backends, but their availability upstream does not mean Composer supports all of them. NVIDIA, SPIR-V, and Metal support must each be demonstrated through the project's own lowering, packaging, launch, and device checks.

Alex preserves supported semantic facts for backend consumption. Hardware knowledge must be represented by actual target data and implemented rules; calling it an extensible architecture does not imply a comprehensive model of every accelerator. See [MLIR's GPU dialect](https://mlir.llvm.org/docs/Dialects/GPU/) for the upstream representation and its scope.

## Warp-Level Thinking

Divergent control flow can reduce active-lane utilization. Grouping similar work or using subgroup collectives may improve it, provided the transformation preserves ordering and value semantics. Shuffle and reduction operations need valid participation masks and target-specific subgroup rules.

Parallel floating-point reduction also changes evaluation order. Its numerical contract must permit that transformation; a faster tree reduction is not automatically equivalent to a sequential fold.

## The Streaming Pipeline

Overlapping transfer and computation is a proposed optimization. It requires independent work, supported queues or streams, suitable memory, and explicit dependencies. Buffer reuse must wait for completion. Measure end-to-end throughput and latency rather than assuming concurrency produces full utilization.

## Tensor Cores and Specialized Units

Specialized matrix operations require supported shapes, layouts, precisions, and accumulation semantics. Pattern recognition could select them when legal and profitable. Conversion, padding, numerical error, and launch costs belong in the decision. Their usefulness is a workload question, not a prediction about which model architecture will prevail.

## Memory Access as First-Class Concerns

Sequential, strided, random, and broadcast accesses suggest different transformations. The proposed compiler analysis should retain those distinctions alongside bounds, aliasing, address spaces, and effects. Flattening a tree or changing a list into an array needs a semantics-preserving transformation and a cost model.

## The Unified Memory Vision

Coherent systems can remove some explicit transfers. They still have locality, bandwidth, synchronization, and cache contention costs. Dataflow and processing-in-memory architectures can reduce selected movements, but data movement and its energy cost do not simply vanish. [Memory Fabrics](/docs/internals/memory-fabrics/) explores those target-dependent tradeoffs.

## Verification and Profiling

Correct results and performance are separate acceptance gates. First compare device output with an appropriate reference across boundary shapes and supported numeric cases. Then investigate traffic, occupancy, stalls, and elapsed time.

### NVIDIA Nsight Compute

Nsight Compute supplies target-specific counters and kernel analysis. Use metrics supported by the actual GPU and profiler version; a low transaction count does not alone prove the kernel reaches peak bandwidth. See the [Nsight Compute profiling guide](https://docs.nvidia.com/nsight-compute/ProfilingGuide/index.html).

### AMD ROCm Profiler

ROCm's profiling tools provide the corresponding AMD investigation path. Counter availability and interpretation vary by GPU. Record the device, driver, profiler version, inputs, and dispatch settings with the result. See the [ROCprofiler-SDK documentation](https://rocm.docs.amd.com/projects/rocprofiler-sdk/en/latest/).

### The Verification Cycle

Keep the source and compiler revisions, generated code object, launch configuration, correctness checks, and measurements together. Compare an optimization with an unchanged baseline under the same conditions. Automatic editor feedback and profile-driven strategy selection remain proposed integrations.

## Performance in Perspective

GPU acceleration must repay launch, transfer, synchronization, and any layout-conversion costs. Small or irregular workloads may favor the CPU. A useful cost model measures the complete path and states which parameters it assumes.

## Beyond Current Constraints

The same access and dependency analysis can inform other accelerator backends. Each still requires a real target contract, implemented lowering, and device evidence. Generality of the representation does not guarantee portability of every algorithm or optimization.

## Pragmatic Bridgework

The next credible increment is a bounded kernel with a reproducible host launch and checked device output. Coalescing, staging, and overlap can then be added as measured improvements. [Bring-Up Beyond the CPU](/docs/internals/hardware/bring-up-beyond-the-cpu/) keeps that distinction between compiler artifacts and device execution explicit.
