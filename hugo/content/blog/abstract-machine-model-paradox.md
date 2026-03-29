---
title: "How Fidelity Solves The Abstract Machine Model Paradox"
linkTitle: "Abstract Machine Model Paradox"
description: "How Clef and MLIR Confound the Limitations of CPU/GPU Computational Theory"
date: 2025-09-05
authors: ["Houston Haynes"]
tags: ["Analysis", "Design", "Innovation"]
params:
  originally_published: 2025-09-05
  original_url: "https://speakez.tech/blog/abstract-machine-model-paradox/"
  migration_date: 2026-02-15
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

The blog post "Abstract Machine Models - Also: what Rust got particularly right" makes a compelling case for Abstract Machine Models (AMMs) as a missing conceptual layer between computer science and hardware. The author, reflecting on a failed microprocessor project, discovers that programmers don't reason about either programming theory or raw hardware, but rather about intermediate mental models that predict extra-functional behavior: execution time, memory usage, concurrency patterns, energy consumption. These AMMs, the author argues, exist independently of both languages and hardware, explaining how a C programmer can transfer skills to Python despite their semantic differences.

The article presents three design philosophies: machine-first (where new hardware drives new AMMs), second-language (reusing existing AMMs), and AMM-first (defining the mental model to control programmer thinking). This taxonomy seems comprehensive, even inevitable. Yet the Fidelity framework and its Composer compiler reveal something the author didn't anticipate: what happens when a language naturally embodies the AMM of its compilation target? What happens when the supposed dichotomy between high-level abstraction and machine control dissolves?

The author's categorization, influenced by their celebration of Rust as a "second-language" success story (reusing C's AMM with added safety), misses a fourth possibility: **AMM-transcendent** design, where a language maintains multiple AMMs simultaneously and selects among them based on semantic analysis and the targeted processor architecture(s). This isn't about choosing one mental model; it's about recognizing that different parts of a program naturally align with different execution strategies.

## The Composer Compiler's Opening Gambit

The Composer compiler doesn't begin with traditional lexing and parsing into an abstract syntax tree. Instead, it takes a longer, more principled path to what was originally dubbed the Program Semantic Graph (PSG), though its true nature demands the name Program Hypergraph (PHG). This isn't merely an intermediate representation; it's a unified structure that maintains semantic, symbolic, and proof information throughout compilation. Every transformation preserves not just correctness but meaning, context, and intent.

This hypergraph enables nanopass compilation, where each transformation is a small, verified step. But here's where things get interesting: because the PHG maintains full semantic context, Composer can express multiple AMMs simultaneously. A single Clef function might be viewed as SSA form for traditional optimization, as continuation-passing style for async operations, and as interaction nets for parallel reduction. The compiler doesn't choose between these views; it maintains all of them, selecting the appropriate representation based on semantic analysis, and eventually, the capabilities of the device on which it will be executed.

The author of the AMM article would likely object that this violates the principle of AMM independence. How can one language embody multiple, seemingly incompatible mental models? The answer lies in Clef's computational expressions and what they reveal about the nature of computation itself.

## The Continuation Revolution in Plain Sight

Clef's async workflows and computation expressions aren't just syntactic sugar; they're explicit representations of continuation-passing style. When a Clef programmer writes an async block, they're not thinking about threads or callbacks; they're thinking in continuations. This mental model, this AMM, happens to be exactly how MLIR's new DCont (delimited continuation) dialect thinks about computation.

