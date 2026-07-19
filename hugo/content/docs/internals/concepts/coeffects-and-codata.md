---
title: "Coeffects and Codata in Composer"
linkTitle: "Coeffects and Codata"
description: "Coeffect tracking and codata recognition in Composer's async compilation design"
weight: 30
date: 2025-08-01
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
aliases:
  - /blog/coeffects-and-codata-in-composer/
  - /blog/coeffects-and-codata-in-firefly/
params:
  originally_published: 2025-08-01
  migration_date: 2026-02-15
---

Modern async and parallel programming presents an engineering challenge: we need both the performance of low-level control and the safety of high-level abstractions. Nearly 20 years ago, the .NET ecosystem pioneered the `async`/`await` syntactic pattern, making concurrent code accessible to millions of developers and influencing other technology stacks in following years. That pattern comes with tradeoffs: runtime machinery that can become opaque when a developer needs to understand or optimize workload behavior.

With Composer we take a different position in that design space. What if we could preserve Clef's async abstractions while compiling them to transparent, predictable machine code? By applying mathematical concepts from programming language theory, specifically **coeffects** (tracking what code needs from its context) and **codata** (recognizing demand-driven computation patterns), the Composer compiler design aims to transform high-level Clef async code into efficient implementations tailored for speed and safety.

This approach complements existing solutions rather than replacing them. We are exploring how to apply functional programming principles in compiler design to improve performance and observability.

## The Engineering Challenge

Consider a typical Clef async workflow that processes sensor data:

```fsharp
let processSensorStream (sensor: ISensor) (cancellationToken: CancellationToken) = async {
    let buffer = Array.zeroCreate 1024

    while not cancellationToken.IsCancellationRequested do
        let! bytesRead = sensor.ReadAsync(buffer, 0, buffer.Length)

        if bytesRead > 0 then
            let data = buffer |> Array.take bytesRead
            let processed = transformData data

            do! writeToDatabase processed
        else
            break
}
```

In traditional compilation, this code generates layers of runtime machinery that are effectively invisible during execution:

### .NET's Opacity Problem

**Hidden State Machines**: The .NET compiler transforms each `async` block into an opaque state machine class within the Common Language Runtime (CLR). These generated types aren't part of source code, making them difficult to profile or debug. When performance problems arise, which state transitions cause the bottlenecks is left to guesswork.

**Allocation Mysteries**: Every `let!` and `do!` potentially creates:

- Heap-allocated Task objects that standard inspection tools do not expose
- Continuation delegates capturing local state
- Boxing of value types in generic contexts
- Internal queue nodes in the thread pool

These allocations happen deep in runtime code, invisible to standard memory profilers. Total memory pressure is visible, but correlating it back to specific async operations requires substantial manual investigation.

**Scheduling Black Boxes**: The .NET thread pool decides when and where continuations run based on heuristics that preclude direct inspection:

- Work-stealing algorithms with unpredictable CPU cache effects
- Timer queues managed by internal data structures
- I/O completion ports handled by the OS with no visibility

When latency spikes occur, determining whether the cause was scheduling delay, queue congestion, or actual work becomes nearly impossible without considerable effort using specialized tracing.

**Context Capture Overhead**: The `SynchronizationContext` and `ExecutionContext` flow through async calls, carrying security, culture, and synchronization state. This ambient data:

- Gets captured and restored at every await point
- Adds memory overhead that cannot be measured directly
- Introduces performance costs that vary by environment
- Creates coupling to runtime implementation details

In practice, an async operation that takes 10ms in development might take 50ms or more in production due to different synchronization contexts. Memory that appears as "framework overhead" in profilers could be these hidden context captures. There is no way to opt out of this overhead when it is known to be unnecessary: the runtime decides.

### Debugging Challenges

When the above sensor processing code exhibits problems in production:

- Stack traces show framework internals, not the logical flow of the source
- Memory dumps reveal generated classes with cryptic names
- Performance profiles highlight symptoms (GC pressure) not causes
- Concurrency bugs hide in the gaps between state machine transitions

> The runtime owns the program's execution model and provides limited windows into its decision-making process.

### The Composer Alternative

The Composer compiler design aspires to a different goal: analyze what source code needs from its environment at compile time, then generate an efficient implementation for the target hardware. By making the implicit explicit, we aim to replace .NET's runtime opacity with transparency at both compile time and runtime.

## Coeffects

Traditional effect systems track what code *does* to its environment - does it perform I/O, throw exceptions, or mutate state? A coeffect system tracks the converse: what code *needs* from its environment. Does it require network access, specific memory patterns, or the ability to suspend execution?

In mathematical notation:

\[f : \Gamma @ R \vdash \tau\]

