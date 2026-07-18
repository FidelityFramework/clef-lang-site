---
title: "Absorbing Alloy"
linkTitle: "Absorbing Alloy"
description: "The Journey from Shadow BCL to Native-First Thinking"
date: 2026-01-04
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2026-01-04
  migration_date: 2026-02-15
---

In March 2025, we published [Building Composer With Alloy](https://speakez.tech/blog/building-firefly-with-alloy/), describing a BCL-free Clef standard library that would provide native type implementations. The goal was to preserve familiar APIs like `Console.WriteLine` while compiling to fat pointers and stack allocations instead of GC-tracked heap objects. Alloy would be to Fidelity what the BCL is to .NET.

Over the months that followed, we deepened our reading of ML-family languages, studied production MLIR frameworks, and refined the Native Type Universe architecture. We reached a different position: types belong in the compiler, not in a library. This may seem obvious to software engineers who live in other ecosystems, but we wanted it to arrive as an emergent property of the design rather than a reflexive withdrawal from current art in the .NET ecosystem.

## The Library Pattern

When we designed Alloy, we followed a well-established pattern. Just as .NET has the Base Class Library, Rust has its standard library, and OCaml has its Core modules, we would provide a library of native types and operations for Clef code targeting native compilation.

The architecture seemed clean:

```mermaid
flowchart TD
    A[Application Code] --> B[Alloy Library]
    B --> C[CCS Type Checking]
    C --> D[Alex Code Generation]
    D --> E[Native Binary]
```

Alloy would define `NativeStr` as a struct with `Pointer` and `Length` fields and implement `Console.Write` as calls to `Sys.write`. CCS would type-check that code like any other Clef code, and Alex would generate MLIR for Alloy's platform bindings.

It worked. Our HelloWorld samples compiled and ran without issues, the tests passed, and the dependency graph made sense. We had achieved BCL-free Clef compilation.

The layering still bothered us: Alloy was defining types on top of CCS, which already defined its own.

## The Realization: Types ARE the Language

In late 2025, while exploring reference implementations for the [nanopass architecture](/docs/internals/concepts/nanopass-navigation/), we spent time with [the Scheme-based nanopass framework](https://github.com/nanopass/nanopass-framework-scheme) and its `define-language` form. The pattern stood out: in nanopass, you don't import a library of types. You define the language's types as part of the compiler itself.

```scheme
(define-language L0
  (terminals
    (variable (x))
    (constant (c)))
  (Expr (e)
    (var x)
    (const c)
    (primapp prim (e ...))
    (app e0 e1)
    (lambda (x ...) e)))
```

The types `Expr`, `var`, and `const` aren't imported from a library. They ARE the language, defined by the compiler rather than consumed by it.

We also spent some time in review of [Triton-CPU, a reference MLIR implementation](https://github.com/triton-lang/triton-cpu). Its approach was similar: types are defined in TableGen dialect specifications, not in a separate library:

```tablegen
def Triton_PointerType : Triton_Type<"pointer", "ptr"> {
  let parameters = (ins
    "Type":$pointeeType,
    "int":$addressSpace
  );
}
```

The `ptr` type isn't imported from a Triton standard library. It's intrinsic to the Triton dialect. The compiler owns it completely.

The same arrangement was already present in what we had built into CCS.

## We Had Already Done It

Over time our CCS (Clef Compiler Services) had come to contain most of what we were seeking. [The NTUKind discriminated union already defined the native type universe](/spec/draft/native-type-mappings/#primitive-types):

```fsharp
type NTUKind =
    // Platform-dependent (resolved via quotations)
    | NTUint      // Platform word, signed
    | NTUuint     // Platform word, unsigned
    | NTUstring   // Fat pointer (ptr + length)
    | NTUptr      // Pointer type
    | NTUbool     // Boolean
    | NTUunit     // Unit type

    // Fixed width (platform-independent)
    | NTUint8 | NTUint16 | NTUint32 | NTUint64
    | NTUuint8 | NTUuint16 | NTUuint32 | NTUuint64
    | NTUfloat32 | NTUfloat64
```

CCS already had intrinsic modules for essential operations:

- `NativePtr.*` for pointer manipulation
- `Sys.*` for platform operations (write, read, exit, etc.)
- `Array.*` for collection operations
- `NativeStr.*` for string operations

The types weren't coming from Alloy and flowing through CCS. CCS was defining the types. Alloy was providing aliases and wrapper functions that resolved to CCS intrinsics anyway.

We were maintaining two layers where one would suffice.

## The Absorption

The absorption of Alloy into CCS follows from this realization. Rather than having:

```mermaid
flowchart TD
    A["Application<br>Console.WriteLine('Hello')"] --> B["Alloy.Console.WriteLine →<br>Alloy.Primitives.writeStr → Sys.write"]
    B --> C[CCS type-checks the chain]
    C --> D[Alex generates MLIR]
```

our design collapses the chain to:

```mermaid
flowchart TD
    A["Application<br>Console.writeln 'Hello'"] --> B[CCS recognizes Console.writeln as intrinsic]
    B --> C[Alex generates MLIR directly]
```

The intermediate library layer is removed. Types and operations are compiler intrinsics from the start.

### Less is More

The architectural simplification carries a concrete benefit. In our design, the compilation pipeline no longer performs extensive reachability analysis and pruning.

With Alloy as a library, compilation followed a subtractive model. The compiler loaded all of Alloy's type definitions, wrapper functions, and platform bindings, then analyzed which portions were actually reachable from the application code. Unreachable code was pruned. This is the same pattern .NET uses with the BCL, and it carries the same computational cost: the compiler must process everything before determining what to discard.

With intrinsics, compilation follows an additive model. When CCS encounters `Console.writeln`, it recognizes an intrinsic and includes exactly what's needed for that operation. There is no library to load and no dependency graph to traverse, so no pruning pass runs.

The difference is large. In our HelloWorld samples, reachability analysis previously processed the entire Alloy typed tree before pruning over 90% of it. With intrinsics that analysis no longer needs to run. The compiler includes `Console.writeln`'s implementation and does nothing further.

This is a structural change to the compilation pipeline, not a local optimization:

| Model | Process | Cost |
|-------|---------|------|
| **Subtractive** (Alloy) | Load all → Analyze reachability → Prune unused | O(library size) |
| **Additive** (Intrinsics) | Include what's referenced | O(application size) |

For small programs, the proportional reduction is large. For large programs, the baseline is lower. Either way, less work means faster compilation and a simpler compiler implementation.

Some reachability analysis remains necessary for platform binding libraries, where user-defined code may have unused paths. But for the core type system and intrinsic operations, the question "is this reachable?" no longer needs to be asked. If the compiler emits it, it's needed.

### New Intrinsic Modules

In our design, CCS provides intrinsic modules across the following surface:

| Module | Purpose | Example Operations |
|--------|---------|-------------------|
| **Sys.*** | Platform operations | write, read, exit, clock_gettime |
| **NativePtr.*** | Pointer manipulation | get, set, add, stackalloc |
| **Array.*** | Collection operations | length, get, set, create |
| **String.*** | String operations | concat, substring, length |
| **Math.*** | Mathematical functions | sin, cos, sqrt (→ LLVM intrinsics) |
| **Bits.*** | Bit manipulation | popcount, clz, ctz (→ LLVM intrinsics) |
| **Console.*** | I/O operations | write, writeln, readln |
| **Uuid.*** | UUID generation | newV4, parse, toString |
| **DateTime.*** | Time operations | now, utcNow, addDays |

### New NTU Types

The type universe expands to include types that Alloy previously defined:

```fsharp
type NTUKind =
    // ... existing types ...

    /// 128-bit UUID (RFC 4122) - platform entropy for generation
    | NTUuuid

    /// DateTime - ticks with platform clock resolution
    | NTUdatetime

    /// TimeSpan - duration in ticks
    | NTUtimespan
```

UUID generation and DateTime operations use platform quotations for resolution, the same mechanism that resolves `NTUint` to 32 or 64 bits based on target architecture. The platform-specific entropy source (getrandom on Linux, BCryptGenRandom on Windows) is part of the platform binding, not the type definition.

## Familiar Surface, Native Core

A reasonable concern at this point: does absorbing Alloy into the compiler mean developers coming from F# need to learn a new API? They do not. The design-time experience largely remains idiomatic Clef.

```fsharp
// Application code looks exactly like standard Clef
let main () =
    let greeting = "Hello, World!"
    Console.writeln greeting

    let id = Uuid.newV4()
    let now = DateTime.now()

    sprintf "Generated %O at %O" id now
    |> Console.writeln
```

This code uses familiar patterns: `let` bindings, string interpolation, module-qualified function calls. A Clef developer reading this code sees ordinary Clef syntax. The types (`string`, `Uuid`, `DateTime`) behave as expected. The functions (`Console.writeln`, `Uuid.newV4`, `DateTime.now`) follow standard naming conventions.

The difference surfaces only when building platform libraries, the code that bridges Clef's type system to platform-specific implementations. Here, quotations become first-class citizens:

```fsharp
// Platform library code - where quotations drive resolution
module Platform.Console =

    let writeln (s: string) : unit =
        <@ fun (str: NTUstring) ->
            let ptr, len = NativeStr.toParts str
            Sys.write Sys.stdout ptr len
            Sys.write Sys.stdout (NativePtr.ofArray "\n"B) 1n
        @>
```

The quotation `<@ ... @>` captures the implementation as data, allowing the compiler to inspect it, transform it based on platform context, and generate appropriate MLIR. On Linux x86_64, `Sys.write` becomes a syscall with arguments in specific registers. On ARM64, the calling convention differs. On [bare-metal embedded targets](/docs/internals/hardware/fidelity-on-mcu/), it might become a UART write sequence.

This separation preserves a clean division of concerns:

| Layer | Sees | Writes |
|-------|------|--------|
| **Application developers** | Familiar Clef APIs | Standard Clef code |
| **Platform library authors** | Quotation-based bindings | Platform-specific implementations |
| **Compiler (CCS + Alex)** | NTU types + quotations | MLIR operations |

Most Clef developers never need to write platform bindings. They consume intrinsic modules (`Console.*`, `Uuid.*`, `DateTime.*`) the same way they would consume any Clef library. The quotation machinery is an implementation detail, invisible unless you're extending the platform. The biggest everyday difference is no more `open System` calls, and smaller executables with faster execution.

The goal throughout was native compilation without obscure syntax. Absorbing Alloy changes where the implementation lives, not how the developer writes code. Types and operations that previously resolved through library indirection now resolve directly in the compiler, while the surface API stays the same.

## A View With Gratitude

Looking back at [Building Composer With Alloy](https://speakez.tech/blog/building-firefly-with-alloy/), the thinking was reasonable for its time. We were coming from a .NET perspective where the BCL/runtime separation shapes the developer experience. With our new direction we sought opportunities to preserve a familiar pattern for users coming from a .NET background.

More importantly, Alloy served as a discovery mechanism. By implementing `NativeStr`, `Console`, and `Memory` as library code, we confirmed many of our expectations for native primitives:

1. **What native types needed to look like:** fat pointers, stack buffers, value-type options
2. **What operations needed to exist:** platform-independent APIs with platform-specific implementations
3. **How SRTP could drive compile-time resolution:** inline functions with type constraints
4. **Where the BCL assumptions lived:** string encoding, GC integration, object hierarchies

Alloy showed us that BCL-free Clef was workable, and that moving the type definitions into the compiler was the cleaner arrangement. That work gave us the vocabulary and patterns CCS now carries directly. Having learned what we needed from Alloy, we could absorb those lessons into the compiler itself.

## What We Kept: The F# Inheritance

Clef descends from F#, and CCS builds on three capabilities that lineage carries:

**Quotations for Platform Binding Resolution**

Quotations, inherited from F#, provide type-carrying, compile-time code inspection. Where string-based or JSON-based code representations leave types as opaque metadata that must be parsed and validated separately, a quotation preserves the compiler's type information as first-class data.

When Alex needs to generate a syscall, it doesn't parse a string to discover that `buffer` is a `nativeptr<byte>`. That information is structurally present in the quotation, verified by the same type checker that validated the original code.

When CCS encounters a platform binding, it uses quotations to resolve the platform-specific implementation:

```fsharp
// Platform binding signature
let write (fd: int) (buffer: nativeptr<byte>) (count: int) : int =
    platform_binding  // Quotation-resolved

// Resolved at compile time via platform descriptor
// Linux x86_64: syscall 1, args in rdi/rsi/rdx
// ARM64: svc 0x40, args in x0/x1/x2
 
```

**Active Patterns for Type Matching**

Active patterns, also inherited from F#, decompose types in the compiler:

```fsharp
let (|FatPointer|_|) (ty: NativeType) =
    match ty with
    | TApp(conRef, _) when conRef.Layout = TypeLayout.FatPointer ->
        Some conRef
    | _ -> None
```

**SRTP for Compile-Time Polymorphism**

[Statically resolved type parameters](/docs/design/types/structural-polymorphism/) drive generic operations without runtime cost:

```fsharp
let inline add (x: ^T) (y: ^T) : ^T
    when ^T : (static member (+) : ^T * ^T -> ^T) = x + y
```

We did not invent these features. They come from F#, the lineage Clef descends from, and Alloy helped us understand how to put them to work for native compilation.

## The New Architecture

The post-absorption architecture we are building toward removes the middle layer:

```mermaid
flowchart TD
    subgraph App["Application Code"]
        A1["let result = Console.writeln 'Hello'"]
        A2["let id = Uuid.newV4()"]
        A3["let now = DateTime.now()"]
    end

    subgraph CCS["CCS (Native Type Universe + Intrinsics)"]
        subgraph NTU["NTUKind"]
            N1[NTUint]
            N2[NTUstring]
            N3[NTUuuid]
            N4[NTUdatetime]
            N5[NTUtimespan]
        end
        subgraph Intrinsics["Intrinsic Modules"]
            I1["Sys.* (platform ops)"]
            I2["NativePtr.* (memory ops)"]
            I3["Array.* (collections)"]
            I4["String.* (string ops)"]
            I5["Math.* (LLVM intrinsics)"]
            I6["Console.* (I/O)"]
        end
        PQ[Platform Resolution via Quotations]
    end

    subgraph Alex["Alex (Witnesses NTU → MLIR)"]
        X1[Type mapping using platform context]
        X2[Intrinsic → MLIR/LLVM operation mapping]
        X3[Platform binding implementation]
    end

    Binary[Native Binary]

    App --> CCS
    CCS --> Alex
    Alex --> Binary
```

The intermediate library layer is gone. Application code calls intrinsics that CCS recognizes as such, and Alex generates the MLIR.

## The Alloy Repository

The Alloy repository remains as a historical artifact. For a short time it will be:

1. **Preserved** with a README explaining its absorbed status
2. **Made read-only** after a transition period
3. **Kept as reference** for the patterns it established

We keep the Alloy repository rather than deleting it, so its lessons stay available.

## New Art

Taking some measure of ML, Rust, and Triton-CPU, our Fidelity framework settles on a single principle: types and operations are the language, and they live in the compiler rather than in an imported library.

In our design, CCS defines the Native Type Universe the framework requires and provides intrinsic modules for essential operations. Platform-specific details resolve via quotations at compile time. The Alex component inside our Composer compiler witnesses NTU types to MLIR. No external library, no intermediate layer, no BCL equivalent.

This is what we mean by "new art" in compiler design for Clef. The lessons from .NET remain useful as an account of what we came from. We have found no other representative implementation of a native-first framework with this compiler-owned type universe in the standing literature we have reviewed.

## Looking Forward

When we started this work, we were conscious of the assumptions inherited from years of .NET development. The BCL as a mental model felt comfortable. Alloy was our attempt to follow that convention, a familiar pattern wrapped around a native compilation target.

It was a reasonable hypothesis: keep the architecture that fellow developers might expect, and swap out the runtime semantics underneath. For a time it worked well enough that we did not question it. Reading the nanopass literature and the MLIR dialect patterns sharpened ideas that were already part of our long-term direction, and we decided to act on them now rather than later.

[In a native type ecosystem the language doesn't consume type definitions](/spec/draft/type-definitions/). From our view, the language *is* type definitions plus evaluation rules. The distinction between "the compiler" and "the standard library" dissolves because there was never a principled boundary there to begin with. It's a vestige of a decision made 25+ years ago.

This changes how we think about Fidelity's future. We are building a native-first framework, with Clef as its language. Our ML heritage gives us the abstractions we want: algebraic data types, pattern matching, quotations for metaprogramming, and statically resolved type parameters for zero-cost generics. Native compilation is the starting point of the design, not a layer bolted onto a .NET alternative.

The absorption of Alloy into CCS is one manifestation of this shift. There will be others. Each time we find ourselves recreating a .NET pattern, we now ask: is this pattern load-bearing, or is it scaffolding we can remove once we have a firm grasp of what's needed for this framework?

Alloy was scaffolding. Building it showed us the architecture we wanted, and we can now develop the design directly without maintaining the intermediate library.

The arrangement we are building toward keeps the compilation path short, with the compiler owning the type universe and the application referencing only what it uses. We will keep asking the same question at each .NET pattern we find ourselves recreating, and removing the ones that turn out to be scaffolding, as the work on Clef and Composer continues.

---

**Cross-References:**

- [Building Composer With Alloy](https://speakez.tech/blog/building-firefly-with-alloy/) - The previous understanding
- [Clef: From IL to NTU](https://speakez.tech/blog/fsharp-native-from-il-to-ntu/) - The type architecture
- [Baker: A Key Ingredient](https://speakez.tech/blog/baker-a-key-ingredient-to-firefly/) - Type resolution in the pipeline
- [Hello World Goes Native](/docs/internals/mlir/hello-world-goes-native/) - Sample programs updated for CCS
