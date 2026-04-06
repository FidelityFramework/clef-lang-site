---
title: "Cache-Conscious Memory Management: CPU Edition"
linkTitle: "Cache-Conscious Memory: CPU"
description: "From Memory-Aware to Cache-Aware: Architecting Performance Through Hierarchical Memory Control"
date: 2025-09-24
authors: ["Houston Haynes"]
tags: ["Architecture", "Performance"]
params:
  originally_published: 2025-09-24
  original_url: "https://speakez.tech/blog/cache-aware-compilation-cpu/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

Modern computing systems present a fundamental paradox: while processor speeds have increased exponentially, memory latency improvements have been modest, creating an ever-widening performance gap. This disparity manifests most acutely in the cache hierarchy, where the difference between an L1 cache hit (approximately 4 cycles) and main memory access (200+ cycles) represents a fifty-fold performance penalty. For systems pursuing native performance without runtime overhead, understanding and exploiting cache behavior becomes not merely an optimization, but an architectural imperative.

The Fidelity framework's Prospero orchestration layer is designed to address this challenge through what we term "cache-conscious memory management," a systematic approach that extends beyond traditional memory awareness to encompass the full memory hierarchy from L1 cache through main memory. This design philosophy recognizes that effective cache utilization cannot be achieved through OS-level allocation alone, but requires coordinated effort across compilation, memory management, and actor placement strategies.

## Developer Spectrum of Control

Before delving into the technical depths of cache management, it's important to acknowledge that the Fidelity framework embraces a philosophy of progressive disclosure in developer experience. As explored in our previous article on [Memory Management by Choice](https://speakez.tech/blog/memory-management-by-choice/), we believe developers should have access to low-level control when needed without being burdened by complexity when it's not required.

The detailed memory layout annotations and cache-specific optimizations presented in this document represent the full spectrum of control available to developers who need maximum performance. However, these represent the exception rather than the rule. Most Clef code written for Fidelity will use standard Clef syntax, with the Composer compiler inferring appropriate memory layouts and cache strategies. Library authors may leverage explicit annotations to provide optimized implementations that application developers consume through clean, idiomatic APIs. This layered approach ensures that complexity appears only where performance demands it, maintaining Clef's elegance for the vast majority of application code.

## Operating System Allocation Challenges

Traditional approaches to memory management rely heavily on operating system allocation primitives, trusting that malloc, mmap, and their variants will provide adequate performance through general-purpose heuristics. While operating systems have evolved sophisticated memory management capabilities, including NUMA awareness and huge page support, these mechanisms operate at too coarse a granularity to effectively manage cache behavior.

Consider the fundamental mismatch: operating systems manage memory at page granularity (typically 4KB or 2MB for huge pages), while cache lines operate at 64-byte boundaries. An L1 cache might total 32KB per core, fitting merely eight standard pages or a fraction of a single huge page. The OS cannot know whether a particular allocation will be accessed frequently enough to warrant L1 residence, occasionally enough for L2, or so rarely that it should bypass cache entirely. These decisions require semantic understanding of application behavior that only the compiler and runtime orchestration can provide.

Furthermore, cache associativity introduces another layer of complexity invisible to standard allocation. Modern processors employ set-associative caches where memory addresses map to specific cache sets. Two frequently accessed data structures that happen to map to the same cache set will evict each other repeatedly, a phenomenon known as conflict misses, despite ample cache capacity elsewhere. The operating system's allocator, unaware of these hardware-specific mappings, cannot prevent such pathological behaviors.

This challenge has long troubled C and C++ developers. The language standards offer no direct support for cache-aware allocation; developers must manually calculate alignments, insert padding bytes, and hope that compiler optimizations don't undo their careful arrangements. The C++17 introduction of `std::hardware_destructive_interference_size` acknowledges this gap, providing a portable way to query cache line size, but places the burden of using this information correctly on the developer. Our designs lead us toward a different approach: the compiler should reason about cache behavior based on semantic information, not leave developers to manually insert padding and pray.

## The Deterministic Foundation: BAREWire's Role

Before examining how Prospero achieves cache consciousness, we must understand the foundation that makes such analysis possible. Traditional systems face a fundamental obstacle to cache optimization: memory layout uncertainty. A managed runtime might reorder object fields for packing efficiency, insert metadata headers of varying sizes, or relocate objects during garbage collection. Even systems languages like C++ offer limited guarantees about where allocators place objects in memory. This uncertainty renders compile-time cache analysis effectively impossible - one cannot predict cache line usage without knowing where data resides.

BAREWire eliminates this uncertainty through deterministic, compile-time memory layouts. Every field offset, structure size, and alignment requirement becomes statically known and guaranteed:

