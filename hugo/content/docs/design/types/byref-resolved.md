---
title: "ByRef Resolved"
linkTitle: "ByRef Resolved"
description: "How our compile-time structural analysis produces native binaries whose memory safety guarantees survive into the artifact"
date: 2025-05-16
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2025-05-16
  original_url: "https://speakez.tech/blog/byref-resolved/"
  migration_date: 2026-02-15
---

> This article was originally published on the [SpeakEZ Technologies blog](https://speakez.tech) as a response to F# community discussion of byref restrictions in .NET. The framework has matured beyond that context, and this site's revision refocuses the framing on what our framework provides for native compilation rather than on what it solves for one specific platform. In the Ship of Theseus sense, the article in front of you is no longer the one that originally appeared on the SpeakEZ blog; this is the most recent step in a gradual rewrite that has replaced most of the original planks. The .NET-byref entry point survives in the title and in one comparison among several; the rest of the article describes the architecture that makes the question of where memory safety lives answerable on different terms.

Memory safety in systems programming has three established homes: in a runtime tracker (garbage collection), in developer-supplied lifetime annotations (Rust), or in the developer's own discipline (manual management). The Clef compiler places it in a fourth: in the compiled artifact itself, with compile-time structural analysis producing the commitments and MLIR lowering preserving them through to the binary.

The article's title points to one familiar instance of the runtime-tracker pattern's limitations. The byref restrictions in .NET exist because the CLR cannot track interior pointers across heap-allocated state machines, which means F# byrefs cannot be captured in async closures. That restriction is a specific case of a broader pattern: when memory safety lives in a runtime tracker, the tracker's limitations become the language's limitations. The framework's contribution is not a workaround for .NET specifically; it is an alternative position for where memory safety can live, with the structural commitments carried by the artifact rather than mediated by a runtime.

The rest of this document describes the architectural commitments that produce this position, the intellectual lineage they draw on, how the approach compares to the established alternatives, and what the practical infrastructure (BAREWire, Reference Sentinels, RAII actor memory) contributes within the architecture.

## Architectural Commitments

The framework's approach rests on commitments that interact rather than layer. They are presented separately below for clarity, but each depends on the others to produce the framework's value.

**Flat closures with explicit capture.** Every closure in Clef carries its captured environment as a structurally-visible value rather than as an opaque heap allocation. The capture relationships are part of the program's compile-time structure, available to the analysis that places the closure in a region and that determines its lifetime.

**Region-based memory management informed by escape classification.** Allocations are placed in regions whose lifetimes are determined by where the allocated values escape to. A value that does not escape its creating scope lives in the scope's region and is reclaimed at scope exit. A value that escapes to a longer-lived region lives there. The escape classification is performed at compile time and produces concrete region placement decisions in the lowered code.

**Joint constraint reasoning over our program hypergraph.** The PHG carries hyperedge structure connecting values, captured environments, region annotations, and lifetime coeffects. Our compile-time analysis reasons over these hyperedges as joint constraints, with a flat closure's region, its captured environment's region, and the function's parameter regions all participating in a single constraint. This is what makes our approach compositional.

**Verification certificate that describes the artifact's memory behavior structurally.** Our compilation pipeline emits a certificate alongside the binary describing which structural properties the binary realizes. The certificate's contents include dimensional types, region annotations, escape classifications, and lifetime coeffects. The certificate is the structural audit trail for what the binary commits to.

**Native compilation through MLIR.** The lowering through MLIR preserves the structural commitments as concrete code generation decisions. The commitments are built into how the binary lays out and accesses memory. This is what we mean when we say the safety lives in the artifact.

These commitments compose. The flat closure representation supports the joint constraint reasoning that the region inference depends on. The escape classification informs the region placement that the verification certificate records. The MLIR lowering preserves the structural commitments that the joint constraint analysis established. The whole is more than the sum of the parts: a developer who writes ML-family code gets a binary whose memory behavior is described by a structural certificate that survives compilation.

## Where the Architecture Comes From

The framework's intellectual lineage runs through several identifiable contributions, each of which we adapt rather than adopt wholesale.

**Tofte and Talpin's region-based memory management** [1] introduced the region as the fundamental unit of memory lifetime, with regions determined statically from the program's scope structure. Their work established that a Standard ML compiler can infer regions with sufficient precision to eliminate garbage collection in many programs. We adopt the region as the fundamental unit but ground region inference in escape classification informed by joint constraint structure rather than in pure scope analysis.

**Appel and Shao's flat closures** [2] introduced the representation of closures as structurally-explicit values carrying their captured environment, replacing the traditional implementation as opaque heap-allocated pairs of code pointer and environment pointer. We take the flat closure specifically; we do not adopt the broader compilation strategy of Standard ML of New Jersey, only the closure representation that makes capture relationships visible to compile-time analysis.

**MLKit's region inference and Standard ML compilation** demonstrated over decades that an ML-family language can be compiled to native code with region-based memory management at production quality. We took flat closures from MLKit's contribution, not MLKit's full architectural approach. The careful bounding here matters: MLKit's many design decisions reflect Standard ML's specific semantics; our design decisions reflect Clef's semantics.

**Perconti and Ahmed's logical relations for compositional compilation** [3] established the formal foundation for reasoning about compilation as preserving structural properties through lowering passes. Our verification certificate depends on this kind of compositional reasoning: the certificate's claims hold because each lowering pass preserves the relevant structural property, and the composition of preserving passes preserves the composition of properties.

The careful framing of these references is part of our intellectual posture: each contribution gives us a specific element, and the elements compose into a system whose behavior is informed by but not derived from any single source.

## How the Approach Compares

The framework occupies a position none of the established approaches fully occupies.

**Rust's ownership system** provides compile-time memory safety through lifetime annotations that thread through every function signature. The safety is real and the annotation burden is real; Rust developers spend significant effort threading lifetimes through their code. Our approach achieves comparable compile-time safety without per-function lifetime annotations, with the analysis driven by the program's structural properties rather than by developer-supplied annotations.

**Garbage collection** provides memory safety with the lowest annotation burden of any approach: developers write code as if memory is infinite, and the runtime reclaims it. The cost is runtime overhead and unpredictable latency. Our approach pays no runtime cost for memory management, with the cleanup happening at deterministic points (region exit, actor termination) rather than at unpredictable garbage collector invocations.

**Manual memory management** in C and C++ provides maximum control with maximum correctness burden. Our approach provides control comparable to manual management (the developer can reason about region placement, escape classification, and lifetime structure) with the correctness burden carried by the compile-time analysis instead of by the developer.

**Managed runtime restrictions**, of which the .NET byref problem is one instance, illustrate the cost of running memory safety through a runtime tracker. The CLR cannot track interior pointers across heap-allocated state machines, which is why F# byrefs cannot be captured in async closures. The pattern generalizes: when memory safety lives in a runtime tracker, the tracker's limitations become the language's limitations. When memory safety lives in compile-time structural analysis, the analysis's reach is what determines the language's expressive power.

The framework's approach has its own reach. The compile-time analysis covers the structural properties the analysis can express, and programs whose properties exceed that expressivity produce conservative findings rather than clean verdicts. Our verification architecture treats these conservative findings as honest acknowledgments of what is not yet covered, with lemma library extensions tracked as part of the verification roadmap.

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

The buffer's lifetime is determined by the region containing it. The capability can be passed around, stored, and used in async contexts; our compile-time analysis confirms that the capability's use respects the buffer's region without requiring the developer to annotate the capability's lifetime.

Our zero-copy claim has specific scope. Within a single process, BAREWire avoids the defensive copying that managed runtimes require for safety. Across process boundaries on the same machine, memory mapping can extend zero-copy operation to inter-process communication where the representations are compatible. Across network boundaries, serialization happens at some point; we reduce but do not eliminate copying in distributed scenarios. The architecture supports zero-copy where it is achievable.

## Reference Sentinels for Cross-Process Reference State

Where BAREWire handles in-process memory access, Reference Sentinels handle cross-process reference state. Distributed systems carry references whose target processes might terminate, restart, or become unreachable; sentinels provide rich state information about why a reference might be invalid:

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

The verification scope distinction matters for sentinels in particular. Our compile-time verification confirms that the binary correctly implements sentinel-based reference handling: the sentinel's state machine is realized faithfully, the batch verification protocol composes, and the dispatch to the appropriate handler follows the structural rules. The compile-time verification does not confirm that any particular runtime invocation produces a valid sentinel state, because the sentinel's state depends on what other processes do at runtime. The sentinel's verification state, the last-verified timestamp, and the batch verification infrastructure all carry runtime information that compile-time analysis cannot determine in advance.

This structural-vs-operational division has precedent in distributed-system verification. McErlang [4], the model checker for Erlang programs developed at KTH, made the same architectural decision: verify the program's structural properties (state-machine correctness, message-handling protocol, supervisor-tree composition) rather than attempting to verify runtime outcomes (which messages arrive in what order, which processes fail when, what the network does). The Erlang community accepted that scope because the alternative produces both an intractable state space and a verification claim too narrow to act on; any specific runtime trace might satisfy or violate any specific property depending on conditions outside the program. Our framework makes the same choice for the same reason, with sentinels as the locus where the structural-vs-operational boundary sits inside the framework's own verification certificate.

The framework's certificate marks this boundary explicitly. The structural commitments hold for the binary's implementation of the sentinel protocol; the operational outcomes of any specific cross-process call depend on conditions outside the artifact's scope. This clarification is part of what verification means for distributed system concerns rather than a limitation of the verification architecture.

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

The certificate is what makes our verification claim concrete. A reader can audit which structural properties the binary commits to. The certificate's confirmation is structural: the binary correctly implements the analyzed program. The structural commitments hold for any invocation; the values produced by a specific invocation depend on the inputs the binary processes. Properties that depend on runtime conditions (network availability, the order in which other processes write to shared memory, hardware fault behavior) sit in different parts of our architecture; the certificate marks the boundary between structural and operational commitments rather than blurring it.

For the broader verification context, see [Building Proofs for the Real World](/blog/proofs-for-the-real-world/) for how the verification architecture treats range-propagation tier obligations, and the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) design notes for the categorical reading of the four-tier proof architecture into which memory safety properties fit.

