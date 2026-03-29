---
title: "Traits Versus Statically Resolved Type Parameters"
linkTitle: "Traits vs SRTP"
description: "How F# SRTP stands in contrast to Rust's approach to polymorphism"
date: 2025-09-05T10:00:00-04:00
authors:
  - SpeakEZ
tags: ["Analysis", "Design", "Innovation"]
params:
  originally_published: 2025-09-05
  original_url: "https://speakez.tech/blog/traits-versus-statically-resolved-type-parameters/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

Rust's trait system is often compared to Haskell's type classes, suggesting that Rust has successfully brought type class polymorphism to systems programming. While Rust's traits are indeed inspired by type classes and provide powerful abstractions, examining the actual mechanics reveals important differences. Rust's traits, as a multi-paradigm systems language feature, make deliberate design tradeoffs that differ in important ways from Haskell's type classes. It's worth taking a sidebar to also review how it compares to Clef's approach to polymorphism.

Clef's Statically Resolved Type Parameters (SRTP), a key feature in the Fidelity framework, delivers a different form of ad-hoc polymorphism with capabilities that follow a distinct design philosophy. These fundamental differences cascade into how polymorphism integrates with compilation, optimization, and how developers think about generic programming.

## Method Call Syntax and Design Philosophy

While Rust traits support both dot notation (`x.test()`) and associated function syntax (`Trait::test(&x)`), the language's design strongly favors method syntax. This isn't merely a syntactic preference; it reflects Rust's philosophy of making ownership and borrowing explicit:

```rust
trait Testable {
    fn test(&self) -> bool;
}

impl Testable for i32 {
    fn test(&self) -> bool {
        *self != 0
    }
}

fn check_value(x: i32) -> bool {
    x.test()  // Idiomatic
    // or
    Testable::test(&x)  // Also valid, but less common
}
```

The trait method `test` can be called both ways, but Rust's ecosystem and standard library heavily favor method syntax. This design choice makes the borrowing explicit (`&self`, `&mut self`, or `self`) and integrates naturally with Rust's ownership model.

This represents a departure from Haskell, where type class functions are typically called as regular functions without privileged receivers. In Haskell, `show x` is the natural way to convert a value to a string, not `x.show()`. Rust's method-centric approach reflects its imperative heritage and focus on explicit ownership, diverging from Haskell's pure functional model where all parameters are treated equally.

Compare this to Clef's SRTP approach:

```fsharp
let inline test< ^T when ^T : (member Test : unit -> bool)> (x: ^T) =
    (^T : (member Test : unit -> bool) x)

// But more naturally:
let inline isNonZero x =
    x <> LanguagePrimitives.GenericZero

// Works for ANY numeric type without explicit implementation
let result1 = isNonZero 42
let result2 = isNonZero 3.14
let result3 = isNonZero 0I  // BigInteger
```

Clef doesn't privilege any particular syntax. Operations can be functions, methods, operators, or properties. The polymorphism emerges from the structure of the types themselves, not from explicit trait implementations. This reflects Clef's functional-first philosophy where functions and values are treated uniformly.

## The Implementation Ceremony

Rust requires explicit implementation of every trait for every type. This is a deliberate design choice for explicitness and the orphan rule (preventing conflicting implementations). While this can create boilerplate, Rust provides powerful tools to manage it:

```rust
use std::ops::{Add, Mul, Sub};

// Derive macros automatically generate common trait implementations
#[derive(Clone, Copy, Debug, PartialEq)]
struct Complex {
    real: f64,
    imag: f64,
}

// For arithmetic operations, explicit implementation is still needed
// but crates like 'derive_more' can auto-generate these too
impl Add for Complex {
    type Output = Complex;

    fn add(self, other: Complex) -> Complex {
        Complex {
            real: self.real + other.real,
            imag: self.imag + other.imag,
        }
    }
}

impl Mul for Complex {
    type Output = Complex;

    fn mul(self, other: Complex) -> Complex {
        Complex {
            real: self.real * other.real - self.imag * other.imag,
            imag: self.real * other.imag + self.imag * other.real,
        }
    }
}

// With procedural macros from external crates:
// #[derive(Add, Mul)]  // Could auto-generate the above implementations
```

The orphan rule ensures coherence but means you must implement traits for every type combination. Rust's robust macro ecosystem, including procedural macros that can inspect type structure and generate implementations, significantly reduces boilerplate in practice. However, the implementations must still exist somewhere in the compiled code, even if generated.

This explicit implementation requirement contrasts sharply with Haskell's more flexible instance declarations. Haskell allows orphan instances (though they're discouraged) and provides more powerful abstraction mechanisms like higher-kinded types and type families. Where Haskell might express complex relationships through type-level programming, Rust requires explicit implementations at the value level, trading expressiveness for predictability and compile-time guarantees about code generation.

