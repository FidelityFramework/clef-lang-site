---
title: "Why Clef Is A Natural Fit for MLIR"
linkTitle: "Why Clef Fits MLIR"
description: "How Clef's design expresses low-level efficiency with safety"
date: 2025-07-28
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2025-07-28
  original_url: "https://speakez.tech/blog/why-fsharp-is-a-natural-fit-for-mlir/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

In 1998, [Andrew Appel published a paper](https://www.cs.princeton.edu/~appel/papers/ssafun.pdf) that heralded a change to how we should think about compiler design. "SSA is Functional Programming" demonstrated that Static Single-Assignment form, the intermediate representation at the heart of modern optimizing compilers, is exactly equivalent to functional programming with nested lexical scope. This insight has profound implications as we enter a new era of hardware-software co-design.

For SpeakEZ Technologies, this revelation validates our approach with the Fidelity framework more than 25 years after that seminal paper's first publication: lowering Clef to native code through MLIR isn't just possible, it's aligned to the fundamental structure of well-principled compilation.

## The Hidden Functional Program

Every imperative program contains a functional program waiting to be discovered. When compilers transform C, Rust, or other imperative languages into SSA form, they're actually reconstructing the functional relationships that were obscured by imperative syntax. This observation applies to Rust as well, despite Rust's emphasis on explicit control and ownership; the compiler still transforms imperative control flow into SSA's functional structure during compilation:

> "The SSA community draws pictures of graphs with basic blocks and flow edges, and the functional-language community writes lexically nested functions, but they're both doing exactly the same thing in different notation." - Andrew Appel

This isn't merely a theoretical observation. The algorithms for optimal φ-function placement in SSA use dominance frontiers to discover the ideal nesting structure, exactly what a functional programmer would write naturally.

Consider this imperative code and its SSA form:

```c
// Imperative code
i = 1
j = 1
k = 0
while (k < 100)
    if (j < 20)
        j = i
        k = k + 1
    else
        j = k
        k = k + 2
return j
```

```
// SSA form with φ-functions
i1 ← 1
j1 ← 1
k1 ← 0
L2: j2 ← φ(j4, j1)
    k2 ← φ(k4, k1)
    if k2 < 100
        if j2 < 20
            j3 ← i1
            k3 ← k2 + 1
        else
            j5 ← k2
            k5 ← k2 + 2
        j4 ← φ(j3, j5)
        k4 ← φ(k3, k5)
        goto L2
    return j2
```

The φ-functions mark where control flow merges and values must be selected based on which path was taken. But look at the equivalent functional program:

```fsharp
// Natural F# representation
let rec loop j k =
    if k < 100 then
        let j', k' =
            if j < 20 then
                i, k + 1  // i is captured from outer scope
            else
                k, k + 2
        loop j' k'
    else
        j

let result =
    let i = 1
    let j = 1
    let k = 0
    loop j k
```

The F# version directly expresses what SSA must construct: function parameters (instead of φ-functions), lexical scoping (instead of dominance relationships), and recursive calls (instead of back edges).

## Why MLIR Changes Everything

MLIR takes the SSA concept further by providing multiple levels of abstraction, each maintaining SSA form but at different semantic levels. This is where Clef's heritage in functional design becomes even more valuable.

### Traditional Compilation: Loss of Intent

When Rust or C++ compile to LLVM:

```rust
// Rust source
async fn process_data(input: &[u8]) -> Result<Output, Error> {
    let validated = validate(input).await?;
    let transformed = transform(validated).await?;
    Ok(finalize(transformed))
}

// Becomes low-level LLVM IR - all high-level structure lost
; Complex state machine with no async semantics visible
```

The compiler must transform the imperative async code into state machines, losing the high-level control flow information in the process. Rust's ownership model provides valuable compile-time guarantees, but those guarantees are checked before SSA transformation; the ownership information does not flow into LLVM IR where further optimizations occur.

### Clef with MLIR: Preserving Structure

With Clef and Fidelity's approach:

```fsharp
// F# source with explicit control flow
let processData input = async {
    let! validated = validate input
    let! transformed = transform validated
    return finalize transformed
}

// Natural mapping to MLIR's async dialect
async.func @processData(%input: !fidelity.buffer) -> !fidelity.result {
    %validated = async.await {
        call @validate(%input) : (!fidelity.buffer) -> !fidelity.validated
    }
    %transformed = async.await {
        call @transform(%validated) : (!fidelity.validated) -> !fidelity.transformed
    }
    %result = call @finalize(%transformed) : (!fidelity.transformed) -> !fidelity.result
    return %result : !fidelity.result
}
```

The structure maps directly to MLIR's dialects, preserving semantic information through multiple compilation stages.

## Delimited Continuations: Making SSA Explicit

While Appel showed that SSA is functional programming, the Fidelity framework takes this further by using delimited continuations to make the SSA structure explicit as the Composer compiler constructs the Program Hypergraph (PHG):

```fsharp
// Traditional F# async
let traditionalAsync data = async {
    let! x = fetchData()
    let! y = processData x
    return x + y
}

// With delimited continuations - SSA structure is explicit
let withDelimitedContinuations data =
    reset (fun () ->
        let x = shift (fun k -> fetchData() |> continueWith k)
        let y = shift (fun k -> processData x |> continueWith k)
        x + y
    )
```

The `shift` and `reset` operators create explicit continuation boundaries that correspond exactly to SSA's basic block boundaries. This isn't coincidence, it's the same mathematical structure expressed directly at the semantic level rather than discovered through multiple intermediate transforms.

### Compilation Advantages

This explicit structure enables several compilation advantages:

1. **Deterministic State Machines**: The compiler knows all possible state transitions at compile time
2. **Optimal Memory Layout**: Continuation boundaries provide natural points for memory management
3. **Precise Resource Tracking**: Resources can be tied to continuation scopes

```fsharp
// Fidelity's approach - resources tied to continuation boundaries
let processWithResources data =
    reset (fun () ->
        let buffer = allocate 4096  // Tied to this continuation
        let result = shift (fun k ->
            processInBuffer buffer data |> continueWith k
        )
        result  // Buffer automatically freed at continuation boundary
    )
```

## Mojo: The def/fn Impedance Mismatch

Mojo's fundamental split between Python-compatible `def` functions and performance-oriented `fn` functions reveals the challenge:

```python
# Mojo can't reconcile dynamic and static in one model
def python_compatible(x):  # Dynamic, can't optimize well
    return process(x)

fn performance_critical(x: Int) -> Int:  # Static, maps to SSA
    return process_fast(x)
```

This split exists because dynamic languages can't naturally express the static structure that SSA requires. Clef doesn't suffer the burden of this split. Its type system and SSA-aligned design already express what SSA needs.

## Practical Implications for Systems Programming

### Memory Management Without Annotations

Clef's abstractions compile efficiently because they already match SSA's structure:

```fsharp
// High-level F# code with type-safe units and functional composition
[<Measure>] type celsius
[<Measure>] type fahrenheit

type SensorReading = {
    Temperature: float<celsius>
    Pressure: float
    Timestamp: DateTime
}

let processReadings (readings: SensorReading array) =
    readings
    |> Array.filter (fun r -> r.Temperature > 0.0<celsius>)
    |> Array.map (fun r ->
        { r with Temperature = r.Temperature * 9.0/5.0 + 32.0<fahrenheit> })
    |> Array.groupBy (fun r -> r.Timestamp.Hour)
    |> Array.map (fun (hour, group) ->
        hour, Array.averageBy (fun r -> float r.Temperature) group)

// This functional pipeline maps directly to SSA form:
// Each operation becomes a basic block with clear data flow
```

This compiles to efficient MLIR because the structure already expresses what SSA represents:

```mlir
// Natural MLIR representation - no reconstruction needed
func @processReadings(%readings: !array<reading>) -> !array<hour_average> {
    // Block for filter - SSA phi functions are just function parameters
    %filtered = scf.for %i = 0 to %n iter_args(%acc = %empty_array) {
        %reading = array.load %readings[%i]
        %temp = struct.extract %reading["temperature"]
        %condition = arith.cmpf "ogt", %temp, %zero : f64
        %new_acc = scf.if %condition {
            array.append %acc, %reading
        } else {
            scf.yield %acc
        }
        scf.yield %new_acc
    }

    // Block for map - direct transformation, no hidden state
    %converted = array.map %filtered {
        ^bb0(%r: !reading):
        %temp_c = struct.extract %r["temperature"]
        %temp_f = arith.mulf %temp_c, %nine_fifths : f64
        %temp_f2 = arith.addf %temp_f, %thirty_two : f64
        %new_r = struct.insert %temp_f2, %r["temperature"]
        yield %new_r
    }

    // Grouping and averaging continue the pattern
    // Each Clef operation = MLIR operation, preserving semantics
}
```

Compare this to how an imperative language must be transformed:

```cpp
// C++ imperative version
vector<pair<int, double>> processReadings(vector<SensorReading>& readings) {
    vector<SensorReading> filtered;

    // Manual loops hide the data flow
    for (auto& r : readings) {
        if (r.temperature > 0.0) {
            r.temperature = r.temperature * 9.0/5.0 + 32.0;
            filtered.push_back(r);
        }
    }

    // Complex grouping logic obscures the transformation
    map<int, vector<double>> groups;
    for (auto& r : filtered) {
        groups[r.timestamp.hour()].push_back(r.temperature);
    }

    // Must reconstruct functional relationships for SSA
    // Compiler must analyze loops, mutations, aliasing
}
```

Clef's operations ***are*** the SSA structure, where no reconstruction is needed.

Rust occupies an interesting middle ground in this comparison. Rust's ownership model shares conceptual similarities with SSA's single-assignment property: each binding owns its value, and ownership transfer is explicit. Some have argued that Rust is therefore also a natural fit for MLIR. We see merit in this perspective; Rust's explicitness about ownership does provide information that compilers can exploit. However, Rust remains fundamentally imperative in control flow, requiring the same reconstruction that C++ needs. Rust's advantages lie in the safety guarantees it provides before compilation, not in the structure it provides during compilation. Both approaches have value; they optimize for different properties.

## The Revelation: Functional Programming IS Efficient Compilation

The point in combining Appel's work with modern MLIR is this:

> Functional programming isn't a high-level abstraction that must be compiled away, it's the natural structure of efficient compilation itself.

When you write Clef code, you're writing in the same structure that optimizing compilers target. When you use delimited continuations, you're making explicit the control flow that SSA must represent. When you compose operations, you're creating the exact relationships that MLIR's passes optimize.

This isn't about forcing functional programming onto systems development. It's recognizing that the most efficient compilation representations (SSA within MLIR) are inherently functional, making a language rooted in those principles the natural choice for MLIR's lowering strategy.

## Looking Forward: The Fidelity Advantage

By starting with Clef and compiling through MLIR, Fidelity achieves several advantages:

1. **Natural Mapping**: No impedance mismatch between source and target
2. **Preserved Intent**: High-level patterns survive through compilation
3. **Better Optimization**: MLIR works with original structure, not reconstructions
4. **Simpler Mental Model**: Developers write with intent naturally aligned to the compiler

As we move toward an era of heterogeneous computing, CPUs, GPUs, TPUs, and custom accelerators, the ability to preserve high-level intent through compilation becomes crucial. MLIR's dialect system excels at this, and Clef's design provides an effective source language for expressing computations that can be efficiently mapped to diverse hardware.

We should note that MLIR itself is language-agnostic, and projects like Rust-GPU demonstrate that imperative languages can target heterogeneous hardware effectively. The question is not whether a language with functional roots is the only path to MLIR but whether functional structure provides advantages in preserving semantic information through the compilation pipeline. Our experience suggests it does, particularly for the kinds of coordination and memory management patterns that Fidelity emphasizes. Different languages will continue to evolve their MLIR integration strategies, each bringing their own strengths to the heterogeneous computing challenge.

## Conclusion

Andrew Appel's insight that "SSA is Functional Programming" isn't just a theoretical curiosity, it's a fundamental truth about efficient compilation. The Fidelity framework embraces this truth, using Clef not because we prefer any particular paradigm, but because Clef's design ***embodies*** the natural form of modern, structurally correct optimizing compilers.

When you write Clef code targeting MLIR through Fidelity, you're not fighting against the compilation model, you're working with it. Your source code largely expresses the SSA structure that other languages must reconstruct. Your delimited continuations make explicit the control flow that others must analyze. Your composition of operations directly maps to the optimization opportunities that MLIR can more easily transform.

This is why Clef is a natural fit for MLIR: not because we've made it work, but because at the deepest level, they speak the same language. That's the language of functional transformations that has been awaiting discovery at the heart of efficient compilation.
