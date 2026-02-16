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

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

In the evolving landscape of programming languages, Mojo has emerged as a fascinating experiment in bridging disparate worlds. Created by Chris Lattner, whose pioneering work on LLVM and MLIR has fundamentally transformed how we think about compiler infrastructure, Mojo represents an ambitious vision: bringing Python's accessibility to systems programming while leveraging the revolutionary capabilities of MLIR.

At SpeakEZ, we've been exploring similar territory with our Fidelity framework and its Clef language. Both projects share a common foundation in MLIR, yet approach the challenge of modern language design from remarkably different philosophical starting points. This divergence offers valuable insights into the design space of next-generation programming languages.

## The MLIR Revolution: A Shared Foundation

At the heart of both Mojo and Fidelity lies MLIR (Multi-Level Intermediate Representation), Lattner's brilliant evolution beyond traditional compiler architectures. MLIR provides a framework for progressive "lowering" through domain-specific dialects, enabling high-level language constructs to be gradually transformed into efficient machine code.

What makes MLIR transformative is its ability to preserve semantic information throughout the compilation pipeline. Where traditional compilers often lose high-level intent early in the process, MLIR's dialect system maintains rich abstractions that enable sophisticated optimizations. Both Mojo and Fidelity leverage this capability, though in distinctly different ways.

Mojo approaches MLIR with the goal of retrofitting Python's dynamic semantics onto a performance-oriented substrate. This is no small feat, Python's design philosophy emphasizes flexibility and runtime introspection, qualities that traditionally conflict with the static analysis required for optimization. Lattner's team has made impressive progress in reconciling these tensions, creating a language that maintains Python's familiar syntax while enabling performance previously reserved for C++ or Rust.

Fidelity, by contrast, begins with F#'s statically-typed functional foundation. This different starting point creates natural alignments with MLIR's architecture, as functional languages' emphasis on immutability and explicit data flow maps cleanly to MLIR's SSA-based intermediate representations. Like Rust, we incorporate C++'s RAII patterns for deterministic memory management, but combine this with pure functional constructs such as delimited continuations. This fusion enables our Program Semantic Graph to naturally align with MLIR's hierarchical operation structure, creating a compilation pipeline where high-level functional abstractions translate directly to efficient low-level operations without semantic loss.

## The def/fn Split: A Fateful Choice

Perhaps the most revealing design decision in Mojo is the split between `def` (Python-compatible functions) and `fn` (Mojo-native functions). This bifurcation represents more than a syntactic choice, it's a fundamental acknowledgment that Python's dynamic semantics and systems programming requirements are difficult to reconcile within a static resolution model that's intrinsic to systems programming:

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

This split is both pragmatic and revealing. It acknowledges that attempting to maintain full Python semantics while achieving systems-level performance would require compromises that would satisfy neither goal. By creating two distinct function types, Mojo allows developers to choose their trade-offs explicitly, but at a cost.

This architectural decision fundamentally compromises Mojo's stated goal of seamless Python compatibility. It would appear that the moment a Python library is used within an `fn` function, or when performance-critical code needs to interact with Python libraries, the abstraction breaks down. This isn't simply a matter of syntax, it's a fundamental incompatibility between two different computational models that cannot be reconciled without sacrificing the very benefits each model provides. Perhaps they will work out a path of "welding" these two models in the compilation process, but we do not envy their options either way.

The implications could be profound if this is unresolved in their ecosystem:

- **Cognitive overhead**: Developers must constantly decide which function type to use, effectively learning two systems
- **Interoperability complexity**: The boundary between def and fn functions becomes a persistent source of friction and performance cliffs
- **Ecosystem fragmentation**: Libraries must choose sides, with pure-Python libraries trapped in the `def` world and performance libraries isolated in `fn`
- **The fundamental contradiction**: You cannot have both "full Python compatibility" and "systems programming performance" when your solution is to split them into separate domains

Perhaps most critically, this split would undermine the very value proposition that made Mojo's original promise attractive: access to Python's vast ecosystem. The moment you need NumPy or Pandas or any other Python library in your performance-critical `fn` function, you may be forced back into the `def` world, negating the performance benefits that justified choosing Mojo in the first place.

This challenge appears intractable. The def/fn split, rather than being a stepping stone toward unification, may  represent a permanent schism that reveals the impossibility of their original vision. Only time and significant engineering effort will tell.

F#, by contrast, benefits from a unified model rooted in its well established Hindley-Milner heritage. While F# supports object-oriented features for .NET interoperability, its core functional model remains consistent throughout. This consistency becomes particularly valuable when compiling through MLIR, as there's no need to maintain two separate compilation strategies or reconcile incompatible semantic models.

## Type Systems: Different Philosophies, Different Trade-offs

Beyond the def/fn distinction, the languages' type systems reflect their different philosophical starting points:

### Mojo's Gradual Typing Journey

Mojo takes an innovative approach to typing that reflects the realities of the Python ecosystem. By supporting gradual typing, Mojo allows developers to incrementally add type annotations where performance matters most:

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

This design choice reflects deep pragmatism, recognizing that the vast Python ecosystem cannot be rewritten overnight, Mojo provides a migration path that attempts to preserve compatibility.

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

These features aren't mere syntactic conveniences, they establish a foundation for how programs can be analyzed and optimized. When the Fidelity framework translates Clef to MLIR, these rich type constraints provide additional optimization opportunities that would be difficult to recover from dynamically-typed code.

