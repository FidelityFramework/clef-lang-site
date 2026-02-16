---
title: "Delimited Continuations: Fidelity's Turning Point"
linkTitle: "Delimited Continuations"
description: "How Continuation Passing Style Unifies Clef Async, Actors, and Native Compilation"
date: 2025-12-14T10:00:00-05:00
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
params:
  originally_published: 2025-12-14
  original_url: "https://speakez.tech/blog/delimited-continuations-fidelitys-turning-point/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

Every compiler must eventually answer a fundamental question: how do you represent "the rest of the computation"? In imperative languages, this question rarely surfaces explicitly. The call stack handles it through it's own instrumentation, and programmers rarely think about what happens after the current statement. But in languages that embrace computation expressions as Clef does, this question moves from implicit mechanism to explicit design decision.

For the Fidelity framework, this question became a turning point. The realization that delimited continuations form the connective tissue between Clef's async expressions, the actor model, and our native compilation strategy transformed how we approach the entire compiler architecture. What began as an implementation detail became a significant organizing principle.

---

Special thanks to [Paul Snively](https://www.youtube.com/watch?v=Cq_IstGhUv4) whose polyglot perspective on continuations, effects, and formal verification helped crystallize this synthesis of ideas through a series of spirited dialectic discussions.

---

## The Shape of "What Comes Next"

Consider a simple async expression:

```fsharp
let fetchAndProcess url = async {
    let! response = Http.getAsync url
    let! data = parseResponse response
    return summarize data
}
```

Each `let!` represents a suspension point: a place where computation pauses, waits for an external result, and then continues. But what exactly does "continue" mean? It means executing everything that follows: parsing the response, summarizing the data, returning the result. That "everything that follows" is the continuation.

Delimited continuations make this notion precise. The `reset` operator establishes a boundary, and `shift` captures everything up to that boundary:

```fsharp
// Conceptually, async { } becomes:
reset (fun () ->
    shift (fun k1 ->  // k1 = "parse response, then summarize, then return"
        Http.getAsync url |> continueWith (fun response ->
            shift (fun k2 ->  // k2 = "summarize, then return"
                parseResponse response |> continueWith (fun data ->
                    k2 (summarize data)
                )
            )
        )
    )
)
```

The transformation looks verbose, but it reveals something profound:

> async expressions are syntax sugar over delimited continuations.

The `let!` keyword hides the explicit continuation capture that happens underneath. As we explored in [The Full Frosty Experience](https://speakez.tech/blog/the-full-frosty-experience/), this transformation enables Clef's elegant async syntax to compile to native code with deterministic memory management.

## Actors as Sugared Continuations

The actor model presents a different face of the same concept. An actor receives a message, processes it, and waits for the next message. That "waits for the next message" is itself a continuation: the computation that will execute when the next message arrives.

Consider the relationship between Clef's `MailboxProcessor` and an async workflow:

```fsharp
// A basic actor loop
let counterActor = MailboxProcessor.Start(fun inbox ->
    let rec loop count = async {
        let! msg = inbox.Receive()
        match msg with
        | Increment -> return! loop (count + 1)
        | GetCount reply ->
            reply.Reply count
            return! loop count
    }
    loop 0
)
```

The `let! msg = inbox.Receive()` line captures a continuation: everything that happens after the message arrives. The `return! loop count` tail call represents another continuation capture: suspend this actor, prepare to receive the next message, and when it arrives, execute the loop body again.

This is why [our Unified Actor Architecture](https://speakez.tech/blog/unified-actor-architecture/) can bridge Fidelity's native Olivier actors with Cloudflare's Durable Objects. Despite radically different runtimes, both are executing the same continuation-based pattern. The actor abstraction is syntactic sugar over delimited continuations with message-driven resumption.

The deeper insight, explored in [Actors Take Center Stage](https://speakez.tech/blog/actors-take-center-stage/), is that supervision hierarchies are themselves continuation management structures. When a supervisor spawns a child, it captures a continuation for handling that child's failure. When the child fails, the supervisor's continuation resumes with a decision: restart, escalate, or terminate. Erlang made "let it crash" famous, but in the Fidelity framework it is actually "capture the failure continuation" by another name.

## Computation Expressions: The Unifying Syntax

Clef's computation expression mechanism provides the surface syntax that makes all this manageable. As [The DCont/Inet Duality](https://speakez.tech/blog/dcont-inet-duality/) explores, computation expressions decompose into two fundamental patterns: sequential effects (DCont) and parallel pure computation (Inet). The CE builder determines which pattern applies.

```fsharp
// Sequential: each step depends on previous (DCont pattern)
let sequential = async {
    let! a = fetchData()
    let! b = transform a
    return combine a b
}

// Parallel: no dependencies (Inet pattern)
let parallel = query {
    for item in items do
    where (isValid item)
    select (transform item)
}
```

The computation expression builder translates `let!` into `Bind` calls:

```fsharp
async.Bind(fetchData(), fun a ->
    async.Bind(transform a, fun b ->
        async.Return(combine a b)))
```

Each `Bind` call is a continuation capture. The second argument to `Bind` (namely `fun a -> ...`) is literally "what to do with the result." The builder orchestrates how these continuations compose, whether they sequence (like async) or fan out (like query).

This mechanism is central to Fidelity's compilation strategy. The Composer compiler recognizes computation expression patterns and routes them through appropriate MLIR dialects. Sequential patterns flow through DCont; parallel patterns flow through Inet. The computation expression syntax that developers write maps to optimized native execution patterns.

## Frosty: Platform-Aware Continuation Compilation

The Frosty subsystem, detailed in [The Full Frosty Experience](https://speakez.tech/blog/the-full-frosty-experience/), handles the challenge of compiling Clef's async expressions to efficient native code across different platforms. Frosty must express continuation-passing style in ways that survive through to code generation. This is not the typical CPS transformation that compilers perform internally, but an explicit articulation of continuation structure that the Composer compiler recognizes and translates to MLIR.

The approach combines delimited continuations with true RAII principles. Where .NET async relies on heap-allocated Tasks and thread pool scheduling, Frosty compiles to stack-based state machines with deterministic resource cleanup:

```fsharp
// What developers write - familiar Clef async
let processFile() = async {
    let! handle = File.openAsync "data.txt"
    let buffer = stackalloc<byte> 4096
    let! bytesRead = handle.readAsync buffer
    return buffer.Slice(0, bytesRead)
}

// What Frosty compiles to:
// - Stack frame for continuation state (0 heap bytes)
// - Automatic resource cleanup at scope boundaries
// - Platform-specific I/O (IOCP on Windows, io_uring on Linux)
```

The types themselves encode CPS structure. Frosty's async primitives make continuation capture explicit in their signatures, and the Composer compiler recognizes these patterns during type resolution, recording them in the Program Semantic Graph for code generation.

The DCont dialect in MLIR provides the target representation:

```mlir
// Alloy async compiles to DCont operations
dcont.func @fetchAndProcess(%url: !alloy.string) -> !alloy.data {
    %k1 = dcont.shift {
        %response = call @http_get_async(%url)
        dcont.resume %k1 %response
    }

    %k2 = dcont.shift {
        %data = call @parse_response(%response)
        dcont.resume %k2 %data
    }

    %result = call @summarize(%data)
    dcont.reset %result
}
```

Each `dcont.shift` captures "the rest of the computation" at that point. The `dcont.resume` delivers a value to the captured continuation. The `dcont.reset` establishes the boundary. This MLIR representation preserves the continuation structure through optimization passes until final lowering to native code.

## From Type Resolution to Code Generation

The [zipper-based pipeline](https://speakez.tech/blog/baker-a-key-ingredient-to-firefly/) that correlates Clef's typed tree with the Program Semantic Graph becomes essential when compiling continuations. During type resolution, the compiler identifies continuation points (suspension, capture, and boundary markers) and annotates the PSG accordingly. These annotations then guide code generation, determining where to emit `dcont.shift`, `dcont.resume`, and `dcont.reset` operations.

The coherence between type resolution and code generation ensures that continuation annotations align exactly with MLIR emission. Variables captured across suspension points are stack-allocated; boundary scopes map to reset operations; suspension points become shift operations with appropriate resume types. No scope mismatch, no lost context.

This principled front-loading has a significant consequence: the Composer compiler requires far fewer MLIR and LLVM passes than compilers for imperative languages. As Andrew Appel demonstrated in his seminal 1998 paper, [SSA is functional programming](https://www.cs.princeton.edu/~appel/papers/ssafun.pdf): the Static Single-Assignment form at the heart of optimizing compilers is mathematically equivalent to functional programming with lexical scope. When C++ or Rust compile to LLVM, their compilers must *reconstruct* the functional relationships that imperative syntax obscures: analyzing loops, tracking mutations, resolving aliasing. Fidelity's pipeline preserves what those compilers must rediscover. The continuation structure, the type information, the scope boundaries: in our model ***these features survive intact*** from Clef source through the PSG to MLIR. This is the meaning behind the framework's name: fidelity to the original program structure yields faster compilation and more predictable optimization, because MLIR operates on preserved intent rather than speculative reconstructions.

## Behind the Scenes: Managing Complexity

A central design goal is that developers should not need to think about continuation-passing style. The `async { }` syntax, the `actor { }` builder, the standard Clef patterns: these should feel familiar if not 'natural'. The CPS machinery operates invisibly.

Consider what happens when a developer writes a simple actor:

```fsharp
let counter = actor {
    let! msg = receive()
    match msg with
    | Increment -> return! loop (state + 1)
    | GetCount reply ->
        tell reply state
        return! loop state
}
```

What they don't see:
- The `receive()` compiles to a `dcont.shift` that captures the message-handling continuation
- The `return!` compiles to a tail call with continuation transfer
- The `tell` compiles to a non-blocking message send that doesn't capture a continuation
- The entire actor loop compiles to a state machine with explicit continuation slots

The Alloy library handles this translation. When developers import `Alloy.Actors`, they get computation expression builders that produce the right continuation structure. The Composer compiler recognizes these patterns and generates efficient native code.

> This is what we mean by "managing the innovation budget."

Delimited continuations are a sophisticated concept with substantial theoretical depth. But **developers adopting Fidelity *don't need* to understand continuation semantics** to write correct, efficient code. The framework encapsulates that complexity.

## The Turning Point

Why do we call this Fidelity's "turning point"? (other than the obvious pun) Because recognizing the centrality of delimited continuations reframed our entire approach.

Before this recognition, we treated async, actors, and computation expressions as separate features requiring separate compilation strategies. The async builder was one thing; the actor model was another; computation expressions were a third. Each had its own MLIR lowering path, its own optimization considerations, its own edge cases.

After this recognition, we saw unity. Async expressions are delimited continuations with I/O-triggered resumption. Actors are delimited continuations with message-triggered resumption. Computation expressions are syntax sugar for explicit continuation manipulation. All of them will eventually compile through the same DCont dialect, share the same optimization passes, and benefit from similar machine-level continuation representations.

This unification enables:

1. **Shared optimization infrastructure**: Continuation fusion, suspension point elimination, and closure conversion apply uniformly across async, actors, and custom CEs.

2. **Consistent memory model**: As [The Continuation Preservation Paradox](https://speakez.tech/blog/the-continuation-preservation-paradox/) explores, continuations can be stack-allocated when their scope is bounded, eliminating heap allocation regardless of whether they originate from async or actors.

3. **Compositional semantics**: An async operation inside an actor message handler composes naturally because both are continuations. No impedance mismatch, no special cases.

4. **Principled extension**: New computation expression builders automatically benefit from the continuation compilation infrastructure. Library authors can create domain-specific patterns that compile efficiently.

5. **Hardware diversity**: The continuation model maps naturally to dataflow architectures beyond Von Neumann machines. As explored in [Hyping Hypergraphs](https://speakez.tech/blog/hyping-hypergraphs/) and [Advent of Neuromorphic AI](https://speakez.tech/blog/advent-of-neuromorphic-ai/), the explicit control flow boundaries that delimited continuations provide align with how CGRAs, neuromorphic processors, and other emerging architectures express computation: as graphs of operations with explicit data dependencies rather than sequential instruction streams.

## Continuations and Hardware Targets

The DCont preservation strategy extends to hardware targeting. As noted in [The Continuation Preservation Paradox](https://speakez.tech/blog/the-continuation-preservation-paradox/), WebAssembly's Stack Switching proposal provides first-class support for delimited continuations. When targeting WASM, the Composer compiler can preserve continuation structure all the way to the runtime:

```mlir
// DCont operations map to WASM stack switching
dcont.shift { ... }
  ↓
ssawasm.suspend $continuation_tag
```

For LLVM targets (native x86-64, ARM, RISC-V), continuations compile to efficient state machines:

```mlir
// DCont operations compile to state machine for native targets
dcont.shift { ... }
  ↓
llvm.switch %state, %continuation_blocks
```

The choice between preservation and compilation happens late in the pipeline, based on target capabilities. The PSG carries the continuation structure; the backend decides how to realize it.

Beyond traditional architectures, continuation structure provides a natural fit for post-Von Neumann hardware. Coarse-Grained Reconfigurable Architectures (CGRAs) from companies like SambaNova and NextSilicon express computation as dataflow graphs, and delimited continuations are precisely dataflow graphs with explicit scheduling boundaries. Neuromorphic processors like Intel's Loihi 2 and BrainChip's Akida operate on spike-based event patterns that mirror continuation-triggered resumption. The explicit "what happens next" structure that DCont preserves maps more directly to these architectures than the implicit control flow of imperative code. This positions Fidelity for hardware targets that barely exist today but will define computing's next decade.

## Integration Across the Framework

Delimited continuations touch every part of the Fidelity framework:

**Alloy.Rx** ([AlloyRx: Native Reactivity in Fidelity](https://speakez.tech/blog/alloyrx-native-reactivity-in-fidelity/)) uses continuation capture for subscription callbacks. When an observable emits, it resumes the captured continuation of each subscriber.

**BAREWire** ([Getting the Signal with BAREWire](https://speakez.tech/blog/getting-the-signal-with-barewire/)) leverages continuations for zero-copy message handling. The deserialization callback is a continuation that processes the message without intermediate allocation.

**Olivier actors** use continuations for both message receipt and supervision. A supervisor's failure handler is a continuation captured when the child was spawned.

**Frosty** ([The Full Frosty Experience](https://speakez.tech/blog/the-full-frosty-experience/)) provides platform-aware async through continuation-based I/O. On Windows, IOCP callbacks resume continuations; on Linux, io_uring completions do the same.

This pervasive use of continuations creates architectural coherence. Different framework components speak the same language at the compilation level, enabling cross-cutting optimizations and uniform semantics.

## The Path Forward

The centrality of delimited continuations to Fidelity's architecture has implications for future development:

**Algebraic effects**: The CE-based effect system described in [The DCont/Inet Duality](https://speakez.tech/blog/dcont-inet-duality/) is essentially typed delimited continuations. As we extend Fidelity's effect tracking, the continuation infrastructure is already in place.

**Formal verification**: Continuation semantics are well-studied mathematically. As we pursue F* integration for proof-carrying code, the continuation-based compilation model provides a clean target for verification.

**Novel hardware**: Interaction nets and dataflow architectures benefit from explicit continuation representation. As Fidelity targets post-Von Neumann architectures, the continuation model adapts naturally.

The turning point wasn't just recognizing that continuations unify our existing features; it was recognizing that they provide the foundation for features we haven't built yet.

## Continuations as Compilation Philosophy

The deeper lesson from Fidelity's turning point is architectural: choose unifying abstractions carefully, and let them inform the entire system. Delimited continuations could have remained an implementation detail, hidden from developers and compartmentalized in the compiler. Instead, by recognizing their centrality and designing around them explicitly, we achieved coherence that would otherwise require extensive special-casing.

For developers, this means writing natural Clef code (async expressions, computation expressions, actor behaviors) while benefiting from a unified compilation strategy. For the framework, it means that improvements to continuation handling propagate across all features that use them. For the ecosystem, it demonstrates that programming language abstractions can serve as practical compilation targets, not just mathematical curiosities.

The pun in our title is intentional: delimited continuations are about "turning points" in computation, places where control flow can be captured and resumed. For Fidelity, recognizing their importance was itself a turning point in how we approach the entire framework.

---

## Further Reading

- [Why F# Is A Natural Fit for MLIR](https://speakez.tech/blog/why-fsharp-is-a-natural-fit-for-mlir/) — SSA as functional programming and the compilation advantage
- [The Full Frosty Experience](https://speakez.tech/blog/the-full-frosty-experience/) — Platform-aware async through delimited continuations
- [The Continuation Preservation Paradox](https://speakez.tech/blog/the-continuation-preservation-paradox/) — How deep can continuations survive in compilation?
- [Actors Take Center Stage](https://speakez.tech/blog/actors-take-center-stage/) — The actor model's role in modern distributed systems
- [Seeking Referential Transparency](https://speakez.tech/blog/seeking-referential-transparency/) — How the PHG chooses between interaction nets and delimited continuations
- [The DCont/Inet Duality](https://speakez.tech/blog/dcont-inet-duality/) — How computation expressions decompose into fundamental patterns
- [A Unified Actor Architecture](https://speakez.tech/blog/unified-actor-architecture/) — Bridging native and edge actors through shared semantics
- [Baker: A Key Ingredient to Composer](https://speakez.tech/blog/baker-a-key-ingredient-to-firefly/) — The zipper-based correlation pipeline
- [Hyping Hypergraphs](https://speakez.tech/blog/hyping-hypergraphs/) — Temporal program graphs and dataflow compilation strategies
- [Advent of Neuromorphic AI](https://speakez.tech/blog/advent-of-neuromorphic-ai/) — Spike-based computing and post-Von Neumann hardware targets

### Academic Papers

- [SSA is Functional Programming](https://www.cs.princeton.edu/~appel/papers/ssafun.pdf) — Appel (1998)
- [A Monadic Framework for Delimited Continuations](https://www.microsoft.com/en-us/research/wp-content/uploads/2005/01/jfp-revised.pdf) — Dybvig, Peyton Jones, Sabry (JFP 2007)
- [The F# Asynchronous Programming Model](https://tomasp.net/academic/papers/async/async.pdf) — Syme, Petricek, Lomov (PADL 2011)
- [The F# Computation Expression Zoo](https://tomasp.net/academic/papers/computation-zoo/computation-zoo.pdf) — Petricek, Syme (PADL 2014)
