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

As we've established in previous entries, our FidelityUI deterministic memory approach serves embedded systems and many desktop applications. The question we take up here is what happens when an application grows beyond simple UI interactions: when it needs to coordinate business logic, handle concurrent operations, and manage rendering pipelines. From our design perspective, this is where the Olivier actor model and Prospero orchestration layer are meant to extend FidelityUI from a UI framework into an application architecture that scales to distributed systems, while keeping deterministic memory management through RAII (Resource Acquisition Is Initialization) principles.

We do not abandon our deterministic memory principles. We extend them with arena-based allocation that follows RAII patterns. The picture we work from is moving from building individual houses to planning a neighborhood, where each house (actor) manages its own property (arena) and handles cleanup when moving out. The construction techniques stay the same, with a systematic approach to organizing the larger whole.

## The Architectural Evolution: From Components to Actors

To follow how FidelityUI scales with the actor model, let's first establish what doesn't change. Our stack-based patterns, compile-time optimizations, and direct LVGL bindings remain as they are. When you write a button component or lay out a grid, you're still using the same stack-based approach. What changes is how these components are organized and coordinated in larger applications.

The actor model introduces a process-based architecture where each process owns memory arenas that its actors can use. Multiple actors within a process would share immutable data while keeping logical separation through message passing. Each actor receives its own arena, reclaimed when the actor terminates, which is RAII at the actor level. Between processes you get strong isolation; within a process you get collaboration with deterministic memory management.

## Understanding RAII in the Actor Model

RAII brings deterministic memory management to our actor system through one principle: resource lifetime is tied to object lifetime. When an actor is created, it gets an arena. When the actor terminates, the arena is reclaimed. No scanning, no pauses, and cleanup that happens exactly when expected.

In our process-actor model, RAII integrates as follows. While we are early in this design, the intended shape is:

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

RAII removes the need for separate memory tracking. When an actor is created, it gets an arena from the pool. When the actor terminates, the arena returns to the pool or is destroyed. This lifecycle is meant to keep memory management predictable and efficient.

## Actor Memory Arenas and Lifecycle Integration

Consider a real-time data visualization dashboard. It might have several processes, each containing multiple actors with their own arenas:

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

In this architecture each actor controls its own memory lifecycle. When an actor terminates, its entire arena is reclaimed immediately. No waiting and no scanning, with cleanup that happens exactly when the actor's dispose method is called.

## Cross-Process References with Sentinels

References between actors in different processes are handled differently in our memory management than in the actor systems we have surveyed. Instead of null references, we use Reference Sentinels that carry state information:

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

The sentinel approach is meant to give actionable information about reference validity without relying on a runtime memory management system.

## Arena Orchestration with Prospero

Prospero is designed to orchestrate arena usage within a process, keeping memory utilization efficient while preserving actor independence:

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

This coordination is meant to manage memory according to application needs without garbage collection algorithms.

## Coordinated Rendering in the UI Process

The UI process shows how multiple actors work together under RAII-based memory management:

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

In this design the view hierarchy lives in the coordinator's arena and is reclaimed when the coordinator terminates. No separate tracking is needed.

## Process Topology with Arena Management

The process-actor hierarchy with RAII supports different deployment patterns:

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

Each deployment scenario configures its arena pools accordingly, and the same cleanup mechanism works across all of them.

## Real-World Example: A Trading Dashboard

Consider how this architecture would handle a real-world application with RAII-based memory management:

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

This architecture is meant to keep separation clean and cleanup predictable. When actors terminate, their arenas are reclaimed immediately. No pauses and no scanning, with deterministic resource management.

## The Developer Experience: Simplicity by Default

The RAII approach is meant to let developers focus on their domain logic without managing memory by hand:

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

With this progressive disclosure, teams could adopt the more advanced features gradually without learning a separate set of memory management concepts.

## Performance Benefits of RAII

The RAII approach is designed to provide several benefits:

**Predictable Performance**: No collection pauses or scanning overhead. Memory is reclaimed when actors terminate.

**Memory Efficiency**: Arena allocation reduces fragmentation. Related allocations are grouped together and cleaned up as a unit.

**Simplified Mental Model**: Developers think in terms of actor lifetimes, not complex collection algorithms.

**Better Cache Locality**: Arena allocation keeps actor data together, improving cache performance.

## Scaling Through One Rule

Our FidelityUI deterministic memory patterns, the Olivier/Prospero actor model, and RAII-based memory management combine to give each actor its own arena with automatic cleanup. From this, our design is meant to provide:

1. **Deterministic memory management** through RAII principles
2. **Strong isolation** between processes with rich failure information via sentinels
3. **Natural concurrency** through the actor model
4. **Progressive complexity** that grows with your application's needs

RAII rests on one rule: when an actor terminates, its memory is reclaimed. There are no garbage collection algorithms to understand, no tuning parameters to adjust, and no collection pauses to work around. That predictability is what makes the system's behavior and performance easier to reason about.

For embedded developers, small arenas keep memory usage predictable. For desktop developers, larger arenas support richer applications. For enterprise developers, process isolation provides fault tolerance. The same RAII principle carries all three.

We will keep building toward this as the rest of the Olivier and Prospero work comes into place, with the aim of making concurrent applications on FidelityUI more predictable to reason about than the garbage-collected alternatives we set out from.
