---
title: "Inferring Memory Lifetimes"
linkTitle: "Inferring Memory Lifetimes"
description: "A design evolution from explicit to inferred memory lifetimes"
date: 2026-01-14T10:00:00+06:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2026-01-14
  original_url: "https://speakez.tech/blog/inferring-memory-lifetimes/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

Most Clef developers take type inference for granted. You write `let x = 5` and the compiler knows it's an integer. You don't annotate every binding; you let the compiler figure out what it can from context. This small convenience accumulates into something significant: code that expresses intent without drowning in declarations.

From early days we held a vision of bringing this notion to Fidelity framework's memory management story. Early explicit arena-passing code we'd written was correct, but it felt like a poor fit compared to what we knew of the idiomatic Clef design-time experience. Our early experiments had too much overhead that existed only to satisfy the compiler.

Our previous discussions, from the foundational ["Memory Management by Choice"](/docs/design/native-memory-management/) to the technical depth of ["ByRef Resolved"](/docs/design/byref-resolved/), established a clear architectural vision. Developers should be able to engage with memory concerns at the level appropriate to their needs, from fully automatic to fully explicit. What we haven't fully articulated until now is the **unifying principle** that makes this vision coherent.

That principle emerged while implementing Arena allocation for the Fidelity framework: **lifetime management should work like type inference**.

## Learning From Those Who Came Before

Fidelity doesn't exist in a vacuum. We've studied OCaml's representation strategies, Rust's ownership model, and even the hard-won lessons from decades of C/C++ systems programming. Each offers wisdom about what to do and what *not* to do.

**From OCaml**, we learned that types can carry runtime representation information without sacrificing expressiveness. The ML family demonstrated that a rich type system doesn't have to mean verbose annotations.

**From Rust**, we learned that lifetimes can be tracked statically, eliminating entire classes of memory bugs. But we also observed the cognitive cost: Rust's explicit lifetime annotations, while powerful, demand attention at every function boundary. For systems code this is often appropriate; for application code it can feel like ceremony.

**From C/C++**, we learned what happens when memory layout decisions are deferred to "lower in the compiler" or left implicit. Subtle bugs emerge: use-after-free, buffer overflows, iterator invalidation. The type system doesn't encode enough information about memory behavior.

The insight that emerged: memory layout should be a **type-level concern**, not something resolved in lower compiler strata where it becomes invisible to analysis and tooling.

## The Innovation Budget

Every language feature consumes what might be called an "innovation budget," the cognitive cost developers must pay to use it effectively. Rust's lifetime annotations are powerful but expensive; they demand significant mental investment. Garbage-collected languages like C# and Java spend their budget differently, hiding memory entirely but paying with unpredictable pauses and bloated memory footprints.

Fidelity's approach is to minimize the innovation budget for the common case while preserving full power for those who need it. Type inference already proved this is possible: most developers write Clef without thinking about types, yet the type system is there when precision matters.

Memory management can follow the same pattern.

## The Design Problem

Our sample applications presented an instructive challenge. The simplest "Hello World" worked fine; static strings live forever. But the moment we added user input:

```fsharp
let hello () =
    Console.write "Enter your name: "
    let name = Console.readln ()  // Where does this string live?
    Console.writeln $"Hello, {name}!"
```

We encountered the byref problem head-on. The `readln` function allocates a buffer on its stack frame, reads input, and returns a string. But that string contains a pointer to stack memory that becomes invalid the moment `readln` returns.

Our initial solution was architecturally sound but syntactically heavy:

```fsharp
let hello (arena: byref<Arena<'lifetime>>) =
    Console.write "Enter your name: "
    let name = Console.readlnFrom &arena
    Console.writeln $"Hello, {name}!"

[<EntryPoint>]
let main argv =
    let arenaMem = NativePtr.stackalloc<byte> 4096
    let mutable arena = Arena.fromPointer (NativePtr.toNativeInt arenaMem) 4096
    hello &arena
    0
```