[The DCont dialect, emerging from CMU research](https://github.com/CMUAbstract/mlir-wasm-dialect), isn't just another control flow mechanism. It recognizes that continuations are fundamental to how modern programs actually execute. Every async operation, every suspendable computation, every cooperative multitasking system is, at its core, manipulating continuations. F# programmers have been writing in this model for years without necessarily knowing it. Their AMM naturally aligns with MLIR's execution model.

But the story deepens with [Martin Coll's Inet dialect for MLIR](https://github.com/colltoaction/mlir-inet-dialect). Interaction nets provide a model of computation where parallel reduction is not only possible but deterministic. Pure functions can be automatically parallelized without race conditions or synchronization overhead. Again, Clef's expression-oriented, immutable-by-default style naturally maps to this model. The language's AMM isn't forcing programmers to think about parallelism; it's structured so that parallelism emerges naturally from normal code.

## The Library of Alexandria

This brings us to the "Alex" component of the Composer compiler, conceptually the Library of Alexandria for transformations. Alex does something that should be impossible according to the AMM article's framework: it uses parser combinator techniques borrowed from Haskell to generate machine-optimized code. This isn't lowering from high-level to low-level; it's using the compositional power of functional programming to construct efficient machine code directly.

Parser combinators, particularly the XParsec framework, treat code generation as the inverse of parsing. Just as parsers compose small recognizers into complex grammars, Alex composes small code generators into complex optimizations. Each combinator preserves semantic context while producing efficient MLIR operations. The result is machine code that's both highly optimized and semantically transparent.

The AMM article would classify this as either machine-first (generating for specific hardware) or AMM-first (controlling programmer thinking). But it's both and neither. The parser combinator approach provides high-level compositionality while generating low-level efficiency. It's a synthesis the article's taxonomy doesn't account for.

## The Rust Lens

The article's perspective is notably shaped by Rust's particular achievements. The author declares that "Rust pushed the Pareto envelope" by combining C's hardware control with additional safety guarantees. This is true as far as it goes, but it reveals a constrained view of what's possible. Rust's innovation was showing that you could add safety to C's AMM without losing control. But what if the real innovation isn't adding safety to imperative programming, but transcending the imperative model entirely while maintaining memory safety and hardware efficiency?

The author's Rust-centric view manifests in several assumptions that limit the analysis. First, the article treats Rust's mandatory memory management as the pinnacle of systems programming, where "every line of code must consider ownership and borrowing." But as demonstrated in Fidelity's BAREWire approach, memory management can be fully present while at design time it remains optional rather than mandatory. This allows developers to focus on business logic and only engage with memory concerns where it delivers meaningful performance benefits. This isn't abdication of responsibility; it's intelligent defaulting with intentional control.

Second, the article assumes that the control/guarantees tradeoff is fundamentally two-dimensional, with Rust having pushed the frontier as far as it can go. But this misses a crucial third dimension: abstraction level. Rust operates at a fixed abstraction level, forced by its direct compilation to LLVM. Fidelity's use of MLIR enables multiple abstraction levels simultaneously, maintaining high-level intent while generating efficient code and multiple avenues to express memory management. The real Pareto frontier isn't just about control versus guarantees; it's about maintaining semantic richness through compilation to maximize efficiency of compute on the targeted processor.

## Why MLIR Changes Everything

The emergence of MLIR itself challenges the AMM article's fundamental assumption that there's a fixed set of AMMs that programmers use. MLIR's dialect system means new AMMs can be created, composed, and transformed. The DCont and Inet dialects aren't just implementation details; they're well-established mathematical models that are built for high-efficiency expression on sympathetic hardware. The theory has existed for more than a generation - delimited continuations since the 1980s, interaction nets since 1990 - and now technology can express them in a way that wouldn't have been contemplated until the advent of these newer architectures.

Clef happens to align naturally with these models not by design but by mathematical necessity. The language's emphasis on expressions over statements, its explicit handling of effects through computation expressions, its type system that preserves information through compilation - these features make Clef uniquely well suited to MLIR's compilation model.

Consider how Clef expresses what MLIR represents: async workflows map to DCont's continuation points, pure functions map to Inet's interaction rules, sequential computations naturally decompose into SSA form where each binding creates a new immutable value. The programmer writes Clef thinking in one unified semantic model, but that model naturally decomposes into the multiple AMMs that MLIR supports. The beauty is that Clef maintains semantic coherence - one language, one mental model - while that single model projects cleanly onto different platforms as needed.

## The Heterogeneous Hardware Revolution

The AMM article's most telling limitation isn't what it says but what it assumes: that a program runs on a single type of processor. This one-program-to-one-chip mindset might have made sense when the choice was between CPU and GPU, but it's hopelessly primitive for the modern processor landscape. The author's focus on Rust as the exemplar of modern systems programming reveals why this blind spot exists: Rust's ownership model fundamentally assumes von Neumann architecture with linear memory. Try compiling Rust to an FPGA's dataflow architecture or a neuromorphic chip's spike-based computation, and the impedance mismatch becomes immediately apparent.

FPGAs operate on dataflow, neuromorphic chips compute with spikes, CGRAs reconfigure their architecture per workload, quantum processors manipulate probability amplitudes. Each demands a fundamentally different AMM. Rust's borrow checker, for all its innovations, cannot reason about spatial computation where there's no concept of "borrowing" because data flows through configured circuits. It cannot model spike-timing-dependent plasticity where information is encoded in temporal patterns rather than memory values.

The Program Hypergraph doesn't just enable multiple AMMs; the forward-looking design will someday enable the compiler to partition a solution across heterogeneous hardware. Consider a modern AI workload: pattern discovery might run on neuromorphic processors, matrix operations on GPUs, control logic on CPUs, and stream processing on FPGAs. The hypergraph maintains both control flow and data flow views, allowing the compiler to identify which portions of code naturally align with which hardware execution models.

This is a pivotal consideration the article misses: **abstraction is precisely what enables hardware-aware compilation**. The author's Rust-influenced view treats abstraction as something that must be fought against or carefully controlled. But the hypergraph demonstrates the opposite: rich abstraction preserves semantic intent while exposing computational opportunities, data dependencies, and multiple patterns. It's not hiding hardware details; it's providing enough information for the compiler to make intelligent process mapping decisions. The lowering passes don't just translate to machine code; they analyze the hypergraph to determine ***which* path** through the hardware constellation will maximize each chip's contribution.

## Control Flow versus Data Flow

The distinction between control flow and data flow isn't just an implementation detail; it's the key to understanding modern computing paradigms. Traditional processors execute control flow - branching, looping, sequential execution. But FPGAs, CGRAs, and neuromorphic processors operate on data flow - values streaming through configured pathways, triggering computations as they arrive.

Here's where the article's Rust-colored glasses become most limiting. Rust's ownership model is fundamentally about control flow - who owns data, when it can be borrowed, when it's dropped. This makes perfect sense for CPUs with their von Neumann architecture, but try expressing an FPGA's spatial computation in terms of ownership and borrowing. There's no "owner" of a signal propagating through configured logic blocks. There's no "borrowing" when data flows through a systolic array. The entire conceptual framework breaks down.

The Composer compiler maintains both CFG (Control Flow Graph) and DFG (Data Flow Graph) views within the same hypergraph structure. A single Clef function might compile to control flow for CPU execution, data flow for FPGA implementation, or spike trains for neuromorphic processing. The compiler doesn't force a choice; it preserves both views and selects based on the target hardware and the computation's inherent structure.

This is why Fidelity's approach of making design-time memory management optional rather than mandatory (as explored in [Memory Management by Choice](https://speakez.tech/blog/memory-management-by-choice/)) is so crucial. In Rust, every function signature must declare its ownership semantics - taking ownership, borrowing immutably, or borrowing mutably. But on an FPGA, these concepts are meaningless. Data doesn't have "ownership"; it has flow paths. Memory isn't "borrowed"; it's accessed through spatial locality. By making memory management an optional optimization rather than a mandatory concern, Fidelity can target architectures where Rust's core abstractions fight the targeted processor's reality.

## A Heterogeneous Future in Action

Consider a theoretical financial risk calculation combining Monte Carlo simulation with pattern recognition. In the AMM article's world, this would run on either a CPU (control flow) or GPU (data parallelism). But Composer's hypergraph analysis reveals a richer structure:

```fsharp
let calculateRisk portfolio market =
    // Pattern matching on historical data - neuromorphic
    let patterns = detectAnomalies market.history

    // Monte Carlo paths - GPU parallel
    let paths = generateScenarios market.volatility

    // Quantum-inspired optimization - FPGA dataflow
    let optimal = findMinimumRisk portfolio paths

    // Regulatory proofs - CPU with F* verification
    let proof = verifyCompliance optimal patterns

    (optimal, proof)
```

The hypergraph would someday capture that `detectAnomalies` is sparse pattern matching (ideal for neuromorphic), `generateScenarios` is embarrassingly parallel (GPU), `findMinimumRisk` is a dataflow pipeline (FPGA), and `verifyCompliance` needs sequential proof construction (CPU). This future compiler doesn't force the entire computation onto one processor if the targeted platform supports those devices. It would be intelligent enough to partition the workload based on each component's inherent advantage.

Fidelity framework's forward-looking design specifically targets our patent-pending BAREWire implementation providing zero-copy data movement across these processors via a variety of mechanisms. In this future state, each piece of the puzzle would be produced for it's target and Fidelity would assemble them coherently with high speed communication offering seamless efficiency and speed.

## The Paradox Resolved

The AMM article presents a useful initial framework for understanding how programmers might choose to think about computation. It correctly identifies that mental models matter more than either programming language theory or hardware details. But it assumes these models are fixed, independent entities that exist in tension with each other.

The Fidelity framework and Composer compiler demonstrate something more profound: AMMs aren't independent but interconnected. A language like Clef that makes effects explicit, continuations transparent, and parallelism eager isn't choosing one AMM over another. It's revealing that these different models are projections of a deeper computational context that must be preserved through compilation.

The Program Hypergraph isn't just a data structure; it's a recognition that all these models - control flow graphs, dataflow graphs, continuation trees, interaction nets - are different views of the same computation. The nanopass compilation strategy is more than an implementation technique; it's an acknowledgment that transformations between AMMs can be tractable, verifiable, and bidirectional.

When the article asks why programmers would choose between functionally equivalent sorting algorithms, it's asking the right question **but *accepting too narrow an answer***. Yes, programmers use AMMs to reason about performance. But with the right language and compilation strategy, they don't have to choose. A well designed compiler can maintain multiple models, analyze the code's semantic intent, and select the optimal execution strategy for the targeted processor.

## The Future They Didn't See Coming

The AMM article, written in 2022, reflects a public discussion still dominated by von Neumann "reflexes". But the computational landscape is fundamentally shifting. Tomorrow's workloads will demand not just parallel execution but also consideration of completely different computational paradigms. The next AI waves will be non-quadratic and will routinely use sparse spike-based inference. Scientific computing will continue to need both deterministic precision and probabilistic approximation. Embedded systems will balance continuous pressure to produce real-time data under increasing power constraints.

These aren't variations on a theme; they're divergent paths of computing. The article's framework, shaped by its Rust-influenced perspective, assumes programmers must choose one mental model and accept its constraints. But modern applications that demand developers consider the hardware they're running on won't fit neatly into artificially constrained paradigms. A computer vision system might need neuromorphic processing for feature detection, FPGA dataflow for convolution, GPU parallelism for training, and CPU control for orchestration - all within the same application.

The Composer compiler's hypergraph approach suggests a different future. By maintaining semantic, symbolic, and proof information throughout compilation, it can target not just different processors but different computational paradigms within a single program. The compiler analyzes which portions of code align with which expression that can be best expressed by the hardware it's targeting.

This isn't science fiction. The technologies exist today; what's been missing is a compilation strategy that can leverage them coherently. Our hypergraph concept will provide exactly that - a representation rich enough to capture the full spectrum of computational models while maintaining the semantic information needed to map workloads to appropriate hardware.

## Abstraction Enables Optimization

The article correctly identifies that AMMs mediate between languages and hardware, but it treats abstraction as a barrier to optimization. This perspective, clearly influenced by Rust's philosophy of explicit control, misses a fundamental truth. The author celebrates Rust for exposing "access to the largest diversity of I/O interactions" while providing safety guarantees, as if maximum exposure to hardware details is the ultimate goal. But what if that exposure is actually limiting?

Consider what Rust achieved: it took C's imperative, von Neumann-centric AMM and added memory safety. Revolutionary for 2015, but fundamentally conservative in that it accepted C's model of computation as the baseline. The author's claim that Rust provides "intuition about hardware behavior that's at least as good as C's AMM" reveals the limitation - why should C's 1970s-era model of sequential memory access be our benchmark for hardware intuition in an era of heterogeneous, parallel, and quantum processors?

The Fidelity framework demonstrates the opposite: proper abstraction is what makes optimization possible. When you preserve semantic intent through compilation, you can make intelligent decisions about hardware mapping. When you maintain proof obligations alongside code transformations, you can verify correctness regardless of the targeted hardware. When you represent computations as hypergraphs rather than trees, you can expose pivots in optimization that would otherwise be hidden in sequential syntax.

The author's failed microprocessor project revealed the importance of AMMs, but perhaps it also revealed something deeper: the problem wasn't that languages and hardware were misaligned, but that we lacked the compilation technology to bridge them effectively. MLIR's dialect system, combined with Clef's computational model and Composer's structural richness, provides that bridge. Unlike Rust's direct LLVM compilation, which locks you into a single lowering path, MLIR enables progressive refinement through multiple abstraction levels.

The Fidelity framework doesn't just compile Clef to native code. It demonstrates that the supposed tradeoffs between abstraction and efficiency are artifacts of limited compilation technology, not fundamental truths about computation. The hypergraph maintains multiple computational views simultaneously. MLIR's dialect system enables progressive refinement through abstraction levels. Proof obligations flow through optimizations. These aren't separate features; they're aspects of a unified compilation strategy that dissolves the old constraints.

The article was right that AMMs matter. Rust did push important boundaries. But the author didn't account for what MLIR and hypergraph compilation would make possible. With the Fidelity framework and Composer compiler, for the first time, programs will have the potential to span heterogeneous processors not through clever hacks but through principled targeting. Abstraction doesn't fight optimization; it enables it. Multiple AMMs coexist in the same program, selected dynamically based on semantic analysis and the hardware's capabilities.

This future is arriving through the Fidelity framework and [the Clef language](https://clef-lang.com), whose parent language OCaml was used to bootstrap Rust itself. Perhaps the real innovations were always latent in functional programming, waiting for the right compilation technology to unlock them. We hope that the Fidelity framework is one of those keys to open a broader conversation of what's possible in new technology landscape.