The \(R\) represents the coeffect: the resources and context required by \(f\) to produce a value of type \(\tau\) from context \(\Gamma\). The compilation decisions below reference this notation.

### Practical Coeffect Tracking

Composer's emergent design is considering incorporation of context requirements as first-class citizens in its type system:

```fsharp
type ContextRequirement =
    | Pure                           // No external dependencies
    | AsyncBoundary                 // Requires suspension/resumption capability
    | ResourceAccess of Set<Resource>  // Files, network, memory-mapped regions
    | MemoryPattern of AccessPattern   // Sequential, random, streaming
    | HardwareFeature of Feature      // SIMD, GPU, specialized instructions
 
```

These annotations will be derived from and flow through the Program Hypergraph (PHG), enabling sophisticated analysis. Consider a case showing how this may work in practice:

```fsharp
// The compiler would infer: Pure @ Sequential
let sumArray (arr: float[]) =
    arr |> Array.fold (+) 0.0

// The compiler would infer: AsyncBoundary @ Network @ Streaming
let downloadData (url: string) = async {
    let! response = httpClient.GetAsync(url)
    return! response.Content.ReadAsStreamAsync()
}

// The compiler would infer: Pure @ Parallel @ SIMD
let matrixMultiply (a: Matrix) (b: Matrix) =
    Matrix.multiply a b
```

Each coeffect annotation is designed to guide compilation strategy:

```mermaid
graph TD
    subgraph "Source Code Analysis"
        CODE[Clef Function] --> INFER[Coeffect Inference]
        INFER --> CTX[Context Requirements]
    end

    subgraph "Compilation Decision Tree"
        CTX --> P1{Pure + Sequential?}
        P1 -->|Yes| TIGHT[Tight Loop<br/>No Allocations]
        P1 -->|No| P2{AsyncBoundary?}
        P2 -->|Yes| CONT[Continuation-Preserving<br/>State Machine]
        P2 -->|No| P3{SIMD Available?}
        P3 -->|Yes| VECTOR[Vectorized Code]
        P3 -->|No| STANDARD[Standard Compilation]
    end

    subgraph "Target Generation"
        TIGHT --> LLVM1[LLVM IR<br/>Optimized]
        CONT --> LLVM2[LLVM Coroutines]
        VECTOR --> LLVM3[LLVM Vector Intrinsics]
        STANDARD --> LLVM4[LLVM Standard]
    end
```

The coeffect system's ability to track resource access patterns directly addresses what we call the 'byref problem' in traditional .NET. By making memory access patterns explicit at compile time, Composer can generate code that uses direct memory references safely - something impossible in systems where garbage collection can move memory unpredictably. This coeffect-driven approach enables the capability-based memory management that BAREWire implements, separating buffer lifetime from access permissions.

### Coeffect-Driven Optimization Transparency

Unlike traditional compilers that make optimization decisions based on heuristics, Composer's currently proposed coeffect system is designed to make these decisions based on explicit and predictable factors:

```fsharp
// A developer can see exactly why this compiles to a tight loop
let processData (data: float[]) =
    data
    |> Array.map (fun x -> x * 2.0 + 1.0)
    |> Array.filter (fun x -> x > threshold)

// And why this will preserve suspension capability
let processFiles (files: string list) = async {
    for file in files do
        use! stream = File.OpenReadAsync(file)
        let! content = readFullyAsync stream
        do! processContent content
}
```

## Codata

While data represents values we can construct and examine, codata represents computations defined by how they're consumed. Composer's compilation strategy for async and streaming code turns on this distinction, which is rooted in category theory.

### The Data/Codata Duality in Practice

Consider these contrasting approaches:

```fsharp
// Data: Eager, space-consuming, all-at-once
type EagerList<'T> =
    | Nil
    | Cons of 'T * EagerList<'T>

// Codata: Lazy, space-efficient, on-demand
type Stream<'T> = unit -> StreamCell<'T>
and StreamCell<'T> =
    | SNil
    | SCons of 'T * Stream<'T>
```

The eager list must materialize all elements immediately. The stream produces elements only when requested. Beyond the memory difference, this distinction changes how the compiler can optimize the code.

#### The Idiomatic Trap

Most Clef developers reach for familiar patterns that inadvertently create expensive computations:

```fsharp
// Natural but expensive: Creates all intermediate collections
let processLargeDataset (data: float[]) =
    data
    |> Array.map (fun x -> x * 2.0)        // Allocates new array
    |> Array.filter (fun x -> x > 100.0)   // Another allocation
    |> Array.map (fun x -> Math.Sqrt x)    // Yet another allocation
    |> Array.sum                           // Finally consumes

// Space-efficient alternative: Single pass, no intermediate arrays
let processLargeDatasetEfficient (data: float[]) =
    data
    |> Array.sumBy (fun x ->
        let doubled = x * 2.0
        if doubled > 100.0 then Math.Sqrt doubled else 0.0)
```

