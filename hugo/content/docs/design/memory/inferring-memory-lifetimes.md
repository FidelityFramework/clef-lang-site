---
title: "Inferring Memory Lifetimes"
linkTitle: "Inferring Memory Lifetimes"
description: "A design evolution from explicit to inferred memory lifetimes"
date: 2026-01-14T10:00:00+06:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2026-01-14
  migration_date: 2026-02-15
---

Type inference is routine in Clef. Writing `let x = 5` lets the compiler determine the integer type from context, with no annotation on every binding. The compiler resolves what it can from usage, and code expresses intent without carrying redundant declarations.

From early on we intended to extend this treatment to the Fidelity framework's memory management. The explicit arena-passing code we first wrote was correct, but it sat poorly against the idiomatic Clef design-time experience. Those early experiments carried overhead that existed only to satisfy the compiler.

Our earlier discussions, from the foundational ["Memory Management by Choice"](/docs/design/memory/native-memory-management/) to the technical treatment in ["ByRef Resolved"](/docs/design/types/byref-resolved/), established the architectural direction. Developers engage with memory concerns at the level appropriate to their needs, from fully automatic to fully explicit. The unifying principle behind this direction is one we set out here.

That principle emerged while implementing Arena allocation for the Fidelity framework: **lifetime management should work like type inference**.

## Prior Art

We have studied OCaml's representation strategies, Rust's ownership model, and the lessons from decades of C/C++ systems programming. Each informs both what to do and what to avoid.

**From OCaml**, we take that types can carry runtime representation information without sacrificing expressiveness. The ML family showed that a rich type system need not mean verbose annotations.

**From Rust**, we take that lifetimes can be tracked statically, eliminating classes of memory bugs. The approach carries a cognitive cost: Rust's explicit lifetime annotations demand attention at every function boundary. For systems code this is often appropriate; for application code it reads as ceremony.

**From C/C++**, we take the consequences of deferring memory layout decisions to "lower in the compiler" or leaving them implicit. Use-after-free, buffer overflows, and iterator invalidation emerge because the type system does not encode enough information about memory behavior.

The direction we draw from this: memory layout should be a **type-level concern**, not something resolved in lower compiler strata where it becomes invisible to analysis and tooling.

## The Innovation Budget

Every language feature consumes an "innovation budget," the cognitive cost developers pay to use it effectively. Rust's lifetime annotations are expressive but expensive; they demand mental investment at each boundary. Garbage-collected languages like C# and Java spend their budget differently, hiding memory entirely and paying with unpredictable pauses and larger memory footprints.

Fidelity minimizes the innovation budget for the common case while preserving full control for those who need it. Type inference already demonstrates that this is possible: most developers write Clef without reasoning about types, and the type system is available when precision matters.

Memory management can follow the same pattern.

## The Design Problem

Our sample applications surfaced the core problem. A "Hello World" with only static strings poses no lifetime question; static strings live for the duration of the program. Adding user input changes that:

```fsharp
let hello () =
    Console.write "Enter your name: "
    let name = Console.readln ()  // Where does this string live?
    Console.writeln $"Hello, {name}!"
```

This is the byref problem directly. The `readln` function allocates a buffer on its stack frame, reads input, and returns a string. That string contains a pointer to stack memory that becomes invalid the moment `readln` returns.

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

The cost is in the surface syntax. Clef is a concurrent language built for expressiveness, and this level of memory-management ceremony works against that. Reducing it is the design problem this document addresses.

## Reclaiming the Machine in CAML

The "ML" in OCaml and F# stands for "Meta Language," and the "CAM" (Categorical Abstract Machine) is often overlooked. The original design included the *machine*, the concrete representation of computation. Managed runtimes abstracted this away over time.

Our Fidelity framework reclaims the machine. Memory layout is a type-carrying quotation: the type system encodes how values are represented in memory. This gives the compiler mechanical efficiency to work with.

When memory layout decisions are pushed to lower compiler strata, several consequences follow:

- The type system can't reason about representation
- Optimizations become heuristics rather than guarantees
- Debug information loses connection to source-level concepts
- Subtle bugs emerge in corners the type checker never sees

By making memory layout a type-level concern, Fidelity ensures that:

- The compiler can verify layout correctness
- Optimizations rest on structure the type system tracks rather than on heuristics
- Tools can introspect representation through types
- Errors surface early, as type errors, not runtime crashes

The Arena type is one example. Its layout is `NTUCompound(3)`: three platform words holding Base pointer, Capacity, and Position. This is not an implementation detail hidden in code generation; it is declared in the type system where it can be verified, optimized, and inspected.

## Recognizing the Parallel

The design followed from one observation about how Clef handles types:

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

Both versions compile to identical code. The second is idiomatic: types are stated where precision matters and inferred otherwise. The compiler does the bookkeeping.

The same distinction appears in the memory management code:

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

The parallel is exact. In both cases, the annotation states something the compiler could determine from context. Type annotations state what the compiler can infer from usage. Lifetime annotations state what the compiler can infer from data flow.

**The principle that governs type inference in Clef should extend to lifetimes.**

## Mapping the Principle to the Three Levels

This reframes our three-level memory management design (see "Memory Management by Choice"). We originally described the levels as:

- **Level 1**: Default (compiler-generated layouts)
- **Level 2**: Hints (developer guidance)
- **Level 3**: Explicit (full control)

But with the lifetime inference lens, these levels correspond directly to how Clef handles types:

| Aspect | Types in Clef | Lifetimes in Fidelity |
|--------|-------------|----------------------|
| Level 1 | `let x = 5` (inferred) | `let name = readln()` (inferred) |
| Level 2 | `let x: int = ...` (guided) | `arena { let! name = ... }` (scoped) |
| Level 3 | `[<Struct>] type X = ...` (explicit) | `Arena<'lifetime> byref` (declared) |

