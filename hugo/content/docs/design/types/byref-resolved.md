---
title: "ByRef Resolved"
linkTitle: "ByRef Resolved"
weight: 30
description: "How our compile-time structural analysis produces native binaries whose memory safety guarantees survive into the artifact"
date: 2025-05-16
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2025-05-16
  migration_date: 2026-02-15
---

Systems programming locates memory safety in three established places: a runtime tracker (garbage collection), developer-supplied lifetime annotations (Rust), or the developer's own discipline (manual management). The Clef compiler adds a fourth: the compiled artifact itself, with compile-time structural analysis producing the commitments and MLIR lowering preserving them through to the binary.

The .NET byref problem is one familiar instance of the runtime-tracker pattern's limitations. The byref restrictions in .NET exist because the CLR cannot track interior pointers across heap-allocated state machines, which means F# byrefs cannot be captured in async closures. That restriction is a specific case of a broader pattern: when memory safety lives in a runtime tracker, the tracker's limitations become the language's limitations. Our contribution is not a workaround for .NET specifically. It is an alternative position for memory safety, with the structural commitments carried by the artifact rather than mediated by a runtime.

## Architectural Commitments

Our approach rests on commitments that depend on one another. They are presented separately below for clarity, but each contributes to our framework's value only in combination with the others.

**Flat closures with explicit capture.** Every closure in Clef carries its captured environment as a structurally-visible value. Capture and use analysis establish its lifetime and select stack, region, static, or permitted heap storage. [Application staging](/docs/design/structure-and-performance/arity-on-the-side-of-caution/) preserves the supplied arguments and their evaluation frontiers; saturation alone does not decide whether a callable has an environment. A captureless function needs none, a nonescaping named nested function passes captures as parameters, and an escaping captured value requires storage covering every use of its environment and shared cells.

**Lifetime-driven storage placement.** Complete use and capture relationships establish the required lifetime; escape events alone do not select an allocator. The available target contracts then admit scope, covering region, program-lifetime static storage or genuinely dynamic heap storage. A region placement must cover every retained reference, and an unavailable covering lifetime requires a diagnostic rather than an assumed hoist.

**Joint constraint reasoning over our program hypergraph.** The PHG carries hyperedge structure connecting values, captured environments, region annotations, and lifetime coeffects. Our compile-time analysis reasons over these hyperedges as joint constraints, with a flat closure's region, its captured environment's region, and the function's parameter regions all participating in a single constraint. Solving those regions together rather than in isolation is what lets the analysis compose across the program.

**Verification evidence tied to the artifact.** The delivery contract requires dimensional, storage, lifetime and transformation claims to remain connected to the actual emitted and lowered artifact. A certificate must describe only the properties its checked evidence establishes. A model rebuilt from the source graph alone cannot certify that the backend preserved the actual wiring or storage behavior.

**Native compilation through MLIR.** The lowering through MLIR preserves the structural commitments as concrete code generation decisions. The commitments are built into how the binary lays out and accesses memory.

These commitments define the acceptance work. The flat closure representation exposes the identities and storage relationships that joint reasoning needs. Placement and each affected lowering must preserve or re-establish their properties against the artifact. A passing closure example does not establish a complete certificate for every native, actor or foreign-boundary path.

## Where the Architecture Comes From

Our intellectual lineage runs through several identifiable contributions, each of which we adapt rather than adopt wholesale.

**Tofte and Talpin's region-based memory management** [1] introduced the region as the fundamental unit of memory lifetime, with regions determined statically from the program's scope structure. Their work established that a Standard ML compiler can infer regions with sufficient precision to eliminate garbage collection in many programs. We adopt the region as the fundamental unit but ground region inference in escape classification informed by joint constraint structure rather than in pure scope analysis.

**Appel and Shao's flat closures** [2] introduced the representation of closures as structurally-explicit values carrying their captured environment, replacing the traditional implementation as opaque heap-allocated pairs of code pointer and environment pointer. We take the flat closure specifically; we do not adopt the broader compilation strategy of Standard ML of New Jersey, only the closure representation that makes capture relationships visible to compile-time analysis.

**MLKit's region inference and Standard ML compilation** demonstrated over decades that an ML-family language can be compiled to native code with region-based memory management at production quality. We took flat closures from MLKit's contribution, not MLKit's full architectural approach. We bounded that borrowing narrowly: MLKit's many design decisions reflect Standard ML's specific semantics; our design decisions reflect Clef's semantics.

**Perconti and Ahmed's logical relations for compositional compilation** [3] established the formal foundation for reasoning about compilation as preserving structural properties through lowering passes. Our verification certificate depends on this kind of compositional reasoning: the certificate's claims hold because each lowering pass preserves the relevant structural property, and the composition of preserving passes preserves the composition of properties.

Each contribution gives us a specific element, and the elements compose into a system whose behavior is informed by but not derived from any single source.

## How the Approach Compares

Our approach combines properties that none of the established approaches offers together.

**Rust's ownership system** provides compile-time memory safety through lifetime annotations that thread through every function signature. The safety is real and the annotation burden is real; Rust developers spend significant effort threading lifetimes through their code. Our approach achieves comparable compile-time safety without per-function lifetime annotations, with the analysis driven by the program's structural properties rather than by developer-supplied annotations.