## Concurrency Models: Evolution vs. Foundation

Both languages approach concurrency with sophisticated models, though from different evolutionary paths:

### Mojo's Emerging Concurrency Story

Mojo's concurrency model is actively evolving, with notes and articles outlining various approaches to parallel execution. Their work on async/await integration and exploration of actor-based models shows thoughtful consideration of modern concurrency patterns. The challenge they face, maintaining Python semantics while enabling true parallelism, requires careful navigation of complex design trade-offs.

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

What's particularly interesting is how F#'s agent model, inspired by Erlang, is essentially a practical application of delimited continuations. This theoretical foundation enables the Fidelity framework to perform sophisticated control flow analysis during compilation to MLIR. The parallel design of control flow and data flow graphs gives the Fidelity framework options into resource targeting that are truly innovative and unique.

## Memory Management: Navigating Constraints

Memory management represents perhaps the most challenging aspect of both projects:

### Mojo's Ownership Innovation

Mojo introduces an ownership model that attempts to provide memory safety without the complexity often associated with Rust:

```python
fn transfer_ownership(owned data: String):
    # Mojo takes ownership, original variable no longer accessible
    process(data)

fn borrow_data(borrowed data: String):
    # Mojo borrows data, original remains accessible
    print(data)
```

This approach shows real innovation, attempting to provide memory safety guarantees while maintaining approachability for Python developers. The technical challenges involved in implementing this, especially when interfacing with Python's reference-counted objects, are substantial.

### Fidelity's Adaptive Approach

The Fidelity framework takes a different path, recognizing that different deployment targets have fundamentally different memory constraints. Through BAREWire, our memory protocol layer, we can adapt memory strategies to target platforms:

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

This isn't a criticism of Mojo's approach, rather, it reflects different design priorities. Mojo prioritizes a unified programming model across contexts, while Fidelity embraces platform-specific optimization.

## The Path Forward: Complementary Innovations

What's most exciting about the current moment in programming language design is how different approaches can explore the vast possibility space opened by MLIR. Mojo's journey to bring Python into the systems programming realm represents a bold experiment that could transform how millions of developers approach performance-critical code.

The technical challenges Mojo faces are substantial. Creating a language that satisfies both Python developers' expectations and systems programmers' requirements involves navigating countless design decisions. The fact that the Mojo compiler remains closed source likely reflects the enormous complexity of this undertaking, getting the fundamentals right before opening the implementation to broader scrutiny.

Meanwhile, F#'s mature foundation provides different opportunities. With twenty years of production use through .NET and Fable compilers with a type system refined through decades of research, the Composer compiler can focus on the compilation and deployment innovations that MLIR enables. The Fidelity framework benefits from F#'s lack of historical baggage from Python's module system or object model, allowing more direct mapping to MLIR's capabilities.

## Learning from Each Other

Both projects can learn from each other's approaches:

**From Mojo, we see**:
- The value of meeting developers where they are, with familiar syntax and gradual adoption paths
- Innovation in making ownership models more approachable
- The importance of first-class AI/ML hardware support in modern languages
- The revealing nature of the def/fn split, which demonstrates both the appeal and perhaps folly of unifying dynamic and static programming models

**From Clef/Fidelity, we see**:
- The power of building on proven theoretical foundations
- How rich type systems enable sophisticated compile-time optimizations
- The benefits of platform-adaptive compilation strategies
- The value of a unified computational model that avoids burdensome design-time overhead

## A Bright Future for Systems Innovation

Rather than viewing Mojo and Fidelity as competitors, we should celebrate them as complementary explorations of MLIR's potential. Chris Lattner's vision of making high-performance computing accessible to Python developers may further democratize systems programming in unprecedented ways. The Mojo team's willingness to tackle the enormous complexity of bridging Python and systems programming deserves recognition and support.

The `def/fn` split in Mojo represents an honest acknowledgment of the fundamental tensions in this bridging effort. While this transparency about the challenges inherent in unifying dynamic and static worlds is commendable, it also reveals what may be an insurmountable contradiction at the heart of the project. The promise of "Python with systems programming performance" effectively dissolves into "Python or systems programming performance," with developers forced to choose between the two at every function boundary. This architectural reality will require not just years of engineering effort but potentially a fundamental re-tooling of the project's goals.

At SpeakEZ, we're excited to be exploring this space alongside Mojo, albeit from a different starting point. F#'s functional heritage and mature design system create unique opportunities for leveraging MLIR's capabilities, particularly for applications requiring deterministic performance across diverse deployment targets. Our unified computational model sidesteps some of the bifurcation challenges, though we fully acknowledge that we're solving a different set of problems.

The future of compiler technology is not a zero-sum game. As MLIR continues to evolve and mature, we'll likely see even more innovative approaches to language design. Some will start from dynamic languages and add performance, like Mojo. Others will bring formality to add flexibility. Still others might explore entirely new points in the design space.

What's certain is that we're entering a golden age of language innovation, enabled by a new Cambrian explosion of new hardware designs and bridging infrastructure like MLIR. Whether you're drawn to Mojo's Python-centric approach or Clef's functional elegance, the future holds exciting possibilities for developers seeking both productivity and performance.

We look forward to seeing Mojo's continued evolution and the innovative solutions it brings to long-standing challenges in language design. The programming community benefits when brilliant minds like Lattner's push the boundaries of what's possible. In that spirit, we're proud to be exploring parallel paths toward the same goal: making powerful, efficient computation accessible to more developers than ever before.
