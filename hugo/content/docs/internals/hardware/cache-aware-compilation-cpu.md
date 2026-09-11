---
title: "Cache-Conscious Memory Management: CPU Edition"
linkTitle: "Cache-Conscious Memory: CPU"
description: "From Memory-Aware to Cache-Aware: Architecting Performance Through Hierarchical Memory Control"
weight: 30
date: 2025-09-24
authors: ["Houston Haynes"]
tags: ["Architecture", "Performance"]
params:
  originally_published: 2025-09-24
  migration_date: 2026-02-15
---

Fidelity's cache-conscious design combines known layouts, ownership boundaries, and placement policy to reduce memory traffic. These are useful inputs to optimization, but they do not determine cache behavior by themselves. The objective is to make performance predictions explicit and testable.

The automatic cache transformations, arena placement policies, and adaptive profiler integration discussed here are design work. The September 9, 2026 review found native layout, mapped-storage, and hosted Ariel carrier machinery; it did not establish an implemented compiler pass for the full cache policy described in earlier versions of this article. Concrete support should be claimed only with a compiler path and an acceptance case.

## Developer Spectrum of Control

Application developers should normally express their computation without cache policy. Platform and library authors can supply layout constraints and target facts where those affect a meaningful decision. The compiler must distinguish required semantics from optional optimization hints, and diagnose requirements a target cannot honor.

Earlier examples using `CacheLineAligned`, `L1Resident`, and cache-specific arena APIs were proposed interfaces, not buildable evidence of support. This article describes the obligations without prescribing unimplemented syntax.

## Operating System Allocation Challenges

Virtual-memory allocation and cache placement operate at different granularities. A page allocation does not reserve cache capacity, and a small object can share a cache line with an adjacent allocation. Application allocation policy, runtime placement, compiler transformations, and the hardware all influence locality.

Alignment and padding are established tools in systems languages. C++'s `hardware_destructive_interference_size` describes recommended separation, not a runtime query that discovers every target's cache geometry. Fidelity's opportunity is to carry such requirements consistently from declarations to allocation and code generation, then check the result. See the [C++ interference-size definition](https://eel.is/c++draft/hardware.interference).

## BAREWire's Deterministic Foundation

A declared binary layout supplies field offsets, sizes, and alignment requirements. It does not, alone, supply an allocation's base address or the cache's indexing function. For an ordinary byte-addressed example with line size `L`, a byte at field offset `o` from base `b` lies in line `floor((b + o) / L)`. Accesses spanning a boundary touch additional lines.

That arithmetic supports conditional footprint analysis. Predicting misses additionally requires access order, reuse distance, associativity, replacement behavior, prefetching, and competing traffic. Physical indexing can also depend on address information unavailable at compilation.

## Prospero's Hierarchical Memory Architecture

The proposed division of work is: compiler analysis describes access patterns, allocation policy honors layout and separation requirements, and Prospero uses declared topology and observed load to guide placement. None of these layers can promise normal RAM will remain in a particular cache tier.

