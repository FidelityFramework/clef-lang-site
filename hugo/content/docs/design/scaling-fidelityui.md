---
title: "Scaling FidelityUI: The Actor Model"
linkTitle: "Scaling FidelityUI"
description: "How Olivier and Prospero extend FidelityUI"
date: 2025-05-24
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2025-05-24
  original_url: "https://speakez.tech/blog/scaling-fidelityui/"
  migration_date: 2026-03-12
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

As we've established in previous entries, FidelityUI's deterministic memory approach provides an elegant solution for embedded systems and many desktop applications. But what happens when your application grows beyond simple UI interactions? When you need to coordinate complex business logic, handle concurrent operations, and manage sophisticated rendering pipelines? This is where the Olivier actor model and Prospero orchestration layer are designed to transform FidelityUI from a capable UI framework into a comprehensive application architecture that scales to distributed systems, all while maintaining deterministic memory management through RAII (Resource Acquisition Is Initialization) principles.

The beauty of this approach lies in its simplicity. We don't abandon our deterministic memory principles; instead, we extend them with arena-based allocation that follows RAII patterns. Think of it as moving from building individual houses to planning entire neighborhoods, where each house (actor) manages its own property (arena) and automatically handles cleanup when moving out. The construction techniques remain the same, but now we have a systematic approach to organizing larger communities.

## The Architectural Evolution: From Components to Actors

To understand how FidelityUI scales with the actor model, let's first establish what doesn't change. All the stack-based patterns, compile-time optimizations, and direct LVGL bindings we've carefully designed remain as they are. When you write a button component or layout a grid, you're still using the same efficient, stack-based approach. What changes is how these components are organized and coordinated in larger applications.

The actor model introduces a process-based architecture where each process owns memory arenas that actors can use. Multiple actors within a process could efficiently share immutable data while maintaining logical separation through message passing. Each actor receives its own arena that is automatically cleaned up when the actor terminates—this is RAII at the actor level. Between processes, you get strong isolation; within processes, you get efficient collaboration with deterministic memory management.

## Understanding RAII in the Actor Model

RAII brings deterministic memory management to our actor system through a simple principle: resource lifetime is tied to object lifetime. When an actor is created, it gets an arena. When the actor terminates, the arena is automatically reclaimed. No scanning, no pauses, no unpredictability—just deterministic cleanup that happens exactly when expected.

Here's how RAII integrates with our process-actor model:

```fsharp
module ProcessManagement =

    // Each process manages a pool of arenas
    type ProcessArenaPool = {
        ProcessId: ProcessId
        TotalSize: int64
        AvailableArenas: Arena list
        AllocatedArenas: Map<ActorId, Arena>
    }

    // Arena configuration for different workloads
    type ArenaConfig = {
        Size: int64
        AllocationStrategy: AllocationStrategy
        GrowthPolicy: GrowthPolicy
    }

    // Create a process with arena pool
    let createProcess name poolSize =
        let pool = {
            ProcessId = ProcessId.generate()
            TotalSize = poolSize
            AvailableArenas = Arena.createPool poolSize
            AllocatedArenas = Map.empty
        }

        let process = Process.create name pool

        // Register with Prospero for orchestration
        Prospero.registerProcess process
        process
```

RAII eliminates the need for complex memory tracking. When an actor is created, it gets an arena from the pool. When the actor terminates, the arena returns to the pool or is destroyed. This simple lifecycle would make memory management predictable and efficient.

## Actor Memory Arenas and Lifecycle Integration

Consider a sophisticated application like a real-time data visualization dashboard. You might have several processes, each containing multiple actors with their own arenas:

