---
title: "Hardware Lessons from LISP"
date: 2025-07-22T00:59:54+06:00
description: "From Boom & Bust to the Rebirth of Dataflow Acceleration"
tags: ["Analysis"]
authors: ["Houston Haynes"]
params:
  originally_published: 2025-07-22
  original_url: https://speakez.tech/blog/hardware-lessons-from-lisp/
  migration_date: 2026-03-29
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

The computing industry stands at a fascinating juncture in 2025. After decades of general-purpose processor dominance that led to the accidental emergence of general purpose GPU, we're witnessing what appears to be a reverse inflection point. Specialized architectures are re-emerging as an economic imperative, but with crucial differences from the LISP machines of the past. Our analysis examines how languages inheriting from LISP's legacy, particularly [Clef](https://clef-lang.com) and others with lineage to OCaml and StandardML, are uniquely positioned to realize the advantages of new hardware coming from vendors like NextSilicon, Groq, Cerebras and Tenstorrent: a concept we're calling Dataflow Graph Architecture (DGA).

## The Phoenix Pattern: An Electric Car Saga

Before diving into the LISP machine story, consider a striking historical parallel: the rise, fall, and resurrection of electric vehicles. In the late 1890s and early 1900s, electric cars weren't just viable; they dominated. By 1900, electric vehicles held the land speed record. In 1912 United States, 38% of automobiles were powered by electricity, with over 33,000 electric cars registered. They were the preferred choice of well-heeled urban customers, featuring luxurious interiors and sophisticated engineering.

{{< img-caption "/images/blog/The_Employment_of_Women_on_the_Home_Front,_1914-1918_Q27982.jpg" "The Employment of Woment on the Home Front" >}}
An employee of the Great Eastern Railway Company driving a battery-powered rail parcel truck somewhere in Britain c. 1914-1918 [Wikipedia]
{{< /img-caption >}}

Yet by 1920, electric vehicles had virtually vanished from the market. The culprits? Henry Ford's mass-produced Model T cost three times less than a typical electric car. Gasoline's energy density provided 20-30 times the range. The electric starter eliminated hand-cranking, removing one of electric's key advantages. Infrastructure development favored gasoline distribution over electrical grids.

Sound familiar? This is precisely the pattern that played out with LISP machines seventy years later. Superior in many technical dimensions, defeated by economics and ecosystem realities, only to return when fundamental constraints; energy efficiency and the promise of reduced environmental impact for electric vehicles, with analogs of computational efficiency for specialized processors; made the "old" approach newly essential.

Today's electric vehicle renaissance isn't merely nostalgia. Tesla's market capitalization exceeds that of the next nine automakers combined. Governments worldwide are mandating the phase-out of internal combustion engines. The technology that seemed permanently obsolete has returned, transformed by lithium-ion batteries, sophisticated power management, and most critically, an environmental imperative that changed the entire cost equation.

This phoenix pattern; where superior but economically disadvantaged technologies lie dormant until external pressures resurrect them in evolved forms; is precisely what we're witnessing with specialized computing architectures.

## The First Golden Age of AI Hardware

To understand where we're heading, we must first appreciate where we've been. In the 1980s, LISP wasn't just another programming language, it was *the* language of artificial intelligence. From MIT's AI Lab to Stanford's Knowledge Systems Laboratory, if you were working on AI, you were almost certainly working in LISP.

{{< img-caption "/images/blog/LISP_machine_ux1200.jpg" "The Employment of Woment on the Home Front" >}}
[The UX-1200 board from Symbolics](http://www.sun3zoo.de/en/lispboard.html). It could be plugged into a Sun VME-Bus Server. Each Lisp machine (a Sun could host up to five) needed a "world", which contained their files and programs. From the outside view this is a (300 MB and more) file.
{{< /img-caption >}}

The dominance was staggering. By the mid-1980s, the AI industry had grown to over $1 billion annually (before adjustment for inflation), with LISP machines representing a significant portion of that market. Companies like Symbolics, LISP Machines Inc. (LMI), Texas Instruments, and Xerox commanded premium prices for their specialized hardware. A single Symbolics 3600 workstation cost $70,000-80,000 in 1983 (approximately $240,000 in 2024 dollars), yet research labs and corporations eagerly paid these prices.

What made LISP machines so compelling? They offered hardware-accelerated features that seemed almost magical at the time:

- **Tagged memory architectures** that performed runtime type checking in hardware.
- **Graph-based memory models** where pointers were edges and data structures were subgraphs
- **Hardware-assisted garbage collection** that made memory management transparent
- **Message-passing primitives** built into the architecture
- **Microcoded instructions** optimized for list processing and symbolic computation

These weren't simple performance optimizations. They represented a fundamentally different view of computation. LISP machines viewed computation as a graph-structured process. Every piece of data carried its type, enabling dynamic hardware dispatch. This core tenet of embedding data semantics directly into the hardware is precisely what modern neuromorphic processors and Coarse-Grained Reconfigurable Arrays (CGRAs) are rediscovering. These new architectures are designed to understand beyond the nature of an operation to what kind of data it is operating on. This enables new levels of parallelism and efficiency that were dismissed as overengineering when general-purpose processors won on cost.

## When That Market Fell Off a Cliff

The collapse of LISP machines, when it came, was swift and brutal; remarkably similar to how electric vehicles disappeared from roads by 1920. What had been a billion-dollar industry dominated by companies like Symbolics and LISP Machines Inc. was effectively extinct by 1992, replaced by general-purpose RISC workstations that cost **three times less** while delivering **2-4 times the raw performance**.

Just as gasoline cars won on cost and convenience despite electric vehicles' technical elegance, RISC workstations crushed LISP machines through sheer economic force. The LISP machine's downfall wasn't merely economic. Software advances in garbage collection algorithms and compiler technology progressively reduced the need for specialized hardware. By the late 1980s, Common LISP compilers on RISC workstations matched the performance of LISP machines for most applications. The broader AI Winter of 1987-1993 delivered the final blow, as expert systems failed to meet inflated expectations and military funding for AI research evaporated. [2]

But here's what the industry overlooked in its focus on cost-efficiency:

> LISP machines weren't wrong about the nature of computation, they were simply ahead of their time and constrained by the economics and technologies of their era.

Like electric vehicles waiting for lithium-ion batteries and climate urgency, LISP's architectural insights would need to wait for their resurrection moment.

## The Pendulum Swings Back

Decades later, the architectural landscape is transforming in ways that mirror the LISP machine era while learning from its failures. Energy efficiency has emerged as the primary constraint driving innovation, with AI workloads consuming unprecedented amounts of power; the same existential crisis that brought electric vehicles roaring back to relevance. This has catalyzed development of radically different approaches to computing that sacrifice generality for domain-specific optimization, but with a crucial evolution: these new architectures transcend traditional Instruction Set Architectures (ISAs) to embrace a spatial computing model.

This puts a double burden on the developer community. There's an imperative to move faster, do more, and create more opportunities in less time. Dataflow architectures offer this inherently, but it requires new thinking. This new paradigm can be aided by the valuable lessons learned in previous technology eras.

### Dataflow Architectures: More Than ISAs

Traditional ISAs have served us brilliantly for sequential computation, embedding assumptions that made sense for their era:

1. **Sequential Execution**: ISAs assume a program counter advancing through memory, perfect for single-threaded work
2. **Temporal Ordering**: Operations happen "before" or "after" each other, ideal for deterministic execution
3. **Thread-Based Control**: Even parallel ISAs model multiple sequential threads, a natural extension of the sequential model
4. **Imperative Commands**: Instructions tell the processor what to do next, clear and unambiguous

Modern dataflow architectures, CGRAs, and neuromorphic systems build upon these foundations while adding new dimensions:

1. **Spatial Execution**: Operations exist at physical locations, complementing temporal sequencing
2. **Data-Triggered Execution**: Operations fire when inputs arrive, enabling natural parallelism
3. **Massive Parallelism**: Thousands of operations execute simultaneously without coordination overhead
4. **Declarative Configuration**: The "program" is a graph topology that describes relationships rather than sequences

As a pointed example of this, [SambaNova speaks to these concepts directly](https://sambanova.ai/blog/accelerating-scientific-applications-with-sambanova-reconfigurable-dataflow-architecture): "The RDA does not have a fixed instruction set architecture (ISA) like traditional architectures, but instead is programmed specifically for each model resulting in a highly optimized, application-specific accelerator." The question then turns to how the current "AI" boom is being supported, where it came from, and why the Von Neumann underpinnings of modern GPU architecture is part of the critical power problems that are limiting the growth of AI.

### GPUs as Accidental AI Infra

Before examining today's purposefully designed dataflow accelerators, we must acknowledge NVIDIA's accidental dominance. Unlike LISP machines' deliberate design for symbolic AI, GPUs stumbled into their AI role through fortunate accidents. Stanford researcher Ian Buck observed: "We started by seeing a fit for matrix multiplies and linear algebra... render[ing] a triangle that could do a matrix multiply." [13] The watershed moment came with AlexNet's 2012 ImageNet victory using just two $500 GTX 580 GPUs.

This accidental infrastructure, subsidized by gaming economics, democratized AI & HPC research in ways LISP machines never could. But what was tolerable as an externality for video games, power-hungry GPUs consuming 300-500 watts for entertainment has metastasized into a global crisis. Training a single large language model now consumes as much electricity as thousands of homes use in a year. Data centers housing GPU clusters strain power grids, consume millions of gallons of water for cooling, and contribute meaningfully to carbon emissions. The same architectural inefficiencies that gamers accepted for better frame rates now threaten the sustainability of AI advancement itself.

This environmental and economic crisis isn't a minor optimization problem, it's an existential challenge that demands fundamental architectural innovation; the same forcing function that resurrected electric vehicles. GPUs, never designed for AI, force square-peg parallel computation through round-hole graphics pipelines. Their power consumption scales catastrophically with model size, creating a hard ceiling on AI progress defined not by algorithmic limits but by the physics of heat dissipation and the economics and logistics of power generation.

### The Renewed Dataflow Landscape

The non-sustainability of the GPU trajectory has catalyzed a renaissance in computer architecture. Companies aren't just seeking marginal improvements, they're racing to find fundamentally different approaches before the current paradigm collapses under its own power consumption. This isn't speculative; major tech companies are already constrained by power availability, not computational capacity.

Reversible computing pioneers like Vaire suggest orders of magnitude in energy improvements by avoiding information erasure entirely. [3] Wafer-scale integration from Cerebras packs 4 trillion transistors onto a single die, achieving 21 PB/s memory bandwidth. [4] RISC-V's extensibility enables heterogeneous architectures like Tenstorrent's combination of general-purpose cores with specialized accelerators. [6]

The emergence of neuromorphic architectures represents perhaps the most radical departure from Von Neumann principles. Companies and research institutions are developing brain-inspired processors that compute through networks of artificial neurons and synapses. Intel's Loihi 2 processor exemplifies this approach with 1 million programmable neurons interconnected through 120 million synapses, all while consuming mere milliwatts of power. Unlike traditional processors that separate memory and computation, Loihi 2 co-locates both functions in its neuron cores, enabling asynchronous, event-driven computation that processes information only when spikes occur.

This spike-based computing paradigm achieves remarkable efficiency for specific workloads; Intel has demonstrated solving optimization problems 50 times faster and with 100 times less energy than conventional CPUs. The architecture excels at tasks requiring adaptation and learning, from odor recognition to robotic control, processing sensory data streams with latencies measured in microseconds.

{{< img-caption "/images/blog/Loihi-2-transparent-with-die.png" "Intel Loihi 2 neuromorphic processor" >}}
The Intel Loihi 2's exposed die reveals its 128 neuromorphic cores arranged in a mesh topology, each containing up to 8,192 neurons that communicate through 'synaptic' connections.
{{< /img-caption >}}

There are many other designs that are at various stages of development. The Fraunhofer EMFT, working within the EU project NeurONN, is developing neurologically inspired computer architectures using novel 2D materials for memristor applications that are up to 330 times more efficient than current technologies in terms of switching speed, lifetime and energy consumption. [15] These neuromorphic systems encode information through coupled oscillating elements, mimicking the distributed computing and storage of biological neural networks; a vision that would have seemed familiar to LISP machine architects, who understood computation as graph transformation decades before the current echelon of advanced processors.

Each of these approaches shares a common thread: they to one degree or another describe computation in terms of graphs, not instruction sequences. This is where Clef and in particular the Firefly compiler within the Fidelity framework is among the vangaurd in being able to maximize the potential of these architectures while continuing to support 'standard die' CPU, GPU and TPU platforms.

## Functional Takes Center Stage (Again)

The architectural features driving these new designs create an unexpectedly favorable environment for functional programming languages, but not for the reasons you might expect. It's not just functional purity or mathematical elegance for its own sake. It's how that semantic model contains natural alignment with dataflow architectures' execution model; the same way electric vehicles' instant torque and simplified drivetrains naturally exceed internal combustion's complexity once battery technology caught up.

### Direct Lineage: LISP → ML → OCaml → Clef

The most authentic line of descent runs through the ML family. When Robin Milner created ML in the 1970s, he preserved LISP's core computational model, computation as graph reduction, while adding static typing. This lineage represents languages that maintain LISP's essential character: viewing computation as transformation of immutable structures through a graph of operations.

### Clef as a Modern Dataflow Bridge

Clef occupies a unique position as a modern, adaptive functional language with both the theoretical foundations and practical tooling for dataflow and control flow architectures. While preserving OCaml's functional core, Clef innovated with several features that extend beyond its ML heritage, a form of principled pragmatism that makes it particularly well-suited to this emerging hybrid paradigm:

**Computation Expressions** represent Clef's most significant innovation beyond OCaml. These enable building domain-specific languages with custom control flow, state management, and sequencing semantics. Unlike monads in other languages that focus primarily on sequencing effects, Clef's computation expressions provide a general framework for defining how computations compose, a natural fit for describing dataflow graphs where the composition rules determine how operations connect and execute. This innovation brought practical, readable syntax to what would otherwise require complex type machinery.

**Asynchronous Workflows**, introduced in 2007, [Don Syme pioneered](https://fsharp.org/history/hopl-final/hopl-fsharp.pdf) the async/await pattern that over the years proliferated throughout other programming languages. While fundamentally a continuation-based pattern for managing asynchronous operations, async workflows proved valuable for both sequential and parallel composition. They elegantly handle the reality that modern systems must coordinate I/O, network calls, and parallel computations without blocking threads. In the context of dataflow architectures, async workflows provide a foundation for expressing computations that can be composed and scheduled efficiently, though the reactive patterns that truly match dataflow's event-driven nature would come from other Clef libraries and patterns built atop this async foundation.

**Units of Measure** represents one of the language's most distinctive innovations, providing compile-time dimensional analysis that becomes essential when compiling to spatial architectures and native hardware. This feature, absent from OCaml and most functional languages, transforms what would be runtime errors into compile-time guarantees.

For example, in dataflow and spatial computing architectures, confusing timing with data flow creates subtle bugs that are nearly impossible to debug at runtime. Units of Measure catch these at compile time:

```fsharp
// Dataflow timing and throughput units
[<Measure>] type token       // Data tokens in flight
[<Measure>] type stage       // Pipeline stages
[<Measure>] type ns          // Nanoseconds

type DataflowNode<'input, 'output> = {
    Latency: int<cycles>
    Throughput: int<token/cycles>
    BufferDepth: int<token>
}

// Compile-time prevention of timing/data confusion
let calculateBackpressure (node: DataflowNode<_,_>) (clockRate: int<cycles/ns>) =
    // This would fail to compile: can't add tokens to cycles
    // let invalid = node.BufferDepth + node.Latency  // ERROR!

    // Correct calculation maintains dimensional consistency
    let maxTokensInFlight = node.BufferDepth
    let processingCapacity = node.Throughput * node.Latency
    min maxTokensInFlight processingCapacity  // Result: int<token>
```

Crucially, Clef's Units of Measure are erased at runtime, meaning this compile-time safety comes with zero performance overhead. Types are carried through MLIR as attributes to serve memory mapping and optimization functions; and when appropriate are elided, leaving primitive numeric types, preserving verified dimensional consistency. This makes Units of Measure ideal for the goal of dependency-free, native compilation: maximum safety during development, maximum performance at runtime.

**Clef's Agent-Based Concurrency via MailboxProcessor**, inherited from Erlang's actor model, provides a message-passing primitive that aligns with dataflow semantics. Each agent maintains its own state and processes messages asynchronously, essentially a dataflow node that activates on message arrival. This Erlang-inspired feature means Clef developers already have the optionality to think in terms of isolated computational units communicating through messages, exactly the mental model needed for dataflow architectures where data packets trigger computation as they arrive at processing junctures.

**Type Providers** enable compile-time integration with external data sources, a critical capability for real-world dataflow systems that must ingest, validate, and process heterogeneous data streams. Unlike traditional approaches that discover data format errors at runtime, Clef's type providers generate strongly-typed representations from external schemas at compile time. This means a dataflow graph processing financial feeds, sensor streams, or scientific datasets knows the exact shape and constraints of its inputs before a single operation executes.

This capability becomes particularly powerful when considering emerging standards like the [Hypergraph Interchange Format (HIF)](https://arxiv.org/html/2507.11520v1), which provides a unified JSON schema for higher-order network data. Clef's type providers could automatically generate type-safe representations from HIF-compliant datasets, enabling seamless integration of complex relational data, from co-authorship networks to chemical reactions to biological interactions, directly into computational pipelines.

> This isn't just about building smarter chatbots.

It's about enabling cross-disciplinary computation where a materials science simulation can safely consume protein interaction networks, or where economic models can incorporate social network dynamics with compile-time guarantees of data compatibility. This unique Clef feature transforms dataflow from an academic exercise into a practical framework for integrating the messy, heterogeneous data streams that define real-world computation.

## What is Old is New Again

The current specialized architecture renaissance differs fundamentally from the LISP machine era in ways that suggest greater staying power, much as today's electric vehicle revolution has economic and environmental tailwinds the 1900s pioneers lacked:

### Economic Incentives Align Differently

Energy costs and AI compute demands create sustained market pressure. Training large language models costs millions of dollars per run [9], justifying specialized hardware investments unthinkable in the 1980s. The market is also broader, from edge inference to datacenter training, providing multiple revenue streams. Just as electric vehicles now benefit from carbon pricing, government incentives, and corporate ESG commitments, specialized AI hardware rides a wave of economic forces that didn't exist for LISP machines.

### Technical Advantages Are More Durable

While compiler improvements eroded LISP machines' advantages (just as improved gasoline engines temporarily defeated early electric cars), the physical limits of energy dissipation (Landauer's principle) and memory bandwidth (the memory wall) create harder constraints that software alone cannot overcome. Dataflow architectures address these fundamental physics limitations, not just software inefficiencies. Electric vehicles similarly now benefit from fundamental physics advantages: electric motors are inherently more efficient than combustion engines, a fact that no amount of gasoline engine optimization can overcome.

### Open Ecosystems Enable Innovation

Modern dataflow architectures benefit from lessons learned about ecosystem development. MLIR provides common compilation infrastructure in a continuation-passing style "hidden" in C++. An emerging ecosystem around dataflow architectures will inevitably lead to standards and practices for vendor-neutral representations as a common entry point for software developers. One of the main bottlenecks of the LISP era was actually the closed-source nature of the business environment. There was some proliferation of ideas among academic institutions. But the shared resources of the open source model is one of the ways that make this era of intelligent systems design particularly promising and materially different from previous technology eras.

## Dataflow Graph Architectures

The evidence strongly validates this theory of a "reverse inflection point", but with crucial differences. We're not simply returning to specialized architectures, we're expanding our computational model to encompass both sequential and spatial paradigms. Dataflow Graph Architectures complement traditional instruction-based computing by representing the natural parallelism in modern workloads: graphs of operations triggered by data availability, working alongside sequential control where it makes sense. Actor model architectures and other distributed systems concepts have been pointing in this direction for years.

For developers, this means *not* hard-pivoting but rather enriching how we express computation. We can still ask "what happens next?" when sequence matters, while also asking "what depends on what?" when parallelism dominates. We can manage state through time when appropriate, while describing transformations through space when beneficial. We can write sequential code where it's natural, while preserving inherent parallelism where it emerges.

The languages best positioned for this transition are those that maintained graph-reduction principles alongside sequential capabilities. Clef and its ML-family siblings, with their dual heritage from LISP and practical programming, treat computation as transformation while supporting imperative patterns where helpful. Their type systems provide the safety that untyped dataflow would lack. Their functional patterns map naturally to spatial computation while retaining the ability to express sequential logic clearly.

## Implications for Fidelity and Beyond

For the Fidelity framework, the timing is perfect. As specialized architectures mature over the next few years, frameworks that can:

- Preserve computational intent through hypergraph representations
- Support arbitrary numeric types (like posits) natively
- Maintain proof obligations through compilation
- Target diverse hardware through generalized dataflow and control flow representations

...will become increasingly valuable. Fidelity's PHG isn't just another intermediate representation, it's a native encoding that can pivot between dataflow computation and control flow mechanics that preserves the structure each architecture needs.

## A Final Homage to LISP

The LISP machines' ghost doesn't haunt modern computing as a cautionary tale but as vindication. Their graph-based memory, tagged architectures, and message-passing primitives weren't wrong, they were both **of** and ***ahead* of** their time. As the marketplace embraces native dataflow architectures that expand beyond traditional instruction sets, we're not leaving the past behind; we're fulfilling a vision that LISP pioneers glimpsed, and that promise holds to this day.

The phoenix pattern teaches us that revolutionary ideas rarely die; they hibernate. Electric vehicles waited ninety years for their resurrection. LISP machines waited forty. Both returned not as nostalgic revivals but as evolved solutions to problems their original incarnations couldn't have imagined. As we stand at this inflection point, the key lesson from history is clear: **being right about architecture isn't enough if you're wrong about timing and ecosystems**. But in 2025, with AI's insatiable appetite for efficient computation, climate urgency demanding sustainable computing, and an open ecosystem emerging around dataflow architectures, the timing is finally right. The graph-based, type-aware, parallel-by-default future that LISP machines imagined is becoming reality, enhanced by, not replacing, the sequential computing foundations we've built over seven decades.

Just as today's electric vehicles bear little resemblance to their 1900s ancestors while embodying the same fundamental advantages, tomorrow's dataflow architectures will realize LISP's vision through technologies and at scales the pioneers could never have contemplated. The old ideas, refined by decades of dormancy and awakened by necessity, are indeed new again.

---

## References

1. Computer History Museum. (1992). [*1992 Timeline of Computer History*.](https://www.computerhistory.org/timeline/1992/)

2. Wikipedia. (2024). [*AI winter*.](https://en.wikipedia.org/wiki/AI_winter)

3. IEEE Spectrum. (2024). [*Reversible Computing Has Potential For 4000x More Energy Efficient Computation*.](https://spectrum.ieee.org/reversible-computing)

4. Cerebras. (2024). [*Product - Chip*.](https://www.cerebras.net/product-chip/)

5. Next Platform. (2020). [*Groq Shares Recipe for TSP Nodes, Systems*.](https://www.nextplatform.com/2020/09/29/groq-shares-recipe-for-tsp-nodes-systems/)

6. Tom's Hardware. (2024). [*Tenstorrent Shares Roadmap of Ultra-High-Performance RISC-V CPUs and AI Accelerators*.](https://www.tomshardware.com/news/tenstorrent-shares-roadmap-of-ultra-high-performance-risc-v-cpus-and-ai-accelerators)

7. Wikipedia. (2024). [*Tail call*.](https://en.wikipedia.org/wiki/Tail_call)

8. Madhavapeddy, A. (2024). [*Programming FPGAs using OCaml*.](https://anil.recoil.org/notes/fpgas-hardcaml)

9. IMF eLibrary. (2024). [*The Economic Impacts and the Regulation of AI: A Review of the Academic Literature and Policy Actions*.](https://www.elibrary.imf.org/view/journals/001/2024/065/001.2024.issue-065-en.xml) IMF Working Papers Volume 2024 Issue 065.

10. Modular. (2024). [*What about the MLIR compiler infrastructure? (Democratizing AI Compute, Part 8)*](https://www.modular.com/blog/democratizing-ai-compute-part-8-what-about-the-mlir-compiler-infrastructure)

11. Wikipedia. (2024). [*Jensen Huang*.](https://en.wikipedia.org/wiki/Jensen_Huang)

12. Tom's Hardware. (2024). [*Intel's former CEO reportedly wanted to buy Nvidia for $20 billion in 2005 ,  Nvidia is worth over $3 trillion today*.](https://www.tomshardware.com/tech-industry/intels-former-ceo-reportedly-wanted-to-buy-nvidia-for-usd20-billion-in-2005-nvidia-is-worth-over-usd3-trillion-today)

13. NVIDIA Developer. (2015). [*Inside the Programming Evolution of GPU Computing*.](https://developer.nvidia.com/blog/inside-the-programming-evolution-of-gpu-computing/)

14. Wikipedia. (2024). [*AlexNet*.](https://en.wikipedia.org/wiki/AlexNet)

15. Fraunhofer EMFT. (2025). [*Energy-efficient neuromorphic computing*.](https://www.emft.fraunhofer.de/en/projects-fraunhofer-emft/energy-efficient-neuromorphic-computing.html)
