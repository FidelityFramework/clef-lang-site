---
title: "Deadlock Freedom as an Obligation"
linkTitle: "Deadlock Freedom"
description: "Why actor-scoped RAII secures memory but not liveness, and how a static acyclicity check moves deadlock freedom into visible machinery"
date: 2026-06-18T10:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Concurrency"]
params:
  originally_published: 2026-06-18
---

Our actor model gives every actor an arena that lives exactly as long as the actor, and Prospero reclaims it deterministically when the actor dies. That discipline forecloses the memory failures of an actor system: use-after-free, dangling byref, a byref escaping its frame, a reference into a dead actor. It does not foreclose deadlock. Two actors can each park a continuation on a reply that only the other could send, every arena intact, every sentinel reading `Valid`, every lifetime correct, and the system makes no progress while reporting green. This is the gap this document closes, in the same general shape as [managed mutability](/docs/design/managed-mutability/).

## Two different properties wearing the same word "safe"

Actor-scoped RAII is a safety property in the technical sense: nothing bad happens to memory. Each actor owns its arena, cross-process references resolve through sentinels, and cleanup is tied to the actor lifecycle rather than to a collector running on its own clock. Those are the failure modes RAII was built to kill, and in [Olivier and Prospero](/docs/design/raii-in-olivier-and-prospero/) it kills them.

Deadlock is a liveness property: something good eventually happens. A set of actors each blocked waiting for a message that only another blocked actor in the set could send will sit there indefinitely. Prospero never retires any of them, because none has crashed or completed. They are all alive and quiescent. The entire memory apparatus stays consistent while the program stops doing anything.

Safety and liveness are independent axes. Our RAII work drove the safety axis to the floor and left the liveness axis to runtime contingency, because the references are what RAII validates, and the wait-for graph is a different object that nothing in the memory model inspects.

## Where the deadlock edge actually enters

The hazard rides in on one construct: a synchronous reply expectation across actors. A `PostAndReply` call suspends the caller's continuation until the callee answers on a reply channel. That suspension is an edge in a wait-for relation, and a cycle of such edges where every actor is simultaneously blocked is the deadlock.

A fire-and-forget `Tell` adds no such edge. The sender posts to a mailbox and returns, so the asynchronous fraction of a program is invisible to this hazard and cannot deadlock through it. The synchronous request and response carries the whole risk, because that is the only place a caller's progress is contingent on a specific message another actor is contractually bound to produce.

We want RPC available. It is genuinely useful in systems work, and closed-loop request and response is a pattern worth keeping rather than legislating away in favor of pervasive callbacks. So the synchronous edge stays in the model. The question is how to make its liveness visible, not how to delete the construct that carries it.

## What the session-types literature gets, and what it costs

Classical Linear Logic gives one well-studied route to deadlock freedom for synchronous communication. In Wadler's CP and its hypersequent successor HCP, a well-typed process cannot deadlock, established as a corollary of the proof structure. The mechanism in CP fuses channel creation and parallel composition under a single cut rule, which forces the communication topology to be a tree. Deadlock freedom follows because a tree has no cycles to wait around. The price is stated plainly in that work: the only processes allowed are tree-structured. Two actors that hold references to each other are already outside the fragment, and that pattern is common in real supervision graphs.

Priority CP, following Kobayashi and Padovani, buys the cyclic topologies back. It annotates communication actions with priorities, a partial order, and the type checker verifies that the order has no cycle. Cyclic connection graphs become typeable as long as the priorities prove the wait-for relation stays acyclic. The recognized cost is compositionality: priorities are non-local, so a library actor's priorities surface in the types of everything that calls it, and the programmer threads those annotations through by hand.

Neither shape fits our constraints as written. The tree restriction forbids reasonable systems patterns. Pervasive priority annotation is the ceremony I am declining when I say I do not want to write Clef as though it were Haskell. And a compiler pass that silently rejects programs with a cyclic wait-for graph, with no surface the developer reads or steers, is the hidden guard I refuse on principle. Memory safety in our model is not a hidden analysis. The `let mutable` is visible syntax, the escape classification is inspectable, and the allocation decision is reachable by a library author. Deadlock freedom should be a citizen of the same kind.

