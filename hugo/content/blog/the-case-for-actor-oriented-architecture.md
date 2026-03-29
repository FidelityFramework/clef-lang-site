---
title: "The Case for Actor-Oriented Architecture"
date: 2024-06-20T16:59:54+06:00
description : "Process-Level Protection Without Runtime Overhead: The Actor Advantage"
tags: ["Design"]
authors: ["Houston Haynes"]
params:
  originally_published: 2024-06-20
  original_url: https://speakez.tech/blog/the-case-for-actor-oriented-architecture/
  migration_date: 2026-03-29
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.

This entry examines the architectural rationale behind avoiding the creation of yet another managed runtime system, instead advocating for our actor-oriented approach. The question isn't merely academic - it strikes at the heart of how we build reliable, performant software across an increasingly heterogeneous computing landscape.

As computing platforms continue to diversify across embedded systems, mobile devices, edge computing, and specialized accelerators, the traditional monolithic runtime model faces increasing challenges. Each new platform variation demands runtime adaptations, updates, and testing cycles that compound over time. Meanwhile, developers find themselves constrained by lowest-common-denominator abstractions that fail to expose the unique capabilities of their target hardware.

Our approach with the Olivier/Prospero actor model provides the security benefits of managed memory without the restrictions and overhead of conventional runtimes. But more importantly, it represents a fundamental shift in how we think about the relationship between application code, platform capabilities, and safety guarantees. Rather than interposing a universal abstraction layer between developers and hardware, we provide compositional building blocks that adapt to each deployment context while preserving consistent semantics.

## THE RUNTIME LANDSCAPE: A REPEATING CYCLE

#### The Legacy of Monolithic Runtimes

Traditional runtimes like the JVM and .NET CLR emerged during an era of relative platform homogeneity, where the primary concern was providing memory safety and cross-platform compatibility across a narrow range of hardware configurations. In the late 1990s and early 2000s, "cross-platform" largely meant spanning Windows, Linux, and perhaps Solaris servers, with occasional forays into desktop applications. The hardware landscape was dominated by x86 and x86-64 architectures, with relatively predictable memory hierarchies and performance characteristics.

These systems introduced significant abstraction layers between application code and hardware, trading performance for an illusion of safety and portability. The JVM, for instance, interposes a bytecode interpreter (or JIT compiler), garbage collector, class loader hierarchy, security manager, and extensive standard library between your application logic and the actual hardware. Each layer adds latency, memory overhead, and unpredictability - particularly problematic when garbage collection pauses interrupt time-sensitive operations.

