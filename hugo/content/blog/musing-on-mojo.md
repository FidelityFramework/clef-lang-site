---
title: "Musings on Mojo: Partially Parallel Paths"
description: "Exploring Two Approaches to Language and Compiler Design"
date: 2023-07-04
authors: ["Houston Haynes"]
tags: ["Analysis"]
params:
  originally_published: 2023-07-04
  original_url: "https://speakez.tech/blog/musing-on-mojo/"
  migration_date: 2026-02-15
---

Mojo is an experiment in bridging two worlds that usually stay apart. Created by Chris Lattner, whose work on LLVM and MLIR reshaped how we build compiler infrastructure, Mojo sets out to bring Python's accessibility to systems programming on top of MLIR.

At SpeakEZ, we've been working in similar territory with our Fidelity framework and its Clef language. Both projects build on MLIR, yet they approach modern language design from different starting points. That divergence is what I want to walk through here.

## The MLIR Foundation They Share

Both Mojo and Fidelity build on MLIR (Multi-Level Intermediate Representation), Lattner's step beyond traditional compiler architectures. MLIR provides a framework for progressive "lowering" through domain-specific dialects, so high-level language constructs are transformed into machine code in stages.

The trait that matters here is that MLIR preserves semantic information through the compilation pipeline. Where traditional compilers often lose high-level intent early, MLIR's dialect system keeps abstractions around long enough to drive optimizations. Both Mojo and Fidelity use this, in different ways.

Mojo uses MLIR to retrofit Python's dynamic semantics onto a performance-oriented substrate. That is hard work: Python emphasizes flexibility and runtime introspection, qualities that conflict with the static analysis optimization wants. Lattner's team has made real progress reconciling those tensions, building a language that keeps Python's familiar syntax while reaching performance once reserved for C++ or Rust.

Fidelity, by contrast, begins with our Clef language's statically-typed functional foundation. That starting point aligns with MLIR's architecture, because a functional language's emphasis on immutability and explicit data flow maps cleanly to MLIR's SSA-based intermediate representations. Like Rust, we incorporate C++'s RAII patterns for deterministic memory management, and we combine that with pure functional constructs such as delimited continuations. The combination lets our Program Semantic Graph line up with MLIR's hierarchical operation structure, so high-level functional abstractions translate to low-level operations without semantic loss.

## The def/fn Split: A Fateful Choice

The most revealing design decision in Mojo is the split between `def` (Python-compatible functions) and `fn` (Mojo-native functions). The split is more than syntax. It acknowledges that Python's dynamic semantics and the static resolution model intrinsic to systems programming are hard to reconcile:

```python
# Python-compatible function with dynamic behavior
def flexible_function(x, y):
    # Dynamic typing, Python semantics
    # Can use any Python features but limited optimization
    return x + y

# Mojo-native function with static guarantees
fn performant_function(x: Int, y: Int) -> Int:
    # Static typing, compile-time checking
    # Enables MLIR optimizations but requires explicit types
    return x + y
```

The split is pragmatic. It accepts that holding full Python semantics and systems-level performance at the same time would force compromises that satisfy neither goal. With two function types, Mojo lets developers choose their trade-offs explicitly, but at a cost.

That decision works against Mojo's stated goal of Python compatibility. The moment a Python library is used inside an `fn` function, or performance-critical code has to call into Python libraries, the abstraction breaks down. The friction is an incompatibility between two computational models that cannot be reconciled without giving up the benefits each model provides. Mojo's team may find a path for "welding" the two models in the compilation process, but we do not envy their options either way.

The implications run deep if this stays unresolved in their ecosystem:

- **Cognitive overhead**: Developers must constantly decide which function type to use, effectively learning two systems
- **Interoperability complexity**: The boundary between def and fn functions becomes a persistent source of friction and performance cliffs
- **Ecosystem fragmentation**: Libraries must choose sides, with pure-Python libraries trapped in the `def` world and performance libraries isolated in `fn`
- **The core contradiction**: You cannot have both "full Python compatibility" and "systems programming performance" when the solution is to split them into separate domains

