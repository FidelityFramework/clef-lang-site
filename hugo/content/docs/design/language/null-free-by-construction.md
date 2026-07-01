---
title: "Null-Free by Construction"
linkTitle: "Null-Free by Construction"
description: "Why Clef excludes null as a representable state rather than checking for it, and why that exclusion is the same property that carries a value across substrates"
date: 2026-07-01
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Language Semantics"]
weight: 20
---

Clef has no null. Not a null that is checked and rejected, and not a null that is caught at a boundary. There is no state in a Clef value in which "absent" is representable, so the question a null check exists to answer never arises. This entry states why that exclusion is a construction commitment rather than a runtime discipline, and why the same property that removes null is the one that lets a value cross from a CPU to a fabric unchanged.

## Exclusion, Not Inspection

A null check answers a question at runtime: is this reference inhabited, or does it point at nothing? The question is only meaningful if "nothing" is a value the reference could hold. Managed runtimes make it one. Every reference type admits null, so every dereference is a place the question could need asking, and the runtime stands ready to answer it, on every access, forever.

Clef removes the question by removing its subject. A [flat closure](/docs/design/memory/gaining-closure/) has a code pointer and its captures, every field initialized at construction; there is no code pointer that is absent and no capture slot that is empty. A [discriminated union](/spec/draft/discriminated-union-representation/) is one of its declared cases and nothing else; there is no case for "no case." Where a program needs to model absence, it says so in the type, with `Option`, and absence becomes a case the compiler makes the reader handle rather than a state any reference can silently fall into. The [option operations specification](/spec/draft/option-operations-representation/) gives that its representation.

The distinction is between a value that might be absent and a type that names absence. The first is null thinking: a reference whose inhabitance is a runtime fact, discovered when you touch it. The second is construction: absence is a case, present in the type, resolved before the program runs. Clef takes the second everywhere, so the machinery of the first is not damped or optimized away. It is never built.

## One Property, Two Faces

A value with no null state is a value that is fully settled. Its layout is determined, every field carries a value, and nothing about it waits on a runtime to be resolved. That settledness is what null-freedom buys, and it has a second face that matters more than the safety.

A settled value carries no runtime metadata. It has a [deterministic layout](/docs/design/memory/gaining-closure/), it can be copied between memory spaces without a collector coordinating the move, and it can be handed across a boundary as a block rather than a graph the other side must interpret. These are the properties an accelerator needs. The same flat closure that has no null pointer on a CPU is one the compiler can lay directly into an FPGA fabric, assign to an [NPU's tiles](/docs/design/categorical-foundations/target-architectures-compilation-strategy/), or send across a wire under the [BAREWire](/spec/draft/discriminated-union-representation/) contract with both ends compiled from the same shape. Null-freedom and substrate portability are not two goals the design pursues separately. They are one property seen from two sides: a value that has already answered "what is this, and is it there" needs neither a null check nor a runtime standing by, on any substrate it lands on.

This is why the exclusion is worth its cost. Admitting null would cost a check on every access. It would also reintroduce a runtime-resolved state into the representation, and a representation with runtime-resolved state is one that assumes a runtime. That assumption is exactly what does not hold once computation moves off the CPU. Excluding null keeps the value settled, and settled is what travels.

## The Reasoning Runs Through the Framework

Null-freedom is not a property of one construct that happens to recur. It is a commitment the [Native Type Universe](/docs/design/types/bcl-to-ntu/) makes at the root and every construct inherits. Closures are null-free because their fields are initialized at construction. Discriminated unions are null-free because a case set has no case for absence. Lazy values are null-free because a [thunk is a flat closure](/docs/design/structure-and-performance/why-lazy-is-hard/) with memoization fields, settled the same way. The [message fabric between actors](/docs/design/concurrency/the-three-layer-actor-contract/) is null-free because both ends interpret a compiled contract, not a tagged payload that could arrive empty.

The pattern holds because it is enforced at the same place each time: at construction, before emission, by a compiler that resolves capture, case, and layout ahead of the walk rather than deferring them to a runtime. What [managed mutability](/docs/design/language/managed-mutability/) is to state that changes, null-freedom is to state that is absent. Both are semantic guarantees expressed as compilation infrastructure, and both hold because Clef declines to carry a runtime that would otherwise answer the question for it.

## See Also

- [Gaining Closure](/docs/design/memory/gaining-closure/) - The flat closure representation, where null-freedom is realized in MLIR lowering
- [Why Lazy Is Hard](/docs/design/structure-and-performance/why-lazy-is-hard/) - How a thunk stays null-free while deferring computation
- [Clef: From BCL to NTU](/docs/design/types/bcl-to-ntu/) - The Native Type Universe that makes the commitment at the root
- [Target Architectures and Compilation Strategy](/docs/design/categorical-foundations/target-architectures-compilation-strategy/) - How a settled value reaches CPU, FPGA, and NPU targets
- [Discriminated Union Representation](/spec/draft/discriminated-union-representation/) - The normative case-set model with no case for absence