The first pattern feels more composable and readable, following the natural Clef style of building pipelines. Yet it allocates three intermediate arrays that might never be needed again.

> For a million-element array, that's 24MB of unnecessary allocations.

Part of this is about growing accustomed to functional patterns that take advantage of these features. And in some cases we hope to provide custom analyzers that will provide helpful prompting where applicable.

#### Recognizing the Shift to Codata Thinking

Composer's type system is built to steer developers from eager data patterns toward lazy codata patterns, and we are still working out how that guidance operates in practice:

```fsharp
// The compiler will recognize this pattern and suggest optimization
let analyzeTimeSeries (readings: float[]) =
    readings
    |> Array.map normalizeReading      // Analyzer: "Consider Seq or AsyncSeq"
    |> Array.filter outlierDetection   // Multiple intermediate arrays detected
    |> Array.windowed 10              // Memory pressure warning
    |> Array.map calculateMovingAvg
    |> Array.toList                   // Final materialization

// Codata version - same logic, radically different performance
let analyzeTimeSeriesCodata (readings: float[]) =
    readings
    |> Seq.map normalizeReading       // No allocation
    |> Seq.filter outlierDetection    // Still no allocation
    |> Seq.windowed 10               // Sliding window, constant memory
    |> Seq.map calculateMovingAvg
    |> Seq.toList                    // Only allocates final result
 
```

The codata version maintains the same idiomatic Clef pipeline style but with fundamentally different execution semantics. Each element flows through the entire pipeline before the next begins, maintaining a constant memory footprint. This execution model also admits parallelism and other optimizations tuned to the target hardware, and we are working to bring these algorithms into practice.

#### Choosing Between Eager and Lazy Evaluation

To our point above, the shift isn't absolute. Sometimes eager evaluation is correct:

```fsharp
// Eager is right: Need random access or multiple traversals
let correlationMatrix (data: float[][]) =
    let normalized = data |> Array.map normalize  // Need to traverse multiple times
    Array.init data.Length (fun i ->
        Array.init data.Length (fun j ->
            correlation normalized.[i] normalized.[j]))

// Lazy is right: Single-pass transformations
let streamingStats (data: seq<float>) =
    data
    |> Seq.scan (fun (sum, count) x -> (sum + x, count + 1)) (0.0, 0)
    |> Seq.map (fun (sum, count) -> sum / float count)
    |> AsyncSeq.ofSeq
    |> AsyncSeq.bufferByTime (TimeSpan.FromSeconds 1.0)
```

Our expectation is that the Composer compiler's coeffect analysis will help identify these patterns and suggest more efficient alternatives while preserving Clef's idiomatic programming style.

#### Design-Time Analyzer Guidance

Custom analyzers will be crucial for guiding developers toward efficient patterns. The analyzer could detect:

```fsharp
// Analyzer detects: Multiple intermediate array allocations
let processData (data: float[]) =
    data
    |> Array.map (fun x -> x * 2.0)      // 💡 Fidelity: "3 intermediate arrays allocated"
    |> Array.filter (fun x -> x > 100.0)  // Suggestion: "Consider Seq for single-pass"
    |> Array.map sqrt                     // Quick Fix: "Convert to Seq pipeline"
 
```

The analyzer would provide contextual hints:

- **Multiple traversals detected**: "Array is appropriate here - data accessed multiple times"
- **Single-pass pattern**: "Consider Seq or AsyncSeq to eliminate intermediate allocations"
- **Large collection warning**: "Array allocation >10MB detected - consider streaming"

With coeffect annotations, the analyzer could even show memory impact:

```fsharp
// Analyzer overlay: "Memory: ~24MB intermediate, ~8MB final"
// Coeffects: DataDriven @ Eager @ MemoryPressure(High)
 
```

This design-time feedback would help developers internalize the data/codata distinction until efficient patterns become second nature.

### Recognizing Codata Patterns

Composer's architecture will identify codata patterns in Clef async sequences and generators:

```fsharp
// Will be recognized as codata: infinite sequence, demand-driven
let sensorReadings (sensor: ISensor) (ct: CancellationToken) = asyncSeq {
    let mutable lastReading = 0.0
    while not ct.IsCancellationRequested do
        let! reading = sensor.ReadAsync()
        // Smooth readings with exponential moving average
        lastReading <- 0.1 * reading + 0.9 * lastReading
        yield lastReading
}

// Consumer controls the pace - only takes what it needs
let consumer = async {
    let mutable count = 0
    let cts = new CancellationTokenSource()
    for reading in sensorReadings sensor cts.Token do
        do! processReading reading
        count <- count + 1
        if count >= 1000 then
            cts.Cancel() // Signal to the producer to stop
            return ()
}
```