## The wait-for relation is already in the graph

A synchronous RPC introduces one structural object: a blocking-wait hyperedge. At a call site that issues `callee.PostAndReply msg` and suspends pending the reply, the caller's continuation waits on the callee's reply obligation. Our [Program Hypergraph](https://speakez.tech/blog/coupling-and-cohesion/) already carries this kind of relation on its joint-constraint axis, the axis that holds the hyperedges flat closures, regions, and actor lifetimes generate.

Collect those edges into a wait-for relation \(W\) over actor behaviors. A node `caller —waits-on→ callee` exists whenever the caller issues a synchronous RPC to the callee and parks until the reply. Deadlock is a cycle in \(W\) whose actors are all in their blocked state at once. The relation is a may-wait over-approximation: an actor that can call either of two callees depending on message content contributes an edge to each. Over-approximating keeps the analysis sound, never claiming deadlock freedom that an execution could violate, at the cost of sometimes flagging a cycle no run reaches. That is the same soundness posture our escape analysis takes, where `EscapeKind` over-approximates escape and never under-approximates it.

For the fragment where every callee is a statically resolvable actor reference, \(W\) is a finite directed graph and deadlock freedom reduces to its acyclicity. A strongly-connected-component pass finds any cycle in linear time. This is the same kind of graph analysis the [graph-coloring pass](https://speakez.tech/blog/speed-and-safety-with-graph-coloring/) runs to license interaction-net breakout, applied to a different edge label, and it lands as a Tier 2 obligation built mechanically from graph structure. Most instances discharge by graph algorithm alone, which makes them cheaper than the arithmetic obligations Z3 handles at the same tier.

## A classification that mirrors EscapeKind

Every synchronous RPC call site gets a wait classification, deliberately the same shape as the escape classification managed mutability assigns to every mutable binding:

```fsharp
type WaitClass =
    | AcyclicStatic                     // callee static, W acyclic: guaranteed, silent
    | OrderedCyclic of priority: int    // callee static, in a connection cycle but
                                        //   priority-orderable: guaranteed, priority inferred
    | Unresolved of routing: RoutingKind // callee value-carried: visible downgrade to
                                        //   supervised timeout, diagnostic emitted
```

`AcyclicStatic` is the common case under a mostly-static topology, and it carries no annotation, no marker, and no ceremony. The check ran, the graph was acyclic, and the developer never hears about it.

`OrderedCyclic` is the case CP's tree restriction wrongly forbids: two actors hold references to each other and call in both directions, forming a cycle in the connection graph but not in the wait-for graph, because their calls are ordered so that no execution waits around the loop. A consistent priority assignment exists exactly when the action-dependency graph is itself a DAG, which the compiler checks by topological ordering. The priority is inferred from the wait-for edges. The developer writes nothing. This is the place our inference thesis earns the difference from textbook Priority CP, where the same priority would be a hand-written annotation. Width is inferred from type structure and surfaced only when inference needs help; the synchronous-action priority is the same inference one axis over.

`Unresolved` is the genuinely dynamic case, where the callee is value-carried through `Actor.self()`-passing, content-based routing, or a runtime-spawned handle whose identity is not statically pinned. Acyclicity over such routing is undecidable in general, which is the wall the asynchronous-and-cyclic process-network literature documents and works around. The program is neither forbidden nor silently admitted. The call site drops out of the static guarantee, the compiler says which call dropped and why, and that call falls back to supervised execution with a timeout. RPC stays available, and the boundary of the guarantee is visible at the exact call that crosses it.

## The developer sees the machinery and can change it

The interaction follows the managed-mutability standard: inferred when it holds, a concrete diagnostic when it does not, and an opt-in annotation for the developer who wants control.

In the common case the developer sees nothing. When a cycle is flagged, the compiler reports the wait-for path the way the escape diagnostic reports an escape path. The message is the actual chain, "`ActorA.handleFoo` waits on `ActorB.query`, which waits on `ActorA.handleBar`, a synchronous cycle," rather than a generic warning that deadlock is possible. The developer reads the structural reason and chooses the resolution: supply an explicit priority that breaks the cycle and that the checker verifies, refactor one leg to a `Tell` with an explicit continuation so the wait-for edge disappears, or mark the call for supervised timeout and opt it out of the static guarantee on purpose. The guard is never hidden, and it is always overridable. That property is the line that separates this design from both the tree restriction and the runtime-only timeout.

Writing an explicit priority is the only place priorities-as-syntax appear, and they appear only at the cyclic-and-ambiguous call sites that genuinely need them, not across every channel in the program. The compositionality cost the literature attaches to priorities is real, and confining it to the call that incurs it is the improvement over pervasive annotation.

## How it rides the architecture

The wait-for edges are hyperedges on the joint-constraint axis our [tier architecture](/docs/internals/verification/) already defines, so deadlock freedom is a section over that axis being free of cycles. The `AcyclicStatic` and `OrderedCyclic` obligations discharge at Tier 2 by graph algorithm, sitting alongside the QF_LIA obligations rather than introducing a new mechanism. When a sub-protocol's acyclicity depends on a fact about a library actor, for instance that a supervised pool never calls back into its caller, a mode shift carries the obligation that the Tier 2 structure admits the Tier 3 refinement supplying that ordering, then projects the result back down, which is the worked traversal shape the architecture uses elsewhere.

Parametricity does not reach this property. Free theorems give independence of pure regions for the interaction-net path, and they say nothing about the liveness of effectful interaction. Deadlock freedom is Tier 2 work and never Tier 1 free. It is free of annotation in the common case, and it is not free of analysis. That is the same line the framework holds between a property that is free by parametricity and an obligation that is discharged.

## The honest ledger

The common case of static RPC with no wait cycle gets compile-time deadlock freedom at zero ceremony. The orderable-cyclic case gets it with inferred priorities and still zero ceremony. The dynamic case keeps RPC working through a visible, local, overridable downgrade to supervised execution. The cost sits where the theory says it must: the dynamic fragment is not statically guaranteed, and a developer who wants a guarantee there annotates the ordering, paying a cost localized to that call, or accepts the timeout.

This is not HCP's whole-calculus theorem, and the claim is not that Clef is deadlock-free without qualification. It is deadlock freedom for a precisely drawn, common, statically resolvable fragment, with a boundary the developer can see and steer. The memory model took dynamic discipline, the collector and its unpredictable clock, and made it a structural property carried to the substrate. The same disposition handles liveness, one axis over, with the wait-for graph in place of the escape classification, and that is the direction I will build it as the actor runtime matures.

---

## Related Reading

### Clef Design Documents

- [Managed Mutability](/docs/design/managed-mutability/) - Escape classification and the inferred-with-override pattern this design mirrors
- [RAII in Olivier and Prospero](/docs/design/raii-in-olivier-and-prospero/) - Actor-scoped arenas, sentinels, and deterministic lifetimes
- [The DCont/Inet Duality](/docs/design/dcont-inet-duality/) - The sequential and parallel compilation patterns
- [Delimited Continuations](/docs/design/delimited-continuations/) - The continuation structure under async, actors, and RPC

### External References

- [Better Late Than Never: A Fully-Abstract Semantics for Classical Processes](https://arxiv.org/abs/1811.02209), Kokke, Montesi, Peressotti (POPL 2019) - HCP and deadlock freedom by typing
- [Prioritise the Best Variation](https://arxiv.org/abs/2103.14466), Kokke, Dardha - Priority GV and deadlock freedom for cyclic topologies
- [Deadlock Freedom for Asynchronous and Cyclic Process Networks](https://arxiv.org/pdf/2110.00146) - The asynchronous-and-cyclic fragment and its priority discipline
