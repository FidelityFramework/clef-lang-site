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

The blog post "Abstract Machine Models - Also: what Rust got particularly right" makes a compelling case for Abstract Machine Models (AMMs) as a missing conceptual layer between computer science and hardware. The author, reflecting on a failed microprocessor project, discovers that programmers don't reason about either programming theory or raw hardware, but rather about intermediate mental models that predict extra-functional behavior: execution time, memory usage, concurrency patterns, energy consumption. These AMMs, the author argues, exist independently of both languages and hardware, explaining how a C programmer can transfer skills to Python despite their semantic differences.

The article presents three design philosophies: machine-first (where new hardware drives new AMMs), second-language (reusing existing AMMs), and AMM-first (defining the mental model to control programmer thinking). This taxonomy seems thorough. Our Fidelity framework and its Composer compiler raise a question the author did not anticipate: what happens when a language naturally embodies the AMM of its compilation target, and the supposed dichotomy between high-level abstraction and machine control no longer holds?

The author's categorization, influenced by their celebration of Rust as a "second-language" success story (reusing C's AMM with added safety), leaves room for a fourth possibility: **AMM-transcendent** design, where a language maintains multiple AMMs simultaneously and selects among them based on semantic analysis and the targeted processor architecture(s). This approach treats the choice as recognizing that different parts of a program align with different execution strategies, rather than committing to a single mental model.

## The Composer Compiler's Opening Gambit

Our Composer compiler does not begin with traditional lexing and parsing into an abstract syntax tree. Instead, it takes a longer path to what was originally dubbed the Program Semantic Graph (PSG), though we now name it the Program Hypergraph (PHG). The PHG is more than an intermediate representation. It is a unified structure that carries semantic, symbolic, and proof information throughout compilation, so each transformation preserves meaning, context, and intent alongside correctness.

The hypergraph supports nanopass compilation, where each transformation is a small, verified step. Because the PHG carries full semantic context, Composer can express multiple AMMs at once. A single Clef function might be viewed as SSA form for traditional optimization, as continuation-passing style for async operations, and as interaction nets for parallel reduction. The compiler is designed to maintain all of these views rather than choose one, selecting the representation based on semantic analysis and, in the design we are building toward, the capabilities of the device the code will run on.

The author of the AMM article would likely object that this violates the principle of AMM independence. How can one language embody multiple incompatible mental models? The answer lies in Clef's computation expressions and what they reveal about computation itself.

## Continuations, Already in the Language

Clef's async workflows and computation expressions are explicit representations of continuation-passing style, not just syntactic sugar. When a Clef programmer writes an async block, they are thinking in continuations rather than in threads or callbacks. This mental model, this AMM, is the same way MLIR's new DCont (delimited continuation) dialect represents computation.