```fsharp
module UIProcess =
    let processPool = createArenaPool "UI" (512 * MB)

    type RenderActor() =
        inherit Actor<RenderMessage>()

        // Actor gets an arena that lives exactly as long as the actor
        let arena = Arena.allocate processPool (50 * MB)

        // RAII collections allocated from actor's arena
        let renderPipeline = RenderPipeline.createIn arena
        let frameBuffers = ResizeArray<FrameBuffer>()

        override this.Receive message =
            match message with
            | CreatePipeline config ->
                // Resources allocated from arena
                renderPipeline.Configure(config)

            | RenderFrame ->
                // Rendering still uses stack-based patterns
                renderPipeline.Execute()

        // RAII: Arena automatically cleaned up with actor
        interface IDisposable with
            member this.Dispose() =
                arena.Dispose()  // Immediate reclamation

    type InputActor() =
        inherit Actor<InputMessage>()

        // Smaller arena for input handling
        let arena = Arena.allocate processPool (10 * MB)

        // RAII collections manage their own cleanup
        let gestureRecognizer = GestureRecognizer.createIn arena

        override this.Receive message =
            match message with
            | TouchEvent data ->
                // Process input using stack operations
                let gesture = recognizeGesture data
                // Communicate with render actor in same process
                RenderActor.Tell(UpdateForGesture gesture)

        interface IDisposable with
            member this.Dispose() =
                arena.Dispose()
```

This architecture aims to provide something elegant: each actor has complete control over its memory lifecycle. When an actor terminates, its entire arena is reclaimed immediately. No waiting, no scanning, just deterministic cleanup that happens exactly when the actor's dispose method is called.

## Cross-Process References with Sentinels

One of the most innovative aspects of our memory management is how we handle references between actors in different processes. Instead of using null references, we use Reference Sentinels that provide rich state information:

```fsharp
module CrossProcessReferences =

    // Sentinel states provide more information than simple null/non-null
    type ReferenceState =
        | Valid              // Actor is alive and reachable
        | Terminated         // Actor has terminated cleanly
        | ProcessUnavailable // Process is down or unreachable
        | Unknown            // State cannot be determined

    // Sentinels track cross-process references
    type CrossProcessSentinel = {
        TargetProcessId: ProcessId
        TargetActorId: ActorId
        mutable State: ReferenceState
        mutable LastVerified: int64
    }

    // Sending messages across processes with automatic verification
    let sendCrossProcess (sender: ActorRef) (recipient: ActorRef) message =
        match recipient.Sentinel with
        | None ->
            // Same process - direct delivery
            deliverLocal recipient message

        | Some sentinel ->
            // Cross-process - verify through sentinel
            match verifySentinel sentinel with
            | Valid ->
                // Serialize and send via BAREWire
                let serialized = BAREWire.serialize message
                BAREWire.send sentinel.TargetProcessId serialized

            | Terminated ->
                // Handle dead letter with rich information
                DeadLetterActor.Tell(DeadLetter(sender, recipient, message, Terminated))

            | ProcessUnavailable ->
                // Process is down - might restart
                handleProcessFailure sender recipient message
```

The sentinel approach aims to provide actionable information about reference validity without relying on runtime memory management systems.

## Arena Orchestration with Prospero

Prospero orchestrates arena usage within a process, ensuring efficient memory utilization while maintaining actor independence:

```fsharp
module Prospero.ArenaOrchestration =

    // Prospero tracks arena usage across actors
    type ProcessArenaState = {
        ProcessId: ProcessId
        TotalArenaSize: int64
        ActorArenas: Map<ActorId, ArenaStats>
        PoolingStrategy: PoolingStrategy
    }

    // Arena pooling strategies
    type PoolingStrategy =
        | FixedSize      // All arenas same size
        | Adaptive       // Size based on actor type
        | OnDemand       // Create as needed

    // Prospero coordinates actor lifecycle with arenas
    let terminateActor (processId: ProcessId) (actorId: ActorId) =
        let arena = getActorArena processId actorId

        // Standard termination
        let actor = getActor actorId
        actor.Mailbox.Complete()

        // RAII handles cleanup automatically
        actor.Dispose()  // This triggers arena.Dispose()

        // Return arena to pool or destroy based on strategy
        match getPoolingStrategy processId with
        | FixedSize ->
            // Return to pool for reuse
            returnArenaToPool processId arena

        | Adaptive ->
            // Decide based on usage patterns
            if arena.Size < getAverageArenaSize processId then
                returnArenaToPool processId arena
            else
                arena.Destroy()  // Too large, don't keep

        | OnDemand ->
            // Always destroy, create fresh when needed
            arena.Destroy()
```

