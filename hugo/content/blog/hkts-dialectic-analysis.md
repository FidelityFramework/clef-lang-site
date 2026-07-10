---
title: "Considering HKTs in Fidelity"
linkTitle: "HKTs Dialectic Analysis"
description: "A Developer-Centered Analysis Of Native Compilation for Clef"
date: 2025-06-09T09:59:54+04:00
authors: ["Houston Haynes"]
tags: ["Analysis", "Design", "Architecture"]
params:
  originally_published: 2025-06-09
  migration_date: 2026-02-15
---

The debate over higher-kinded types (HKTs) in F# surfaces a tension between theoretical expressiveness and practical accessibility. This analysis examines that tension through two lenses: first, Don Syme's stance that has shaped standard F#, and second, how our Fidelity framework's position as a native Clef compiler changes the calculus. I want to understand both perspectives while considering whether Fidelity's different constraints warrant a different approach.

For readers from different language communities, this analysis offers distinct insights. .NET developers will gain perspective on why F# made different choices than C#'s recent type system expansions. Haskell practitioners will understand the trade-offs that lead some functional languages to limit type-level expressiveness. Scala developers will recognize familiar patterns in how language communities grapple with the complexity that HKTs introduce. All three communities share an interest in understanding how type system design affects real-world software development, and how emerging compilation techniques might reshape these traditional trade-offs.

## Don Syme's Philosophy and Practicality