Clef with SRTP handles this through structural typing:

```fsharp
type Complex = {
    Real: float
    Imag: float
}
with
    static member (+) (a, b) =
        { Real = a.Real + b.Real; Imag = a.Imag + b.Imag }

    static member (*) (a, b) =
        { Real = a.Real * b.Real - a.Imag * b.Imag
          Imag = a.Real * b.Imag + a.Imag * b.Real }

    static member (*) (a, s: float) =
        { Real = a.Real * s; Imag = a.Imag * s }

    static member (*) (s: float, a) =
        { Real = a.Real * s; Imag = a.Imag * s }

// Now we can write truly generic mathematical functions
let inline square x = x * x

// Works for int, float, Complex, or ANY type with (*)
let c = { Real = 3.0; Imag = 4.0 }
let c2 = square c
let n2 = square 5
let f2 = square 3.14
```

The Clef compiler infers the constraints from usage. No explicit trait bounds in most cases, minimal implementation ceremony, just structural typing that works.

## Generic Mathematics: Different Approaches

When writing generic mathematical code, Rust's approach requires explicit bounds:

```rust
use num_traits::{Zero, One};  // External crate for numeric traits

fn dot_product<T>(a: &[T], b: &[T]) -> T
where
    T: Add<Output = T> + Mul<Output = T> + Copy + Zero,
{
    a.iter()
        .zip(b.iter())
        .map(|(x, y)| *x * *y)
        .fold(T::zero(), |acc, x| acc + x)
}
```

The Rust ecosystem has addressed the numeric traits issue through crates like `num-traits`, which provides standard numeric abstractions. This is explicit and type-safe, though it requires dependencies.

Clef achieves similar results through SRTP and built-in generic operators:

```fsharp
let inline dotProduct a b =
    Array.map2 (*) a b
    |> Array.sum

// Works for ANY numeric type
let intDot = dotProduct [|1; 2; 3|] [|4; 5; 6|]
let floatDot = dotProduct [|1.0; 2.0; 3.0|] [|4.0; 5.0; 6.0|]
let complexDot = dotProduct [|c1; c2|] [|c3; c4|]  // Our Complex type

// Can even mix types if multiplication is defined
let inline scale scalar vector =
    Array.map ((*) scalar) vector

let scaled = scale 2.5 [|1; 2; 3|]  // float * int array, if supported
```

Clef infers that `sum` needs addition and a zero, that `map2` needs multiplication. The constraints emerge from the operations themselves.

## Compile-Time Resolution Strategies

Both SRTP and Rust traits provide compile-time polymorphism, but through different mechanisms:

**Rust's Monomorphization:**
- Generates specialized code for each concrete type used with a generic function
- Trait bounds are checked at compile time
- Can lead to code bloat with many instantiations
- Trait objects (`dyn Trait`) provide runtime polymorphism when needed

**Clef's SRTP:**
- Performs structural type checking at compile time
- Inlines generic functions with resolved type parameters
- Generates specialized code at each call site
- No runtime polymorphism equivalent; purely compile-time

```fsharp
// This function works with ANY type that has a Length property
let inline getLength x =
    (^T : (member Length : int) x)

// Works with arrays, lists, strings, or custom types
let arrayLen = getLength [|1; 2; 3|]
let stringLen = getLength "hello"
let listLen = getLength [1; 2; 3]

// Can constrain multiple members
let inline process< ^T when ^T : (member Length : int)
                        and ^T : (member Item : int -> 'a)> x =
    if getLength x > 0 then
        Some (x.[0])
    else
        None
```

This structural approach means Clef can work with any type that has the required shape, without explicit implementation.

## Operator Inference and Generic Programming

Clef can infer operator requirements without declarations:

```fsharp
let inline genericSum lst =
    List.reduce (+) lst

let inline genericProduct lst =
    List.reduce (*) lst

let inline pythagoras a b =
    sqrt (a * a + b * b)

// These work for ANY types with the right operators
let sumInts = genericSum [1; 2; 3; 4]
let sumFloats = genericSum [1.0; 2.0; 3.0]
let productMatrices = genericProduct [m1; m2; m3]  // If matrices define (*)
```

The compiler tracks which operations are used and ensures they're available at each call site. The polymorphism emerges from the code's structure.

## Compile-Time Computation Capabilities

Both languages provide powerful compile-time computation, but through different mechanisms:

**Rust's Const Generics and Typestate Pattern:**