The compiler design aims to recognize this producer-consumer pattern and generate code that:
- Never buffers more than one value
- Suspends the producer when the consumer isn't ready
- Resumes exactly where it left off
- Uses no heap allocations for the streaming machinery

### Pull-Based Backpressure

Async enumeration on a managed runtime often involves hidden buffering and additional cancellation logic. Codata patterns are designed to compile to natural backpressure:

```fsharp
// Multiple stages of transformation, all demand-driven
let pipeline =
    rawSensorData
    |> AsyncSeq.map validate
    |> AsyncSeq.filter (fun x -> x.IsValid)
    |> AsyncSeq.scan aggregate initialState
    |> AsyncSeq.bufferByTime (TimeSpan.FromSeconds 1.0)
    |> AsyncSeq.map computeStatistics
```

Each stage will pull from the previous only when ready, creating a self-regulating pipeline without explicit coordination.

## Mathematical Foundations

Each optimization decision in Composer's design corresponds to an explicit formalism, set out below.

### Coeffect Algebras and Context Composition

Coeffects form a semilattice structure that enables compositional analysis. Given two computations with coeffects, we can determine the coeffect of their composition:

\[\frac{\Gamma @ R_1 \vdash e_1 : \tau_1 \quad \Gamma, x:\tau_1 @ R_2 \vdash e_2 : \tau_2}{\Gamma @ R_1 \sqcup R_2 \vdash \text{let } x = e_1 \text{ in } e_2 : \tau_2}\]

where \(\sqcup\) represents the least upper bound operation. In plain language, this means "when we combine two computations, the resources required are the combination of resources needed by each computation."

In practical terms:

\[\begin{align}
\text{Pure} \sqcup \text{Pure} &= \text{Pure} \\
\text{Pure} \sqcup \text{AsyncBoundary} &= \text{AsyncBoundary} \\
\text{ResourceAccess}(S_1) \sqcup \text{ResourceAccess}(S_2) &= \text{ResourceAccess}(S_1 \cup S_2)
\end{align}\]

Three properties follow from the semilattice structure:
- Coeffect inference is deterministic and complete
- Composition preserves safety properties
- The compiler can make optimal decisions based on combined requirements

### The Comonad Structure of Context

Coeffects arise from the comonadic structure of context-dependent computation. A comonad \(W\) provides:

\[\begin{align}
\epsilon &: W\,\tau \to \tau \quad &\text{(extract)} \\
\delta &: W\,\tau \to W\,(W\,\tau) \quad &\text{(duplicate)} \\
\text{fmap} &: (\tau_1 \to \tau_2) \to W\,\tau_1 \to W\,\tau_2 \quad &\text{(functor map)}
\end{align}\]

For async computations, the comonad tracks suspension capability:

\[W_{\text{async}}\,\tau = \text{SuspensionContext} \to (\tau + \text{Continuation})\]

This formalism will directly inform code generation:
- \(\epsilon\) determines where we can safely extract pure values
- \(\delta\) shows where context must be preserved across suspension points
- \(\text{fmap}\) indicates when transformations can be fused

### Codata as Final Coalgebras

Codata structures are formally defined as final coalgebras. For a functor \(F\), the final coalgebra \(\nu F\) represents the largest fixed point:

\[\nu F = \{x \mid x \cong F(x)\}\]

For streams, the functor is \(F(X) = 1 + A \times X\), giving us:

\[\text{Stream}\,A = \nu X. 1 + A \times X\]

The coalgebra structure \(\alpha : \text{Stream}\,A \to 1 + A \times \text{Stream}\,A\) defines the observation:

```fsharp
type StreamObs<'a> =
    | Done
    | Yield of 'a * Stream<'a>
```

The coalgebraic view explains why codata compiles efficiently:
- Observations are the only operations (no hidden state)
- Memory requirements are predictable (one element at a time)
- Composition preserves the coalgebraic structure

Single-element observation gives Fidelity's broader memory architecture its synchronization points. Each yield in a codata structure is a suspension point that doubles as a resource lifetime boundary, precisely where RAII principles ensure deterministic cleanup. When combined with BAREWire's memory-mapped I/O, these yield points become optimal locations for resource acquisition and release, enabling zero-copy streaming between processes while maintaining memory safety through hardware protection rather than runtime checks.

### Delimited Continuations and Stack Calculus

The theoretical foundation for zero-copy async comes from delimited continuations. In the \(\lambda_{\text{cont}}\) calculus:

\[\frac{\Gamma \vdash e : \tau \quad \Gamma, k : \tau \to \sigma \vdash e' : \sigma}{\Gamma \vdash \text{shift}\,k\,\text{ in }\,e' : \sigma}\]

This translates to stack manipulation operations:

\[\begin{align}
\text{capture} &: \text{Stack} \to \text{Continuation} \\
\text{restore} &: \text{Continuation} \to \text{Stack} \to \text{Stack} \\
\text{switch} &: \text{Continuation} \to \text{Continuation} \to \text{unit}
\end{align}\]

In WAMI's implementation:
- \(\text{capture}\) becomes a stack pointer save
- \(\text{restore}\) becomes a stack pointer restore
- \(\text{switch}\) becomes an atomic pointer swap

These operations require no heap allocation, only pointer arithmetic.

### Parametricity and Free Theorems

Composer's design will also leverage parametricity to derive optimization theorems. For a polymorphic function:

\[f : \forall \alpha. F[\alpha] \to G[\alpha]\]

The parametricity theorem gives us a free theorem about \(f\)'s behavior. For async sequences:

\[\forall \alpha, \beta. \forall g : \alpha \to \beta. \text{map}\,g \circ f_\alpha = f_\beta \circ \text{map}\,g\]

This will mean:
- Map fusion is always valid
- Order of operations can be rearranged
- The compiler can pipeline transformations

These theorems license optimizations that would be unsound in languages without parametric polymorphism.

#### Network-Transparent Optimization via Inet Dialect

Parametricity also applies across process boundaries through MLIR's Inet dialect. Consider a distributed pipeline:

```fsharp
// Data flows across process boundaries
let processRemoteData =
    remoteSource
    |> AsyncSeq.map validate      // Could run locally
    |> AsyncSeq.map transform     // Or remotely
    |> AsyncSeq.filter predicate  // Or split across nodes
 
```

Parametricity guarantees that these transformations can be safely relocated across network boundaries. The Inet dialect will leverage this to:

- **Fuse operations before transmission**: Send `validate >> transform` as a single remote operation
- **Push filters upstream**: Move predicates closer to data sources to reduce network traffic
- **Preserve correctness**: The free theorem ensures behavior remains identical regardless of where operations execute

#### Massive Parallelism Through Accelerator Backends

The same guarantees extend to transparent GPU and accelerator deployment. When the compiler can prove that operations are pure and data-parallel:

```fsharp
// Parametric operations automatically eligible for GPU execution
let processImages =
    images
    |> Array.map (fun img -> img |> resize |> blur |> normalize)
    |> Array.map detectFeatures
    |> Array.filter (fun features -> features.Length > threshold)
```

The free theorems guarantee that this can be safely transformed into:

- **CPU parallel operations** via SIMD vectorization, multi-threading, and process distribution
- **GPU kernels** via MLIR's GPU dialect
- **TPU operations** via MLIR's TensorFlow backends
- **Custom ASIC deployments** via specialized MLIR targets

These free theorems let Composer automatically:

- Batch pure operations into single kernel launches
- Fuse map operations to minimize memory transfers
- Partition work across heterogeneous accelerators
- All while preserving exact Clef semantics

These transformations rest on mathematical soundness rather than heuristic guesswork, which lets a single Clef expression compile to efficient code whether targeting a CPU, GPU cluster, or custom silicon.

## Hardware-Aware Code Generation

The combination of coeffect analysis and codata recognition will enable Composer to choose optimal compilation strategies for different hardware targets. We design for hardware diversity: the same Clef code will compile differently based on deployment context.

### Native Code via LLVM

For native targets, Composer's architecture leverages LLVM's optimization infrastructure while preserving Clef's semantics:

#### Pure Computations

When coeffects indicate pure, data-driven computation:

```fsharp
// Coeffects: Pure + Sequential + NoAllocation
// Blur a single pixel using a 2x2 kernel (simplified Gaussian blur)
let inline blurPixel2x2 (img: float[]) (width: int) (idx: int) =
    // Using width parameter for stride calculation
    let tl = img.[idx]                // top-left
    let tr = img.[idx + 1]            // top-right
    let bl = img.[idx + width]        // bottom-left (using width as stride)
    let br = img.[idx + width + 1]    // bottom-right

    // Simple box blur: average of 4 pixels
    (tl + tr + bl + br) * 0.25
```

This function exemplifies pure computation - it reads from immutable input, performs arithmetic operations, and returns a result with no side effects. The coeffect system recognizes this purity and enables aggressive optimization. When processing an entire image, this function would be called in a tight loop, and the compiler can inline it completely.