This works correctly. The arena is created on `main`'s stack, passed by reference to `hello`, and `readlnFrom` allocates from that arena. The string survives because the arena survives. The lifetime parameter `'lifetime` tracks this at the type level.

But looking at this code, something felt wrong. This is Clef, a concurrent language celebrated for its elegance and expressiveness. Why does memory management demand such ceremony?

## Putting the Machine Back in CAML

The "ML" in OCaml and F# stands for "Meta Language," but the "CAM" (Categorical Abstract Machine) is often forgotten. The original vision included the *machine*, the concrete representation of computation. Over time, managed runtimes abstracted this away entirely.

Fidelity reclaims the machine. The key insight is that **memory layout can be a type-carrying quotation**: the type system itself encodes how values are represented in memory. This isn't just architectural elegance; it's mechanical efficiency.

Consider what happens when memory layout decisions are pushed to lower compiler strata:

- The type system can't reason about representation
- Optimizations become heuristics rather than guarantees
- Debug information loses connection to source-level concepts
- Subtle bugs emerge in corners the type checker never sees

By making memory layout a type-level concern, Fidelity ensures that:

- The compiler can verify layout correctness
- Optimizations are provable, not hopeful
- Tools can introspect representation through types
- Errors surface early, as type errors, not runtime crashes

The Arena type exemplifies this. Its layout is `NTUCompound(3)` - three platform words containing Base pointer, Capacity, and Position. This isn't an implementation detail hidden in code generation; it's declared in the type system where it can be verified, optimized, and inspected.

## The Moment of Recognition

The insight came from a simple observation. Consider how Clef handles types:

```fsharp
// You CAN write this
let processDocument (doc: Document) : ProcessedResult =
    let words: string[] = doc.Text.Split(' ')
    let count: int = words.Length
    { WordCount = count; Keywords = extractKeywords words }

// You USUALLY write this
let processDocument doc =
    let words = doc.Text.Split(' ')
    let count = words.Length
    { WordCount = count; Keywords = extractKeywords words }