```rust
// Const generics for compile-time dimensional checking
struct Matrix<const R: usize, const C: usize> {
    data: [[f64; C]; R],
}

impl<const R: usize, const C: usize, const K: usize>
    std::ops::Mul<Matrix<C, K>> for Matrix<R, C> {
    type Output = Matrix<R, K>;

    fn mul(self, other: Matrix<C, K>) -> Matrix<R, K> {
        // Dimensions verified at compile time
        // Compilation fails if dimensions don't match
        todo!()
    }
}

// Typestate pattern for compile-time state machines
struct Locked;
struct Unlocked;

struct Door<State> {
    _state: std::marker::PhantomData<State>,
}

impl Door<Locked> {
    fn unlock(self) -> Door<Unlocked> {
        Door { _state: std::marker::PhantomData }
    }
}

impl Door<Unlocked> {
    fn open(&self) { /* can only open unlocked doors */ }
}
```

**Clef's Units of Measure and Phantom Types:**

```fsharp
// Units of measure - completely erased at runtime
[<Measure>] type m
[<Measure>] type s
[<Measure>] type kg

let inline speed (distance: float<'u>) (time: float<'v>) =
    distance / time

let v = speed 100.0<m> 10.0<s>  // Type: float<m/s>

// Phantom types for type-safe dimensions
type Matrix<'rows, 'cols> =
    Matrix of float[,]

let inline multiply (Matrix a) (Matrix b) : Matrix<'r, 'c> =
    // Dimensions checked at compile time
    Matrix(multiplyImpl a b)
```

Rust's const generics provide similar compile-time guarantees with explicit const parameters, while Clef's units of measure offer a more specialized solution for dimensional analysis. The typestate pattern in Rust achieves compile-time state machine verification similar to Clef's phantom types, though with more explicit state transitions.

## Method Resolution Strategies

Rust's trait method resolution follows complex but deliberate rules that integrate tightly with the borrow checker to prevent aliasing bugs and ensure memory safety:

```rust
// Rust's method resolution works with the borrow checker
struct Data {
    value: Vec<i32>,
}

impl Data {
    fn process(&mut self) {
        // Mutable borrow of self
        self.value.push(42);
    }

    fn read(&self) -> &[i32] {
        // Immutable borrow of self
        &self.value
    }
}

trait Transform {
    fn transform(&mut self);
}

impl Transform for Data {
    fn transform(&mut self) {
        // The trait system understands borrowing rules
        // Prevents aliasing and data races at compile time
        self.process();  // OK: mutable self
    }
}

// Method resolution respects lifetimes and borrowing
fn use_data(data: &mut Data) {
    data.transform();  // Trait method
    data.process();    // Inherent method
    // Both checked for correct borrowing semantics
}
```

The complexity of Rust's resolution rules serves a critical purpose: ensuring that polymorphic code respects ownership and borrowing rules, preventing entire categories of bugs related to aliasing and concurrent access. The deref coercion, auto-ref, and method precedence rules all work together to make borrowing ergonomic while maintaining safety.

Clef's SRTP resolution is structurally simpler because it operates in a different context, primarily immutable and functional:

```fsharp
type Container<'T> =
    { Items: 'T array }
    member this.Length = this.Items.Length
    member this.Get(i) = this.Items.[i]

let inline totalLength containers =
    Array.sumBy (fun c -> (^a : (member Length : int) c)) containers

// Works with our Container type
let containers = [| { Items = [|1; 2|] }; { Items = [|3; 4; 5|] } |]
let total = totalLength containers  // 5

// Also works with strings, arrays, lists
let stringTotal = totalLength [| "hello"; "world" |]  // 10
```

Clef's resolution is based purely on structure: if the type has the member, it works. This simplicity is possible because Clef doesn't need to track ownership and lifetimes in the same way. The tradeoff is that 'standard' F# relies on the .NET runtime's garbage collector for memory management, while Fidelity framework aims to provide different memory management strategies.

## Performance Characteristics

Both approaches aim for zero-cost abstractions with different tradeoffs:

**Rust:**
- Monomorphization generates specialized code, potential for code bloat
- Trait objects (`dyn Trait`) add indirection through vtables when runtime polymorphism is needed
- Link-time optimization (LTO) can reduce duplication across compilation units
- Excellent cache locality for monomorphized code
- Direct access to SIMD intrinsics and platform-specific optimizations
- Mature LLVM backend with decades of optimization work
- `#[inline]` attributes give fine-grained control over inlining decisions

**Clef SRTP:**
- Aggressive inlining with specialized code generation
- No runtime type information needed for SRTP code paths
- Can lead to larger binaries with extensive inlining
- Compile-time resolution enables mathematical optimizations
- In standard F#, benefits from .NET's JIT optimizations
- In Fidelity, CCS preserves SRTP semantics through the PSG for custom optimization passes