The design targets compilation to:
```armasm
blurPixel2x2:
    ; x0 = img pointer, x1 = width, x2 = idx
    lsl     x3, x1, #3           ; x3 = width * 8 (bytes per float64)
    add     x4, x0, x2, lsl #3   ; x4 = img + idx*8 (base address)

    ldr     d0, [x4]             ; Load top-left
    ldr     d1, [x4, #8]         ; Load top-right (next element)
    ldr     d2, [x4, x3]         ; Load bottom-left (width stride)
    add     x5, x4, x3           ; Calculate bottom-right address
    ldr     d3, [x5, #8]         ; Load bottom-right

    fadd    d4, d0, d1           ; Add top pixels
    fadd    d5, d2, d3           ; Add bottom pixels
    fadd    d4, d4, d5           ; Sum all four
    fmul    d0, d4, #0.25        ; Multiply by 0.25 (average)
    ret
```

Notice how the pure functional Clef code compiles directly to efficient assembly with no allocations, no function call overhead, and optimal use of floating-point registers. The purity guarantee allows the compiler to vectorize this operation across multiple pixels when used in a larger image processing pipeline.

#### Async Computations

When coeffects indicate suspension points, the compiler will utilize LLVM coroutine intrinsics:

```fsharp
// Coeffects: AsyncBoundary + ResourceAccess
let processNetworkStream = async {
    let buffer = Array.zeroCreate 4096
    let! bytesRead = stream.ReadAsync(buffer)
    let processed = transform buffer bytesRead
    do! writeResult processed
}
```

The planned compilation approach would use LLVM coroutine intrinsics (future work, not yet implemented):
```llvm
// Proposed: LLVM IR for async continuations (design phase)
define i8* @processNetworkStream() {
entry:
    %hdl = call i8* @llvm.coro.begin(...)
    %suspend = call i8 @llvm.coro.suspend(...)
    switch i8 %suspend, label %suspend [i8 0, label %resume
                                       i8 1, label %cleanup]
resume:
    ; Continuation after async operation
    ; Stack and registers restored exactly
cleanup:
    ; Deterministic resource cleanup
}
```

These continuation points map onto Fidelity's actor-based memory model. When an async operation suspends at a continuation boundary, it aligns with actor message boundaries - the precise moments when Prospero can coordinate memory management decisions. The alignment follows from shared structure: async operations and actor systems are both organized around computational boundaries.

### WebAssembly via WAMI

The WAMI (WebAssembly Machine Interface) backend preserves delimited continuations (dcont) through every stage of compilation, from Clef source to WebAssembly machine code. Control flow survives as a first-class construct at the machine level.

#### The Delimited Continuation Advantage

Compilers conventionally discard the high-level structure of control flow, lowering async/await or yield patterns to opaque state machines. WAMI instead keeps them as delimited continuations:

```fsharp
// Codata pattern: infinite generator
let fibonacci = seq {
    let mutable (a, b) = (0L, 1L)
    while true do
        yield a
        a, b <- b, a + b
}
```

This will compile to WAMI's DCont dialect:

```wasm
(func $fibonacci_generator (param $cont i32) (result i32)
    (local $a i64) (local $b i64) (local $temp i64)

    ;; Initialize state
    (local.set $a (i64.const 0))
    (local.set $b (i64.const 1))

    (loop $generate
        ;; Yield current value using stack switching
        (suspend $yield_tag
            (local.get $a))

        ;; Calculate next value
        (local.set $temp (i64.add (local.get $a) (local.get $b)))
        (local.set $a (local.get $b))
        (local.set $b (local.get $temp))

        (br $generate)))
```

#### True Zero-Copy Suspension

The `suspend` instruction is a machine-level operation that:

1. **Captures the current stack**: The entire computation state, including all locals and the instruction pointer
2. **Packages it as a first-class value**: This continuation can be stored, passed around, or resumed
3. **Requires zero heap allocation**: The captured state resides in the WASM linear memory stack

Compare this to traditional approaches:

```csharp
// Traditional: Heap-allocated state machine
class FibonacciEnumerator : IEnumerator<long> {
    private int state = 0;
    private long a = 0, b = 1, current;

    public bool MoveNext() {
        switch (state) {
            case 0: current = a; state = 1; return true;
            case 1: /* compute next */; return true;
        }
    }
}
```

The traditional approach allocates objects, switches on integers, and loses all connection to the original control flow. WAMI keeps the delimited continuation intact in the generated code.

#### Engineering Benefits of Machine-Level Continuations

**1. Debugging Transparency**: When you pause in a debugger, you see the actual suspended computation, not a synthetic state machine:
```
Stack frame: fibonacci_generator
  $a: 34
  $b: 55
  Suspended at: yield point
  Continuation: can be inspected/resumed
```

