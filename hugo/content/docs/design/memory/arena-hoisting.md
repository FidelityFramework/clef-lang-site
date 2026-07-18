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

When a value allocated in one actor escapes to another, our memory model admits two placements with the same safety guarantee and different runtime cost. Guarding the escaped reference at runtime keeps the program safe under any lifetime relationship between the actors. Placing the value in the ancestor actor's arena at compile time makes the lifetime static and removes the guard. Arena hoisting is the analyzer we are designing to identify the cases where the second placement is available, keep the guarded placement as the default, and present the static placement as an override. Its mechanism is the [memory coeffect algebra](/docs/internals/verification/memory-coeffect-algebra/).

## Trigger Condition

An actor owns an arena whose lifetime equals the actor's. Allocations that never leave the actor carry no lifetime cost: the arena is released deterministically at actor termination. An allocation that escapes into an ancestor actor separates the value's required lifetime from the owning actor's, and the placement decision resolves that separation.

The escape analysis classifies these cases. The value carries an `ActorHierarchyEscape` classification, and the analysis records whether the receiving ancestor is resolvable at build time or chosen at runtime. The analyzer triggers when an escaped value carries a runtime guard that a compile-time placement could remove.

```fsharp
// inside a worker actor
let frame = Span.zeroCreate<Sample> 256   // escapes to the parent aggregator
parent <! Accumulate frame
```

Here `frame` escapes to `parent`. The default keeps `frame` in the worker's arena and guards the reference held by the aggregator, checking the aggregator's access against the worker's liveness. This placement is safe. It also carries a runtime check that would not exist if `frame` were allocated in the aggregator's arena, where its lifetime would equal the aggregator's and require no guard.

## The Guarded Default

The default placement guards rather than hoists. The escaped reference stays in the worker's arena, and the sentinel validates it at the access boundary, returning `Valid` or `ActorTerminated`. A developer who never opens the suggestion compiles a correct program. The guard is a property of the graph, and Lattice surfaces it as a navigable annotation with a plain-language reason, the same presentation it gives an escape promotion.

This design places the safe result under no developer action: the compiler applies the guard and records that it did. It follows the discipline the [deadlock-freedom design](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) applies to liveness, where the unprovable case receives a guard by default and the developer is offered the option of converting the guard into a proof.

## The Hoist Suggestion

When the receiving ancestor is statically resolvable, the analyzer offers the hoist. The message names the guard and the effect of removing it:

> `frame` escapes to parent actor `aggregator` and is guarded at the boundary. Hoisting its allocation into `aggregator`'s arena makes the lifetime static and removes the guard.

Accepting the suggestion is a semantics-preserving optimization. The allocation moves to the ancestor's arena, the sentinel is removed, and the value's lifetime becomes a compile-time fact. The worker's arena reverts to a pure no-escape arena. Declining retains a correct program carrying one guard that a hoist would have removed. Both placements are safe. The analyzer surfaces the tradeoff and leaves the choice to the developer.

The analyzer offers no hoist where the receiving actor is chosen by live data. The compiler cannot place the allocation statically in that case, the guard is not optional, and the analyzer reports the condition rather than proposing a transformation it cannot guarantee. This is the memory analogue of the dynamic deadlock fragment, and it receives the same treatment: proven where the structure is statically visible, guarded and labeled where it is not.

## Elevation Posture as Configuration

Whether the compiler holds to guard-by-default or elevates aggressively is a declared preference. A project that prioritizes actor isolation and predictable arena boundaries retains the default. A real-time or [unikernel target](/blog/getting-to-the-heart-of-unikernels/) that requires the thinnest artifact declares aggressive elevation, which hoists wherever a static ancestor is provable and retains a guard only where the dynamic case requires one. The recording of an accepted hoist is a preference of the same kind: a declaration on the allocation, a rewrite that moves it into the ancestor scope, or a project-level record, chosen once rather than per call site.

The preference lives in the project configuration, beside the platform and memory-strategy choices, which keeps the configured posture findable. A second developer reading the project can determine which posture is in force. An elevation that crosses a memory-coherence boundary surfaces a diagnostic under any posture, so an aggressive setting does not make a material placement invisible.

The escape analysis produces a correct program under every posture. The preference selects where the program sits on the guard-versus-static-placement tradeoff, not whether it is safe. This is a fixed safety invariant under a configurable performance posture: the compiler does not admit a preference that produces an unsafe program, and within that floor the developer tunes for the target.

## Surfacing Criteria

Escapes into ancestor actors are routine, and an analyzer that reported every one would produce noise. The escape is the trigger. The filter is whether acting on the escape changes a measurable cost. The primary filter is the runtime guard: an escaped reference carrying a guard that a hoist would remove. A second filter is coherence cost, whether the placement crosses a memory-pool boundary that the [next-generation memory coherence](/docs/internals/memory-fabrics/next-generation-memory-coherence/) work makes visible, such as a NUMA node or a CXL pool. A hoist that both sheds a guard and collapses a cross-pool reference clears the filter. A hoist with no measurable effect does not.

The cost model available to the analyzer differs between general-purpose and bare-metal targets. On a conventional CPU under an operating system, placement cost is temporal and largely determined by hardware outside the compiler's control: the cache hierarchy, the scheduler, the memory controller. A static, fabric-level cost read of the kind an FPGA toolchain produces from spatial resource use is not available there, and the analyzer does not claim one. The costs it reads statically are structural properties of the artifact, fixed before the run: pool-boundary crossings, sentinel sites, allocation counts per scope. These are the signals the filter requires, and they ground the suggestion on targets where cycle-level cost stays out of reach.

On a unikernel or bare-metal target the static cost model gains precision. With no operating system between the compiled artifact and the hardware, the memory layout the compiler emits is the layout that executes, so the static structural model is a faithful account of the access pattern rather than an approximation an OS would perturb. This does not provide FPGA-style cycle budgeting, but it shares the structural property FPGA budgeting relies on: the structure of the artifact is the structure of the execution. Extending the coherence filter into a fuller compute-budget model on those targets is open work, and removing the intervening abstraction layers is a precondition for it.

## Related Reading

- [Memory Safety as Coeffect Algebra](/docs/internals/verification/memory-coeffect-algebra/) - The escape classification and the hoisting mechanic this analyzer surfaces
- [Managed Mutability](/docs/design/language/managed-mutability/) - Escape classification and the inferred-with-override pattern
- [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/) - Actor-scoped arenas, sentinels, and deterministic lifetimes
- [Deadlock Freedom as an Obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) - The sibling liveness property, proven where visible and guarded where not
- [Leveling Up With Lattice](/docs/tooling/leveling-up-with-lattice/) - The language server that presents these suggestions