```fsharp
[<BAREStruct>]
type OrderBook = {
    [<BAREField(0, Offset=0)>] BidCount: uint32      // Bytes 0-3, always
    [<BAREField(1, Offset=4)>] AskCount: uint32      // Bytes 4-7, guaranteed
    [<BAREField(2, Offset=8)>] Bids: BAREArray<Order> // Starting byte 8, predictable
    [<BAREField(3, Offset=256)>] Asks: BAREArray<Order> // Cache line aligned
}
```

This determinism transforms cache analysis from speculation to calculation. When the Composer compiler encounters an actor processing OrderBook messages, it will know precisely which cache lines each field access will touch. The semantic graph we're developing captures not merely data types but exact access patterns over deterministic layouts, enabling the cache-aware optimizations that follow.

## Prospero's Hierarchical Memory Architecture

Building on BAREWire's deterministic foundation, our vision for Prospero transcends simple memory awareness to embrace cache consciousness as a first-class architectural concern. This design recognizes three fundamental principles that guide our implementation approach:

First, actors exhibit predictable memory access patterns that remain stable across message processing cycles. A high-frequency trading actor consistently accesses price data and order books, while a logging actor sequentially writes to buffers. These patterns, discoverable through static analysis, inform optimal cache tier placement.

Second, cache locality benefits compound when related actors share cache domains. Two actors engaged in producer-consumer communication benefit enormously from L3 cache sharing, as messages pass between them without traversing main memory. This observation drives our actor placement strategies.

Third, not all memory accesses benefit from caching. Streaming data that will be processed once and discarded pollutes cache with data that will never be reused, evicting potentially valuable cached content. Recognizing and segregating these access patterns prevents cache thrashing.

Fourth, and perhaps most consequentially: per-actor arenas eliminate false sharing by construction, not by discipline. When each actor's mutable state resides in its own arena, no other actor can address that memory. The problem of independent data sharing cache lines simply cannot arise across actor boundaries.

This structural guarantee emerges from Fidelity's capability-based ownership model. Each actor holds exclusive capability to its arena. Messages transfer ownership through typed channels; the sender relinquishes access when sending, the receiver gains exclusive access upon receipt. This capability model supersedes the Rust borrow checker's approach to memory safety. Where Rust tracks lifetimes through lexical scope and reference annotations, Fidelity tracks ownership through actor boundaries and message passing. The result is more powerful: Rust's borrow checker cannot reason about cache behavior or false sharing, while Fidelity's capability model makes cache isolation a structural consequence of the ownership rules.

Consider the implications for concurrent programming. In Rust, avoiding false sharing requires manual padding with `#[repr(align(64))]` or wrapper types like `CachePadded<T>`. Developers must remember to apply these annotations; the compiler cannot detect their absence. In Fidelity, actors that don't share memory cannot share cache lines. The type system enforces what Rust leaves to programmer discipline.

The contrast with C++ is starker still. C++ developers face false sharing as a perpetual hazard, discoverable only through profiling after the fact. The language provides `alignas` for explicit alignment and `std::atomic` for memory ordering, but no mechanism to verify that these tools have been applied correctly. A structure might lack the `alignas(64)` annotation that would prevent contention; nothing in the type system catches this omission. The developer must remember to pad, remember to align, remember to profile, and remember to fix what profiling reveals. We anticipate that Fidelity's structural approach will prove not merely safer but more productive: developers focus on application logic while the compiler handles cache considerations.

This architectural choice reflects a broader principle: the right abstractions eliminate entire categories of bugs rather than detecting them. The actor model, combined with capability-based ownership, eliminates false sharing between actors. It eliminates data races. It eliminates use-after-free across actor boundaries. These guarantees hold at compile time; no runtime checking is required.

These principles manifest in Prospero's three-tier approach to cache management: compile-time analysis, arena configuration, and runtime placement.

### Compile-Time Cache Behavior Analysis

The Composer compiler's sophisticated cache analysis will become possible through BAREWire's deterministic memory layouts. Where traditional compilers must make conservative assumptions about memory access patterns, Composer is designed to operate with complete knowledge of data placement.

Consider how the compiler analyzes a market data processing actor:

```fsharp
type MarketTick = {
    [<BAREField(0)>] Symbol: uint64     // Bytes 0-7
    [<BAREField(1)>] Price: float32     // Bytes 8-11
    [<BAREField(2)>] Volume: uint32     // Bytes 12-15
    [<BAREField(3)>] Timestamp: uint64  // Bytes 16-23
}

let processTickBatch (ticks: BAREArray<MarketTick>) =
    for tick in ticks do
        updatePrice tick.Symbol tick.Price  // Compiler knows: touches bytes 0-11
        if tick.Volume > threshold then      // Adds bytes 12-15 to working set
            recordHighVolume tick            // Full structure access
```

Composer's semantic graph captures this precisely: the common path touches 12 bytes per tick (partial cache line), while high-volume events access all 24 bytes. This deterministic analysis will enable three categories of optimization:

Working set size calculation becomes exact rather than estimated. With BAREWire layouts, Composer computes that processing 1000 ticks requires exactly 12KB for the common path, fitting within L1 cache, versus 24KB for high-volume batches, suggesting L2 optimization.

Access pattern classification leverages known offsets to identify sequential, random, and strided patterns. When iterating through a BAREArray, the compiler knows the exact stride (24 bytes in this example) and can determine whether this creates cache-friendly sequential access or problematic cache line splits.

Temporal locality analysis tracks which fields are accessed repeatedly versus once. Because BAREWire guarantees field positions, our Composer implementation will generate code that prefetches only the frequently accessed Symbol and Price fields while leaving Volume and Timestamp to demand-loading.

### Arena Configuration for Cache Optimization

Based on compile-time analysis, we envision Prospero configuring actor arenas to optimize cache utilization. This configuration will extend beyond simple size selection to encompass alignment, placement, and access strategies - all made possible by BAREWire's compile-time layout guarantees.

Arena sizing aligns with cache tier boundaries using precise working set calculations. Because BAREWire provides exact structure sizes and field offsets, our Prospero architecture can allocate arenas that precisely match cache capacities:

```fsharp
// BAREWire enables exact arena sizing
let configureArena (actor: ActorProfile) =
    match actor.MessageType with
    | BAREType size when size * actor.MaxQueueDepth <= 32<KB> ->
        // Entire message queue fits in L1
        { Size = size * actor.MaxQueueDepth
          Alignment = 64  // Cache line aligned
          Strategy = L1Resident }

    | BAREType size when size * actor.BatchSize <= 256<KB> ->
        // Processing batch fits in L2
        { Size = size * actor.BatchSize
          Alignment = size  // Align to message size
          Strategy = L2Optimized }
```

Cache line alignment prevents false sharing with surgical precision. BAREWire's field-level offset control enables Prospero to ensure that frequently modified fields begin on cache line boundaries. The `[<CacheLineAligned>]` attribute automates this padding:

```fsharp
[<BAREStruct>]
type ActorState = {
    [<BAREField(0, Offset=0)>] ReadOnlyConfig: Config     // Shared read, cache line 0
    [<BAREField(1, Offset=64)>] MutableCounter: uint64    // Isolated write, cache line 1
    [<BAREField(2, Offset=128)>] LocalBuffer: BAREArray   // Private data, cache line 2+
}

// Alternatively, automatic padding to cache line boundary
[<CacheLineAligned>]
type WorkerCounter = {
    mutable value: int64
}
// Compiler pads to 64 bytes (or 128 on Apple Silicon)
```

Note that cache line sizes vary by architecture: x86-64 and most ARM64 processors use 64-byte lines, while Apple Silicon uses 128-byte lines. BAREWire resolves this from the target triple at compile time, ensuring correct padding without manual adjustment.

Arena coloring, inspired by page coloring techniques, deliberately positions arenas at specific cache set offsets. BAREWire's predictable layouts enable our planned Prospero implementation to calculate exactly which cache sets each structure will map to, preventing conflicts between frequently accessed actors. This technique proves particularly valuable in systems with high actor density where cache competition would otherwise degrade performance.

### Runtime Actor Placement and Affinity

While compile-time analysis provides valuable insights, runtime conditions ultimately determine optimal actor placement. Our planned implementation of Prospero's runtime placement engine will consider both cache topology and dynamic system state when positioning actors.

L3 domain awareness groups communicating actors within shared L3 cache regions. Modern processors partition L3 cache among core clusters; placing related actors within the same cluster ensures their communication remains cache-resident. This placement strategy is intended to particularly benefit producer-consumer patterns where one actor's output becomes another's input.

Hyperthread considerations recognize that logical cores sharing physical execution units also share L1 and L2 caches. Our Prospero design avoids placing cache-competing actors on sibling hyperthreads, preventing cache thrashing from simultaneous execution.

Dynamic load balancing will incorporate cache effects into migration decisions. Moving an actor between cores incurs cache warming costs as its working set must be loaded into the new core's caches. Prospero's planned migration algorithm weighs these costs against load balancing benefits, avoiding migrations that would degrade cache performance.

### Memory Ordering and Concurrent Access

Cache-conscious layout addresses spatial concerns: where data resides relative to cache line boundaries. Concurrent mutable access introduces a second dimension: temporal coordination. When multiple cores modify shared state, the order in which writes become visible matters for program correctness.

Modern processors reorder memory operations for performance. A write to one location may become visible before a logically prior write to another location. This reordering, invisible to single-threaded code, can cause subtle bugs in concurrent programs. The C++ memory model introduced `memory_order_relaxed`, `memory_order_acquire`, `memory_order_release`, and `memory_order_seq_cst` to address this; these are not language abstractions but direct mappings to hardware capabilities.