**2. Composition Without Overhead**: Multiple generators can compose without intermediate allocations:
```fsharp
let composed =
    fibonacci
    |> Seq.map (fun x -> x * x)
    |> Seq.filter (fun x -> x % 2L = 0L)
    |> Seq.take 100
```

Each stage suspends and resumes directly, passing values through registers, not heap-allocated queues.

**3. Predictable Performance**: The cost model is transparent:
- Suspend: Save stack pointer + registers (< 10 instructions)
- Resume: Restore stack pointer + registers (< 10 instructions)
- No GC pressure, no allocation, no hidden costs

#### Theoretical Correspondence

What Danvy and Filinski described mathematically in 1990, WAMI implements mechanically in 2025:

\[\langle E[\text{shift}\,k.e] \rangle \leadsto \langle e[k \mapsto \lambda x.\langle E[x] \rangle] \rangle\]

The `suspend` instruction performs exactly this reduction:
- E is the evaluation context (the stack)
- shift k.e is the suspend point
- λx.⟨E[x]⟩ is the captured continuation

Mathematical abstractions compile to efficient machine operations without semantic loss.

### Optimization Decision Transparency

Unlike traditional compilers where optimization decisions are opaque, Composer's design will provide its reasoning through detailed compilation telemetry:

```mermaid
flowchart TB
    subgraph "Input"
        SRC[Clef Source Code]
        SRC --> ANALYSIS[Coeffect Analysis]
    end

    subgraph "Analysis Phase"
        ANALYSIS --> MEM{Memory<br/>Pattern?}
        ANALYSIS --> ASYNC{Async<br/>Pattern?}
        ANALYSIS --> PURE{Pure<br/>Function?}

        MEM -->|Sequential| SEQ[Sequential Access]
        MEM -->|Random| RND[Random Access]
        MEM -->|Streaming| STR[Stream Access]

        ASYNC -->|Codata| COD[Demand-Driven]
        ASYNC -->|Data| DAT[Eager Evaluation]

        PURE -->|Yes| NOEFF[No Side Effects]
        PURE -->|No| EFF[Has Effects]
    end

    subgraph "Optimization Selection"
        SEQ --> VEC[Vectorization<br/>+ Prefetch]
        RND --> TILE[Cache Tiling]
        STR --> ZERO[Zero-Copy I/O]

        COD --> CONT[Continuations<br/>Stack Switching]
        DAT --> BUFF[Buffer Pooling]

        NOEFF --> INLINE[Aggressive Inlining]
        EFF --> PRESERVE[Preserve Order]
    end

    subgraph "Output"
        VEC --> CODE[Generated Code]
        TILE --> CODE
        ZERO --> CODE
        CONT --> CODE
        BUFF --> CODE
        INLINE --> CODE
        PRESERVE --> CODE

        CODE --> REPORT[Compilation Ledger:<br/>• Decisions made<br/>• Rationale<br/>• Performance hints]
    end
```



## Deterministic Resource Management

In .NET async code, resource cleanup timing depends on the interaction between `IDisposable`, finalizers, and garbage collection: disposal is explicit, but finalization and collection run on the runtime's schedule. Composer's approach seeks to remedy this architectural friction by associating resource lifetime with continuation boundaries, using a resource calculus based on linear types.

### Linear Resource Tracking

Resources in Composer will follow linear typing discipline, ensuring each resource is used exactly once:

\[\frac{\Gamma, r : \text{Resource}[\tau] \vdash e : \sigma \quad r \in \text{used}(e)}{\Gamma \vdash \text{use}\,r = \text{acquire}() \text{ in } e : \sigma}\]

The type system enforces:
- \(\text{acquire} : \text{unit} \to \text{Resource}[\tau]\) produces a linear resource
- \(\text{release} : \text{Resource}[\tau] \to \text{unit}\) consumes it exactly once
- No duplication: \(\text{Resource}[\tau] \not\to \text{Resource}[\tau] \times \text{Resource}[\tau]\)
- No dropping: \(\text{Resource}[\tau] \not\to \text{unit}\)

This linear resource tracking forms the foundation for Fidelity's complete memory model. While stack-only allocation demonstrates that functional programming doesn't need managed runtimes, the linear type discipline enables sophisticated patterns like arena allocation and actor-based memory management. Each resource's deterministic lifetime - enforced through linear types - becomes a building block for larger architectural patterns where entire actor arenas follow the same RAII principles at a coarser granularity.

Linearity translates to deterministic cleanup in generated code, without requiring the developer to declare it at every termination point:

```fsharp
let processMultipleFiles (files: string list) = async {
    for file in files do
        // Resource coeffect tracked through type system
        use! handle = openFileAsync file
        let! data = handle.ReadAllAsync()

        // Nested resource with guaranteed ordering
        use! compressor = createCompressor()
        let! compressed = compressor.CompressAsync(data)

        do! saveCompressed file compressed
        // Compressor released here, at continuation boundary
    // File handle released here, at continuation boundary
}
```