[The DCont dialect, emerging from CMU research](https://github.com/CMUAbstract/mlir-wasm-dialect), recognizes that continuations are central to how modern programs execute. Every async operation, every suspendable computation, every cooperative multitasking system manipulates continuations to do its work. F#, the lineage Clef descends from, has carried this model in its async workflows for years, and that AMM aligns with MLIR's execution model.

The picture extends with [Martin Coll's Inet dialect for MLIR](https://github.com/colltoaction/mlir-inet-dialect). Interaction nets provide a model of computation where parallel reduction is both possible and deterministic. Pure functions can be parallelized automatically without race conditions or synchronization overhead. Clef's expression-oriented, immutable-by-default style maps to this model. Our language's AMM does not force programmers to think about parallelism; it is structured so that parallelism emerges from normal code.

## The Library of Alexandria

This brings us to our "Alex" component of the Composer compiler, conceptually the Library of Alexandria for transformations. Alex does something the AMM article's framework treats as out of reach: it uses parser combinator techniques borrowed from Haskell to generate machine-optimized code. Rather than lowering from high-level to low-level, it uses the compositional structure of functional programming to construct efficient machine code directly.

Parser combinators, particularly the XParsec framework, treat code generation as the inverse of parsing. Just as parsers compose small recognizers into larger grammars, Alex composes small code generators into larger optimizations. Each combinator preserves semantic context while producing efficient MLIR operations. The result is machine code that is both optimized and semantically transparent.

The AMM article would classify this as either machine-first (generating for specific hardware) or AMM-first (controlling programmer thinking), but it sits across both. The parser combinator approach provides high-level compositionality while generating low-level efficiency, a synthesis the article's taxonomy does not account for.

## The Rust Lens

The article's perspective is shaped by Rust's particular achievements. The author declares that "Rust pushed the Pareto envelope" by combining C's hardware control with additional safety guarantees. This is true as far as it goes, but it carries a constrained view of what is possible. Rust's contribution was showing that you could add safety to C's AMM without losing control. The further question is whether the next step is adding safety to imperative programming, or moving past the imperative model while keeping memory safety and hardware efficiency.

The author's Rust-centric view carries several assumptions that limit the analysis. First, the article treats Rust's mandatory memory management as the pinnacle of systems programming, where "every line of code must consider ownership and borrowing." In our BAREWire approach, memory management is fully present while at design time it remains optional rather than mandatory. This lets developers focus on business logic and engage with memory concerns only where it delivers performance benefits, which is intentional control with an informed default, not an abdication of responsibility.

Second, the article assumes that the control/guarantees tradeoff is two-dimensional, with Rust having pushed the frontier as far as it can go. This leaves out a third dimension: abstraction level. Rust operates at a fixed abstraction level, forced by its direct compilation to LLVM. Our use of MLIR opens multiple abstraction levels at once, holding high-level intent while generating efficient code and several avenues to express memory management. The Pareto frontier here is not only control versus guarantees. It also covers holding semantic detail through compilation to maximize the efficiency of compute on the targeted processor.

## What MLIR Changes

The emergence of MLIR challenges the AMM article's assumption that there is a fixed set of AMMs that programmers use. MLIR's dialect system means new AMMs can be created, composed, and transformed. The DCont and Inet dialects are not just implementation details. They are established mathematical models built for efficient expression on sympathetic hardware. The theory has existed for more than a generation (delimited continuations since the 1980s, interaction nets since 1990), and current technology can express them in a way that newer architectures now make practical.

Clef aligns with these models by mathematical correspondence rather than by deliberate design. Our language's emphasis on expressions over statements, its explicit handling of effects through computation expressions, and its type system that preserves information through compilation make it well suited to MLIR's compilation model.

Consider how Clef expresses what MLIR represents: async workflows map to DCont's continuation points, pure functions map to Inet's interaction rules, and sequential computations decompose into SSA form where each binding creates a new immutable value. The programmer writes Clef thinking in one semantic model, and that model decomposes into the multiple AMMs that MLIR supports. Clef holds semantic coherence, one language and one mental model, while that single model projects onto different platforms as needed.

## Heterogeneous Hardware

The AMM article's most telling limitation is an assumption: that a program runs on a single type of processor. This one-program-to-one-chip framing made sense when the choice was between CPU and GPU, but it does not match the current processor landscape. The author's focus on Rust as the exemplar of modern systems programming shows why this gap exists, since Rust's ownership model assumes von Neumann architecture with linear memory. Compile Rust to an FPGA's dataflow architecture or a neuromorphic chip's spike-based computation, and the impedance mismatch becomes apparent.

FPGAs operate on dataflow, neuromorphic chips compute with spikes, CGRAs reconfigure their architecture per workload, and quantum processors manipulate probability amplitudes. Each demands a different AMM. Rust's borrow checker, for all its contributions, has no model for spatial computation, where data flows through configured circuits and "borrowing" has no referent. It has no model for spike-timing-dependent plasticity, where information rides in temporal patterns rather than memory values.

Our Program Hypergraph supports multiple AMMs, and the design we are building toward is meant to let the compiler partition a solution across heterogeneous hardware. Consider a modern AI workload: pattern discovery might run on neuromorphic processors, matrix operations on GPUs, control logic on CPUs, and stream processing on FPGAs. The hypergraph carries both control flow and data flow views, which lets the compiler identify which portions of code align with which hardware execution models. Holding both views is also what makes the cost of choosing one measurable: [flow loss analysis](/blog/going-deep-with-flow-loss-analysis/) reads the parallelism a control-flow target gives up as a figure off the graph.

The article misses a consideration we treat as central: **abstraction is what enables hardware-aware compilation**. The author's Rust-influenced view treats abstraction as something to be fought against or carefully controlled. The hypergraph points the other way. Abstraction that carries semantic detail also exposes computational opportunities, data dependencies, and patterns. It supplies enough information for the compiler to make process-mapping decisions, rather than hiding hardware details. The lowering passes do more than translate to machine code. They analyze the hypergraph to determine ***which* path** through the hardware constellation will maximize each chip's contribution.

## Control Flow versus Data Flow

The distinction between control flow and data flow is more than an implementation detail. It is the key to reading modern computing paradigms. Traditional processors execute control flow: branching, looping, sequential execution. FPGAs, CGRAs, and neuromorphic processors operate on data flow, with values streaming through configured pathways and triggering computations as they arrive.

This is where the article's Rust-colored view becomes most limiting. Rust's ownership model is about control flow: who owns data, when it can be borrowed, when it is dropped. That fits CPUs with their von Neumann architecture. It does not transfer to an FPGA's spatial computation. There is no "owner" of a signal propagating through configured logic blocks. There is no "borrowing" when data flows through a systolic array. The conceptual framework stops applying.

Our Composer compiler carries both CFG (Control Flow Graph) and DFG (Data Flow Graph) views within the same hypergraph structure. A single Clef function might compile to control flow for CPU execution, data flow for FPGA implementation, or spike trains for neuromorphic processing. The compiler holds both views and selects based on the target hardware and the computation's inherent structure rather than forcing a choice.

This is why our approach of making design-time memory management optional rather than mandatory (as explored in [Memory Management by Choice](https://speakez.tech/blog/memory-management-by-choice/)) carries weight here. In Rust, every function signature must declare its ownership semantics: taking ownership, borrowing immutably, or borrowing mutably. On an FPGA those concepts have no referent. Data has flow paths, not ownership. Memory is accessed through spatial locality, not borrowed. When memory management is something the developer reaches for where it pays off, instead of a discipline every value carries, the framework can target architectures where Rust's core abstractions fight the targeted processor's reality.

## A Heterogeneous Future in Action

Consider a theoretical financial risk calculation combining Monte Carlo simulation with pattern recognition. In the AMM article's world, this would run on either a CPU (control flow) or GPU (data parallelism). Composer's hypergraph analysis would surface a richer structure:

```fsharp
let calculateRisk portfolio market =
    // pattern matching on historical data: neuromorphic
    let patterns = detectAnomalies market.history

    // Monte Carlo paths: GPU parallel
    let paths = generateScenarios market.volatility

    // optimization as a dataflow pipeline: FPGA
    let optimal = findMinimumRisk portfolio paths

    // regulatory proofs: CPU, verifier-discharged
    let proof = verifyCompliance optimal patterns

    (optimal, proof)
```

The hypergraph would capture that `detectAnomalies` is sparse pattern matching (suited to neuromorphic), `generateScenarios` is embarrassingly parallel (GPU), `findMinimumRisk` is a dataflow pipeline (FPGA), and `verifyCompliance` needs sequential proof construction (CPU). This future compiler would not force the entire computation onto one processor when the targeted platform supports those devices. It would partition the workload based on each component's structural fit.

Our framework's design targets our patent-pending BAREWire implementation, which is meant to provide zero-copy data movement across these processors through several mechanisms. In the design we are building toward, each piece would be produced for its target and the framework would assemble them with high-speed communication.

## The Paradox Resolved

The AMM article presents a useful initial framework for how programmers might choose to think about computation. It identifies that mental models matter more than either programming language theory or hardware details. It then assumes these models are fixed, independent entities held in tension with each other.

Our Fidelity framework and Composer compiler point to a different reading: AMMs are interconnected rather than independent. A language like Clef that makes effects explicit, continuations transparent, and parallelism eager is not choosing one AMM over another. It treats these models as projections of a shared computational context that compilation must preserve.

The Program Hypergraph is more than a data structure. It is a recognition that all these models (control flow graphs, dataflow graphs, continuation trees, interaction nets) are different views of the same computation. The nanopass compilation strategy is more than an implementation technique. It treats transformations between AMMs as tractable, verifiable, and bidirectional.

When the article asks why programmers would choose between functionally equivalent sorting algorithms, it asks the right question with too narrow an answer. Programmers do use AMMs to reason about performance. With the right language and compilation strategy, they would not have to choose. A well-designed compiler can carry multiple models, analyze the code's semantic intent, and select the execution strategy that fits the targeted processor.

## The Future They Didn't See Coming

The AMM article, written in 2022, reflects a public discussion still dominated by von Neumann "reflexes". The computational landscape is shifting. Tomorrow's workloads will demand parallel execution and also different computational paradigms. The next AI waves will be non-quadratic and will routinely use sparse spike-based inference. Scientific computing will continue to need both deterministic precision and probabilistic approximation. Embedded systems will balance continuous pressure to produce real-time data under increasing power constraints.

These are divergent paths of computing rather than variations on a theme. The article's framework, shaped by its Rust-influenced perspective, assumes programmers must choose one mental model and accept its constraints. Modern applications that ask developers to consider the hardware they run on do not fit neatly into a single constrained paradigm. A computer vision system might need neuromorphic processing for feature detection, FPGA dataflow for convolution, GPU parallelism for training, and CPU control for orchestration, all within the same application.

Our Composer compiler's hypergraph approach points to a different direction. By carrying semantic, symbolic, and proof information throughout compilation, it is designed to target different computational paradigms within a single program, not only different processors. The compiler analyzes which portions of code the targeted hardware can best express.

The underlying technologies exist today. What has been missing is a compilation strategy that can use them coherently. The hypergraph is meant to supply that: a representation rich enough to capture the full range of computational models while carrying the semantic information needed to map workloads to appropriate hardware.

## Abstraction Enables Optimization

The article identifies that AMMs mediate between languages and hardware, but it treats abstraction as a barrier to optimization. This perspective, influenced by Rust's philosophy of explicit control, sets aside a point worth examining. The author celebrates Rust for exposing "access to the largest diversity of I/O interactions" while providing safety guarantees, as if maximum exposure to hardware details is the goal. That exposure may itself be limiting.

Consider what Rust achieved: it took C's imperative, von Neumann-centric AMM and added memory safety. That was a notable step in 2015, and a conservative one, since it accepted C's model of computation as the baseline. The author's claim that Rust provides "intuition about hardware behavior that's at least as good as C's AMM" carries the limitation with it. C's 1970s-era model of sequential memory access is a weak benchmark for hardware intuition in an era of heterogeneous, parallel, and quantum processors.

Our Fidelity framework points the other way: abstraction that carries semantic intent is what makes optimization possible. Preserve semantic intent through compilation, and the compiler can make informed decisions about hardware mapping. Carry proof obligations alongside code transformations, and it can verify correctness against the targeted hardware. Represent computations as hypergraphs rather than trees, and optimization opportunities that sequential syntax would hide become visible.

The author's failed microprocessor project showed the importance of AMMs. It may also have shown something further: the problem was not that languages and hardware were misaligned, but that the compilation technology to bridge them was not yet in place. MLIR's dialect system, combined with Clef's computational model and our Composer compiler's structural detail, supplies that bridge. Where Rust's direct LLVM compilation commits to a single lowering path, MLIR allows progressive refinement through multiple abstraction levels.

Our framework does more than compile Clef to native code. It treats the supposed tradeoffs between abstraction and efficiency as artifacts of limited compilation technology rather than facts about computation. The hypergraph holds multiple computational views at once. MLIR's dialect system allows progressive refinement through abstraction levels. Proof obligations flow through optimizations. These are aspects of one compilation strategy that loosens the old constraints.

The article was right that AMMs matter, and Rust did push important boundaries. The author did not account for what MLIR and hypergraph compilation would make possible. With our Fidelity framework and Composer compiler, programs would have the potential to span heterogeneous processors through principled targeting in place of ad hoc workarounds. We have found no other representative implementations of this AMM-transcendent approach in the standing literature we have reviewed. Abstraction supports optimization rather than fighting it. Multiple AMMs coexist in the same program, selected based on semantic analysis and the hardware's capabilities.

This direction runs through our Fidelity framework and [the Clef language](https://clef-lang.com), which descends from F# and the wider ML lineage whose OCaml branch was used to bootstrap Rust itself. The structures functional programming has carried for decades may have been waiting for the right compilation technology, and our current interest lies in building that technology out as the work continues.

## See also

- [Breaking the P vs NP Mystique]({{< ref "breaking-the-p-vs-np-mystique" >}}): extends the same control-flow-versus-data-flow argument to "intractable" NP-complete problems, showing that the von Neumann reflex, not algorithmic complexity, is what makes them seem hard.
