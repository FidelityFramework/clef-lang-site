---
title: "Arena Hoisting"
linkTitle: "Arena Hoisting"
description: "How Lattice surfaces actor-scoped lifetime suggestions that trade a runtime guard for a compile-time placement, with a pit-of-success default the developer can override"
date: 2026-06-18T11:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Design", "Tooling", "Memory Management"]
params:
  originally_published: 2026-06-18
---

When a value allocated in one actor escapes to another, our memory model has a correct answer and a faster answer, and they are not the same answer. The correct answer is to guard the escaped reference at runtime so the program is safe no matter what. The faster answer is to place the value in the ancestor actor's arena at compile time, which sheds the guard. Arena hoisting is the analyzer we are designing to find these cases, keep the safe answer as the default, and offer the faster one as a choice the developer can see and take. The mechanism underneath it is the [memory coeffect algebra](/docs/internals/verification/memory-coeffect-algebra/); this article is the surface Lattice is planned to present.

## The case the analyzer fires on

An actor owns an arena that lives as long as the actor. Most allocations inside an actor never leave it, and those are free: the arena is released deterministically when the actor terminates. The case worth attention is the one where a value escapes into an ancestor actor, because then the actor's death is no longer the value's death, and something has to account for the gap.

Our escape analysis already classifies this. The value carries an `ActorHierarchyEscape` classification, and the analysis knows whether the receiving ancestor is resolvable at build time or chosen at runtime. That fact is the trigger. The analyzer fires when an escaped value is sitting under a runtime guard that a compile-time placement could remove.

```fsharp
// inside a worker actor
let frame = Span.zeroCreate<Sample> 256   // escapes to the parent aggregator
parent <! Accumulate frame
```

Here `frame` escapes to `parent`. The default keeps `frame` in the worker's own arena and guards the reference the aggregator holds, so the aggregator's access is checked against the worker still being alive. That is correct and it is safe. It also carries a runtime check that would disappear if `frame` were allocated in the aggregator's arena to begin with, because then its lifetime would be the aggregator's and no guard would be needed.

## The default is the safe one, and it is visible

The default is to guard, not to hoist. The escaped reference stays in the worker's arena and the sentinel validates it at the access boundary, returning `Valid` or `ActorTerminated`. A developer who never opens the suggestion ships a correct program. Nothing about the default is unsafe, and nothing about it is hidden: the guard is a property of the graph, and Lattice surfaces it the same way it surfaces an escape promotion, as a navigable annotation with a plain-language reason.

This is the pit-of-success posture. The compiler does the safe thing on its own and tells the developer it did, which is the same discipline the [deadlock-freedom design](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) applies to liveness: the unprovable case gets a guard by default, and the developer is offered a way to convert the guard into a proof rather than being forced to.

## The suggestion trades a guard for a placement

When the receiving ancestor is statically resolvable, the analyzer offers the hoist. The message names the guard and what removing it costs:

> `frame` escapes to parent actor `aggregator` and is guarded at the boundary. Hoisting its allocation into `aggregator`'s arena makes the lifetime static and removes the guard.

Accepting is an optimization with guaranteed semantics. The allocation moves to the ancestor's arena, the sentinel is gone, and the value's lifetime is now a compile-time fact. The worker's own arena returns to being a pure no-escape arena, which is the fast common path. A developer who declines keeps a correct program that carries one guard it could have shed. The choice is real, both directions are safe, and the analyzer makes the tradeoff legible instead of deciding it silently.

The case the analyzer does not offer a hoist for is the one where the receiving actor is chosen by live data. There the compiler cannot place the allocation statically, so the guard is not optional, and the analyzer says so rather than proposing a transformation it cannot guarantee. This is the memory analogue of the dynamic deadlock fragment, and it gets the same honest treatment: proven where the structure is visible, guarded and labeled where it is not.

## The posture is a preference, with a default that holds when nothing is set

Whether the compiler holds to guard-by-default or elevates aggressively is a declared preference. A team that prizes actor isolation and predictable arena boundaries keeps the default. A real-time or unikernel target that wants the thinnest possible artifact declares aggressive elevation, which hoists wherever a static ancestor is provable and leaves a guard only where the dynamic case forces one. How an accepted hoist is recorded is the same kind of preference: a declaration on the allocation, a rewrite that moves it into the ancestor scope, or a project-level record, chosen once rather than per call site.

The preference lives in the project configuration, beside the platform and memory-strategy choices it travels with, because the discipline that keeps this honest is that the configured posture is findable. A second developer reading the project can see which posture is in force, and an elevation that crosses a memory-coherence boundary still surfaces a diagnostic regardless of the posture, so an aggressive setting never makes a material placement invisible. The preference changes the default action; it does not change what the developer is allowed to see.

The escape analysis produces a correct program under every posture. The preference moves only where the program sits on the guard-versus-static-placement tradeoff, never whether it is safe. That separation, a fixed safety invariant under a configurable performance posture, is the same one that runs through the rest of the framework: the compiler will not let a preference produce an unsafe program, and within that floor it lets the developer tune for the target.

## When the suggestion is worth surfacing

Escapes into ancestor actors are routine, and an analyzer that lit up on every one of them would be noise that developers learn to mute. The trigger is the escape; the filter is whether acting on it buys something. The clearest payoff is the one already named: an escaped reference currently carrying a runtime guard that a hoist would remove. A second filter is coherence cost, whether the placement crosses a memory-pool boundary that the [next-generation memory coherence](/docs/internals/hardware/next-generation-memory-coherence/) work makes visible, such as a NUMA node or a CXL pool. A hoist that both sheds a guard and collapses a cross-pool reference is worth the developer's attention; a hoist that changes nothing measurable is not.

Reading the full cost of a placement is where general-purpose targets and bare-metal targets diverge, and the honest account of that divergence is worth stating. On a conventional CPU under an operating system, the cost of where a value lives is temporal and largely determined by hardware the compiler does not control: the cache hierarchy, the scheduler, the memory controller. A static, fabric-level cost read of the kind an FPGA toolchain produces from spatial resource use is not available there, and the analyzer does not claim one. What it can read statically are the structural costs that are properties of the artifact rather than the run: pool-boundary crossings, sentinel sites, allocation counts per scope. Those are exactly the signals the filter needs, so the suggestion is well-founded even where cycle-level cost is not.

On a unikernel or bare-metal target the picture sharpens, and this is the direction worth building toward. With no operating system between the compiled artifact and the metal, the memory layout the compiler emits is the layout that runs, so the static structural model stops being an approximation an OS would scramble and becomes a faithful account of the real access pattern. That does not deliver FPGA-style cycle budgeting, but it approaches the property that makes FPGA budgeting trustworthy: the structure of the artifact is the structure of the execution. Extending the coherence filter into a fuller compute-budget model on those targets is open work, and removing the intervening abstraction layers is what brings it within reach.

## Related Reading

- [Memory Safety as Coeffect Algebra](/docs/internals/verification/memory-coeffect-algebra/) - The escape classification and the hoisting mechanic this analyzer surfaces
- [Managed Mutability](/docs/design/language/managed-mutability/) - Escape classification and the inferred-with-override pattern
- [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/) - Actor-scoped arenas, sentinels, and deterministic lifetimes
- [Deadlock Freedom as an Obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) - The sibling liveness property, proven where visible and guarded where not
- [Leveling Up With Lattice](/docs/internals/tooling/leveling-up-with-lattice/) - The language server that presents these suggestions