The compiler will generate cleanup code at precise continuation points:

- Resources are released in reverse acquisition order
- Cleanup happens deterministically, not dependent on GC
- The generated code includes cleanup in the state machine itself
- Exception paths guarantee cleanup through compiler-generated finally blocks

### Memory-Mapped Resources and Zero-Copy I/O

For resources that support memory mapping (via BARE protocol integration with our patent-pending BAREWire implementation), Composer's design will enable true zero-copy async I/O:

```fsharp
// Coeffects: AsyncBoundary + MemoryMapped + ZeroCopy
let processLargeFile (path: string) = async {
    // Memory maps the file, no copying
    use! mapped = MemoryMappedFile.OpenAsync(path)

    // Creates a view, still no copying
    let! view = mapped.CreateViewAsync(offset, length)

    // Process directly on mapped memory
    let result = processInPlace view

    // View released, mapping released, deterministic
    return result
}
```

The zero-copy async I/O enabled by memory mapping reaches cross-process scenarios through Reference Sentinels. When processes share memory-mapped regions, the codata streaming patterns ensure that only one element at a time needs to be accessible, while Sentinels provide rich state information about process availability. This combination enables true zero-copy communication across process boundaries with deterministic cleanup when processes terminate.

## Real-World Benefits



### 1. Predictable Performance Profiles

Developers will be able to reason about performance characteristics at compile time:

```fsharp
// This WILL compile to a tight loop:
let fastPath data = Array.map transform data

// This WILL preserve suspension points:
let asyncPath data = async {
    let! processed = remoteProcess data
    return processed
}
```

Developers will not need to guess whether a JIT will inline, whether allocations will trigger GC, or whether a thread pool will introduce latency.

### 2. Stack-Based Async Patterns

Codata compilation is designed to eliminate allocation overhead in streaming scenarios:

```fsharp
// Traditional: Allocates tasks, delegates, and buffers
let traditional = async {
    let! batches =
        source
        |> AsyncSeq.bufferByCount 100
        |> AsyncSeq.map process
        |> AsyncSeq.toListAsync
    return batches
}

// Composer: Will compile to stack-based state machine
let efficient =
    source
    |> AsyncSeq.bufferByCount 100
    |> AsyncSeq.map process
    |> AsyncSeq.fold accumulate initial
```

Early benchmarks suggest potential for 10-100x reduction in allocation rates for streaming workloads.

### 3. Transparent Cross-Platform Deployment

The architecture enables the same Clef code to compile optimally for different targets:

```fsharp
// Cloud server: Will compile to LLVM with coroutines
// Browser: Will compile to WAMI with stack switching
// Embedded: Will compile to interrupt-driven state machine
let universalAsync = async {
    let! sensor = readSensor()
    let processed = computeResult sensor
    do! transmitResult processed
}
```

Each platform will receive an implementation suited to its constraints, all from one source.

### 4. Debugging and Profiling Transparency

Unlike opaque runtime machinery, Composer's generated code is designed to be debuggable:

- Stack traces will show your actual call flow
- Memory profilers will see your allocations, not framework overhead
- CPU profiles will map directly to your source code
- Continuation points will be visible in tooling

## Academic Foundations

The theoretical underpinnings of Composer's design draw from several areas of programming language research:

**Coeffect Systems** (Petricek et al., 2014) formalized context-dependent computation, providing the mathematical framework for tracking what programs need from their environment. Composer extends this work by using coeffects to drive compilation decisions, an application with no representative implementations in the literature we have reviewed.

**Codata and Demand-Driven Computation** has roots in Turner's work on total functional programming (1995) and was further developed by Danielsson et al. (2006). We have not found prior treatments, in the literature we have reviewed, of the observation that codata patterns map onto continuation-based compilation strategies.

**Delimited Continuations** (Danvy and Filinski, 1990) provide the theoretical foundation for WAMI's stack switching implementation. By recognizing async/await as a syntax for delimited continuations, Composer achieves zero-copy context switching without runtime support.

The integration of these concepts, using coeffects to identify codata patterns and compiling them via delimited continuations, is the synthesis Composer is built on. The mathematical principles that guide the compilation decisions also inform the memory-management strategies. Three of them rest on the coeffect-and-codata substrate: the byref treatment through capability-based memory management, the RAII-based actor cleanup, and the zero-copy cross-process communication through memory mapping and Reference Sentinels.

[Opining Upon Reflection](/blog/opining-upon-reflection/) traces the placed-then-observed pattern from the coeffect substrate to a design-time reflection surface.