These runtimes never delivered on the "build once, run anywhere" [marketing that was promoted at the time](https://www.theregister.com/2003/06/09/sun_preps_500m_java_brand/). Instead, developers discovered that subtle differences in runtime implementations, platform-specific bugs, and varying performance characteristics meant that "write once, test everywhere" became the reality. A Java application that ran smoothly on one JVM version might exhibit entirely different behavior on another, even on the same operating system. The promise of universality gave way to the pragmatic recognition that platform diversity couldn't be abstracted away - it could only be managed through comprehensive testing and platform-specific tuning.

#### The Rust Community's Runtime Renaissance

Despite the industry's experience with the limitations of monolithic runtimes, we continue to see new runtime environments emerging. This pattern reflects a persistent belief that the problem wasn't runtimes themselves, but rather the specific implementation choices of previous generations. The thinking goes: "If we could just build a *better* runtime with modern engineering practices and lessons learned, we could avoid the pitfalls of the past."

In the Rust ecosystem, projects like Wasmtime and WASI are effectively creating new runtimes, albeit with different design goals centered around sandboxing and portability. The Lunatic project explicitly positions itself as a WebAssembly-based Erlang VM alternative built in Rust, recreating many runtime characteristics including lightweight processes, supervision trees, and message passing - but with the added complexity of a WebAssembly compilation target. Even Rust's async ecosystem has spawned multiple executor implementations (Tokio, async-std, smol), each effectively functioning as mini-runtimes with their own threading models, I/O abstractions, and scheduling semantics.

The proliferation of async runtimes in Rust illustrates a fundamental problem: when you create a runtime abstraction, you inevitably face choices about scheduling, resource management, and execution semantics that fragment the ecosystem. A library built for Tokio may not work seamlessly with async-std without adaptation layers. This fragmentation mirrors the earlier Java ecosystem's struggles with different servlet containers and application servers, or the Node.js ecosystem's proliferation of competing module systems.

These efforts, while innovative and well-intentioned, ultimately reproduce many of the same challenges that plagued earlier runtime systems: version fragmentation, performance overhead, and adverse deployment burden. A WebAssembly-based runtime still requires a WebAssembly host environment. An async runtime still needs careful tuning for different workload characteristics. And critically, each runtime imposes architectural constraints that limit how directly you can interact with platform capabilities, from hardware accelerators to OS-specific features.

#### The .NET Story: Rescued but Still Confined

The .NET ecosystem presents a particularly instructive case study in the challenges of runtime portability and ecosystem evolution. Despite Microsoft's considerable resources and engineering talent, .NET initially struggled with cross-platform adoption. The original .NET Framework was deeply intertwined with Windows, relying on Win32 APIs and Windows-specific infrastructure for everything from file I/O to UI rendering. Promises of cross-platform compatibility through the CLI specification remained largely theoretical for years.

It took the independent Mono project, driven by Miguel de Icaza and later commercialized by Xamarin, to demonstrate the viability of .NET beyond Windows. Mono represented a substantial reverse-engineering effort, reimplementing the entire .NET runtime and base class libraries on Linux, macOS, and eventually iOS and Android. This external innovation eventually influenced Microsoft to create .NET Core as a truly cross-platform alternative, beginning a long migration path from the Windows-centric .NET Framework.

Yet even this evolution required breaking changes, ecosystem fragmentation, and years of parallel development between .NET Framework, .NET Core, and now unified .NET. Libraries had to be updated or rewritten to work across different runtime versions. Deployment scenarios multiplied as teams needed to target Framework for legacy systems while adopting Core for new development. The friction of this transition demonstrates how deeply architectural decisions become embedded once a runtime achieves significant adoption.

However, even with these advancements and Microsoft's renewed commitment to open-source and cross-platform development, .NET continues to face significant adoption challenges outside Microsoft's ecosystem. The framework remains primarily utilized within Microsoft-adjacent environments, enterprise settings with existing Microsoft investments, and internal line-of-business applications. Despite technical merits - including excellent performance, sophisticated tooling, and a well-designed type system - .NET has not achieved the platform-agnostic ubiquity of environments like Node.js or Python in developer communities focused on startups, embedded systems, or highly specialized computing domains.

This pattern reveals a fundamental truth: even well-designed runtimes struggle to achieve genuine platform diversity once architectural decisions (and public perceptions) are "locked in". The runtime becomes a dependency that must be justified, installed, managed, and maintained across diverse environments. Each new platform or deployment target requires runtime adaptation, testing, and validation. The runtime itself becomes a barrier to adoption, regardless of the quality of the languages and libraries built atop it.

### WHY NOT ANOTHER RUNTIME?

#### The Diminishing Returns of Runtime Development

Building a new runtime system presents several significant challenges that compound over time:

**Architectural Complexity**: Modern runtimes require sophisticated memory management, JIT compilation, AOT compilation options, profiling tools, debugging infrastructure, and comprehensive standard libraries. The JVM took over a decade to reach maturity, with countless person-years invested in optimizing the garbage collector alone. The .NET CLR similarly required sustained investment from one of the world's largest software companies. This represents years of engineering effort before delivering practical value, during which developers must work around immature tooling and incomplete features.

**Performance Overhead**: Every abstraction layer introduces performance costs. While JIT techniques can mitigate some issues through runtime optimization and adaptive compilation, the fundamental overhead of runtime services remains. Garbage collection pauses interrupt real-time operations. Type reflection and dynamic dispatch add indirection costs. Security sandboxing requires permission checks. These overheads prove particularly problematic for resource-constrained environments like embedded systems or edge devices, where every millisecond and megabyte matters.

**Deployment Complexity**: Runtime dependencies complicate deployment across diverse environments, from cloud infrastructure to embedded systems. Each target environment requires the runtime to be installed, configured, and maintained. Version mismatches between development and production environments cause subtle bugs. Security updates to the runtime necessitate coordinating deployments across entire fleets of systems. Container images balloon in size when they must include entire runtime environments. CI/CD pipelines grow complex as they manage multiple runtime versions and platform variations.

**Platform Adaptation Lag**: As hardware diversity increases with specialized AI accelerators, quantum co-processors, and novel architectures, runtimes face an ever-widening gap between their abstractions and the hardware capabilities they must support. A new GPU architecture with novel memory hierarchies requires runtime updates before applications can leverage it. Specialized neural network accelerators demand runtime modifications for efficient access. This lag between hardware innovation and runtime support delays the practical application of new computing capabilities.

**Maintenance Burden**: Once deployed, runtimes create significant backwards compatibility constraints that limit architectural evolution and innovation. Breaking changes fragment the ecosystem, as libraries and applications depend on specific runtime behaviors. Yet maintaining compatibility prevents fixing fundamental design issues. The Java platform still carries legacy APIs from the 1990s that no one recommends but that can't be removed. The .NET Framework compatibility surface became so large that Microsoft effectively had to start over with .NET Core to enable meaningful architectural improvements.

#### The Hidden Costs of "Write Once, Run Anywhere"

The traditional runtime promise of "write once, run anywhere" reveals itself as "write once, debug everywhere" in practice. Consider a Java application that works perfectly on OpenJDK 11 on Linux but exhibits different behavior on Oracle JDK 11 on Windows due to subtle differences in file system handling or thread scheduling. Or a .NET application that runs smoothly in development on Windows but crashes intermittently in production on Linux because of differences in how signals are handled or how DNS resolution works.

Subtle platform differences in runtime behavior persist despite abstraction attempts. DateTime handling varies across platforms. File path normalization behaves differently. Network timeout semantics diverge. These differences often surface only in production under specific load conditions, leading to more complex debugging scenarios than direct native compilation approaches would encounter. With native compilation, platform differences are explicit and visible during development. With runtime abstraction, they hide beneath layers of indirection, emerging unexpectedly as "works on my machine" problems scaled to infrastructure level.

### THE ACTOR MODEL: A MORE FLEXIBLE ALTERNATIVE

#### Security Without the Runtime Tax

Our actor model implementation provides memory safety guarantees traditionally associated with managed runtimes, but without the architectural overhead. The key insight is that process isolation already provides the memory protection we need, while RAII provides deterministic cleanup within process boundaries. We don't need to reinvent these wheels.

**Process-Level Memory Protection**: By using OS processes as the primary isolation boundary, we leverage existing hardware protection mechanisms rather than duplicating them in software. When Actor A sends a message to Actor B running in a different process, the operating system's memory management unit enforces isolation. Actor A cannot accidentally corrupt Actor B's memory, not because of runtime checks or software sandboxing, but because the hardware prevents it. This is the same mechanism that prevents your web browser from corrupting your text editor's memory. It's battle-tested, hardware-accelerated, and costs us nothing to leverage.

**Shared Memory Pools**: Within process boundaries, actors share memory pools managed with RAII (Resource Acquisition Is Initialization). When an actor allocates memory for a message, that memory's lifetime is bound to lexical scope. When the scope exits, the memory is deterministically freed. No garbage collection pauses. No mark-and-sweep algorithms. No generational collection heuristics. The compiler tracks object lifetimes and inserts cleanup code at the appropriate points, providing efficient memory utilization while maintaining logical separation between actors sharing a process.

**Message-Passing Security**: The actor model's emphasis on message passing naturally enforces clean memory boundaries between components. Actors don't share mutable state. They send copies of data (or ownership transfers) via messages. This eliminates entire classes of concurrency bugs - data races, deadlocks from lock ordering issues, and race conditions from unsynchronized access. The programming model itself prevents the most common memory safety issues without requiring garbage collection overhead or complex runtime analysis.

#### Dynamic Allocation Where It Matters

Our architecture provides dynamic memory management where beneficial without imposing it universally. This flexibility stems from treating memory management as a compositional concern rather than a fixed runtime characteristic.

**Configurable Memory Strategies**: Different deployment scenarios (embedded, mobile, server) can use different memory management approaches through functional composition, without changing the programming model. An embedded system might use fixed-size memory pools allocated at initialization, providing deterministic allocation with zero runtime overhead. A mobile application might use a hybrid approach, with critical paths using stack allocation while background tasks use heap allocation. A server deployment might embrace more dynamic allocation patterns, taking advantage of abundant memory and sophisticated OS memory management. The same application code compiles to all three scenarios because memory management strategy is orthogonal to application logic.

**Workload-Adaptive Allocation**: The actor supervisor hierarchy allows dynamic adaptation to changing workloads, allocating resources where needed without the heavy-handed approach of global garbage collection. When request volume increases, supervisors can spawn additional worker actors. When load decreases, workers can be retired and their resources reclaimed. This happens at the granularity of individual actors, not entire application heaps. If a particular subsystem experiences memory pressure, its supervisor can take corrective action without affecting the rest of the application. This fine-grained resource management enables graceful degradation under load rather than application-wide garbage collection pauses.

**Zero-Copy Optimization**: The BAREWire protocol enables efficient communication with minimal copying, particularly beneficial for data-intensive applications. When transferring large buffers between actors, we can pass ownership of memory regions rather than copying data. The receiving actor takes ownership of the buffer, the sending actor releases it, and no memcpy occurs. For shared-memory communication between actors in the same process, we can use direct pointer passing with ownership semantics enforced by the type system. For cross-process communication, we can use shared memory segments with appropriate synchronization. The protocol adapts to the deployment context, using the most efficient mechanism available while maintaining consistent semantics.

#### Platform Diversity Without Abstraction Penalties

Unlike monolithic runtimes that attempt to abstract away platform differences, our approach embraces platform diversity. We recognize that different platforms have different strengths, and attempting to hide those differences behind a universal abstraction inevitably compromises performance on all targets.

**Direct Compilation Path**: The direct [Clef](https://clef-lang.com) to MLIR/LLVM compilation path generates truly native code for each target platform without runtime interpretation overhead. When you compile for x86_64, you get x86_64 machine code that uses native calling conventions, register allocation, and instruction selection. When you target ARM, you get ARM code optimized for that architecture's pipeline characteristics and instruction set. There's no bytecode interpretation, no JIT compilation overhead, no runtime code generation. The compilation happens once, during the build process, producing binaries that start instantly and run at native speeds.

**Hardware-Specific Optimization**: Platform-specific code generation strategies leverage the unique capabilities of each target environment without compromising the programming model. MLIR's dialect system enables progressive lowering from high-level abstractions to platform-specific representations. A vectorized operation in your Clef code can lower to AVX-512 instructions on capable x86 processors, to NEON instructions on ARM, or to scalar operations on platforms without SIMD support. A GPU-accelerated computation can target CUDA, ROCm, or Vulkan depending on available hardware. The same source code, different target-specific optimizations, all without runtime overhead or fallback layers.

**Graceful Degradation**: When boundary conditions cause elements to fall into an unknown state, the system gracefully adapts rather than halting or requiring significant intervention. If an actor fails, its supervisor can restart it, route around it, or fail gracefully according to policy. If a platform capability isn't available, the compiler selects an alternative implementation path rather than throwing a runtime exception. If memory pressure develops, the actor system can shed load by refusing new work rather than thrashing in garbage collection. The system acknowledges that failures happen and plans for them structurally rather than treating them as exceptional conditions requiring human intervention.

### Transcending the Runtime Paradigm

Building another runtime would represent a step backward into an architectural pattern that is increasingly misaligned with the realities of modern computing. The computing landscape of 2024 and beyond bears little resemblance to the relatively homogeneous environment that spawned the JVM and CLR. We now deploy to:

* Embedded microcontrollers with kilobytes of RAM
* Edge devices balancing power consumption against compute requirements
* Mobile platforms with heterogeneous CPU/GPU architectures
* Cloud environments spanning diverse instance types and accelerators
* Specialized AI hardware with novel memory hierarchies
* Quantum co-processors requiring entirely different execution models

The traditional runtime approach of abstracting these differences behind a universal interface leaves performance on the table everywhere. An abstraction broad enough to span this diversity necessarily compromises on each platform's unique strengths.

The future demands embracing hardware diversity rather than abstracting it away, providing developers with consistent programming models that adapt to their deployment targets rather than forcing those targets to adapt to a runtime. This means compilation targeting specific platforms, not interpretation of universal bytecode. It means leveraging OS primitives for process isolation rather than software sandboxing. It means using the type system to enforce safety properties at compile time rather than checking them at runtime.

Our actor-oriented approach with Olivier/Prospero represents this forward-looking architecture. It provides the security benefits traditionally associated with managed runtimes through process isolation and RAII. It offers the flexibility to adapt to diverse computing environments through compositional memory management and platform-specific code generation. And it delivers the performance characteristics of native code by compiling directly to machine instructions without runtime overhead.

Rather than building yet another walled garden, we're creating a framework that thrives in the diverse computing ecosystem of the future. The actor model provides consistent semantics across platforms while allowing implementations to leverage each platform's unique capabilities. Supervision hierarchies enable fault tolerance without global runtime mechanisms. Message passing enforces isolation without garbage collection pauses.

The question isn't whether we can build a better runtime. The question is whether we need a runtime at all. And increasingly, the answer is no. We need better compilation strategies, better compositional abstractions, and better ways to leverage platform capabilities directly. The actor-oriented architecture provides these without the baggage of traditional runtimes.