This coordination aims to ensure that memory is managed efficiently based on application needs without the complexity of garbage collection algorithms.

## Coordinated Rendering in the UI Process

The UI process showcases how multiple actors work together efficiently with RAII-based memory management:

```fsharp
module UIProcessArchitecture =
    let uiProcessConfig = {
        PoolSize = 512 * MB
        ArenaConfig = {
            DefaultSize = 50 * MB
            AllocationStrategy = FastAlloc
            GrowthPolicy = DoubleOnDemand
        }
    }

    // Shared immutable data with RAII lifetime
    type SharedViewHierarchy = {
        RootView: View
        ViewCache: Map<ViewId, View>
        LayoutCache: Map<ViewId, LayoutInfo>
    }

    // UI Coordinator with dedicated arena
    type UICoordinatorActor() =
        inherit Actor<UIMessage>()

        // Arena for view hierarchy
        let arena = Arena.allocate "UI" (100 * MB)

        // RAII collections for view management
        let viewHierarchy = SharedViewHierarchy.createIn arena

        override this.Receive message =
            match message with
            | UpdateView (id, updater) ->
                let newHierarchy = viewHierarchy.Update(id, updater)

                // Other actors can safely reference immutable data
                LayoutActor.Tell(RecalculateLayout newHierarchy)
                RenderActor.Tell(PrepareRender newHierarchy)

        interface IDisposable with
            member this.Dispose() =
                arena.Dispose()  // All view data cleaned up
```

This design leverages RAII's simplicity. The view hierarchy would live in the coordinator's arena and be automatically cleaned up when the coordinator terminates. No complex tracking needed.

## Process Topology with Arena Management

The process-actor hierarchy with RAII enables different deployment patterns:

```fsharp
// Embedded deployment - single process, minimal arenas
let configureEmbedded() =
    { Processes = [
        { Name = "Main"
          ArenaPoolSize = 32 * MB
          ArenaConfig = {
              DefaultSize = 4 * MB
              AllocationStrategy = Conservative
          }
          Actors = [
              createActor<UICoordinatorActor>()
              createActor<SimpleRenderActor>()
          ]}
      ]}

// Desktop deployment - multiple specialized processes
let configureDesktop() =
    { Processes = [
        { Name = "UIProcess"
          ArenaPoolSize = 512 * MB
          ArenaConfig = {
              DefaultSize = 50 * MB
              AllocationStrategy = Balanced
          }
          Actors = [
              createActor<UICoordinatorActor>()
              createActor<RenderActor>()
              createActor<InputActor>()
              createActor<AnimationActor>()
          ]}
        { Name = "DataProcess"
          ArenaPoolSize = 1 * GB
          ArenaConfig = {
              DefaultSize = 100 * MB
              AllocationStrategy = BulkOriented
          }
          Actors = [
              createActor<DataLoaderActor>()
              createActor<DataTransformActor>()
              createActor<CacheManagerActor>()
          ]}
      ]}
```

Each deployment scenario configures arena pools appropriately. The beauty of RAII is that the same simple cleanup mechanism works across all scales.

## Real-World Example: A Trading Dashboard

Let's see how this architecture handles a complex real-world application with RAII-based memory management:

