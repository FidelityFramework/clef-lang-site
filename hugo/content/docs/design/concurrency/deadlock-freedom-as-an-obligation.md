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

Our actor model gives every actor an arena that lives exactly as long as the actor, and Prospero reclaims it deterministically when the actor dies. That discipline, Resource Acquisition Is Initialization (RAII) drawn to the actor boundary, forecloses the memory failures of an actor system: use-after-free, dangling byref, a byref escaping its frame, a reference into a dead actor. **However, it does not foreclose deadlock.** Two actors can each park a continuation on a reply that only the other could send, every arena intact, every sentinel reading `Valid`, every lifetime correct, and the system makes no progress while reporting green. This is the gap this document closes, in the same general shape as [managed mutability](/docs/design/managed-mutability/).

## Two properties use the word "safe"

Actor-scoped RAII is a safety property in the technical sense: nothing bad happens to memory. Each actor owns its arena, cross-process references resolve through sentinels, and cleanup is tied to the actor lifecycle rather than to a collector running on its own clock. Those are the failure modes RAII was built to kill, and in [Olivier and Prospero](/docs/design/raii-in-olivier-and-prospero/) it kills them.

Deadlock is a liveness property: something good eventually happens. A set of actors each blocked waiting for a message that only another blocked actor in the set could send will sit there indefinitely. Prospero never retires any of them, because none has crashed or completed. They are all alive and quiescent. The entire memory apparatus stays consistent while the program stops doing anything.

> Safety and liveness are independent axes. 

Our RAII work drove the safety axis to the floor and left the liveness axis to runtime contingency, because the references are what RAII validates, and the wait-for graph is a different object that nothing in the memory model inspects.

## Where the deadlock edge actually enters

The hazard rides in on one construct: a synchronous reply expectation across actors. A `PostAndReply` call suspends the caller's continuation until the callee answers on a reply channel. That suspension is an edge in a wait-for relation, and a cycle of such edges where every actor is simultaneously blocked is the deadlock.

A fire-and-forget `Tell` adds no such edge. The sender posts to a mailbox and returns, so the asynchronous fraction of a program is invisible to this hazard and cannot deadlock through it. The synchronous request and response carries the whole risk, because that is the only place a caller's progress is contingent on a specific message another actor is contractually bound to produce.

We want remote procedure call (RPC) available. It is genuinely useful in systems work, and closed-loop request and response is much better than legislating it away in favor of pervasive callbacks. So the synchronous edge stays in the model. The question is how to make its liveness visible in a way that fits our design-time norms.

## What session-types get, and what it costs

Classical Linear Logic (CLL) gives one well-studied route to deadlock freedom for synchronous communication. Wadler's [Classical Processes (CP)](https://homepages.inf.ed.ac.uk/wadler/papers/propositions-as-sessions/propositions-as-sessions.pdf) put session-typed communication in exact correspondence with CLL, and in CP and its successor Hypersequent Classical Processes (HCP) a well-typed process cannot deadlock, established as a corollary of the proof structure. The mechanism in CP fuses channel creation and parallel composition under a single cut rule, which forces the communication topology to be a tree. Deadlock freedom follows because a tree has no cycles to wait around. The price is stated plainly in that work: the only processes allowed are tree-structured. Two actors that hold references to each other are already outside the fragment, and that pattern is common in real supervision graphs.

Priority CP (PCP), following Kobayashi and Padovani, buys the cyclic topologies back. It annotates communication actions with priorities, a partial order, and the type checker verifies that the order has no cycle. Cyclic connection graphs become typeable as long as the priorities prove the wait-for relation stays acyclic. The recognized cost is compositionality: priorities are non-local, so a library actor's priorities surface in the types of everything that calls it, and the programmer threads those annotations through by hand.

Neither shape fits our constraints as written. The tree restriction forbids reasonable systems patterns. Pervasive priority annotation is the ceremony I am declining when I say I do not want to write Clef as though it were Haskell. And a compiler pass that silently rejects programs with a cyclic wait-for graph, with no surface the developer reads or steers, is the hidden guard I refuse on principle. Memory safety in our model is not a hidden analysis. The `let mutable` is visible syntax, the escape classification is inspectable, and the allocation decision is reachable by a library author. Deadlock freedom should be a citizen of the same kind.

## The wait-for relation is already in the graph

