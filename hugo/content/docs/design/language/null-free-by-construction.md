---
title: "Null-Free by Construction"
linkTitle: "Null-Free by Construction"
description: "Why Clef excludes null as a representable state rather than checking for it, and why that exclusion is the same property that carries a value across substrates"
date: 2026-07-01
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Language Semantics"]
weight: 20
---

Clef has no null. Not a null that is checked and rejected, and not a null that is caught at a boundary. There is no state in a Clef value in which "absent" is representable, so the question a null check exists to answer never arises. That exclusion is a construction commitment, not a runtime discipline, and the same property that removes null is the one that lets a value cross from a CPU to a fabric unchanged.

## Exclusion, Not Inspection

A null check answers a question at runtime: is this reference inhabited, or does it point at nothing? The question is only meaningful if "nothing" is a value the reference could hold. Managed runtimes make it one. Every reference type admits null, so every dereference is a place the question could need asking, and the runtime checks it on every access.

Clef removes the question by removing its subject. A [flat closure](/docs/design/memory/gaining-closure/) has a code pointer and its captures, every field initialized at construction. No code pointer is absent and no capture slot is empty. A [discriminated union](/spec/draft/discriminated-union-representation/) is one of its declared cases and nothing else. There is no case for "no case." Where a program needs to model absence, it says so in the type, with `Option`, and absence becomes a case the compiler makes the reader handle rather than a runtime state a reference could hold undetected until it is dereferenced. The [option operations specification](/spec/draft/option-operations-representation/) gives that its representation.

The distinction is between a value that might be absent and a type that names absence. Null thinking treats a reference's inhabitance as a runtime fact, discovered when you touch it. Construction makes absence a declared case in the type, resolved before the program runs. Clef takes construction everywhere, so it never builds the machinery null thinking requires.

## One Property, Two Faces

A value with no null state is a value that is fully settled. Its layout is determined, every field carries a value, and nothing about it waits on a runtime to be resolved. That settledness is what null-freedom buys, and it is also what makes the value portable across substrates.

A settled value carries no runtime metadata. It has a [deterministic layout](/docs/design/memory/gaining-closure/), it can be copied between memory spaces without a collector coordinating the move, and it can be handed across a boundary as a block rather than a graph the other side must interpret. An accelerator has no collector to coordinate a move and no runtime to interpret a graph, so a settled value is exactly what it can take. The same flat closure that has no null pointer on a CPU is one the compiler can lay directly into an FPGA fabric, assign to an [NPU's tiles](/docs/design/categorical-foundations/target-architectures-compilation-strategy/), or send across a wire under the [BAREWire](/spec/draft/discriminated-union-representation/) contract with both ends compiled from the same shape. Null-freedom and substrate portability follow from one property. A value whose type and inhabitance are fixed at construction requires neither a null check nor a resident runtime on any target substrate.

Admitting null would cost a check on every access. It would also reintroduce a runtime-resolved state into the representation, and a representation with runtime-resolved state is one that assumes a runtime. Once computation moves off the CPU, that assumption breaks. Excluding null keeps the value settled, and a settled value copies unchanged across substrates.

## The Reasoning Runs Through the Framework

Null-freedom is not a property of one construct that happens to recur. It is a commitment the [Native Type Universe](/docs/design/types/bcl-to-ntu/) makes at the root and every construct inherits. A closure inherits it because its fields are initialized at construction, a discriminated union because its case set has no case for absence. A lazy value inherits it too: its [thunk is a flat closure](/docs/design/structure-and-performance/why-lazy-is-hard/) with memoization fields, settled the same way. The [message fabric between actors](/docs/design/concurrency/the-three-layer-actor-contract/) is null-free because both ends interpret a compiled contract, not a tagged payload that could arrive empty.

The pattern holds because it is enforced at the same place each time: at construction, before emission, by a compiler that resolves capture, case, and layout during compilation rather than deferring them to a runtime. What [managed mutability](/docs/design/language/managed-mutability/) is to state that changes, null-freedom is to state that is absent. Both are semantic guarantees expressed as compilation infrastructure, and both hold because Clef declines to carry a runtime that would otherwise answer the question for it.

## See Also

- [Gaining Closure](/docs/design/memory/gaining-closure/) - The flat closure representation, where null-freedom is realized in MLIR lowering
- [Why Lazy Is Hard](/docs/design/structure-and-performance/why-lazy-is-hard/) - How a thunk stays null-free while deferring computation
- [Clef: From BCL to NTU](/docs/design/types/bcl-to-ntu/) - The Native Type Universe that makes the commitment at the root
- [Target Architectures and Compilation Strategy](/docs/design/categorical-foundations/target-architectures-compilation-strategy/) - How a settled value reaches CPU, FPGA, and NPU targets
- [Discriminated Union Representation](/spec/draft/discriminated-union-representation/) - The normative case-set model with no case for absence