```

Both versions compile to identical code. The second is idiomatic: types are there when you need precision, inferred when you don't. The compiler does the bookkeeping.

Now look at our memory management code again:

```fsharp
// What we're writing (explicit)
let hello (arena: byref<Arena<'lifetime>>) =
    let name = Console.readlnFrom &arena
    ...

// What we WANT to write (inferred)
let hello () =
    let name = Console.readln ()
    ...
```

The parallel is exact. In both cases, we're annotating something the compiler could determine from context. Type annotations state what the compiler can infer from usage. Lifetime annotations state what the compiler can infer from data flow.

**The same principle that made Clef pleasant to use for types should apply to lifetimes.**

## From Insight to Architecture

This realization reframes our entire three-level memory management design (see "Memory Management by Choice"). We originally described the levels as:

- **Level 1**: Default (compiler-generated layouts)
- **Level 2**: Hints (developer guidance)
- **Level 3**: Explicit (full control)

But with the lifetime inference lens, these levels correspond directly to how Clef handles types:

| Aspect | Types in Clef | Lifetimes in Fidelity |
|--------|-------------|----------------------|
| Level 1 | `let x = 5` (inferred) | `let name = readln()` (inferred) |
| Level 2 | `let x: int = ...` (guided) | `arena { let! name = ... }` (scoped) |
| Level 3 | `[<Struct>] type X = ...` (explicit) | `Arena<'lifetime> byref` (declared) |

The levels aren't just about how much control developers have; they're about **where inference stops and declaration begins**.

## What Each Level Requires

### Level 3: Declaration (Current)

This is what we have today. Every lifetime is declared:

```fsharp
let hello (arena: byref<Arena<'lifetime>>) =
    let name = Console.readlnFrom &arena
    Console.writeln $"Hello, {name}!"
```

The compiler's job is verification: ensure the declared lifetimes are consistent, that `'lifetime` flows correctly through the code, that no pointers escape their declared scope.

This is analogous to requiring type annotations on every binding. Correct, but ceremonious.

### Level 2: Bounded Inference (Next)

The developer provides scope boundaries; the compiler infers within those bounds:

```fsharp
let hello () = arena {
    Console.write "Enter your name: "
    let! name = Console.readln ()
    Console.writeln $"Hello, {name}!"
}
```

The `arena { }` computation expression marks the lifetime boundary. The `let!` syntax signals "this needs allocation from the arena." The compiler handles parameter threading, byref passing, and cleanup.

Or with attributes:

```fsharp
[<UseArena(Size = 4096)>]
let hello () =
    Console.write "Enter your name: "
    let name = Console.readln ()
    Console.writeln $"Hello, {name}!"
```

The attribute provides hints. The compiler transforms the signature and call sites.

This is analogous to annotating function signatures while leaving local bindings inferred, a common and comfortable pattern in Clef.

### Level 1: Full Inference (Goal)

The developer writes pure business logic:

```fsharp
let hello () =
    Console.write "Enter your name: "
    let name = Console.readln ()
    Console.writeln $"Hello, {name}!"
```

The compiler performs escape analysis, determines that `readln`'s result escapes its stack frame, infers the minimum required lifetime (`hello`'s scope), and generates appropriate arena code.

This is analogous to how Clef handles most code today: inference handles the common case, with annotations available when needed.

## The Inference Algorithm (Sketch)

For Level 1 to work, the compiler needs several capabilities:

**Escape Analysis**: Track where values "flow" to determine if they escape their creating scope.

```fsharp
let readln () =
    let buffer = NativePtr.stackalloc<byte> 256
    let len = readLineInto buffer 256
    NativeStr.fromPointer buffer len  // Returns reference to stack!
```

The compiler must recognize that the returned string contains a pointer to `buffer`, which lives on `readln`'s stack. This value "escapes" its creating scope.

**Lifetime Requirements**: Determine the minimum lifetime needed for escaped values.

```fsharp
let processInput () =
    let name = Console.readln ()  // Created here
    let greeting = $"Hello, {name}!"  // Used here
    Console.writeln greeting  // Last use
    // Lifetime: processInput's scope
```

The string must live from creation until last use. The compiler infers this scope.

**Arena Injection**: Transform function signatures to accept arena parameters.

```fsharp
// Source
let hello () =
    let name = Console.readln ()
    ...

// Transformed (internal representation)
let hello (arena: byref<Arena<'auto>>) =
    let name = Console.readlnFrom &arena
    ...
```

Call sites are similarly transformed to provide arenas.

## The IDE Experience

Crucially, inference doesn't mean invisible. Just as Clef IDEs show inferred types in tooltips, Fidelity IDEs would show inferred lifetimes:

```
┌─────────────────────────────────────────────┐
│ Function: hello                             │
│ Inferred arena: 4KB on caller's stack       │
│ Lifetime scope: hello                       │
│ Allocations: 1 string (max 256 bytes)       │
│                                             │
│ [Show explicit form] [Adjust arena size]    │
└─────────────────────────────────────────────┘
```

The information exists. It's queryable. It's just not required in source code.

This follows the Clef philosophy: the compiler knows things the source doesn't state. Good tooling surfaces that knowledge without demanding it be written down.

## Contrast With Alternatives

| Approach | Explicit? | Memory Determinism | Innovation Budget | Pitfalls |
|----------|-----------|-------------------|-------------------|----------|
| **Rust** | All lifetimes declared | Deterministic | High (every boundary) | Learning curve |
| **C/C++** | Manual, unchecked | Programmer-dependent | Low syntax, high bugs | Subtle memory errors |
| **C#/Java** | Hidden entirely | GC pauses | Low initially | Performance cliffs |
| **Fidelity L3** | All declared | Deterministic | Moderate | Ceremony |
| **Fidelity L1** | Inferred | Deterministic | Low | None (verified) |

### Rust: Everything Explicit

Rust requires lifetime annotations:

```rust
fn process_input<'a>(arena: &'a Arena) -> &'a str {
    let name = arena.alloc_str(&read_line());
    name
}
```

Every function that handles references must declare lifetimes. This forces developers to think about memory at every boundary. The approach is valuable for systems code but burdensome for application code.

### Managed Runtimes: Everything Hidden

C# and Java hide all memory management:

```csharp
string ProcessInput() {
    string name = Console.ReadLine();
    return name;  // GC handles it
}
```

Simple to write, but the abstraction leaks under pressure. GC pauses, memory bloat, and unpredictable performance plague systems that need determinism.

### C/C++: Trust the Programmer

```c
char* read_input() {
    char buffer[256];
    fgets(buffer, 256, stdin);
    return buffer;  // Bug: returning stack pointer!
}
```

The type system doesn't prevent this. The bug might not manifest until production, under specific conditions. This is what happens when memory layout is handled "below" the type system.

### Fidelity: Inference With Escape Hatches

Fidelity aims for inference as the default with explicit control available:

```fsharp
// Level 1: Inferred (default)
let processInput () =
    let name = Console.readln ()
    name