The split also works against the value that made Mojo's original promise attractive: access to Python's ecosystem. The moment you need NumPy or Pandas or any other Python library in a performance-critical `fn` function, you may be forced back into the `def` world, giving up the performance that justified choosing Mojo in the first place.

This looks hard to resolve. Rather than a stepping stone toward unification, the def/fn split may be a lasting schism that runs against the original vision. Time and a lot of engineering effort will tell.

Clef, by contrast, has a unified model rooted in F#'s Hindley-Milner heritage. Clef inherits F#'s object-oriented features for .NET interoperability, but its functional core stays consistent throughout. That consistency pays off when compiling through MLIR, since there is no need to maintain two separate compilation strategies or reconcile incompatible semantic models.

## Type Systems: Different Philosophies, Different Trade-offs

Beyond the def/fn distinction, the languages' type systems reflect their different philosophical starting points:

### Mojo's Gradual Typing Journey

Mojo's typing reflects the realities of the Python ecosystem. By supporting gradual typing, Mojo lets developers add type annotations incrementally where performance matters most:

```python
# Mojo's flexible typing approach
def process_data(data):  # Dynamic typing for compatibility
    return [x * 2 for x in data]

fn process_data_fast[T: Numeric](data: List[T]) -> List[T]:  # Static typing for performance
    var result = List[T]()
    for item in data:
        result.append(item * 2)
    return result
```

This is a pragmatic choice. The Python ecosystem cannot be rewritten overnight, so Mojo provides a migration path that tries to preserve compatibility.

### F#'s Type System Heritage

F#, inheriting its type system from OCaml, brings different capabilities to the table. Features like discriminated unions and units of measure have been a proven part of F# for well over a decade:

```fsharp
// F#'s discriminated unions enable precise domain modeling
type ParseResult<'T> =
    | Success of 'T
    | Error of string

// Units of measure provide zero-cost type safety
[<Measure>] type meter
[<Measure>] type second
let velocity = 10.0<meter/second>
```

These features are more than syntactic conveniences. They set the terms for how programs can be analyzed and optimized. When our Fidelity framework translates Clef to MLIR, those type constraints carry optimization opportunities that would be hard to recover from dynamically-typed code.

## Concurrency Models: Evolution vs. Foundation

Both languages bring developed concurrency models, from different evolutionary paths:

### Mojo's Emerging Concurrency Story

Mojo's concurrency model is still in motion, with notes and articles outlining several approaches to parallel execution. Their work on async/await integration and their exploration of actor-based models show careful attention to modern concurrency patterns. The challenge they face, holding Python semantics while enabling real parallelism, means navigating a tangle of design trade-offs.

### F#'s Mature Concurrency Abstractions

F#'s concurrency story, integrated at its inception and refined over two decades, offers multiple complementary approaches:

```fsharp
// Async workflows for I/O-bound operations
let fetchDataAsync url = async {
    let! response = httpClient.GetAsync(url) |> Async.AwaitTask
    let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    return parseContent content
}

// Agent-based concurrency for state isolation
let agent = MailboxProcessor.Start(fun inbox ->
    let rec loop state = async {
        let! msg = inbox.Receive()
        let newState = processMessage state msg
        return! loop newState
    }
    loop initialState
)
```

F#'s agent model, inspired by Erlang, is a practical application of delimited continuations. That foundation lets our Fidelity framework carry control flow analysis through compilation to MLIR. Running control flow and data flow graphs in parallel gives the framework options for resource targeting that we have found no other representative implementations of in the standing literature we have reviewed.

## Memory Management: Navigating Constraints

Memory management represents perhaps the most challenging aspect of both projects:

### Mojo's Ownership Innovation

Mojo introduces an ownership model that aims to provide memory safety without the complexity often associated with Rust:

```python
fn transfer_ownership(owned data: String):
    # Mojo takes ownership, original variable no longer accessible
    process(data)

fn borrow_data(borrowed data: String):
    # Mojo borrows data, original remains accessible
    print(data)
```

The approach aims to provide memory safety guarantees while staying approachable for Python developers. The implementation work involved, especially when interfacing with Python's reference-counted objects, is substantial.

### Fidelity's Adaptive Approach

Our Fidelity framework takes a different path. Different deployment targets have different memory constraints, and through BAREWire, our memory protocol layer, we adapt memory strategies to the target platform:

```fsharp
// Platform-adaptive memory configuration
let embeddedConfig =
    MemoryConfig.create
    |> MemoryConfig.withAllocation Static
    |> MemoryConfig.withHeapSize (kilobytes 64)

let serverConfig =
    MemoryConfig.create
    |> MemoryConfig.withAllocation RegionBased
    |> MemoryConfig.withGarbageCollection Concurrent
```

The two approaches reflect different design priorities. Mojo prioritizes a unified programming model across contexts, while Fidelity targets platform-specific optimization.

## The Path Forward: Complementary Directions

What I find worth watching about this moment in language design is how many different approaches can explore the design space MLIR opens. Mojo's effort to bring Python into systems programming is an experiment that could change how a large set of developers approach performance-critical code.

The challenges Mojo faces are substantial. A language that satisfies both Python developers' expectations and systems programmers' requirements means working through a long list of design decisions. That the Mojo compiler remains closed source likely reflects the scope of the undertaking, getting the basics right before opening the implementation to broader scrutiny.

F#'s mature foundation offers different opportunities. With twenty years of production use through .NET and Fable compilers, and a type system refined through decades of research, our Composer compiler can focus on the compilation and deployment work that MLIR enables. The framework benefits from F#'s lack of historical baggage from Python's module system or object model, which allows a more direct mapping to MLIR's capabilities.

## Learning from Each Other

Both projects can learn from each other's approaches:

**From Mojo, we see**:
- The value of meeting developers where they are, with familiar syntax and gradual adoption paths
- Work toward making ownership models more approachable
- The importance of first-class AI/ML hardware support in modern languages
- What the def/fn split reveals, showing both the appeal and perhaps the folly of unifying dynamic and static programming models

**From Clef/Fidelity, we see**:
- The payoff of building on proven theoretical foundations
- How rich type systems open up compile-time optimizations
- The benefits of platform-adaptive compilation strategies
- The value of a unified computational model that avoids design-time overhead

## Where the Two Paths Diverge

I read Mojo and Fidelity as complementary explorations of MLIR rather than competitors. Chris Lattner's aim of making high-performance computing accessible to Python developers could widen access to systems programming, and the Mojo team's willingness to take on the work of bridging Python and systems programming deserves recognition.

The `def/fn` split is an honest acknowledgment of the tensions in that bridging effort. The transparency about the difficulty of unifying dynamic and static worlds is worth crediting, and it also surfaces what may be a contradiction the project cannot resolve. The promise of "Python with systems programming performance" dissolves into "Python or systems programming performance," with developers choosing between the two at every function boundary. Working past that may take not just years of engineering but a re-tooling of the project's goals.

At SpeakEZ, we're working in this space alongside Mojo from a different starting point. F#'s functional heritage and mature design create their own openings for MLIR, particularly for applications that need deterministic performance across diverse deployment targets. Our unified computational model sidesteps some of the bifurcation challenges, though we are solving a different set of problems.

As MLIR matures, I expect more approaches to language design to follow. Some will start from dynamic languages and add performance, like Mojo. Others will start from a formal foundation and add flexibility. Still others will explore points in the design space neither of us is looking at.

We'll keep watching Mojo's evolution as we build Fidelity out, and we'll keep learning from the parts of the design space the Mojo team is mapping that we are not. That is the work I want to continue: carrying the Clef language through MLIR toward deterministic performance across targets, and seeing how far the unified model holds as the rest of the framework comes into place.