The C++ memory model, standardized in C++11 and refined since, represents a necessary response to the realities of modern hardware. Yet its complexity burdens every developer who touches concurrent code. The distinction between `memory_order_acquire` and `memory_order_consume`, the subtleties of release sequences, the interaction between atomics and non-atomic accesses; these require expertise that most application developers cannot reasonably acquire. Our designs lead us to a layered model where this complexity exists but remains contained. Library implementers who need fine-grained control have it. Application developers who use actors see sequential consistency without effort. The complexity matches the need.

Clef provides atomic operations with defined ordering semantics through CCS intrinsics:

```fsharp
// Sequential consistency (default, safe for most uses)
let value = Atomic.SeqCst.load ptr
Atomic.SeqCst.store ptr newValue

// Acquire/release for synchronization patterns
let flag = Atomic.Acquire.load lockPtr
Atomic.Release.store lockPtr 0

// Relaxed for counters where ordering doesn't matter
Atomic.Relaxed.fetchAdd counterPtr 1L
```

The actor model largely eliminates the need for explicit atomics in application code. Actors communicate through messages; they do not share mutable state. This design choice provides sequential consistency semantics without the complexity of memory ordering decisions. Library implementers building lock-free data structures have access to the full spectrum of ordering control, but application developers rarely need it.

This layered approach reflects a broader principle in Fidelity: complexity appears only where performance demands it. Most code uses actors and sees sequential consistency automatically. The rare code that requires fine-grained ordering control can access it through clearly marked APIs.

## Processor-Specific Optimization

The effectiveness of cache optimization strategies varies significantly across processor architectures. Intel and AMD processors, while supporting the same instruction set architecture, exhibit distinct cache hierarchies, prefetching behaviors, and performance characteristics. We're designing Prospero to leverage LLVM's target triple mechanism to generate processor-specific optimizations.

This is where Composer's processor-specific optimization capabilities become essential. The Composer compiler maintains a comprehensive repository of processor-specific optimization patterns, cache characteristics, and lowering strategies. Like the ancient Library of Alexandria that sought to collect all human knowledge, Composer catalogues the quirks, optimizations, and performance characteristics of diverse processor architectures. When encountering a target triple, Composer retrieves the optimal lowering strategies for that specific processor's cache hierarchy, instruction scheduling preferences, and memory subsystem behavior.

### Target Triple Architecture

LLVM target triples encode processor architecture, vendor, and operating system information, enabling architecture-specific code generation. Our approach with Prospero, guided by Composer's optimization knowledge base, extends this mechanism to include cache-specific optimizations tailored to processor families.

For Intel processors, identified through triples like `x86_64-intel-linux-gnu`, Composer's optimization repository guides mlir-opt to apply cache-specific optimizations tailored to Intel's inclusive L3 cache architecture. This design, where L3 contains copies of all L1 and L2 contents, influences prefetching strategies and cache bypass decisions.

Composer emits portable MLIR using `memref`, `arith`, and `func` dialects. The code remains architecture-agnostic, allowing mlir-opt and the LLVM backend to apply processor-specific optimizations:

```mlir
// Composer emits portable MLIR
%data = memref.alloca() : memref<1024xf32>
%c0 = arith.constant 0 : index
%index = arith.addi %c0, %offset : index
%value = memref.load %data[%index] : memref<1024xf32>
```

For Intel targets with aggressive hardware prefetchers, mlir-opt can introduce prefetch hints during lowering. For sequential access patterns, the backend recognizes the memref access pattern and generates processor-specific prefetch instructions.

AMD processors, identified through triples like `x86_64-amd-linux-gnu`, employ a non-inclusive L3 cache where data may exist in L3 without being present in L1 or L2. Composer's knowledge base informs different optimization strategies for this architecture. The same portable MLIR allows the backend to minimize L3 evictions and leverage AMD's larger L3 caches:

```mlir
// Same portable MLIR for AMD targets
%data = memref.alloca() : memref<1024xf32>
%value = memref.load %data[%index] : memref<1024xf32>
// mlir-opt applies non-temporal hints for streaming data
```

The mlir-opt tool and LLVM backend handle architecture-specific decisions. For AMD targets, non-temporal load hints can be applied for data that will not be reused, minimizing cache pollution.