```fsharp
// This generates specialized machine code for each type
let inline fastSum (arr: ^T[]) =
    let mutable acc = LanguagePrimitives.GenericZero< ^T>
    for i in 0 .. arr.Length - 1 do
        acc <- acc + arr.[i]
    acc

// Compiles to tight loops with no abstraction overhead
let sumInts = fastSum [|1..1000000|]      // Integer addition loop
let sumFloats = fastSum [|1.0..1000000.0|] // Floating-point loop
```

For Fidelity's compilation through MLIR, SRTP's preservation of type relationships enables domain-specific optimizations that can target specific hardware accelerators, a key advantage for heterogeneous computing scenarios.

## The Compilation Pipeline Advantage

When Composer compiles Clef with SRTP to MLIR, CCS (Clef Compiler Services) preserves the polymorphism information, which flows through a sophisticated multi-stage optimization pipeline that addresses traditional SRTP concerns:

**Stage 1: Program Semantic Graph (PSG)**
- Semantic-level optimization that understands relationships
- Identifies and merges redundant specializations across boundaries
- Preserves high-level intent while eliminating duplication
- Applies optimizations not available to symbolic-only representations

**Stage 2: MLIR Transformations**
- Cross-function specialization with full visibility across inline boundaries
- Constraint propagation flows type requirements through the entire program
- Domain-specific optimizations for specific computing patterns
- Hardware-specific lowering creates different specializations for each target

**Stage 3: LLVM LTO**
- Final cross-module optimization pass
- Static binding of C/C++ libraries that are included in the solution
- Identical code folding merges duplicate functions where allowed
- Profile-guided optimization for hot paths

This multi-stage approach means Fidelity doesn't "blindly implement" SRTP. Instead of naive inlining that leads to code bloat, the compiler performs intelligent optimization at each level. We imagine that similar options are available at the LLVM level with Rust. However, our PSG understands when two specializations are semantically equivalent even if syntactically different. This is above MLIR where passes can also recognize repeated patterns and apply optimizations. LLVM's mature optimization infrastructure provides the final polish but in most cases this type of optimization is applied in higher compilation passes. We've found that focusing LLVM's optimization burden is the best way to keep compilation times from dilating.

The result is that SRTP's theoretical weakness (code bloat from excessive specialization) becomes a practical strength in Fidelity: CCS and the PSG give the compiler more semantic information to work with at every stage, enabling optimizations that would be impossible with early monomorphization or vtable-based polymorphism.

## Evolution and Future Directions

Both languages continue to evolve their approaches to polymorphism:

**Rust's ongoing improvements:**
- Generic Associated Types (GATs) now stable, enabling more expressive trait definitions
- Const generics continue to expand, allowing more compile-time computation
- Specialization will eventually allow optimized implementations for specific types
- Effects systems and keyword generics are being explored for even more expressive abstractions

**Clef's SRTP in Fidelity:**
- Liberation from .NET's constraints enables new optimization strategies
- Direct compilation through MLIR preserves more semantic information
- Potential for custom type and numeric system extensions specific to scientific computing
- Option for future heterogeneous computing models through partitioned compilation to multiple hardware targets

## The Cognitive Liberation of SRTP

Rust's trait system and Clef's SRTP represent fundamentally different philosophies about polymorphism, and the difference matters. While Rust chose explicit implementation requirements and complex resolution rules to manage memory safety, this choice imposes a significant cognitive burden on developers, even with the benefit of automatic code generation. Every generic function requires explicit trait bounds. Every type needs explicit implementations. Every operation must consider borrowing semantics, lifetimes, and ownership.

Clef's SRTP in the Fidelity framework demonstrates that there's a less burdensome way for systems programming:

- **Write once, work everywhere**: Generic functions that just work with any type that has the right shape
- **Zero ceremony**: No trait implementations to write, no bounds to declare, no orphan rules to navigate
- **Lighter cognitive load**: Focus on the problem domain, not on satisfying the type system's bureaucracy
- **True zero-cost**: Compile-time resolution without vtables, trait objects, or runtime overhead

The developer ergonomics alone make Clef with SRTP a powerful choice for Fidelity's domain. When you're implementing complex business processes, dealing with heterogeneous computing, or building scientific applications, the last thing you need is to fight with trait bounds and implementation ceremonies. You need a language that supports expression of intent clearly with precision.

For Fidelity's vision of bringing functional programming to bare metal, SRTP isn't just a feature; it's a fundamental design advantage. It enables a development experience where polymorphism emerges naturally from code structure. The integrity of mathematical relationships are preserved through compilation and focus is on the domain rather than on appeasing the type system.

This isn't about different tools for different problems. It's about recognizing that for intelligent systems programming, machine learning, scientific applications, and developer productivity, Clef's SRTP provides a uniquely direct approach to polymorphism. The Fidelity framework leverages this advantage to deliver a form of high-level, functional systems programming that aims to set a new standard in a rapidly expanding technology landscape.