[Don Syme's resistance to HKTs](https://github.com/fsharp/fslang-suggestions/issues/243#issuecomment-916079347) stems from a coherent philosophy about language design that prioritizes accessibility and pragmatism. His now-famous declaration captures it:

> "I don't want F# to be the kind of language where the most empowered person in the Discord chat is the category theorist."

This recognizes how advanced type features can stratify programming communities. In the standard F# context, the philosophy makes sense. F# must maintain interoperability with the .NET ecosystem, which means any type system features must map cleanly to the Common Language Runtime's type model. The CLR wasn't designed with HKTs in mind, so adding them would require encoding schemes that could break at the boundaries between F# and C# code. This constraint alone justifies caution.

Beyond technical constraints, Syme observed how languages with HKTs tend to develop two-tier communities. In Scala, the split between those comfortable with advanced type-level programming and those who aren't created real friction. Libraries written with HKTs often become incomprehensible to developers without deep theoretical backgrounds, effectively gatekeeping advanced functionality behind mathematical knowledge. This directly contradicts F#'s mission to make functional programming accessible to mainstream developers coming from object-oriented backgrounds.

The compilation performance concern, while less documented, reflects real-world experience. Type-level computation adds another dimension of complexity to type inference and checking. Languages with rich type-level features often suffer from exponential blowups in compilation time for seemingly innocent code. For F# to remain viable in enterprise settings where build times matter, this represents a genuine risk.

## The Fidelity Framework: A Different Design

Our Fidelity framework changes the HKT calculus by operating in a different design space from standard F#. As a native framework targeting MLIR and LLVM directly, Fidelity sidesteps the CLR interoperability constraints that influence F#'s original design. This opens different possibilities and introduces different trade-offs.

For .NET developers, the idea of a Fidelity framework represents a departure: Clef code compiled to native binaries without runtime dependencies. For Haskell practitioners, it offers a view of functional programming with predictable performance and deterministic memory management. For Scala developers familiar with GraalVM native image compilation, Fidelity makes native compilation the primary target instead of an afterthought. Consider what the framework does by default. It uses Clef's units of measure to encode static dimensions and constraints at the type level, which lets it verify tensor shapes and physical units at compile time:

```fsharp
[<Measure>] type row
[<Measure>] type col

type Matrix<'T, [<Measure>] 'Rows, [<Measure>] 'Cols> =
    private {
        Data: AlignedBuffer<'T>
        RowCount: int<'Rows>
        ColCount: int<'Cols>
    }

// Compile-time dimension verification
let multiply (a: Matrix<float, 'R1, 'C1>)
             (b: Matrix<float, 'C1, 'C2>) : Matrix<float, 'R1, 'C2> =
    // 'C1 must match: verified at compile time
    let result = Matrix.createAligned (a.RowCount, b.ColCount)
    // exact dimensions feed MLIR's SIMD lowering
    Matrix.multiplyInto a b result
    result

// This below would be a compile error - dimensions don't match:
// let bad = multiply (Matrix<float, 3<row>, 4<col>>) (Matrix<float, 5<row>, 2<col>>)
 
```

This use of type-level programming shows that our framework's foundation already embraces complexity where it serves correctness goals. The deterministic memory guarantees and compile-time memory layout verification require type system features that go beyond what standard F# typically employs. Haskell developers might recognize this as similar to using type-level naturals for sized vectors, while Scala developers might see parallels with 'shapeless' or refined types.

The domains our framework targets, embedded systems programming, large distributed systems, and machine learning with Furnace, align with patterns where HKTs traditionally provide value. The ability to abstract over type constructors could eliminate duplication in these domains while maintaining the compile-time guarantees that make Fidelity a candidate for systems programming.

## Potential Benefits of HKTs

The case for HKTs in our framework rests on concrete benefits that address pain points in the current design. Consider how our concurrency model handles its two dual computation types. The `Observable` (hot) and `Incremental` (cold) primitives each require separate implementations of common patterns. With HKTs, these could share a single abstraction:

```fsharp
// Current approach requires duplication
type Observable<'T> = ...   // hot, push-based
type Incremental<'T> = ...  // cold, pull-based

// With HKTs, could abstract over the computation constructor
type Computation<'F, 'T> = ...  // where 'F :: * -> *
 
```

This pattern *could* apply to multiple places in our design. Our BAREWire protocol implements serialization for numerous container types. The Furnace ML library must handle operations across different tensor shapes and storage strategies. The Olivier actor model needs to work with various message queue implementations. Each of these represents a case where abstracting over type constructors could eliminate duplication while preserving type safety.

HKTs could *theoretically* extend our correctness guarantees. Our framework already uses type-level programming for deterministic memory management and static memory layouts. HKTs would potentially enable expressing more invariants, such as ensuring that certain transformations preserve memory alignment properties or that message passing protocols maintain type safety across actor boundaries. We've made other choices for those assurances, but that's not to say HKTs couldn't also serve those purposes.

The MLIR integration presents another opportunity. MLIR's dialect system maps to higher-kinded abstractions, where operations are parameterized by the types they operate on. This relationship reveals a tension: MLIR dialects **already** provide abstraction benefits at the compilation level, handling polymorphism and type-driven optimization transparently. The question becomes whether duplicating these abstractions at the source level adds value or creates another layer of complexity for developers to navigate. HKTs could in theory provide a more direct and type-safe mapping between abstractions and MLIR dialects, potentially improving both compilation and the quality of generated code. They might **also** obscure the abstractions *already* happening in the compilation pipeline.

## The Costs and Complexities

Even in our framework's open design space, HKTs would introduce complexities that deserve consideration. The first concern is implementation. Adding HKTs to our Composer compiler would require substantial engineering effort. The compiler would need to track and verify higher-kinded constraints throughout the compilation pipeline, from source through MLIR dialects to final LLVM IR. This would mean restructuring how types are represented and manipulated. It would transform the syntax into a different programming language, which works against a core aim of Fidelity: to remain an implementation option for Clef developers.

The debugging experience presents another challenge, one that becomes acute in our framework's target domains. Without HKTs, it can keep error messages tied directly to Clef source constructs.

> With HKTs, error messages often become abstract and difficult to understand. A type mismatch deep in a generic abstraction might surface as an incomprehensible error about type constructors not unifying, leaving developers puzzled about what went wrong in their concrete code.

This problem plagued early Scala and continues to challenge Haskell developers. And frankly there are enough 'rough edges' in F# error messaging that taking the wrong tack here would seriously hamper user adoption. For Fidelity, this debugging complexity carries additional weight. Since the framework targets embedded systems, distributed systems, and performance-critical domains, debugging clarity isn't just about developer experience, it's about system reliability and safety.

In contexts where understanding exactly what went wrong matters for correctness or even physical safety, HKT-induced error message opacity could represent a genuine hazard. When a type error might indicate a protocol violation or memory safety issue, developers need clear, actionable diagnostics, not abstract category theory.

Most concerning for our framework's goals is the potential impact on compilation predictability. The design targets deterministic memory defaults and compile-time verification of memory layouts. Adding HKTs might make aspects of these analyses more complex. Type-level computation could introduce cases where the compiler cannot statically determine memory requirements, which would undermine the guarantees meant to make Fidelity valuable for embedded systems.

There's also the ecosystem consideration. While our framework is designed to operate independently of .NET, it still benefits from F#'s ecosystem and developer familiarity. Introducing HKTs would create a form of Clef that diverges from what F# developers know. This could limit adoption and make it harder to port existing F# code to Fidelity, which would reduce the framework's practical utility.

## The "Developer Out" Perspective

One argument against HKTs in our framework comes from its design philosophy, particularly as embodied in our BAREWire. The goal isn't to provide "top down" the most theoretically expressive abstractions; it is to create a "from the developer out" experience where developers can compose their way *to forgetting about* memory safety concerns [if they choose](/docs/design/memory/memory-management-by-choice/). This is a different approach from both Rust's borrow checker and traditional HKT-based abstractions.

Consider the contrast in developer experience across different approaches to memory safety. Rust achieves memory safety by making ownership semantics explicit, and by extension, unavoidable. Every reference requires a decision about mutability. Every lifetime needs consideration. Every borrow must be carefully managed. Haskell achieves safety through immutability and garbage collection, with HKTs enabling abstractions over these safe operations at a compile-time cost. Both approaches have merit, and both require developers to understand and work within their respective models.

Our BAREWire takes a different tack. Instead of exposing complexity and asking developers to manage it correctly, it aims to hide complexity behind composition patterns. A developer working with BAREWire shouldn't need to deal with the minutiae of memory layouts, ownership transfers, or type-level relationships. They should compose operations that preserve safety:

```fsharp
// Using FSharp.UMX
[<Measure>] type untrusted
[<Measure>] type sanitized
[<Measure>] type persisted

// The BAREWire way: complexity hidden through composition
module Transaction =
    // Each function handles its own safety
    let parse (input: string) : Result<Transaction, Error> =
        BAREWire.parseJson input  // Validates structure

    let sanitize (transaction: Transaction) : Transaction =
        BAREWire.sanitizeFields transaction  // Ensures safety

    let persist (transaction: Transaction) : Async<unit> =
        BAREWire.atomicWrite transaction  // Handles consistency

    let processInput input =
        parse input
        |> Result.map sanitize
        |> Result.map persist
```

This philosophy resonates differently with each language community. The "dive in when needed" principle matters here. Good library design should support gradual depth. Developers use high-level, safe compositions for most tasks but can selectively optimize specific operations without being forced to scaffold the entire lattice of type machinery. HKTs tend to work against this by creating an all-or-nothing abstraction barrier. Readability and explainability of code translate to a developer experience *asset* that should *not* automatically create a compile-time or runtime *liability*.

## LTO Changes The Calculation

Given these competing concerns and our "developer out" philosophy, the path forward becomes clearer when we consider one technical reality: Link Time Optimization (LTO) in the MLIR and LLVM pipeline changes the cost-benefit analysis of code duplication.

Traditional arguments for HKTs often center on reducing code duplication at the source level. If you have eight different stream types, the argument goes, you're maintaining eight copies of similar code. Our compilation model is designed to render this concern largely moot. Since Fidelity is designed for source-code-based library inclusion instead of binary linking, the MLIR and LLVM lowering process can identify and eliminate this duplication during compilation across the entire code base, dependencies included.

Consider the design of our Composer compiler pipeline. When multiple computation expressions share similar patterns, the progressive lowering through MLIR dialects is meant to identify these commonalities. The LLVM optimization passes, particularly with full LTO enabled, can merge identical code sequences, inline appropriately, and eliminate redundancy. The "different task types" in IcedTasks that seem like problematic duplication at the source level may compile down to deduplicated machine code.

```fsharp
// Source level: appears duplicated across different stream types
module HotStream =
    let map f stream =
        // Specific hot stream mapping logic
        stream |> processHot |> applyFunction f |> finalizeHot

    let filter predicate stream =
        // Specific hot stream filtering logic
        stream |> processHot |> applyPredicate predicate |> finalizeHot

module ColdStream =
    let map f stream =
        // Specific cold stream mapping logic
        stream |> processCold |> applyFunction f |> finalizeCold

    let filter predicate stream =
        // Specific cold stream filtering logic
        stream |> processCold |> applyPredicate predicate |> finalizeCold

// After MLIR lowering and LLVM LTO:
// - Common applyFunction/applyPredicate patterns merged
// - Identical control flow optimized into shared implementations
// - Type-specific processHot/processCold kept only where semantically different
// - Final binary contains minimal, optimized machine code
 
```

This strengthens the case against HKTs in our framework. The primary cost of avoiding HKTs, code duplication, is addressed by the compilation pipeline. The primary benefit of avoiding them, a simpler developer experience, stays intact. Modern compilation techniques let us keep both: source code that prioritizes developer understanding, and final executables that are as efficient as if we had used the abstractions.

The compilation time trade-off is worth naming. LTO analysis takes time, but this is a one-time cost during builds, not a tax that developers pay every time they read or write code. LLVM's caching mechanisms make this more attractive once the "one time cost" of compiling a stable code area is complete. A few extra seconds of compilation time is a small price for code that developers can understand immediately without consulting category theory textbooks. This aligns with our philosophy: **push complexity into the tooling, not onto the developer**.

> If Fidelity were to adopt any HKT-like features, they would need to demonstrate benefits that LTO cannot provide.

Since LTO handles code deduplication and optimization, HKTs would need to offer something else, perhaps better error messages or stronger compile-time guarantees. But HKTs typically make error messages worse, not better, and our framework already grounds its compile-time guarantees in existing type-level features.

## Philosophy and Technology in Harmony

The question of HKTs in F# and its variants ultimately illuminates how philosophical choices and technical capabilities together shape language design. Don Syme's avoidance of HKTs for standard F# stems from a commitment to accessibility and pragmatism that has proven wise given F#'s broad audience and .NET integration requirements. The challenges observed in Scala and other HKT-heavy languages validate his concerns about community fragmentation and complexity spirals.

For our Fidelity framework, the calculation differs but reaches a similar conclusion through different reasoning. While the framework operates free from CLR constraints and already embraces type-level programming for verification, its philosophy of compositional simplicity argues against exposing HKT abstractions to users. The aim is to make memory-safe systems programming feel as direct as writing idiomatic Clef code. There will always be some additional burden in working with a future Fidelity framework. The goal is to ensure those points of friction are essential to the targeted system and represent a worthwhile exchange for the developer.

The role of Link Time Optimization in this decision matters for all communities to understand. From this early point in the life of our framework design, LTO appears able to remove the primary technical argument for HKTs, code duplication, by optimizing away redundancy at the compilation stage. This would let Fidelity maintain multiple specialized types that provide the right interface for each use case, with the MLIR and LLVM pipeline merging duplicate implementations. The framework would reach what Haskell accomplishes through type classes, what Scala achieves through 'implicits', and what .NET achieves through interfaces, but at the compilation level instead of the source level.

This synthesis of design philosophy and technical capability shapes how we approach language evolution. Instead of pursuing HKTs because they're theoretically appealing (as a Haskell developer might advocate) or avoiding them due to implementation complexity (as a pragmatic .NET developer might prefer), our framework can make decisions based on what best serves its users. The compilation pipeline handles the optimization, which leaves developers free to work with specific types that make their intent obvious and their code safe by construction.

## Conclusion

In the end, the "category theorist problem" that Syme identified remains relevant across language communities, though for evolving reasons. The issue isn't whether developers could learn category theory, clearly many Haskell and Scala developers have (and some F# developers cross-pollinate there too). The question is whether they should *have to* for a given problem domain. For systems programming with strong safety requirements, Fidelity's answer is no. The type proliferation that might seem like a limitation to a Haskell developer, or unnecessary duplication to a Scala developer, becomes a strength when combined with modern compilation techniques.

The path forward for our framework involves continued investment in making complex operations feel simple, with optimization techniques carrying that simplicity through without a performance cost. The synthesis is accepting "inefficiency" at the source level while trusting modern compilation to deliver efficiency in the final product. It is a bet that developer time and understanding are worth more than source code terseness, one I expect to bear out as the framework and the applications built on it take shape.

Other language communities are working through the same tension between expressiveness and accessibility, and our approach is one answer among several. By leaning on compilation techniques to preserve both developer ergonomics and runtime performance, we can keep specialized types at the source level and let the pipeline reconcile them. The question we will keep returning to as the framework matures is the balance itself: making sure that when more abstraction becomes necessary, it still serves the "developer out" philosophy and holds the safety guarantees that matter for critical systems. That is where our attention sits as the work continues.