A synchronous RPC introduces one structural object: a blocking-wait hyperedge. At a call site that issues `callee.PostAndReply msg` and suspends pending the reply, the caller's continuation waits on the callee's reply obligation. Our [Program Hypergraph](https://speakez.tech/blog/coupling-and-cohesion/) already carries this kind of relation on its joint-constraint axis, the axis that holds the hyperedges flat closures, regions, and actor lifetimes generate.

Collect those edges into a wait-for relation \(W\) over actor behaviors. A node `caller —waits-on→ callee` exists whenever the caller issues a synchronous RPC to the callee and parks until the reply. Deadlock is a cycle in \(W\) whose actors are all in their blocked state at once. The relation is a may-wait over-approximation: an actor that can call either of two callees depending on message content contributes an edge to each. Over-approximating keeps the analysis sound, never claiming deadlock freedom that an execution could violate, at the cost of sometimes flagging a cycle no run reaches. That is the same soundness posture our escape analysis takes, where `EscapeKind` over-approximates escape and never under-approximates it.

For the fragment where every callee is a statically resolvable actor reference, \(W\) is a finite directed graph and deadlock freedom reduces to its acyclicity. A strongly-connected-component pass finds any cycle in linear time. This is the same kind of graph analysis the [graph-coloring pass](https://speakez.tech/blog/speed-and-safety-with-graph-coloring/) runs to license interaction-net breakout, applied to a different edge label, and it lands as a Tier 2 obligation built mechanically from graph structure. Most instances discharge by graph algorithm alone, which makes them cheaper than the arithmetic obligations Z3 handles at the same tier.

## A classification that mirrors EscapeKind

Every synchronous RPC call site gets a wait classification, deliberately the same shape as the escape classification managed mutability assigns to every mutable binding:

```fsharp
type WaitClass =
    // callee static, W acyclic: guaranteed, silent
    | AcyclicStatic    
    // callee static, in a connection cycle but                 
    //   priority-orderable: guaranteed, priority inferred
    | OrderedCyclic of priority: int    
    // callee value-carried: visible downgrade to     
    //   supervised timeout, diagnostic emitted                               
    | Unresolved of routing: RoutingKind 
                                        
```

`AcyclicStatic` is the common case under a mostly-static topology, and it carries no annotation, no marker, and no ceremony. The check ran, the graph was acyclic, and the developer never hears about it.

`OrderedCyclic` is the case CP's tree restriction wrongly forbids: two actors hold references to each other and call in both directions, forming a cycle in the connection graph but not in the wait-for graph, because their calls are ordered so that no execution waits around the loop. The order is a rank: an integer assigned to each actor behavior such that every wait-for edge strictly increases it, which exists exactly when the relation is acyclic. The priority annotation a developer might write is that rank, and in the common case the solver finds it. The developer writes nothing. This is the place our inference thesis earns the difference from textbook Priority CP, where the same priority would be a hand-written annotation. Width is inferred from type structure and surfaced only when inference needs help; the synchronous-action priority is the same inference one axis over.

`Unresolved` is the genuinely dynamic case, where the callee is value-carried through `Actor.self()`-passing, content-based routing, or a runtime-spawned handle whose identity is not statically pinned. Acyclicity over such routing is undecidable in general, which is the wall the asynchronous-and-cyclic process-network literature documents and works around. The program is neither forbidden nor silently admitted. The call site drops out of the static guarantee, the compiler says which call dropped and why, and that call falls back to supervised execution with a timeout. RPC stays available, and the boundary of the guarantee is visible at the exact call that crosses it.

## The developer sees the machinery and can change it

The interaction follows the managed-mutability standard: inferred when it holds, a concrete diagnostic when it does not, and an opt-in annotation for the developer who wants control.

In the common case the developer sees nothing. When a cycle is flagged, the compiler reports the wait-for path the way the escape diagnostic reports an escape path. The message is the actual chain, "`ActorA.handleFoo` waits on `ActorB.query`, which waits on `ActorA.handleBar`, a synchronous cycle," rather than a generic warning that deadlock is possible. The developer reads the structural reason and chooses the resolution: supply an explicit priority that breaks the cycle and that the checker verifies, refactor one leg to a `Tell` with an explicit continuation so the wait-for edge disappears, or mark the call for supervised timeout and opt it out of the static guarantee on purpose. The guard is never hidden, and it is always overridable. That property is the line that separates this design from both the tree restriction and the runtime-only timeout.

Writing an explicit priority is the only place priorities-as-syntax appear, and they appear only at the cyclic-and-ambiguous call sites that genuinely need them, not across every channel in the program. The compositionality cost the literature attaches to priorities is real, and confining it to the call that incurs it is the improvement over pervasive annotation.

## How it rides the architecture

The wait-for edges are hyperedges on the joint-constraint axis our [tier architecture](/docs/internals/verification/) already defines, so deadlock freedom is a section over that axis being free of cycles. Acyclicity is not a graph algorithm that needs its own machinery; it encodes as the rank constraint, \(\forall (u \to v) \in W.\ r(u) < r(v)\), which is satisfiable exactly when the relation is acyclic, and that constraint is QF_LIA. So the `AcyclicStatic` and `OrderedCyclic` obligations are ordinary Tier 2 obligations in the same fragment as our interval and bound checks, discharged by the same solver, with the inferred rank serving as the priority witness. When a sub-protocol's acyclicity depends on a fact about a library actor, for instance that a supervised pool never calls back into its caller, a mode shift carries the obligation that the Tier 2 structure admits the Tier 3 refinement supplying that ordering, then projects the result back down, which is the worked traversal shape the architecture uses elsewhere.

Parametricity does not reach this property. Free theorems give independence of pure regions for the interaction-net path, and they say nothing about the liveness of effectful interaction. Deadlock freedom is Tier 2 work and never Tier 1 free. It is free of annotation in the common case, and it is not free of analysis. That is the same line the framework holds between a property that is free by parametricity and an obligation that is discharged.

## How it survives lowering

A check that runs only in the front end and evaporates before code generation is theater. The CakeML standard is the right bar: the property is established once and carried to the target, rather than asserted at the source and hoped for below. For deadlock freedom that carrying is direct, because the obligation is already a Tier 2 verification condition and Tier 2 conditions already lower to the MLIR SMT dialect and discharge at the seam. There is no separate mechanism for this one.

The shape in MLIR answers a question worth making explicit: is deadlock freedom a structural invariant of an op, or a proof obligation on a scope? It is the second. A single blocking call cannot carry acyclicity, because acyclicity is a property of the whole set of wait edges and no individual op can see the set. SSA dominance can make use-after-definition structurally impossible at the op level, and there is no analogue here. So each synchronous RPC lowers to a blocking primitive that carries only its own wait edge as local fact:

```mlir
// each PostAndReply lowers to a suspend that names who it waits on
%r = dcont.suspend_on_reply %callee : !actor.ref<"inventory">
    { rpc.wait_edge = #wait<from = "order", to = "inventory"> }
```

The acyclicity proof is hung on the scope that encloses the set of those edges. The enclosing region carries the obligation as an attribute, which instructs the seam to gather every wait edge in the region and prove the relation admits a rank:

```mlir
module @order_system attributes { verif.obligation = #tier2.acyclic_wait } {
  // actor behaviors and their suspend_on_reply ops
}
```

Lowering emits the verification condition for that scope into the SMT dialect, and Z3 discharges it exactly as it discharges an interval obligation:

```mlir
%edges = collect rpc.wait_edge in @order_system
smt.assert (forall (u v) (=> (wait %u %v) (lt (rank %u) (rank %v))))
smt.check   // sat: acyclic. unsat: the core is the cycle, reported as CCS8031.
```

The unsat core is the wait-for path the front-end diagnostic names. The solver returns it as the minimal set of edges that cannot be jointly ranked, which is precisely the cycle, so the proof failure and the developer-facing message are the same object.

The scope is the part to choose deliberately. The wait relation ranges over the actors that can address one another, so the obligation belongs on the smallest region closed under "can send a synchronous reply to." That is a `module` when an actor system is a module, and a dedicated actor-system region when actors span modules. A smaller scope is a cheaper proof, and a synchronous call that crosses a scope boundary is an `Unresolved` site by the same definition the front end already applies, because the callee is outside the relation the scope can see.

## The honest ledger

The common case of static RPC with no wait cycle gets compile-time deadlock freedom at zero ceremony. The orderable-cyclic case gets it with inferred priorities and still zero ceremony. The dynamic case keeps RPC working through a visible, local, overridable downgrade to supervised execution. The design-time cost is minimized: the dynamic fragment is not statically guaranteed, and a developer who wants a guarantee there annotates the ordering, paying a cost localized to that call, or accepts the timeout.

This is not HCP's whole-calculus theorem, and doesn't intend to be a full theoretic exercise. The claim is not that Clef is deadlock-free without qualification. It is deadlock freedom for a precisely drawn, common, statically resolvable fragment, with a boundary the developer can see and steer. Our memory model took dynamic discipline, the collector and its unpredictable clock, and made it a structural property carried to the substrate. And now we have a mechanisme that handles liveness, one axis over, with the wait-for element in our hypergraph in place of the escape classification, and that is the direction we will build it as the actor runtime matures.

---

## Related Reading

### Clef Design Documents

- [Managed Mutability](/docs/design/managed-mutability/) - Escape classification and the inferred-with-override pattern this design mirrors
- [RAII in Olivier and Prospero](/docs/design/raii-in-olivier-and-prospero/) - Actor-scoped arenas, sentinels, and deterministic lifetimes
- [The DCont/Inet Duality](/docs/design/dcont-inet-duality/) - The sequential and parallel compilation patterns
- [Delimited Continuations](/docs/design/delimited-continuations/) - The continuation structure under async, actors, and RPC

### External References

- [Propositions as Sessions](https://homepages.inf.ed.ac.uk/wadler/papers/propositions-as-sessions/propositions-as-sessions.pdf), Wadler (ICFP 2012) - CP and the exact correspondence between session types and Classical Linear Logic
- [Better Late Than Never: A Fully-Abstract Semantics for Classical Processes](https://arxiv.org/abs/1811.02209), Kokke, Montesi, Peressotti (POPL 2019) - HCP and deadlock freedom by typing
- [Prioritise the Best Variation](https://arxiv.org/abs/2103.14466), Kokke, Dardha - Priority GV and deadlock freedom for cyclic topologies
- [Deadlock Freedom for Asynchronous and Cyclic Process Networks](https://arxiv.org/pdf/2110.00146) - The asynchronous-and-cyclic fragment and its priority discipline
