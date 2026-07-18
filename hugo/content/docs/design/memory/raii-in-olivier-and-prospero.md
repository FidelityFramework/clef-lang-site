---
title: "RAII in Olivier and Prospero"
linkTitle: "RAII in Olivier and Prospero"
description: "Actor-Aware Memory Management Through Deterministic Lifetimes"
date: 2023-06-06
authors: ["Houston Haynes"]
tags: ["Design", "Innovation"]
params:
  originally_published: 2023-06-06
  migration_date: 2026-03-12
---

In our work to bring [the Clef language](https://clef-lang.com) to systems programming, we're pursuing deterministic memory management outside the familiar boundaries of managed runtimes. For developers who have only known automatic memory management as an omnipresent runtime service, applying RAII (Resource Acquisition Is Initialization) principles to actor-based systems departs from established patterns. Our current research focuses on how three complementary systems work together: RAII-based arena allocation, the Olivier actor model we're developing, and our proposed Prospero orchestration layer.

These three components integrate through memory management strategies determined at compile time to match actor-based application architectures. Applying RAII principles to actor systems is intended to bring predictable memory management to systems programming while maintaining the deterministic performance characteristics that real-time applications demand. These ideas continue to evolve as we refine our implementation.

The term RAII originates in C++, where resource cleanup is tied to destructor invocation at scope exit. C++ RAII works well for single-threaded, lexically scoped resources, with concurrent ownership requiring additional patterns like reference counting or explicit synchronization. Rust advances this model by making ownership explicit through the borrow checker, with the Drop trait providing deterministic cleanup. Both languages continue to evolve their approaches to concurrent resource management. Rust's async ecosystem and C++'s coroutine support represent ongoing work in this space. Fidelity takes a different approach by extending RAII principles to actor boundaries: each actor owns an arena that lives exactly as long as the actor. This provides deterministic cleanup aligned with the actor lifecycle, a design built into our actor-oriented architecture rather than adapted from a general-purpose ownership model.

## Three Systems in Concert

Effective memory management in a zero-runtime environment requires more than simply allocating and freeing memory. In traditional runtime environments, memory management operates as a global service treating all allocations uniformly. This approach, while suitable for general-purpose applications, fails to exploit the structured nature of actor-based systems. Our design proposes a different philosophy based on deterministic resource lifetimes.

The Olivier actor model, which takes lessons from Erlang, provides the organizational structure that makes structured memory management practical without runtime support. In our design, actors are not only concurrent entities. They are natural boundaries for resource ownership and lifecycle management. Each actor has a defined creation point, lifetime, and termination point. RAII binds resource lifetime to that temporal structure. When an actor terminates, we deterministically reclaim all its resources, a guarantee that follows from the bounded actor lifecycle.

Prospero, our proposed orchestration layer, transforms this actor structure into actionable memory management strategies. Beyond scheduling actor execution, Prospero coordinates arena allocation, resource pooling, and cross-actor references. Our design has Prospero distinguish a UI actor that processes frequent small messages from a data-processing actor that handles large batches, since the two exhibit different allocation patterns. Those differing patterns drive targeted arena configurations, each matched to the actor it serves.

RAII provides the foundational principle: resources are tied to object lifetimes. In our actor system, this means each actor owns an arena that lives exactly as long as the actor does. Cleanup is deterministic, tied to actor lifecycle completion rather than scanning or heuristics. The compile-time specialization process tailors allocation strategies to the specific actor topology of each application, going beyond linking memory management as a library.

This approach addresses challenges that each language handles differently. In C++, resources shared between threads require explicit synchronization. Smart pointers like `shared_ptr` provide one solution with runtime reference counting. Rust's ownership model enforces single ownership at compile time, with channels providing safe message passing between threads. Both approaches work well within their design constraints. Fidelity's actor-scoped RAII offers an alternative: the actor boundary provides the ownership scope, and message passing transfers capabilities between scopes. Our actor-oriented design makes actors the unit of both computation and resource ownership.

## Designing Actor-Aware Memory Architecture

Our current architectural exploration centers on one design principle: each process owns a pool of arenas, with actors within that process receiving dedicated arenas from that pool. This design is intended to balance isolation against efficiency:

```fsharp
module Fidelity.Memory

open Olivier
open Prospero

type ProcessConfiguration = {
    Name: string
    ArenaPoolSize: uint64
    ActorCapacity: int
    PoolingStrategy: PoolingStrategy
}

and PoolingStrategy =
    | FixedSize of size: uint64    // All arenas same size
    | Adaptive                     // Size based on actor type
    | OnDemand                     // Create as needed

// Native bindings to arena management via FidelityExtern
module ArenaManagement =
    [<FidelityExtern("arena_mgmt", "arena_create_pool")>]
    let createArenaPool (size: uint64) (config: nativeptr<PoolConfig>) : nativeint =
        Unchecked.defaultof<nativeint>

    [<FidelityExtern("arena_mgmt", "arena_allocate")>]
    let allocateArena (pool: nativeint) (size: uint64) : nativeint =
        Unchecked.defaultof<nativeint>

    [<FidelityExtern("arena_mgmt", "arena_release")>]
    let releaseArena (arena: nativeint) : unit =
        Unchecked.defaultof<unit>
```

This design proposes that when Prospero creates a process, it initializes an arena pool specifically configured for that process's expected workload. Within this pool, each actor receives a dedicated arena that serves as its private allocation space.

## Prospero's Role

In our current design thinking, Prospero serves as more than a scheduler. We envision it as an orchestrator that relates actor behavior to resource patterns, using that relationship to drive allocation strategy:

```fsharp
module Prospero.LifetimeOrchestration

type ActorResourceLifecycle = {
    ActorId: uint64
    Arena: nativeint
    AllocationPattern: AllocationPattern
    CleanupStrategy: CleanupStrategy
}

and AllocationPattern =
    | SmallFrequent      // Many small allocations
    | LargeBatch         // Few large allocations
    | Mixed              // Combination of patterns

and CleanupStrategy =
    | Immediate          // Release arena on termination
    | Pooled             // Return to pool for reuse
    | Deferred           // Batch cleanup for efficiency

let createActor<'T when 'T :> Actor<'Message>> (hint: AllocationPattern) =
    // Prospero uses the hint to configure arena
    let arenaSize = match hint with
                    | SmallFrequent -> 10UL * MB   // Small arena, expect reuse
                    | LargeBatch -> 100UL * MB     // Large arena for bulk data
                    | Mixed -> 50UL * MB           // Balanced size
    
    let arena = ArenaManagement.allocateArena pool arenaSize
    
    // Actor created with arena association
    Olivier.spawnWithArena<'T> arena
```

Coupling orchestration to resource management enables optimizations that a runtime-serviced allocator cannot make, because the allocation decisions are fixed before execution. Prospero observes actor behavior and adjusts allocation strategies while maintaining the zero-runtime principle through compile-time specialization.

## Compile-Time Specialization

The mechanism that makes this integration possible is the Composer compiler's approach to compile-time transformation. Memory management is a compile-time concern that CCS and the nanopass pipeline specialize for each application, resolved before the artifact runs rather than serviced during it:

```fsharp
// Compile-time transformation
module CompileTimeIntegration

// Developer writes standard actor code
type DataProcessor() =
    inherit Actor<DataMessage>()
    
    let mutable cache = Map.empty<string, ProcessedData>
    
    override this.Receive message =
        match message with
        | Process data ->
            let result = performComplexProcessing data
            cache <- Map.add data.Id result cache
        | Retrieve id ->
            Map.tryFind id cache |> ReplyChannel.send

// Composer compiler automatically manages resources through
// delimited continuations and scope analysis. The developer
// never writes cleanup code - it's inserted during IR lowering
// based on actor lifecycle boundaries and continuation points.
 
```

The Composer compiler, through CCS (Clef Compiler Services) and the nanopass enrichment pipeline, identifies actor state, determines allocation patterns, and generates RAII semantics in the MLIR, without runtime overhead or developer intervention.

## The Byref Problem Under Deterministic Lifetimes

Our RAII-based architecture addresses the "byref problem" familiar from traditional .NET development. In managed systems, the issue is unpredictable memory movement during garbage collection. Our RAII approach removes that uncertainty:

```fsharp
module DeterministicLifetimes

type MemoryStrategy =
    | StackOnly           // Pure zero-allocation
    | ArenaLinear         // Linear allocation, no movement
    | ArenaCompacting     // Allows compaction at message boundaries

// Configuration example
let configureMemoryStrategy (actorType: Type) : MemoryStrategy =
    match Prospero.analyzeMemoryPattern actorType with
    | HighFrequencyProcessing ->
        // Stack-based for maximum performance
        StackOnly
        
    | DataIntensive ->
        // Arena with no movement for byref safety
        ArenaLinear
        
    | GeneralPurpose ->
        // Arena with controlled compaction
        ArenaCompacting
```

This design addresses the byref problem through three complementary approaches:

1. **Deterministic Lifetimes**: Unlike garbage collection where cleanup timing is unpredictable, RAII reclaims memory at well-defined points: when actors terminate or scopes end. Byrefs remain valid for their intended lifetime.

2. **Linear Arena Allocation**: Many actors can use linear allocation within their arenas, meaning memory never moves. This allows unlimited use of byrefs within an actor's message processing without any safety concerns.

3. **Message Boundary Control**: For actors that do use compacting arenas, memory reorganization happens only at message boundaries when no byrefs can exist. This provides a safe window for memory optimization without invalidating references.

Memory lifetime is explicit and predictable. In traditional .NET, the garbage collector operates independently of application logic, which is the source of byref unsafety. In our system, memory management is deterministic and integrated with actor lifecycle, making byref usage both safe and efficient.

Rust addresses similar concerns through its borrow checker, ensuring references cannot outlive their referents. Rust's approach has proven effective, influencing memory safety discussions across the industry. The tradeoff involves lifetime annotations in complex scenarios, which some developers find challenging while others appreciate the explicitness. Fidelity's arena-based approach takes a different path: references within an actor's arena are valid for the actor's lifetime, with the actor boundary providing the scope that Rust encodes through lifetime parameters. Neither approach is universally better. Each reflects a different design philosophy about where lifetime information should live in the system.

## Cross-Process References with RAII

Actor systems extend beyond single-process boundaries. Our RAII-based approach handles distributed references as follows:

```fsharp
module CrossProcessReferences

[<Struct>]
type ReferenceSentinel = {
    ProcessId: uint64
    ActorId: uint64
    mutable State: ReferenceState
    mutable LastVerified: int64
}

and ReferenceState =
    | Valid
    | ActorTerminated
    | ProcessUnavailable
    | Unknown

// Integration with deterministic cleanup
let sendCrossProcess (sender: ActorRef) (target: ActorRef) message =
    match target.Location with
    | LocalProcess ->
        // Direct delivery within process
        Olivier.deliverLocal target message
        
    | RemoteProcess sentinel ->
        // Verify through Prospero's protocol
        match Prospero.verifySentinel sentinel with
        | Valid ->
            // Serialize using arena for temporary buffer
            use buffer = sender.Arena.CreateTemporary()
            let data = BAREWire.serializeInto buffer message
            Prospero.sendRemote sentinel.ProcessId data
            // Buffer cleanup happens automatically at scope exit
            
        | ActorTerminated ->
            sender.Tell(DeliveryFailed(target, ActorNoLongerExists))
            
        | ProcessUnavailable ->
            Prospero.scheduleRetry sentinel message
```

This design provides rich information about reference validity while maintaining RAII principles. Temporary buffers for serialization are cleaned up automatically through scope analysis, and cross-process references are managed without relying on distributed garbage collection.

## Memory Patterns in Practice

Our design combines these mechanisms for a real-time analytics system under RAII principles:

```fsharp
module AnalyticsSystem

// High-frequency data ingestion actor
type IngestionActor() =
    inherit Actor<IngestMessage>()
    
    override this.Receive message =
        match message with
        | DataPoint point ->
            // Temporary allocations within message processing
            // are automatically scoped and cleaned up
            let validated = validateDataPoint point
            let transformed = transformForAnalysis validated
            
            AnalysisRouter.Tell(Analyze transformed)
            // Cleanup happens automatically at message boundary

// Long-lived aggregation actor
type AggregationActor() =
    inherit Actor<AggregateMessage>()
    
    // Long-lived state persists across messages
    let aggregates = MetricAggregates.create()
    
    override this.Receive message =
        match message with
        | UpdateMetric metric ->
            aggregates.Update(metric)  // In-place update
            
        | QueryMetric query ->
            let result = aggregates.Query(query)
            ReplyChannel.send result
    
    // No disposal code needed - cleanup occurs when
    // the actor terminates based on lifecycle analysis
 
```

Different actors have different memory patterns, all managed through RAII principles implemented in the compilation process. The ingestion actor uses scoped allocations for temporary data, while the aggregation actor maintains long-lived state, both with automatic deterministic cleanup.

## Implications and Future Directions

This integration points toward a systems-programming model where resource management is structured and predictable. Our research indicates several directions:

**Compile-Time Optimization**: By analyzing actor topologies at compile time, the Composer compiler can generate specialized allocation code for each application, eliminating the overhead of runtime memory management decisions.

**Hardware Integration**: Modern processors include features like memory protection keys that our compile-time approach can leverage more effectively than runtime-based systems.

**Formal Verification**: With deterministic resource lifetimes visible at compile time, we envision opportunities for formal verification of memory safety properties, providing guarantees impossible with garbage-collected systems.

**Zero-Overhead Abstractions**: RAII enables zero-overhead abstractions where the memory management code compiles away entirely in optimized builds.

## Deterministic Actor Memory Management

The design we're pursuing rethinks memory management in actor systems. By treating actors as boundaries for resource ownership, and by using RAII principles to tie resource lifetime to actor lifetime, we bring predictable memory management to domains where garbage collection has been impractical.

Integrating RAII principles, our Olivier actor model, and Prospero's orchestration does more than avoid garbage collection. It shows that deterministic resource management can be applied across a wide range of workloads. Through this design and its compilation techniques, we envision Clef spanning embedded devices to distributed systems under a single approach to memory management that stays predictable and efficient.

The comparison with C++ and Rust illustrates how different design foundations lead to different solutions. C++ pioneered RAII and continues to evolve with features like smart pointers and static analysis tools. Rust's ownership model encodes single ownership and borrowing in the type system, a significant contribution to systems programming. Both languages support actor-style programming through libraries, with channels providing safe message passing. Fidelity's contribution is designing the type system around actor boundaries from the start: each actor owns its resources through capabilities that transfer via messages. This reworks RAII for actor-oriented systems rather than refining the existing model in place. We extend the principle from "resource acquisition is initialization" to "resource transfer is message passing," with the actor lifecycle providing the scope boundaries. Different applications will benefit from different approaches. We believe actor-intensive systems will find Fidelity's model particularly natural.

We continue to refine these concepts as we work toward practical implementations. The aim is functional systems programming that keeps the safety and expressiveness developers expect while delivering the performance and predictability systems programming demands. Our explorations indicate that RAII-based, actor-aware memory management is a workable direction for systems programming in functional languages.

*This article was originally written in 2023 and has since been updated to reflect recent Fidelity platform development and the latest research and information on the topic that has influenced our designs.*