Actor ownership and cache isolation are distinct. Two disjoint, exclusively owned allocations can occupy different bytes of the same line. Writes by separate cores can then cause false sharing without any shared language-level object or data race. Avoiding that case requires aligned allocation bases, sufficient padding or rounded extents, and separation at the relevant hardware granularity. Shared allocator metadata and queues need their own treatment. See [Intel's optimization manual](https://www.intel.com/content/www/us/en/developer/articles/technical/intel64-and-ia32-architectures-optimization.html).

Ownership can make this analysis easier by identifying who may write. It is not proof of cache-line separation, nor does message ownership automatically describe how a transport publishes data or reclaims its buffers.

### Compile-Time Cache Behavior Analysis

A useful compiler report separates bytes used from lines fetched. Suppose 1,000 records have a 24-byte stride, and each iteration reads only their first 12 bytes. The useful data totals 12,000 bytes, but the accesses span almost the entire 24,000-byte array. With an aligned base and 64-byte lines, every one of its 375 lines is touched. Calling this a 12 KB cache footprint would undercount it by half.

A structure-of-arrays transformation might reduce that traffic when only selected fields are needed. It must preserve observable layout and aliasing requirements; a foreign ABI or wire format can prohibit changing the original representation. A separate compute layout may introduce conversion costs that the cost model must include.

### Arena Configuration for Cache Optimization

Alignment, size rounding, hot/cold separation, and bounded working sets are useful allocation policies. A working set smaller than L1 is a capacity estimate, not an L1-residency guarantee. Conflict misses, other code and data, and sibling hardware threads can still evict it.

Page or arena coloring requires a validated cache-indexing model and sufficient control over address placement. Known field offsets alone do not provide either. On hosted systems, physical placement may require cooperation from the OS.

### Runtime Actor Placement and Affinity

Keeping communicating work within a shared cache domain can reduce transfer costs. Affinity can also increase contention or prevent useful load balancing. A placement policy should compare migration and cache-warming costs against queueing delay and available execution capacity.

The current hosted Ariel acceptance demonstrates native carrier execution. It does not establish an automatic topology-aware Prospero policy or guarantee cache-resident actor communication.

### Memory Ordering and Concurrent Access

Layout optimization does not supply synchronization. Shared queues, publication of initialized data, and reclamation require the language's atomic and lifetime contracts even when actor payloads are exclusively owned.

Volatile is not a substitute for those contracts. LLVM distinguishes volatile accesses from atomic synchronization; backend selection must preserve the source memory model. See [LLVM's atomic operations guide](https://llvm.org/docs/Atomics.html) and the [Clef atomic operations contract](/spec/draft/atomic-operations/).

## Processor-Specific Optimization

### Target Triple Architecture

A target triple identifies broad architecture and ABI information. Cache line size, hierarchy, sharing domains, and instruction features need a CPU model, platform description, or supported runtime discovery. Do not infer one universal cache line size from `aarch64` or an OS name.

### Instruction Selection for Cache Efficiency

Vectorization, prefetch instructions, and non-temporal accesses can improve particular workloads. Each needs legality checks and a cost model. A legal vector transformation can increase traffic or register pressure; an available instruction is not evidence that it improves the application.

## Large Pages and Streaming Architectures

### Explicit Page Management

Large pages may reduce translation overhead. They do not enlarge data caches or ensure locality, and their availability depends on deployment policy and allocation behavior. A platform requirement should state whether the optimization is optional and how allocation failure is handled.

### Pointer-Heavy L3 Operations

Pointer-heavy structures can limit prefetching and memory-level parallelism. Compact representations or different traversal orders may help, provided they preserve semantics. Naming such a structure an “L3 operation” does not assign it to L3.

## Advanced Cache Bypass Strategies

### Late Binding and Lazy Evaluation

Deferring work can avoid unused accesses, but can also postpone them into a latency-sensitive path or introduce state. Measure the resulting access trace and end-to-end latency.

### Copy-on-Write and Cache Coherency

Copy-on-write changes ownership and allocation behavior. Its page faults, copies, synchronization, and reclamation costs depend on the implementation; it is not a general cache-bypass mechanism.

### Prefetching Distance Calibration

Prefetch distance depends on latency, iteration cost, available concurrency, and cache pressure. It should be calibrated for a workload and target. Fetching unused lines can make performance worse.

## Integration with Zero-Copy Architecture

### Cache-Line Aligned Transfers

Zero-copy removes a particular copy from a data path. It does not remove cache misses, coherence traffic, synchronization, or ownership transitions. Cache-line alignment helps only when allocation extent and access patterns also satisfy the intended separation.

### NUMA-Aware Zero-Copy

A buffer can be shared without copying and still reside on a remote NUMA node. Placement, access direction, and the lifetime of the shared mapping belong in the cost model. Sometimes one deliberate copy gives better locality for subsequent reuse.

## Performance Verification and Adaptation

### Hardware Performance Counter Integration

Measure elapsed time and relevant hardware events against a reproducible baseline. Record the CPU, topology, toolchain, affinity, input, and counters available on that machine. Compare instructions and access patterns as well as headline throughput.

HITM observations can help locate coherence contention, including true sharing. They are not uniquely a false-sharing count, and zero sampled HITM does not prove no false sharing occurred. Interpret samples with addresses and the actual allocation layout. See the [Linux perf c2c manual](https://man7.org/linux/man-pages/man1/perf-c2c.1.html).

### Adaptive Strategy Selection

A feedback-driven optimizer is a proposed extension. It needs stable measurements, a bounded policy, and a way to detect regressions. Profiling can support or refute a performance prediction for the measured workload; it does not turn that prediction into a universal guarantee.

## Migration Path from .NET to Fidelity

### Source Compatibility Strategy

Clef's native contracts differ from hosted .NET assumptions. Portability must be checked against the supported language surface and target services rather than promised from similar syntax.

### Gradual Optimization Opportunities

Begin with a correct implementation and a representative workload. Change one relevant layout or placement decision, inspect the generated artifact, and measure the result.

### Library Ecosystem Considerations

Foreign layouts, serialization contracts, and callback lifetimes constrain transformations. Library metadata should carry those constraints so optimization cannot silently violate them.

### Actor Model Migration

Moving work into actors can clarify ownership and scheduling boundaries. It does not automatically supply physical cache isolation or a profitable placement policy.

### Realistic Expectations

Cache-conscious compilation can reduce avoidable traffic where the compiler has enough information. Dynamic workloads and shared hardware retain uncertainty; reports should state their assumptions and distinguish estimated from measured results.

## Hierarchical Coordination

The same discipline extends to [GPU memory](/docs/internals/hardware/cache-aware-compilation-gpu/) and [freestanding scheduling](/docs/internals/hardware/scheduling-on-metal/): declare what the substrate provides, preserve semantics through lowering, and validate the particular artifact on the intended target.