## Closing

Memory safety as architecture means the safety is a property of the artifact. Our compile-time analysis runs once. The structural certificate records what the analysis confirmed. The binary realizes the commitments through its layout and access patterns, with the structural decisions baked into the code generation. The position sits between Rust and garbage collection: compile-time safety comparable to Rust's, annotation freedom comparable to GC's, and a cost profile different from both. The intellectual lineage from Tofte and Talpin, Appel and Shao, MLKit, and Perconti and Ahmed gives the architecture its specific shape; the joint constraint reasoning over our program hypergraph is what makes the components compose.

## References

[1] Tofte, M., & Talpin, J. P. (1997). Region-based memory management. *Information and Computation*, 132(2), 109-176.

[2] Appel, A. W., & Shao, Z. (1994). Empirical and analytic study of stack versus heap cost for languages with closures. *Journal of Functional Programming*, 4(4), 415-435.

[3] Perconti, J. T., & Ahmed, A. (2014). Verifying an open compiler using multi-language semantics. In *Programming Languages and Systems*, ESOP 2014, LNCS 8410, 128-148.

[4] Fredlund, L. Å., & Svensson, H. (2007). McErlang: a model checker for a distributed functional programming language. In *Proceedings of the 12th ACM SIGPLAN International Conference on Functional Programming* (ICFP '07), 125-136.