As new processor architectures emerge (ARM's sophisticated cache hierarchies, RISC-V implementations, or specialized AI processors), Composer's optimization library will expand to encompass their optimization patterns. This architectural knowledge repository ensures that Fidelity applications benefit from processor-specific optimizations without requiring developers to understand the intricacies of each target platform.

### Instruction Selection for Cache Efficiency

Beyond target triple differentiation, our Prospero implementation will select instructions based on their cache implications. Modern processors provide specialized instructions for cache management that Prospero, guided by Composer's architectural knowledge, is intended to leverage when beneficial.

Non-temporal moves (`movnti`, `movntq`) bypass cache hierarchy entirely, writing directly to memory. Composer emits standard memref operations for memory copies. The mlir-opt tool applies non-temporal hints based on the optimization repository's guidance for large streaming writes:

```mlir
// Composer emits portable memory copy
%dest = memref.alloca() : memref<?xi8>
%src = memref.alloca() : memref<?xi8>
memref.copy %src, %dest : memref<?xi8> to memref<?xi8>
// mlir-opt applies non-temporal hints for streaming data
```

Cache line management operations (such as `clzero` on AMD or `cldemote` on Intel) occur during final code generation. Composer represents memory allocation and access using memref operations. The backend selects cache management instructions based on access patterns and target architecture.

## Large Pages and Streaming Architectures

High-performance systems frequently operate on datasets exceeding cache capacity, necessitating sophisticated strategies for managing the boundary between cached and uncached memory. Our approach for Prospero centers on two complementary techniques: large page management for TLB efficiency and streaming architectures that minimize cache pollution.

### Explicit Page Management

Translation Lookaside Buffer (TLB) misses represent a hidden performance cost in systems with extensive memory usage. Each memory access requires virtual-to-physical address translation; TLB misses necessitate expensive page table walks. Large pages (2MB or 1GB versus standard 4KB) dramatically reduce TLB pressure by covering more memory with fewer TLB entries.

We envision Prospero intelligently selecting huge page allocation for actors based on their memory access patterns. Actors with large, contiguous memory regions will benefit from huge pages, while actors with sparse access patterns may suffer from memory waste. The selection process is designed to consider both actor characteristics and system-wide memory availability:

```fsharp
let selectPageSize (actor: ActorProfile) (systemMemory: MemoryState) =
    match actor.WorkingSetSize, actor.AccessDensity, systemMemory.HugePagesAvailable with
    | size, density, available when size > 2<MB> && density > 0.75 && available > 10 ->
        // Dense access pattern justifies huge pages
        PageSize.Huge2MB
    | size, _, available when size > 1<GB> && available > 2 ->
        // Very large working set benefits from 1GB pages
        PageSize.Huge1GB
    | _ ->
        // Default to standard pages
        PageSize.Standard4KB
```

The distinction between transparent huge pages (THP) managed by the kernel and explicit huge page allocation through `mmap` with `MAP_HUGETLB` provides our Prospero design with flexibility. For latency-sensitive actors, explicit huge pages guarantee TLB efficiency without the unpredictability of THP's background defragmentation.

### Pointer-Heavy L3 Operations

Modern software architectures frequently separate control flow from data flow, maintaining pointer structures in cache while streaming large objects through memory. This pattern appears in graph processing, database operations, and message routing systems. Our Prospero design optimizes for this dichotomy through split arena allocation.

Consider a routing actor that maintains a routing table (small, frequently accessed) while forwarding large messages (streaming, accessed once). Prospero allocates the routing table in a cache-optimized arena while messages reside in streaming memory:

```fsharp
type RouterArenas = {
    RoutingTable: CacheOptimizedArena  // Fits in L2/L3, frequently accessed
    MessageBuffer: StreamingArena       // Bypasses cache, sequential access
}

let routeMessage (arenas: RouterArenas) (message: Message) =
    // Routing decision uses cached routing table
    let destination = arenas.RoutingTable.Lookup message.Header

    // Message data streams through memory without cache pollution
    use streamBuffer = arenas.MessageBuffer.AllocateTransient message.Size
    BAREWire.zeroCopyTransfer message.Data streamBuffer destination
```

This separation ensures that pointer traversal operations benefit from cache locality while bulk data movement doesn't evict critical control structures.

## Advanced Cache Bypass Strategies

While caching generally improves performance, certain access patterns benefit from deliberately bypassing cache hierarchy. Our planned Prospero implementation will incorporate sophisticated strategies for identifying and optimizing these patterns.

### Late Binding and Lazy Evaluation

Functional programming's lazy evaluation naturally creates opportunities for cache optimization. Data structures that may or may not be accessed shouldn't pollute cache until actually needed. We're designing Prospero to leverage LLVM's metadata system to mark potentially-unused allocations:

```llvm
; Allocation for lazy evaluation
%lazy_data = call ptr @arena_allocate(i64 %size), !maybe_unused !0

; Later, when actually accessed
%is_needed = load i1, ptr %force_flag
br i1 %is_needed, label %force_lazy, label %skip

force_lazy:
  ; Prefetch when actually needed
  call void @llvm.prefetch.p0(ptr %lazy_data, i32 0, i32 3, i32 1)
  %value = load i64, ptr %lazy_data
  br label %continue

skip:
  ; Never touches cache if not needed
  br label %continue
```

This approach prevents speculative caching of data that may never be accessed, preserving cache capacity for actively used data.

### Copy-on-Write and Cache Coherency

Copy-on-write (COW) semantics present interesting cache optimization opportunities. Initially shared data shouldn't trigger cache coherency traffic until actual modification occurs. Our approach with Prospero will generate code that maintains read-only copies in shared cache levels until write access forces privatization:

```fsharp
module COWOptimization =

    type COWBuffer<'T> = {
        mutable Data: nativeptr<'T>
        mutable IsPrivate: bool
        OriginalData: nativeptr<'T>
    }

    let write (buffer: COWBuffer<'T>) index value =
        if not buffer.IsPrivate then
            // First write triggers copy
            let privateData = ArenaAllocate buffer.Size

            // Non-temporal copy to avoid cache pollution
            memcpyNonTemporal privateData buffer.OriginalData buffer.Size

            buffer.Data <- privateData
            buffer.IsPrivate <- true

        // Subsequent writes go directly to private copy
        NativePtr.set buffer.Data index value
```

### Prefetching Distance Calibration

Hardware prefetchers excel at detecting sequential access patterns but struggle with irregular patterns. Our vision for Prospero includes generating explicit prefetch instructions calibrated to processor-specific characteristics and access patterns, with Composer maintaining a detailed database of prefetcher behaviors across architectures:

```fsharp
let calculatePrefetchDistance (pattern: AccessPattern) (processor: ProcessorInfo) =
    // Composer provides processor-specific prefetch characteristics
    match pattern, processor.Vendor with
    | Sequential(stride), Intel ->
        // Intel's aggressive prefetcher needs modest software assistance
        max (128L / stride) 4L  // 4-8 elements ahead

    | Sequential(stride), AMD ->
        // AMD benefits from more aggressive software prefetching
        max (256L / stride) 8L  // 8-16 elements ahead

    | Irregular(probability), _ when probability > 0.7 ->
        // Likely branches benefit from prefetching
        2L  // Conservative prefetch

    | _ ->
        // No software prefetch for random access
        0L
```

## Integration with Zero-Copy Architecture

Cache consciousness becomes particularly crucial when combined with Fidelity's zero-copy architecture. The efficiency gains from eliminating memory copies can be negated by poor cache behavior if not carefully managed. BAREWire's deterministic layouts ensure that zero-copy operations preserve cache efficiency rather than destroying it.

### Cache-Line Aligned Transfers

Zero-copy operations must respect cache line boundaries to prevent partial line transfers that trigger unnecessary coherency traffic. BAREWire's compile-time layout control ensures that message buffers align naturally with cache lines:

```fsharp
[<BAREStruct>]
type Message =
    struct
        [<BAREField(0, Offset=0)>]
        val mutable Header: MessageHeader  // Exactly 64 bytes via BAREWire

        [<BAREField(1, Offset=64)>]  // Next cache line boundary, guaranteed
        val mutable Payload: BAREArray<byte>

        // BAREWire ensures no hidden padding or metadata
    end
```

This deterministic alignment means that header access never triggers unnecessary payload fetches, and concurrent access to different message fields doesn't cause false sharing. The Composer compiler will be able to verify at compile time that zero-copy operations preserve cache line boundaries:

```fsharp
// Compiler-verified cache-friendly zero-copy
let forwardMessage (msg: Message) (dest: ActorRef) =
    // Composer knows: Header access touches only first cache line
    if msg.Header.Priority = High then
        // BAREWire zero-copy preserves alignment
        BAREWire.zeroCopyTransfer msg dest  // No cache line splits
```

### NUMA-Aware Zero-Copy

In NUMA systems, zero-copy operations must consider not just cache topology but also memory controller topology. BAREWire's deterministic layouts will enable Prospero to predict exactly which cache lines will be accessed during a zero-copy transfer, optimizing the operation based on this knowledge:

```fsharp
let optimizeZeroCopy (source: ActorRef) (dest: ActorRef) (msg: BAREMessage) =
    // BAREWire provides exact size and alignment
    let cacheLines = (msg.Size + 63) / 64  // Compiler-known value

    match getNumaNode source, getNumaNode dest, cacheLines with
    | same, _, lines when same = getNumaNode dest && lines <= 4 ->
        // Small message on same node: direct zero-copy
        ZeroCopyStrategy.Direct

    | _, _, lines when lines > 16 ->
        // Large transfer: use non-temporal to avoid cache pollution
        ZeroCopyStrategy.NonTemporal

    | sourceNode, destNode, lines ->
        // BAREWire layout enables precise prefetch calculation
        ZeroCopyStrategy.PrefetchAssisted(lines * 64)
```

The synergy between BAREWire's memory mapping and Prospero's cache management is designed to create a system where zero-copy truly means zero overhead - no unexpected cache misses, no coherency storms, no hidden costs.

## Performance Verification and Adaptation

Fidelity makes compile-time claims about cache behavior: arena isolation prevents false sharing, `[<CacheLineAligned>]` types occupy distinct cache lines, working sets fit within predicted cache tiers. These claims derive from BAREWire's deterministic layouts and Composer's static analysis. Runtime verification confirms that actual hardware behavior matches these predictions.

This verification cycle distinguishes Fidelity from approaches that rely solely on runtime heuristics or developer discipline. The compiler produces verifiable guarantees; profiling tools confirm them.

### Hardware Performance Counter Integration

Modern processors expose performance counters that track cache behavior at the hardware level. The most relevant counter for false sharing detection is HITM (Hit Modified): an event triggered when a core reads a cache line that another core has modified. High HITM counts indicate cache line contention.

On Linux, `perf c2c` (cache-to-cache) provides direct access to these events:

```bash
# Record cache contention events
perf c2c record -o perf.data ./my_program

# Analyze and report contention points
perf c2c report --stdio
```

This tool maps contention events back to source locations through debug symbols. When Fidelity claims that two actors have isolated arenas, `perf c2c` should report zero HITM events between them. When the claim proves true, it substantiates the compile-time guarantee. When violations appear, they indicate either a compiler bug or incorrect usage patterns.

Beyond false sharing detection, standard performance counters track cache hierarchy behavior:

```fsharp
type CacheCounters = {
    L1DHits: uint64
    L1DMisses: uint64
    L2Hits: uint64
    L2Misses: uint64
    L3Hits: uint64
    L3Misses: uint64
    CoherencySnoops: uint64
    CacheLineEvictions: uint64
}

let monitorCachePerformance (actor: ActorRef) =
    let counters = readPerformanceCounters actor.CoreId

    let l1HitRate = float counters.L1DHits / float (counters.L1DHits + counters.L1DMisses)
    let l2HitRate = float counters.L2Hits / float (counters.L2Hits + counters.L2Misses)

    match l1HitRate, l2HitRate with
    | l1, _ when l1 < 0.9 ->
        // Poor L1 performance suggests working set too large
        SuggestOptimization.ReduceWorkingSet

    | _, l2 when l2 < 0.8 ->
        // L2 misses indicate potential for arena resizing
        SuggestOptimization.OptimizeArenaSize

    | _ ->
        // Cache performance acceptable
        NoOptimizationNeeded
```

### Adaptive Strategy Selection

Based on performance counter feedback, our Prospero implementation will adapt its cache strategies. This adaptation is designed to occur at actor migration points, preventing disruption of running actors while enabling optimization over time:

```fsharp
let adaptCacheStrategy (actor: ActorRef) (metrics: CacheMetrics) =
    match analyzeMetrics metrics with
    | ExcessiveCoherency ->
        // Too much cache line bouncing
        migrateToIsolatedCore actor

    | WorkingSetTooLarge ->
        // Thrashing in current cache level
        match actor.CurrentCacheTier with
        | L1Optimized -> promoteToL2Strategy actor
        | L2Optimized -> promoteToL3Strategy actor
        | L3Optimized -> enableStreamingMode actor

    | ConflictMisses ->
        // Cache associativity conflicts
        recolorArena actor.Arena
```

## Migration Path: From .NET to Fidelity

An important consideration in our design is providing a viable migration path for existing F# codebases currently running on .NET. While the full viability of migration depends heavily on the specific .NET dependencies and runtime features used by each project, we're designing Fidelity to minimize the friction of transition where possible.

### Source Compatibility Strategy

For F# projects with minimal .NET framework dependencies, we envision a migration path that preserves most source code unchanged. The Composer compiler is being designed to recognize common F# patterns and generate appropriate memory layouts automatically:

```fsharp
// Existing .NET F# code
module DomainModel =
    type Customer = {
        CustomerId: Guid
        Name: string
        Email: string
        Orders: Order list
        LastModified: DateTime
    }

    let processCustomer (customer: Customer) =
        customer.Orders
        |> List.filter (fun o -> o.Status = Active)
        |> List.sumBy (fun o -> o.Total)
```

This standard F# code would compile directly under Fidelity, with the Composer compiler making intelligent default choices: Guid becomes a 16-byte value type with appropriate alignment, strings map to BAREString with efficient length-prefix encoding, and lists transform to BAREArrays with growth strategies inferred from usage patterns. The DateTime type would map to a 64-bit timestamp, maintaining semantic compatibility while optimizing representation.

### Gradual Optimization Opportunities

Once running on Fidelity, developers could selectively optimize hot paths identified through profiling, without modifying the majority of their codebase:

```fsharp
// Same domain model with selective hints for optimization
module DomainModel =
    [<Struct>]  // Standard F# attribute suggesting value semantics
    type Customer = {
        CustomerId: Guid
        Name: string
        Email: string
        [<FrequentlyAccessed>]  // Hint for cache optimization
        Orders: Order list
        LastModified: DateTime
    }
```

These hints guide the compiler's optimization strategies while maintaining source compatibility. The code remains valid F# that could still compile on .NET, ensuring a bidirectional compatibility during the migration period.

### Library Ecosystem Considerations

The most significant challenge in migration lies not in language features but in library dependencies. Projects heavily dependent on .NET-specific libraries like Entity Framework, ASP.NET Core, or Windows Forms would require more substantial rework. However, for domains where Fidelity excels - high-performance computing, embedded systems, real-time processing - many .NET dependencies could be replaced with Fidelity-native alternatives that provide better performance characteristics.

We're particularly focused on ensuring that core F# idioms and patterns translate cleanly:
- Discriminated unions map to efficient tagged unions in BAREWire
- Record types maintain their immutable semantics with copy-on-write optimizations
- Computational expressions preserve their monadic structure while compiling to efficient state machines
- Active patterns continue to work with optimized pattern matching

### Actor Model Migration

For applications that could benefit from actor-based architecture, we envision tooling to help identify components suitable for actor transformation:

```fsharp
// Original .NET service class
type OrderService(repository: IOrderRepository) =
    member this.ProcessOrder(order: Order) =
        async {
            let! validated = this.ValidateOrder(order)
            let! saved = repository.Save(validated)
            return saved
        }

// Suggested actor transformation
type OrderActor() =
    inherit Actor<OrderMessage>()

    override this.Receive message =
        match message with
        | ProcessOrder order ->
            let validated = validateOrder order
            OrderRepository.Tell(Save validated)
```

This transformation preserves business logic while enabling the cache-locality and isolation benefits of the actor model.

### Realistic Expectations

We recognize that not all .NET codebases are suitable for migration to Fidelity. Applications deeply integrated with the .NET ecosystem, relying heavily on runtime reflection, or dependent on specific CLR behaviors may be better served remaining on .NET. Our goal is not to replace .NET wholesale but to provide a compelling alternative for scenarios where predictable performance, minimal runtime overhead, and explicit control over memory layout provide significant value.

The migration path represents one possible entry point to the Fidelity ecosystem, allowing teams to leverage existing F# expertise and codebases while gaining access to the performance benefits of native compilation and cache-conscious memory management. As the framework matures, we expect the migration tools and compatibility layers to evolve based on real-world usage patterns and community feedback.

## A Holistic Approach to Memory Hierarchy

Cache-conscious memory management in Prospero represents a fundamental shift from treating memory as a flat resource to acknowledging and exploiting its hierarchical nature. This achievement rests on the deterministic foundation provided by BAREWire's compile-time memory mapping, which transforms cache optimization from a runtime heuristic into compile-time engineering.

By coordinating compile-time analysis, runtime orchestration, and hardware-specific optimization, we envision Prospero achieving performance levels that simple OS-level allocation cannot attain. BAREWire's guaranteed layouts will enable the Composer compiler to reason precisely about cache behavior, generating code that respects cache boundaries, minimizes coherency traffic, and maximizes locality.

This approach acknowledges that modern system performance depends more on cache behavior than raw computational throughput. A cache miss costing 200 cycles negates the benefit of dozens of optimized instructions. By making cache consciousness a first-class architectural concern - and by providing the deterministic memory layouts necessary to make such consciousness meaningful - our design for Prospero aims to ensure that Fidelity applications achieve their full performance potential.

The planned integration of cache awareness with actor-based concurrency and zero-copy communication creates a synergistic system where each component reinforces the others. Actors provide natural boundaries for cache optimization, BAREWire's deterministic layouts make cache behavior predictable, and zero-copy operations preserve cache locality. The result is a system design where memory placement decisions made at compile time translate directly into runtime performance benefits.

As processor architectures continue evolving with deeper cache hierarchies, novel coherency protocols, and heterogeneous memory systems, the combination of BAREWire's determinism and Prospero's orchestration is designed to provide the flexibility to adapt. The semantic graph maintained by the Composer compiler will capture not just program logic but the exact memory access patterns that determine cache behavior, enabling optimizations that would be impossible in systems with dynamic memory layouts.

This cache-conscious design philosophy, combined with RAII-based deterministic memory management and the BAREWire zero-copy protocol, positions Fidelity to deliver the promise of concurrent programming with the performance of hand-tuned systems code. Composer's optimization repository - our "Library of Alexandria" of architectural knowledge - ensures that processor-specific optimizations remain manageable and extensible as new architectures emerge. Predictable memory layouts enable predictable performance - and BAREWire's compile-time guarantees, combined with Composer's architectural knowledge, provide exactly the predictability needed for true cache consciousness.