The levels mark **where inference stops and declaration begins**, not only how much control the developer holds.

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

Inference does not mean the information is hidden. Just as Clef IDEs show inferred types in tooltips, Fidelity IDEs are planned to show inferred lifetimes:

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

The information exists and is queryable; it is not required in source code.

This follows the Clef design: the compiler holds facts the source does not state, and tooling surfaces those facts without requiring them to be written down.

## Contrast With Alternatives

| Approach | Explicit? | Memory Determinism | Innovation Budget | Pitfalls |
|----------|-----------|-------------------|-------------------|----------|
| **Rust** | All lifetimes declared | Deterministic | High (every boundary) | Learning curve |
| **C/C++** | Manual, unchecked | Programmer-dependent | Low syntax, high bugs | Subtle memory errors |
| **C#/Java** | Hidden entirely | GC pauses | Low initially | Performance cliffs |
| **Fidelity L3** | All declared | Deterministic | Moderate | Ceremony |
| **Fidelity L1** | Inferred | Deterministic | Low | None (verified) |

### Rust

Rust requires lifetime annotations:

```rust
fn process_input<'a>(arena: &'a Arena) -> &'a str {
    let name = arena.alloc_str(&read_line());
    name
}
```

Every function that handles references must declare lifetimes, which forces developers to reason about memory at every boundary. The approach suits systems code and burdens application code.

### Managed Runtimes

C# and Java hide all memory management:

```csharp
string ProcessInput() {
    string name = Console.ReadLine();
    return name;  // GC handles it
}
```

Simple to write, and the abstraction leaks under load. GC pauses, memory bloat, and unpredictable performance affect systems that require determinism.

### C and C++

```c
char* read_input() {
    char buffer[256];
    fgets(buffer, 256, stdin);
    return buffer;  // Bug: returning stack pointer!
}
```

The type system does not prevent this. The bug may not manifest until production, under specific conditions. This is the result of handling memory layout "below" the type system.

### Fidelity

Fidelity is designed for inference as the default with explicit control available:

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

Both forms carry the same semantics at different levels of visibility, and the developer chooses based on need.

## Implementation Status

We've completed Level 3. The Arena intrinsic is fully functional:

- Type constructor with measure parameter for lifetime tracking
- Layout specification: `NTUCompound(3)`, holding Base pointer, Capacity, Position
- Full operation set: `fromPointer`, `alloc`, `allocAligned`, `remaining`, `reset`
- Correct handling of byref parameter passing
- Integration with CCS type system

The Arena type was originally designed in BAREWire for binary serialization and was elevated to a CCS intrinsic once its broader applicability became clear, an existing capability applied in a new context.

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

The lifetime inference principle extends beyond Arena, throughout Fidelity:

**Capabilities**: Must developers declare read/write permissions, or can the compiler infer them from usage patterns?

**Regions**: Are memory regions explicit annotations, or inferred from data flow analysis?

**Actor Arenas**: Are actor memory sizes declared in configuration, or determined from message pattern analysis?

Each follows the same design question: what can the compiler determine that developers currently must state?

## Connection to RAII and Actor Boundaries

This lifetime inference principle connects directly to our RAII-based actor architecture (see [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/)). In that design, each actor owns an arena that lives exactly as long as the actor does. The actor boundary provides a natural lifetime scope.

Lifetime inference extends this further:

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

This also connects to delimited continuations in Prospero (see [Delimited Continuations: Fidelity's Turning Point](/docs/design/concurrency/delimited-continuations/)). When an actor's execution is suspended (awaiting a message, yielding to scheduler), the continuation boundary aligns with potential arena compaction points. Memory can be reorganized at message boundaries precisely because no byrefs can span those boundaries, a guarantee the lifetime system enforces.

## Standing Art

The capabilities we're describing aren't novel in isolation. Type inference has existed for decades. Escape analysis is well-studied. Arena allocation is a known pattern. Even lifetime tracking at the type level isn't new; Rust proved it viable.

What is novel is the *combination* and *application*: bringing these techniques together in a way that preserves Clef's expressiveness while providing the memory determinism native compilation requires. The contribution is in composing standing art from different traditions, not in any single piece.

This is the Fidelity philosophy: respect what works, learn from what doesn't, and invest the innovation budget where it creates genuine new capability rather than reinventing established patterns.

## Conclusion

The path from the first realizations behind "ByRef Resolved" to the current implementation in Composer has been one of pattern recognition. We solved the byref problem with explicit arena management, built the type system infrastructure for lifetime tracking, and implemented the intrinsics and operations.

The reframing this document sets out is that lifetime management should work like type inference. That principle unifies our three-level memory management model.

Type inference is already familiar to Clef developers, and it reduces the annotation cost of working with types. We are designing lifetime inference to do the same for systems programming, maintaining the mechanical efficiency and memory safety native compilation requires while preserving Clef's expressiveness.

The explicit form remains the foundation everything else builds on. It is the escape hatch rather than the default. Developers write Clef that looks like Clef, and the compiler handles the memory concerns that can be determined from context.

This is the fuller sense of "Memory Management by Choice": the choice covers both when to optimize and, one level up, whether memory enters the developer's thinking at all.

---
*This article continues our exploration of native Clef compilation. See ["Memory Management by Choice"](/docs/design/memory/native-memory-management/) for the foundational three-level design, ["ByRef Resolved"](/docs/design/types/byref-resolved/) for the technical solution to .NET's byref restrictions, [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/) for how these lifetime principles extend to actor-based systems, and ["Delimited Continuations: Fidelity's Turning Point"](/docs/design/concurrency/delimited-continuations/) for the connection between continuation boundaries and memory management.*