```fsharp
module TradingDashboard =
    // UI Process - Must be responsive
    module UIProcess =
        let config = {
            ArenaPoolSize = 1 * GB
            DefaultArenaSize = 100 * MB
        }

        type MarketDataDisplay() =
            inherit Actor<MarketDisplayMessage>()

            let arena = Arena.allocate "UI" (200 * MB)

            // RAII collections for market data
            let marketView = MarketView.createIn arena

            override this.Receive = function
                | UpdatePrices prices ->
                    // Update view in place
                    marketView.UpdatePrices(prices)

                    // Share with other UI actors
                    ChartRenderer.Tell(RenderPriceChart marketView)
                    GridUpdater.Tell(UpdatePriceGrid marketView)

            interface IDisposable with
                member this.Dispose() = arena.Dispose()

        type AlertManager() =
            inherit Actor<AlertMessage>()

            // Small arena for alerts
            let arena = Arena.allocate "UI" (10 * MB)

            // RAII map for active alerts
            let activeAlerts = Dictionary<AlertId, Alert>()

            override this.Receive = function
                | PriceAlert (symbol, price) ->
                    let alert = Alert.createIn arena symbol price
                    activeAlerts.[alert.Id] <- alert
                    NotificationUI.Tell(ShowAlert alert)

                | DismissAlert id ->
                    activeAlerts.Remove(id) |> ignore

            interface IDisposable with
                member this.Dispose() = arena.Dispose()
```

This architecture aims to ensure clean separation and predictable cleanup. When actors terminate, their arenas are immediately reclaimed. No pauses, no scanning, just deterministic resource management.

## The Developer Experience: Simplicity by Default

The RAII approach means developers can focus on their domain logic without worrying about memory management:

```fsharp
// Simple app - memory management is invisible
let simpleApp() =
    window "My App" {
        label "Hello, World!"
        button "Click me" (fun () -> printfn "Clicked!")
    }

// Add an actor - RAII handles cleanup
let actorApp() =
    let dataActor = Actor.Create<DataActor>()  // Gets arena automatically

    window "My App" {
        button "Load Data" (fun () -> dataActor.Tell(LoadData))
    }

// Scale to multiple processes - still simple
let multiProcessApp() =
    let uiProcess = Process.Create("UI", arenaSize = 512 * MB)
    let dataProcess = Process.Create("Data", arenaSize = 1 * GB)

    let ui = uiProcess.SpawnActor<UIActor>()
    let data = dataProcess.SpawnActor<DataActor>()

    window "My App" {
        button "Process" (fun () -> data.Tell(ProcessDataset))
    }
```

The progressive disclosure of complexity means teams could adopt advanced features gradually without learning complex memory management concepts.

## Performance Benefits of RAII

The RAII approach is designed to provide measurable benefits:

**Predictable Performance**: No collection pauses or scanning overhead. Memory is reclaimed immediately when actors terminate.

**Memory Efficiency**: Arena allocation reduces fragmentation. Related allocations are grouped together and cleaned up as a unit.

**Simplified Mental Model**: Developers think in terms of actor lifetimes, not complex collection algorithms.

**Better Cache Locality**: Arena allocation keeps actor data together, improving cache performance.

## Elegant Scaling Through Simplicity

The combination of FidelityUI's deterministic memory patterns with the Olivier/Prospero actor model and RAII-based memory management aims to create something unique. By giving each actor its own arena with automatic cleanup, this architecture is designed to provide:

1. **Deterministic memory management** through RAII principles
2. **Strong isolation** between processes with rich failure information via sentinels
3. **Natural concurrency** through the actor model
4. **Progressive complexity** that grows with your application's needs

The beauty of RAII is its simplicity. No garbage collection algorithms to understand, no tuning parameters to adjust, no collection pauses to work around. Just a simple rule: when an actor dies, its memory is reclaimed. This predictability would make it easier to reason about system behavior and performance.

For embedded developers, small arenas provide predictable memory usage. For desktop developers, larger arenas enable rich applications. For enterprise developers, process isolation provides fault tolerance. All using the same simple RAII principles.

This is the elegance of concurrent systems programming: taking a simple concept (RAII) and applying it systematically to create powerful, scalable architectures. With FidelityUI, Olivier/Prospero, and RAII-based memory management, the goal is to make building high-performance concurrent applications simpler and more predictable than ever before.