**Garbage collection** can reclaim unreachable storage without explicit release sites. Clef's native design instead requires admitted placement and deterministic lifetime protocols. Allocation, initialization, region reset, release and any required sharing or publication still have runtime costs; avoiding a tracing collector does not make those costs zero.

**Manual memory management** in C and C++ provides maximum control with maximum correctness burden. Our approach provides control comparable to manual management (the developer can reason about region placement, escape classification, and lifetime structure) with the correctness burden carried by the compile-time analysis instead of by the developer.

**Managed runtime restrictions**, of which the .NET byref problem is one instance, illustrate the cost of running memory safety through a runtime tracker. The CLR cannot track interior pointers across heap-allocated state machines, which is why F# byrefs cannot be captured in async closures. The pattern generalizes: when memory safety lives in a runtime tracker, the tracker's limitations become the language's limitations. When memory safety lives in compile-time structural analysis, the language's expressive power is bounded by the reach of that analysis.

The compile-time analysis covers the structural properties it can express, and programs whose properties exceed that expressivity produce conservative findings rather than clean verdicts. Our verification architecture treats these conservative findings as honest acknowledgments of what is not yet covered, with lemma library extensions tracked as part of the verification roadmap.

## BAREWire and In-Process Capability Access

Within a single address space, our BAREWire infrastructure provides capability-based access to memory regions. The capability separates buffer ownership from access rights:

```fsharp
let processLargeData () =
    // Buffer with explicit lifetime managed through region inference
    let buffer = BAREWire.createBuffer<LargeStruct> 1

    // Get a capability that can be passed around
    let writeCapability = buffer.GetWriteAccess ()

    // The capability can be passed to async functions; the buffer's region
    // outlives the async closure because the analysis confirms the escape
    let processAsync (capability: WriteCapability<LargeStruct>) = async {
        do! Async.Sleep 100

        // Direct memory access without copying
        let s = capability.GetDirectAccess ()
        s.UpdateInPlace newValue

        return capability
    }

    async {
        let! cap1 = processAsync writeCapability
        let! cap2 = processOtherData cap1
        return cap2
    }
```

The buffer's lifetime is determined by the region containing it. The capability can be passed around, stored, and used in async contexts. Our compile-time analysis confirms that its use respects the buffer's region without requiring the developer to annotate the capability's lifetime.

Our zero-copy claim has specific scope. Within a single process, BAREWire avoids the defensive copying that managed runtimes require for safety. Across process boundaries on the same machine, memory mapping can extend zero-copy operation to inter-process communication where the representations are compatible. Across network boundaries, serialization happens at some point; we reduce but do not eliminate copying in distributed scenarios. The architecture supports zero-copy where it is achievable.

## Reference Sentinels for Cross-Process Reference State

Where BAREWire handles in-process memory access, Reference Sentinels handle cross-process reference state. Distributed systems carry references whose target processes might terminate, restart, or become unreachable. Sentinels provide rich state information about why a reference might be invalid:

```fsharp
let callActorWithSentinel (actorRef: ActorRef) message =
    match actorRef.Sentinel with
    | None ->
        actorRef.Tell message

    | Some sentinel ->
        match verifySentinel sentinel with
        | Valid ->
            BAREWire.send sentinel.TargetProcessId message

        | Terminated ->
            DeadLetterOffice.Tell (ActorTerminated (actorRef, message))

        | ProcessUnavailable ->
            RetryQueue.Schedule (actorRef, message, TimeSpan.FromSeconds 5.0)

        | Unknown ->
            handleAmbiguousState actorRef message
```

Batch verification reduces the IPC overhead of frequent reference checking by grouping verifications per target process:

```fsharp
let efficientApproach actors messages =
    let byProcess =
        List.zip actors messages
        |> List.groupBy (fun (actor, _) -> actor.Sentinel.TargetProcessId)

    for processId, actorMessages in byProcess do
        let sentinels = actorMessages |> List.map (fun (actor, _) -> actor.Sentinel)
        let results = BAREWire.batchVerifyActors processId sentinels

        for (actor, message), state in List.zip actorMessages results do
            actor.Sentinel.State <- state
            actor.Sentinel.LastVerified <- getCurrentTimestamp ()

            match state with
            | Valid -> deliverMessage actor message
            | _ -> handleFailedDelivery actor message
```

Sentinels bring runtime observations into a protocol whose behavior can be verified. The intended compile-time evidence must establish that the binary implements the sentinel state machine, batch verification and handler dispatch under declared process, network and failure semantics. A particular invocation may observe an available or unavailable process. A proof can nevertheless establish how the implementation handles every admitted outcome, including intervening state changes. A validity observation alone does not establish continued availability through delivery; that stronger guarantee requires an applicable lifetime or protocol premise.

McErlang [4] provides a distributed-system verification precedent. Its controlled execution model explicitly explores message ordering, scheduling and fault behavior. It supports safety checks and temporal-logic properties of executions; the supervisor case study [5] checks both safety and liveness, with state-space growth limiting the configurations that could be fully explored. The scope follows the modeled semantics and checked scenarios. Predicting which execution will occur is unnecessary for proving that every admitted execution satisfies a property.

