---
title: "Clef on Metal Extended"
linkTitle: "On Metal Extended"
description: "Substrate requirements from reset vector to hosted process, current Farscape tooling, and the memory contracts across the range"
weight: 20
date: 2025-12-28T11:00:00-05:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
aliases:
  - /docs/internals/hardware/on-metal-revisited/
params:
  originally_published: 2025-12-28
  migration_date: 2026-02-15
---

[Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/) describes the direct-silicon EK-RA6M5 milestone. The wider Fidelity design spans freestanding images, virtual machines, containers, and hosted processes. A common language and platform contract can serve those environments, but source portability depends on the capabilities and semantics a program requires.

This page separates that design from the implementation reviewed on September 9, 2026. The compiler has native lowering and foreign-boundary machinery, and hosted Ariel has recorded carrier acceptance. Complete freestanding scheduling, automatic cache placement, and universal peripheral binding remain separate implementation obligations.

## The Substrate Spectrum

| Substrate | What the application image must account for | Possible linkage |
| --- | --- | --- |
| Freestanding | reset/entry, memory, vectors, device access, progress | no libc required; explicit startup and ABI boundaries still exist |
| MicroVM guest | virtual boot and devices, interrupts, vCPU progress | chosen guest service layer; libc is an implementation choice |
| Container | host syscalls and policy, process startup, carrier progress | static or dynamic libraries as the deployment permits |
| Hosted process | OS services, native ABI, scheduler and loader behavior | generated bindings and selected native libraries |

A container shares the host kernel. A microVM supplies virtual hardware and needs a guest implementation that can use it. Static linkage alone does not make either one a freestanding unikernel, and musl is not a requirement imposed by these substrate categories.

The [platform descriptor](/spec/draft/platform-bindings/) should declare the facts and capabilities a build relies on. A program confined to a shared supported subset may reuse its source across targets. A program requiring Wayland, a particular cryptographic peripheral, or multicore carrier threads needs a corresponding implementation or a capability rejection. Changing a declaration cannot manufacture a missing service.

[Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/) covers the dispatch obligations. [Bring-Up Beyond the CPU](/docs/internals/hardware/bring-up-beyond-the-cpu/) covers accelerator artifacts and their separate host/device execution paths.

## The Problem with Wrappers

The useful distinction is between extracting declarations, representing a foreign boundary, and implementing device behavior. A binding generator can help with the first two. A correct function signature or struct layout does not replace the vendor function's implementation, initialization requirements, or side effects.

### Wrapper Indirection

Farscape moved from CppSharp-based parsing to direct Clang invocation. Current `Farscape.Core/CppParser.fs` obtains AST JSON, preprocessor definitions, and record layout information from Clang. The generator is a .NET-hosted tool. This reduces one layer of tooling, but does not mean that Farscape is already a self-hosted Clef C/C++ parser with no dependencies.

Build-tool dependencies and deployed-image dependencies are different decisions. Clang and .NET used during generation need not run on an MCU. The final image's linkage and undefined symbols determine which runtime components it needs.

### Semantic Mismatch

CMSIS `__I`, `__O`, and `__IO` communicate intended register access. Their expanded C qualifiers are not a complete peripheral specification. In particular, write-only commonly expands to `volatile`; the C type system does not thereby forbid reading it. Write-one-to-clear behavior, read-to-clear status, unlock sequences, reserved fields, and allowed bus widths require additional device metadata or manual reconciliation.

## XParsec: A Typed Parser for Clef

XParsec participates in the current tooling's parsing and transformation work. It does not replace Clang's preprocessing, target-specific layout computation, or C/C++ semantic analysis. Any future replacement must demonstrate equivalence on the headers and target configurations used by supported packages.

## Quotation-Based Output

The design carries hardware and ABI declarations as compiler-readable data, allowing the compiler to inspect them before lowering. Current generated native bindings and proposed register descriptors should be described separately. There is no basis for claiming that every vendor header already produces a complete peripheral library with enforced hardware protocols.

### Why Quotations?

