---
title: "Getting the Signal with BAREWire"
linkTitle: "Getting the Signal with BAREWire"
description: "Typed messages and owned buffers feeding demand-driven reactive views across native and web targets"
date: 2025-12-03
lastmod: 2026-09-18
authors: ["Houston Haynes"]
tags: ["Design", "Innovation"]
params:
  originally_published: 2025-12-03
  migration_date: 2026-03-12
---

A sensor panel can display the latest temperature and discard intermediate readings. A recorder needs the samples between screen refreshes, too. Put a musical instrument on the same device and a dropped note-off can leave a voice sounding after the player has released it. We want these applications to share a convenient reactive vocabulary while keeping their delivery requirements explicit.

Our BAREWire declarations describe the representation and boundary contract. Within the receiving application, we use `Observable` for event delivery and `Incremental` for demanded computation. The `Signal`, `Memo` and `Effect` surface is convenient for local state and derived values. Our [reactive design]({{< relref "/blog/native-reactivity-in-clef" >}}) describes that foundation, including its cold construction and ownership rules.

## Local Projections

An incoming message first passes through the receiver's framing and decoding checks. Schema agreement and bounded lengths are prerequisites for interpreting its bytes. The application then validates the decoded values before applying a state transition.

Within one owner, that transition can update a signal. A memo would cache a derived value and recompute when stale and demanded. An effect would observe the required result. For several related writes, our `Batch.run` operation specifies one local stabilization boundary. We define these operations in [Reactive Signals](/spec/draft/reactive-signals/), over the language intrinsics.

An illustrative projection using those specified APIs would look like this. The example has not been compiled or exercised through a transport adapter:

```fsharp
type Reading = {
    Temperature: int
    Pressure: int
}

let activateProjection initial present =
    let current = Signal.create initial
    let alarm = Memo.create (fun () ->
        let reading = Signal.get current
        reading.Temperature > 100 && reading.Pressure > 1050)

    let output = Effect.create (fun () -> present (Memo.get alarm))
    let accept reading = Signal.set current reading
    let retire () = Effect.dispose output
    accept, retire
```

The integers keep this example small. In an application we would give each measurement its domain units and permitted range. After validating delivery, the caller invokes `accept` on the owner. Disposing `output` ends its observation. The owner also has to retire the transport subscription and release graph storage after pending work has finished.

## Delivery Semantics

We distinguish current values from occurrences. A latest-temperature consumer may coalesce readings, while a recorder would need a different queue and overflow policy. A repeated note or command is still a distinct occurrence. Our [Observable contract](/spec/draft/observable-computation/) therefore leaves duplicate suppression explicit.

At a cross-owner boundary, a received snapshot becomes local state. The receiver needs identity and revision information to reject obsolete results. A reconnecting client may need a fresh snapshot before it can apply subsequent changes. Commands can require acknowledgments and application-level idempotency, especially when a retry might repeat an action.

We can express these messages in BAREWire, with delivery and recovery rules specified by the application protocol. A batch within one reactive graph has a local consistency boundary. Distributed coordination requires a protocol among the participating owners.

## Buffer Ownership

Our BAREWire declarations connect types and layouts to bounded byte extents. Compiler type and proof metadata need not become payload tags. Declared discriminants and presence information still belong in the representation, along with any protocol framing. Both endpoints must agree on that format.

For a buffer-backed update, the consumer needs the format and readable extent as well as access to the storage. A borrowed view is usable only during its admitted lifetime. For longer use we could extend ownership through an admitted lease. Where sharing is unsuitable, we would retain an independently owned result or copy the required data.

Zero-copy access is useful where those conditions hold. A shared address still requires correct publication and synchronization. Variable-length wire fields may require decoding, and network or device boundaries may require transfers. Browser-owned storage has its own access rules. Our [memory-region contract](/spec/draft/memory-regions/) defines the native lifetime vocabulary used to describe these obligations.

Cancelling an area update can prevent stale publication while a worker still reads its buffer. Graphics and DMA consumers can also retain access. We need completion evidence before reusing storage, independently of the notification that first made the buffer available.

## Demand and Lifetime

Our UI descriptions begin cold. Construction defers subscriptions and platform effects until an owner activates the required work. `Incremental` defers computation, and an additional cold wrapper can defer graph construction. Since `Effect.create` activates an always-demanded sink, we defer that call until owner activation.

A service can keep a telemetry projection current while its chart is closed. Reopening the chart attaches a visual observer without restarting the service. An application could instead retain a stale cache and recompute on demand, or prepare a bounded view ahead of presentation. We would measure the background work and retained memory against the improvement in request-time latency.

We intend the [shared UI model]({{< relref "/docs/design/user-interfaces" >}}) to preserve those ownership rules through native areas and DOM realization. Native resources require deterministic retirement. JavaScript-hosted observers also require explicit unsubscribe and cancellation, alongside garbage-collected storage.

## Target Integration

Our WrenHello application uses an experimental native host with an embedded Solid frontend. Its current bridge exchanges bounded ASCII WebKit messages. We are considering a BAREWire/WebSocket bridge separately from the proposed Composer Clef frontend path. HelloWayland provides the native presentation experiment, including bounded parallel raster work.

For the shared UI work, we would connect a bounded producer to the same component through both hosts. Closing and reopening the view during delivery would exercise ownership and recovery. A delayed buffer consumer would expose premature reuse, while a loss-sensitive event stream would exercise the queue policy. We would record retained memory and presentation latency alongside those functional results.
