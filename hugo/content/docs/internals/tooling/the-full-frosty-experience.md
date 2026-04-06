---
title: "The Full Frosty Experience"
linkTitle: "Full Frosty Experience"
description: "Platform-Aware Async Compilation Through Delimited Continuations"
date: 2025-05-22
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2025-05-22
  original_url: "https://speakez.tech/blog/the-full-frosty-experience/"
  migration_date: 2026-03-12
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

The Fidelity framework represents an ambitious project to bring [the Clef language](https://clef-lang.com) to native compilation without runtime dependencies. One of the most challenging aspects of this endeavor is the treatment of asynchronous programming. This design document outlines our approach to compiling Clef's async computation expressions to efficient native code through delimited continuations, and introduces Frosty, an enhancement that brings advanced async patterns to this runtime-free environment.

---

Updated July 25, 2025

- True RAII principles for automatic async resource management
- Bidirectional PSG "zipper" computation expressions for async transformations
- Integration with Olivier actor model for structured concurrency

...with special thanks to [Paul Snively for his polyglot perspective](https://podcasts.apple.com/us/podcast/37-the-future-of-everything-with-paul-snively/id1531666706?i=1000531977557) that led to many of the connections drawn through the latest updates.

---

### Architectural Update: Frosty Is a Compiler Feature, Not a Library

Since this entry was originally published, work on the [DCont/Inet duality](/blog/dcont-inet-duality/) and [Fidelity.UI](/blog/fidelity-ui/) clarified where Frosty's concepts belong architecturally. The design thinking here is sound, but "Frosty" is not a library that developers import. It is a name for how Composer compiles async code.

The concepts decompose into layers that already exist:

| Frosty Concept | Architectural Home |
|---|---|
| `async { }` compilation to stack state machines | **Composer** DCont dialect (MLIR lowering) |
| True RAII resource tracking across async boundaries | **Composer** PSG analysis + coeffect passes |
| Platform I/O (IOCP, io_uring, kqueue) | **Fidelity.Platform** |
| Structured concurrency and cancellation | **Prospero/Olivier** supervision trees |
| Resource-returning function metadata | **CCS** intrinsics |

The developer-facing surface is unchanged: `async { }` with `let!` / `do!` / `return`. What changes is everything underneath. No `Task<T>` object, no heap allocation, no thread pool, no `IDisposable`. The continuation IS the value. Cancellation is structural (Prospero kills child actors), not token-based.

This also resolves the question of whether a `coldTask` or similar CE is needed. In Fidelity, `async { }` is inherently "cold" - it compiles to a DCont-based stack state machine that doesn't execute until invoked. The hot/cold distinction is an artifact of .NET's `Task<T>` semantics and has no equivalent in native compilation. Similarly, `cancellableTask`, `poolingValueTask`, and other IcedTasks CEs solve problems that only exist because of the .NET runtime model. Fidelity has none of these problems.

What follows is the original design exploration, preserved because the analysis of delimited continuations, RAII, and platform-aware compilation directly informed Composer's async compilation strategy.

---

This document presents our current thinking and architectural plans. The designs described here are in various stages of development and prototyping, with implementation scheduled across multiple phases of the Fidelity project. We present these ideas to the Clef community for feedback and discussion as we work toward making concurrent programming viable for systems-level development.

## The Challenge: Async Without a Runtime

Clef's async computation expressions have long been one of the language's most elegant features, predating similar constructs in other languages by many years. However, traditional async relies on sophisticated runtime infrastructure that presents fundamental challenges for systems programming:

```fsharp
// Traditional Clef async - beautiful but runtime-dependent
let downloadAndProcess url = async {
    let! content = Http.downloadStringAsync url      // Heap-allocated Task
    let processed = processContent content           // Thread pool scheduling
    do! File.writeAllTextAsync "output.txt" processed // More allocations
    return processed.Length
}

// Problems for systems programming:
// - Tasks allocated on heap (GC pressure)
// - Thread pool scheduling (non-deterministic timing)
// - Exceptions for error handling (hidden control flow)
// - No compile-time resource guarantees
```

For Fidelity's native compilation model, we need a fundamentally different approach - one that preserves Clef's elegant async programming model while enabling static analysis, deterministic resource management, and compile-time controlled memory allocation.

## Understanding Resource Management Across Languages

Before diving into Frosty's design, it's crucial to understand how different languages approach resource management, as Fidelity aims to combine the best of all worlds:

### C++ RAII: Automatic but Chaotic

```cpp
// C++ RAII - automatic cleanup but no structure
void processFile() {
    std::ifstream file("data.txt");      // Constructor acquires
    std::vector<char> buffer(1024);      // Stack allocation

    processData(file, buffer);
    // Both automatically cleaned up at scope exit
    // But: easy to create dangling references, no async story
}
```

### .NET: Managed but Non-Deterministic

```fsharp
// F#/.NET - explicit disposal, GC-managed
let processFile() =
    use file = new FileStream("data.txt", FileMode.Open)  // IDisposable
    let buffer = Array.zeroCreate 1024  // Heap allocated, GC'd
    processData file buffer
    // file.Dispose() called, but buffer cleanup is non-deterministic

// Problems:
// - Must remember 'use' keyword
// - Finalizers are unpredictable
// - Can't use byrefs safely (GC can move memory)
```

### Rust: Safe but Rigid

```rust
// Rust - borrow checker ensures safety but complex
async fn process_file() -> Result<(), Error> {
    let mut file = File::open("data.txt").await?;
    let mut buffer = vec![0u8; 1024];  // Owned, moved

    process_data(&mut file, &mut buffer).await?;
    // Automatic cleanup via Drop trait
    // But: lifetime annotations can be complex
}
```

### Fidelity's Vision: Structured Automatic Management

```fsharp
// Fidelity - automatic cleanup with Clef elegance
let processFile() = async {
    let! file = File.openAsync "data.txt"    // Compiler knows this needs cleanup
    let buffer = stackalloc<byte> 1024       // Stack allocated, auto-cleaned

    let! result = processData file buffer
    return result
}   // Compiler inserts all cleanup - no explicit disposal needed

// Compiler understands resource lifetime from async structure
```

## The Solution: Delimited Continuations with True RAII

The driver of Frosty's design is combining delimited continuations (for explicit async control flow) with true RAII principles (for automatic resource management). This isn't the .NET pattern of IDisposable - it's genuine automatic cleanup.

### Understanding Delimited Continuations

Delimited continuations provide explicit control over "the rest of the computation":

```fsharp
// Conceptual transformation
let asyncOperation() = async {
    let! x = fetchData()
    let! y = process x
    return x + y
}

// Becomes explicit continuation structure:
let asyncOperation'() =
    reset (fun () ->
        shift (fun k1 ->  // k1 is "everything after fetchData"
            fetchData() |> continueWith (fun x ->
                shift (fun k2 ->  // k2 is "everything after process"
                    process x |> continueWith (fun y ->
                        k1 (x + y)
                    )
                )
            )
        )
    )
```

### True RAII: Compiler-Managed Cleanup

Unlike .NET's IDisposable pattern, true RAII means the compiler automatically inserts cleanup at scope boundaries:

```fsharp
// What you write - no disposal interfaces needed
let processWithResources() = async {
    let! connection = Database.connect "server"
    let! file = File.openWrite "output.txt"
    let buffer = Arena.allocate 4096

    let! data = connection.query "SELECT * FROM data"
    do! file.writeAsync buffer data

    return data.Length
}   // Compiler ensures all resources cleaned up here

// What the compiler generates (conceptually):
let processWithResources'() = async {
    let! connection = Database.connect "server"
    try
        let! file = File.openWrite "output.txt"
        try
            let buffer = Arena.allocate 4096
            try
                let! data = connection.query "SELECT * FROM data"
                do! file.writeAsync buffer data
                return data.Length
            finally Arena.release buffer
        finally File.close file
    finally Database.disconnect connection
}
```

The crucial difference from .NET:
- **No IDisposable interfaces** - cleanup is structural, not interface-based
- **No forgotten cleanups** - compiler tracks all resources
- **No finalizers** - everything is deterministic
- **No GC pressure** - resources freed immediately at scope exit

## Integration with Composer's PSG

The explicit control flow of delimited continuations integrates perfectly with Composer's Program Hypergraph (PHG). The PSG tracks resource lifetime through the program structure:

```fsharp
// PSG node structure with automatic resource tracking
type PSGNode = {
    Id: NodeId
    Kind: PSGNodeKind
    Range: SourceRange
    Symbol: FSharpSymbol option
    Resources: TrackedResource list  // Compiler-identified resources
}

and TrackedResource = {
    Type: ResourceType
    AcquisitionPoint: NodeId
    CleanupPoint: NodeId  // Compiler-determined, not user-specified
    CleanupAction: CleanupStrategy
}

and CleanupStrategy =
    | StackUnwind          // Automatic for stack resources
    | ArenaReset           // Bulk deallocation
    | CustomCleanup of (unit -> unit)  // For external resources
```

### How the Compiler Identifies Resources

The Composer compiler recognizes resources through patterns and type analysis:

```fsharp
// Compiler recognizes resource patterns
module ResourceInference =
    let identifyResources (expr: TypedExpr) =
        match expr with
        | Call(method, args) when method.ReturnsResource ->
            // File.open, Database.connect, etc. marked in metadata
            Some (InferredResource method.ResourceType)

        | StackAlloc(size) ->
            // Stack allocations are auto-cleaned
            Some (StackResource size)

        | ArenaAlloc(arena, size) ->
            // Arena allocations tied to arena lifetime
            Some (ArenaResource(arena, size))

        | _ -> None

// Example: compiler metadata for resource-returning functions
[<ReturnsResource(ResourceType.FileHandle, Cleanup.CloseFile)>]
let openFile (path: string) : FileHandle = ...
```

## Memory Management Without Runtime

The combination of delimited continuations and true RAII enables sophisticated memory management without any runtime support:

### Stack-Based Async State Machines

```fsharp
// What developers write
let processSequence items = async {
    let mutable sum = 0
    for item in items do
        let! processed = transform item
        sum <- sum + processed
    return sum
}

// Compiler generates fixed-size state machine
type ProcessSequenceStateMachine = struct
    val mutable state: int
    val mutable sum: int
    val mutable currentItem: Item
    val mutable processed: int
    // Total size: 16 bytes, stack allocated
end

// No heap allocation, no GC, predictable memory usage
```

### Arena-Based Bulk Operations

For operations that do need dynamic memory, arenas provide deterministic bulk cleanup:

```fsharp
// Arena allocation with automatic cleanup
let batchProcess records = async {
    let arena = Arena.create 10_000_000  // 10MB arena

    // All allocations within this scope use the arena
    let! results = async {
        let accumulator = ResizeArray(records.Length, arena)

        for record in records do
            let! processed = transform record
            let entry = arena.allocate<ProcessedEntry>()
            entry.Data <- processed
            accumulator.Add(entry)

        return accumulator.ToArray()
    }

    return results
}   // Arena automatically reset here - O(1) cleanup

// Compare to .NET where each allocation would need GC
```

## Platform-Aware Compilation

Standard async syntax compiles to platform-specific implementations while maintaining RAII guarantees:

### Windows I/O Completion Ports

```fsharp
// What you write
let readFileAsync path = async {
    let! handle = File.openAsync path
    let buffer = stackalloc<byte> 4096
    let! bytesRead = handle.readAsync buffer
    return buffer.Slice(0, bytesRead)
}

// Compiles to IOCP with automatic cleanup
// - OVERLAPPED structure on stack (auto-cleaned)
// - Completion routine registration (auto-unregistered)
// - Buffer on stack (auto-freed)
// - Handle closed at scope exit (compiler-inserted)
```

### Linux io_uring

```fsharp
// Same Clef code compiles differently for Linux
// - Submission queue entry (SQE) on stack
// - Ring buffer mapping (auto-unmapped)
// - Automatic cleanup of ring resources
// - No manual resource management needed
```

## Advanced Patterns with Automatic Management

### Structured Concurrency

Parallel operations with automatic resource management per branch:

```fsharp
// Each parallel branch gets automatic cleanup
let processInParallel items = async {
    let! results =
        items
        |> List.map (fun item -> async {
            let buffer = Arena.allocate 1024  // Per-branch allocation
            let! result = process buffer item
            return result
        })  // Buffer cleaned up here
        |> Async.Parallel

    return List.sum results
}

// Compiler ensures each branch's resources are properly scoped
```

### Railway-Oriented Programming with Guaranteed Cleanup

Error handling that preserves automatic resource management:

```fsharp
// Railway-oriented async with automatic cleanup
type AsyncResult<'T, 'Error> = Async<Result<'T, 'Error>>

let processFileRailway path = asyncResult {
    let! handle = openFileResult path     // Cleaned up on any track
    let! content = readContentResult handle
    let! parsed = parseContentResult content
    return parsed
}   // Handle closed whether success or error - compiler guaranteed

// No need for try-finally or explicit disposal
```

### Cross-Async Resource Sharing

Fidelity enables safe resource sharing across async boundaries:

```fsharp
// Parent async owns the arena
let parentOperation() = async {
    let arena = Arena.create 1_000_000

    // Child asyncs can safely use parent's arena
    let! result1 = childOperation1 arena
    let! result2 = childOperation2 arena

    return result1 + result2
}   // Arena cleaned up after all children complete

// Safe because compiler tracks arena lifetime through async structure
```

## Integration with Actor Systems

The Olivier actor model extends RAII to concurrent systems:

```fsharp
// Actor with automatic resource management
type DataProcessor() =
    inherit Actor<DataMessage>()

    // Resources tied to actor lifetime - no explicit disposal
    let connection = Database.connect "server"
    let cache = Cache.create 10000
    let arena = Arena.create 50_000_000

    override this.Receive message = async {
        match message with
        | Query sql ->
            let! results = connection.query sql
            cache.put sql results
            return results

        | Process data ->
            use scope = arena.scope()  // Temporary scope
            let! result = processData scope data
            return result
    }
    // When actor terminates, all resources automatically cleaned up
    // No Dispose() method needed!
```

## Compile-Time Guarantees

The combination of delimited continuations and true RAII enables strong compile-time guarantees:

### Resource Leak Prevention

```fsharp
// Compiler error: resource escapes its scope
let leakyFunction() =
    let mutable leaked = None
    async {
        let buffer = Arena.allocate 1024
        leaked <- Some buffer  // Error: buffer can't escape async scope
        return ()
    }
    leaked  // Compiler knows this would be invalid

// This is caught at compile time, not runtime!
```

### Bounded Resource Usage

```fsharp
// Compiler computes maximum resource usage
[<MaxStackUsage(4096)>]
[<MaxArenaUsage(1_000_000)>]
let boundedOperation data = async {
    let buffer1 = stackalloc<byte> 2048
    let buffer2 = stackalloc<byte> 2048
    // Compiler: Total stack = 4096 bytes ✓

    let arena = Arena.create 1_000_000
    let! result = processWithArena arena data
    return result
}

// Exceeding limits produces compile error, not runtime failure
```

## Why This Matters: The Best of All Worlds

Fidelity's approach combines advantages from multiple languages while avoiding their drawbacks:

**From C++ RAII**: Automatic cleanup at scope exit
**Better because**: Type-safe, no undefined behavior, integrated with async

**From .NET**: Familiar Clef syntax and patterns
**Better because**: Deterministic timing, no GC pauses, safe byrefs

**From Rust**: Memory safety without GC
**Better because**: No lifetime annotations, natural Clef syntax

**Unique to Fidelity**: Async-aware RAII with compile-time resource tracking

## Future Directions

With Frosty understood as a compiler feature of Composer (not a standalone library), research continues through its architectural homes:

- **DCont/Inet Compilation**: The [DCont/Inet duality](/blog/dcont-inet-duality/) extends Frosty's async compilation to all computation expressions. Sequential CEs (async, state) compile to delimited continuations. Pure CEs (query, list) compile to interaction nets for parallel execution. The compiler recognizes which strategy applies automatically.
- **Cross-Process RAII**: Extending automatic cleanup across Prospero actor boundaries, where resource lifetimes are tied to actor lifetimes in supervision trees
- **Hardware-Accelerated Cleanup**: Using memory protection keys for instant arena cleanup
- **Formal Verification**: Proving resource safety properties at compile time through coeffect analysis
- **GPU Resource Management**: Extending RAII to GPU memory and kernels, informed by the Inet compilation path

## From Frosty to Composer

The design exploration in this entry asked the right question: how do you compile Clef's async computation expressions to native code without a runtime? The answer turned out to be simpler and more principled than a dedicated library.

Delimited continuations give Composer explicit control over "the rest of the computation" at each suspension point. True RAII gives the compiler automatic resource management at scope boundaries. Platform-aware compilation routes I/O to the right kernel interface (IOCP, io_uring, kqueue). Prospero/Olivier provides structured concurrency with supervision-based cancellation. Together, these give developers everything they need:

- **Automatic resource management** without runtime support
- **Deterministic cleanup** without explicit disposal interfaces
- **Safe async programming** with compile-time guarantees
- **Platform optimization** while maintaining safety
- **Familiar Clef syntax** with nothing hidden

The developer writes `async { }` and gets all of this for free. No new CE to learn. No library to import. No `coldTask` vs `cancellableTask` vs `poolingValueTask` decision tree. Just `async { }`, compiled to native code that is correct by construction.

Clef developers coming from .NET will ask "where's Task?" The answer: there is no Task. The continuation is the value. The state machine lives on the stack. Resources are tracked by the compiler and cleaned up at scope exit. Everything IcedTasks exists to fix - hot-start semantics, cancellation token threading, ValueTask footguns, async/Task bridging - are artifacts of the .NET runtime that simply don't exist in Fidelity's compilation model.

Frosty was never a library waiting to be built. It was always a description of what Composer does when it sees `async { }`.