The framework's certificate must identify the established behavioral claims and the environmental premises on which they depend. The [proof-composition architecture](/docs/internals/verification/proof-composition-and-tooling/) extends this approach through reusable distributed-system laws from foundations such as Iris, Aneris and Verdi. A parameterized theorem can cover a family of protocols or configurations and compose with resource, numerical and lowering evidence. Its supported application premises may be simple graph-derived checks, regardless of the richness of the distributed conclusion.

## RAII Actor Memory

Each actor in Clef receives a memory region whose lifetime is bound to the actor's lifecycle. When the actor terminates, the region is reclaimed:

```fsharp
type PaymentProcessor () =
    inherit Actor<PaymentMessage> ()

    // Allocations from this actor live in the actor's region
    let transactionCache = Dictionary<TransactionId, Transaction> ()

    override this.Receive message =
        match message with
        | ProcessPayment payment ->
            let validated = validatePayment payment
            transactionCache.[payment.Id] <- validated

    // No disposal code: the compiler emits region cleanup at actor termination
 
```

The cleanup is deterministic but not free. A large region with many allocations takes measurable time to reclaim, and that work happens at actor termination. The framework's commitment is predictability rather than zero cost: actor termination latency includes the region reclamation work, and developers building latency-sensitive systems can plan for that deterministic cost. The qualitative difference from garbage collection is that the cost is bounded, predictable, and tied to a specific event in the program's structure rather than distributed across program execution at unpredictable intervals.

Process-level configuration lets developers shape the region pool to match the workload:

```fsharp
let createProcessWithOptimizedArenas workloadType =
    let arenaConfig =
        match workloadType with
        | UIWorkload ->
            { ArenaSize = 50 * MB
              PoolSize = 10
              AllocationStrategy = FastRelease
              CleanupTrigger = OnActorTermination }
        | DataWorkload ->
            { ArenaSize = 500 * MB
              PoolSize = 4
              AllocationStrategy = BulkOperations
              CleanupTrigger = OnArenaFull }
        | RealtimeWorkload ->
            { ArenaSize = 8 * MB
              PoolSize = 20
              AllocationStrategy = Predictable
              CleanupTrigger = Immediate }

    Arena.createProcessPool arenaConfig
```

The configuration informs how regions are sized and when they reclaim, but the structural commitment that allocations live in the actor's region is invariant across configurations.

## Verification Certificate

Our compilation pipeline emits a certificate alongside the binary that describes the structural commitments the binary realizes. The certificate's contents include the region annotations, the escape classifications, the dimensional types, and the lifetime coeffects that the compile-time analysis confirmed.

The certificate puts the verification claim in concrete form. A reader should be able to audit which structural and behavioral properties the evidence establishes for the binary, together with their assumptions and checked realization. Inputs and runtime conditions determine the particular execution. Distributed laws can still establish properties across all executions admitted by the stated network, scheduling and failure model. The certificate records that scope and retains the dependencies when these laws compose with structural evidence.

For the broader verification context, see [Building Proofs for the Real World](/blog/proofs-for-the-real-world/) for how the verification architecture treats range-propagation tier obligations, and the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) design notes for the categorical reading of the four-tier proof architecture into which memory safety properties fit.

## Closing

Memory safety as architecture means the safety is a property of the artifact. Our compile-time analysis runs once, and the structural certificate records what it confirmed. From there the binary realizes those commitments through its layout and access patterns, with the structural decisions baked into code generation. The position sits between Rust and garbage collection: compile-time safety comparable to Rust's, annotation freedom comparable to GC's, and a cost profile different from both. The intellectual lineage from Tofte and Talpin, Appel and Shao, MLKit, and Perconti and Ahmed gives the architecture its specific shape; the joint constraint reasoning over our program hypergraph is what makes the components compose. The displacement argument this document opens with has a companion piece for program metadata in [Opining Upon Reflection](/blog/opining-upon-reflection/).

## References

[1] Tofte, M., & Talpin, J. P. (1997). Region-based memory management. *Information and Computation*, 132(2), 109-176.

[2] Appel, A. W., & Shao, Z. (1994). Empirical and analytic study of stack versus heap cost for languages with closures. *Journal of Functional Programming*, 4(4), 415-435.

[3] Perconti, J. T., & Ahmed, A. (2014). Verifying an open compiler using multi-language semantics. In *Programming Languages and Systems*, ESOP 2014, LNCS 8410, 128-148.

[4] Fredlund, L. Å., & Svensson, H. (2007). McErlang: a model checker for a distributed functional programming language. In *Proceedings of the 12th ACM SIGPLAN International Conference on Functional Programming* (ICFP '07), 125-136.

[5] Castro, D., Gulías, V. M., Benac Earle, C., Fredlund, L.-Å., & Rivas, S. (2011). [A case study on verifying a supervisor component using McErlang](https://dcastrop.github.io/files/2011-prole-mcerlang.pdf). *Electronic Notes in Theoretical Computer Science*, 271, 23–40.