Representing declarations as values makes their facts available to compiler analysis. Correctness still depends on the extractor, the schema, and the consumer agreeing on their meaning. A field offset is a layout fact; a required barrier or destructive read is a device effect. Neither should silently stand in for the other.

### Active Patterns for Recognition

Composer uses witnesses and patterns to recognize supported graph operations. Recognition of a foreign HAL call would not, on its own, justify replacing that call with MMIO. Such a transformation needs a specified semantic boundary and its own tests.

## CCS Integration

CCS resolves sanctioned native types and attaches facts to the Program Semantic Graph. Composer consumes those facts through its supported witnesses and backend operations. The [backend contract](/spec/draft/backend-lowering-architecture/) requires a typed MMIO operation to survive until serialization; the reviewed MCU implementation still needs that path wired through.

Earlier versions of this article presented a `MemoryModel` record and generated GPIO APIs as an existing integration surface. Those sketches were not evidence of a working end-to-end implementation. Use the current [platform bindings](/spec/draft/platform-bindings/) and [FFI boundary](/spec/draft/ffi-boundary/) as the contract, and the compiler's acceptance cases as evidence of support.

## Multiple Microcontroller Families

The architecture allows family-specific descriptions under a common platform model. Each family still needs its own memory map, startup, interrupt routing, access rules, and toolchain validation. Even Cortex-M devices differ in flash placement and peripheral behavior.

The RA6M5 credential project motivates the immediate work, beginning with HelloBlinky. Cryptographic identity, key custody, provisioning, TrustZone policy, and secure boot need their own device-specific security design. The presence of a hardware security block does not by itself establish a usable identity scheme or an authenticated boot chain.

## Beyond Stack Allocation

The first MCU milestone can use fixed storage and bounded handlers. More elaborate memory strategies are relevant when the application needs them; they are not prerequisites for blinking LEDs and exercising vectors.

### RAII and Actor-Aware Arenas

Actor-owned arenas supply a useful lifetime boundary in the Olivier/Prospero design. Safe retirement also needs outstanding references, callbacks, DMA, and device resources accounted for. Releasing an arena's storage in bounded time does not prove that all external work using it has stopped.

### Context-Aware Compilation

Coeffects can carry target requirements and inform lowering. MMIO ordering must ultimately follow the hardware contract and explicit backend semantics. It cannot be inferred safely from a register's address or a language expression's apparent sequencing alone.

### Cache-Conscious Memory Management

Known field layouts support footprint and access analysis. Cache occupancy also depends on allocation bases, alignment, access traces, associativity, physical mapping, and competing traffic. The [CPU cache design](/docs/internals/hardware/cache-aware-compilation-cpu/) distinguishes layout facts from performance predictions.

### Delimited Continuations as the Unifying Abstraction

The [DCont representation](/spec/draft/dcont-representation/) provides a common design for suspension and resumption. Sharing a representation does not make every async, actor, or freestanding execution path implemented. Hosted carrier acceptance and a conforming MCU scheduler are separate milestones with different substrate assumptions.

### The Spectrum of Control

Applications should consume clear platform APIs while library authors encode hardware detail. Explicit register access remains useful for platform development. Both levels need supported operations and traceable contracts; speculative surface syntax should not masquerade as code a reader can build today.

## Principled Efficiency

Self-hosting remains a toolchain goal. Current build dependencies should be stated accurately, and each target's deployed dependencies should be measured from its artifact. Claims of zero allocation, bounded latency, or equivalent machine code require evidence for the specific path and optimization settings.

## Continued Evolution

For the RA6M5, progress means a sequence of reviewable artifacts: target-correct LLVM, exact-width MMIO, a resettable image, then timer and external interrupts. The full platform description grows alongside those verified uses. Scheduler and storage support can build on that foundation without being counted as already delivered by it.

## Further Reading

- [Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/)
- [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/)
- [Storage on Metal](/docs/internals/hardware/storage-on-metal/)
- [Bring-Up Beyond the CPU](/docs/internals/hardware/bring-up-beyond-the-cpu/)
- [Memory Management by Choice](/docs/design/memory/memory-management-by-choice/)
