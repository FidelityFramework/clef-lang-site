---
title: "Linear Memory Mapping"
linkTitle: "Linear Memory Mapping"
description: "WebAssembly's linear memory as a BAREWire contract surface: described layouts, zero-copy boundaries, and the arena shapes the standard is growing toward"
date: 2026-08-31T12:00:00-04:00
weight: 20
authors: ["Houston Haynes"]
tags: ["Architecture", "Design"]
---

A WebAssembly module owns a linear memory: a flat, byte-addressable region, bounds-checked by the engine, growable in 64 KiB pages, with no garbage collector inside it and no pointers leaving it. Most toolchains treat that region as a heap to manage, and ship an allocator into every module to manage it. We read it differently. A flat region whose layout the compiler settles completely is a contract surface, and the contract language already exists in the framework: BAREWire, where a layout is something both sides interpret by construction rather than a convention one side documents and the other side trusts.

## A Layout Is a Contract, Not a Convention

Clef types map to concrete offsets at compile time, and on this target those offsets land in linear memory exactly as they land in a native image:

```fsharp
// a telemetry frame; layout settled at compile time
type Frame = { Seq : uint32; Stamp : uint64; Payload : Vector3 }

// BAREWire derives: Seq @ 0, Stamp @ 8, Payload @ 16, size 32, align 8
```

Discriminated unions keep the same discipline. A case is a compile-time index in a described layout, never a string tag in a dynamic object:

```fsharp
type Reading =
    | Ok of Frame
    | Fault of FaultCode
// the case index is a constant the layout describes, checked where the type is checked
```

This is the sharpest contrast with the [JavaScript pathway](/docs/design/javascript-targeting/), where values ultimately live as engine-managed objects and the boundary work goes into keeping their shapes honest. In linear memory there is no second shape. The record in the module is the record on the wire is the record at rest, one described layout serving all three, the same authority the storage design leans on for [segments and ledger entries](/blog/an-emergent-file-system-model/).

## The Boundary Without a Serializer

A module's memory can be exported, and to a JavaScript host it is an `ArrayBuffer`. That one fact removes the serializer from the boundary, because a host holding the layout can read a described region in place:

```js
// the host's view of the same frame, no deserialization step
const mem = new DataView(instance.exports.memory.buffer);
const seq   = mem.getUint32(frameBase + 0, true);
const stamp = mem.getBigUint64(frameBase + 8, true);
```

We imagine both ends of this pair generated from the one schema: the module side laid out by the compiler, the host side emitted as accessors over the exported buffer, each derived from the same BAREWire description so that drift between them is a build failure rather than a runtime surprise. There is no JSON tagging at this seam and no reflective machinery behind it, and the compiler is never in the runtime loop. What crosses the boundary is bytes at described offsets, which is what crosses every other boundary in the framework.

## Shared Memory and the Actor Seam

WebAssembly's shared memory with atomics extends the same reading to concurrency. An actor mailbox is, structurally, a ring of described frames plus two cursors, and a shared linear memory can hold exactly that. A host-side worker and a module would exchange messages through BAREWire frames in the shared region, cursors advanced with atomic operations, no copy and no marshaling on the hot path. That is the zero-copy transport discipline our actor layer already defines for native boundaries, arriving at the web boundary unchanged. The scheduling above it stays with the host until the [stack-switching question](/docs/design/wasm-targeting/coroutine-versus-stack-switching/) settles, and the memory discipline does not need to wait for it.

## Growing Room in the Standard

Two extensions widen the mapping as they settle into engines. Memory64 lifts the address space beyond 4 GiB, which matters for the WASI face of the target more than the browser face. Multiple memories matter more structurally: a module holding several distinct linear memories could dedicate one to a described arena, its lifetime bounded and its layout closed, apart from the general heap. That shape rhymes with the arena-per-actor discipline our memory model is built around, and it is the direction we watch most closely, because a described arena in its own memory is a BAREWire region with a hardware-enforced fence around it.

The practical reading is the same one this section keeps arriving at. Linear memory asks for exactly the discipline the framework already practices: layout decided at compile time, contracts held by construction on both sides, nothing interpreted at runtime that was decidable before it. The examples above are ahead of what ships, and none of them requires machinery we have not already built for harder targets.