// Level 3: Explicit (when needed)
let processInput (arena: byref<Arena<'lifetime>>) =
    let name = Console.readlnFrom &arena
    name
```

The same semantics, different levels of visibility. Developers choose based on need.

## Implementation Status

We've completed Level 3. The Arena intrinsic is fully functional:

- Type constructor with measure parameter for lifetime tracking
- Layout specification: `NTUCompound(3)` - Base pointer, Capacity, Position
- Full operation set: `fromPointer`, `alloc`, `allocAligned`, `remaining`, `reset`
- Correct handling of byref parameter passing
- Integration with CCS type system

The Arena type was originally designed in BAREWire for binary serialization scenarios but was elevated to a CCS intrinsic when we recognized its broader applicability. This is the standing art pattern in action: a well-designed capability finding new application.

The path to Level 2 is clear:

- Computation expression builder for `arena { }`
- Attribute processing for `[<UseArena>]`
- Call-site transformation passes

Level 1 requires the most sophisticated compiler work:

- Escape analysis pass in the PSG
- Lifetime inference algorithm
- Hidden parameter injection

But the architecture supports this evolution. Each level builds on the previous, adding inference while preserving the ability to be explicit.

## The Broader Principle

This isn't just about Arena. The lifetime inference principle applies throughout Fidelity:

**Capabilities**: Must developers declare read/write permissions, or can the compiler infer them from usage patterns?

**Regions**: Are memory regions explicit annotations, or inferred from data flow analysis?

**Actor Arenas**: Are actor memory sizes declared in configuration, or determined from message pattern analysis?

Each follows the same design question: what can the compiler determine that developers currently must state?

## Connection to RAII and Actor Boundaries

This lifetime inference principle connects directly to our RAII-based actor architecture (see [RAII in Olivier and Prospero](https://speakez.tech/blog/raii-in-olivier-and-prospero/)). In that design, each actor owns an arena that lives exactly as long as the actor does. The actor boundary provides a natural lifetime scope.

With lifetime inference, this becomes even more powerful. Consider:

```fsharp
// Actor code - what the developer writes
type DataProcessor() =
    inherit Actor<DataMessage>()

    override this.Receive message =
        match message with
        | Process data ->
            let buffer = readInput ()  // Where does this live?
            let result = transform buffer
            reply result
```

Without inference, the developer must either:
- Explicitly pass the actor's arena to `readInput`
- Use attributes to mark the method as arena-using
- Configure arena sizes in actor configuration

With Level 1 inference, the compiler recognizes:
- `readInput` returns data that escapes its stack frame
- The data is used within `Receive`, which is bounded by message processing
- The natural lifetime scope is the actor's arena

The actor boundary becomes the implicit lifetime scope for allocations that outlive individual function calls but don't outlive the actor. This is the same insight as our RAII architecture, that actors provide natural resource boundaries, but applied to inference rather than explicit management.

This also connects to delimited continuations in Prospero (see [Delimited Continuations: Fidelity's Turning Point](/docs/design/delimited-continuations/)). When an actor's execution is suspended (awaiting a message, yielding to scheduler), the continuation boundary aligns with potential arena compaction points. Memory can be reorganized at message boundaries precisely because no byrefs can span those boundaries, a guarantee the lifetime system enforces.

## Standing Art, Applied

The capabilities we're describing aren't novel in isolation. Type inference has existed for decades. Escape analysis is well-studied. Arena allocation is a known pattern. Even lifetime tracking at the type level isn't new; Rust proved it viable.

What's novel is the *combination* and *application*: bringing these techniques together in a way that preserves Clef's expressiveness while providing the memory determinism that native compilation demands. The innovation isn't in any single piece, but in recognizing how standing art from different traditions can compose into something greater.

This is the Fidelity philosophy: respect what works, learn from what doesn't, and invest the innovation budget where it creates genuine new capability rather than reinventing established patterns.

## Conclusion

The process we embarked on from the first realizations that led to "ByRef Resolved" to this concrete implementation in Composer has been one of pattern recognition. We solved the byref problem with explicit arena management. We built the type system infrastructure for lifetime tracking. We implemented the intrinsics and operations.

But the deeper insight, that lifetime management should work like type inference, is an elegant reframing we thought worth sharing here. It's not just an implementation technique; it's a design principle that unifies our three-level memory management model.

Clef developers already know the power of inference. Type inference transformed programming from ceremony to expression. We believe lifetime inference can do the same for systems programming, maintaining the mechanical efficiency and memory safety that native compilation requires while preserving the expressiveness that makes Clef a joy to use.

The explicit form isn't going away; it's the foundation everything else builds on. But it should be the escape hatch, not the default. Developers should write Clef that looks like Clef, and trust the compiler to handle the memory concerns that can be determined from context.

That's what "Memory Management by Choice" means in its fullest form: choosing not just when to optimize, but when to even think about memory at all.

---
*This article continues our exploration of native Clef compilation. See ["Memory Management by Choice"](/docs/design/native-memory-management/) for the foundational three-level design, ["ByRef Resolved"](/docs/design/byref-resolved/) for the technical solution to .NET's byref restrictions, [RAII in Olivier and Prospero](https://speakez.tech/blog/raii-in-olivier-and-prospero/) for how these lifetime principles extend to actor-based systems, and ["Delimited Continuations: Fidelity's Turning Point"](/docs/design/delimited-continuations/) for the connection between continuation boundaries and memory management.*
