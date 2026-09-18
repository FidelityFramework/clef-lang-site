---
title: "Scaling Fidelity.UI"
linkTitle: "Scaling Fidelity.UI"
description: "Owned reactive graphs, background demand and explicit distribution in Fidelity.UI"
date: 2025-05-24
lastmod: 2026-09-18
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2025-05-24
  migration_date: 2026-03-12
---

## Ownership & Demand

Close a chart and its data feed may still need to run. Open a second chart and both can share the same history, even if they display different time windows. These are ordinary expectations for a dashboard, and our Fidelity.UI model is intended to express them without a tangle of application-managed subscriptions.

We are designing [Fidelity.UI]({{< relref "/docs/design/user-interfaces" >}}) around owner-local reactive graphs that can drive several visual areas. Larger sections could have independent owners where isolation or scheduling calls for them. That gives us a starting point for an instrument screen and a way to organize larger applications without tying component syntax to a thread or process.

## Local consistency

A signal supplies a current value. A memo caches a derivation. An effect creates demand for work with an external consequence. Several input changes can stabilize as one local update so a dependent view observes a consistent result.

Actor messages have a different responsibility: they carry occurrences under a delivery contract. A message may update a signal, append an event to a history or request an operation. Coalescing the mailbox cannot by itself guarantee dependency-ordered stabilization, and equal payloads do not make two commands interchangeable.

One actor can own many incremental nodes and visual areas. The runtime need not turn every binding or widget into an actor. Prospero's ownership and supervision can enclose the graph, while the graph retains its own demand, invalidation and cutoff semantics.

## Background demand

Consider a dashboard with a live history, two charts and an inspector. Ingestion may need to preserve every admitted event even when no chart is visible. A latest-value projection can serve a meter, while a history store supports time-window calculations. Those requirements should be explicit before selecting a coalescing policy.

The charts can use different readiness policies:

| Policy | While the chart is closed | On reopening |
|---|---|---|
| On demand | No visual demand; owned services follow their own policy | Compute demanded results |
| Retain cache | Preserve results and storage, allowing staleness | Validate and recompute as needed |
| Keep data current | A service observer maintains selected derivations | Attach to current data; prepare visual resources |
| Scoped prewarm | Prepare selected data, layout or paint under a budget | Reuse preparation whose inputs remain valid |

A service observer can keep its demand after the inspector closes. We would account for that work under the service owner, with a separate policy for any retained component state.

We favor cold construction by default. For a view that must open quickly, an application could explicitly demand preparation in advance. We would measure the resulting switching latency alongside background work and retained memory.

## Execution boundaries

We might give latency-sensitive input its own execution owner, or isolate a subsystem whose restart should leave the rest of the interface usable. Moving work to another core also introduces scheduling and communication costs. We would choose those boundaries for the workload rather than assign one to every visual area.

A worker can receive a bounded task over immutable inputs and return prepared results. An independent actor can own a larger domain with its own state and lifecycle. A process can provide stronger isolation. Each choice needs an admitted input/output contract, and platform window or DOM mutation remains with its authorized owner.

A pure reducer is useful where domain transitions need explicit messages and reproducible state changes. Components can combine those transitions with local signals and incremental selectors. This preserves the benefits of functional state modeling without imposing one application-wide rendering loop.

## Retirement and resource release

A component's owner covers its subscriptions, timers, callbacks and asynchronous operations. Retirement stops new work and invalidates pending results. A completion should carry enough identity to establish that it belongs to the current owner generation and input revision.

Logical cancellation is distinct from physical completion. A worker may still be writing an output buffer. A GPU or compositor may still be reading a submitted surface. Storage must remain valid until those uses finish, even when the visual result is no longer wanted.

Our native implementation needs a reclamation policy for repeatedly replaced children. Keeping every child allocation until actor retirement would allow storage to grow during a long session. We would test sustained mount, update and removal alongside logical cleanup on JavaScript, where the host manages storage.

## Distributed projections

A remote update arrives through a protocol and becomes input to the client's local graph. The client sends commands to the authority responsible for the domain operation. We would make that boundary explicit so reconnect and stale-data behavior remain visible in the application design.

A concrete protocol needs to specify:

- Stable entity identity, schema version and payload validation.
- Snapshot and update ordering, including how a client detects a missing revision.
- Reconnect, resynchronization and stale-data presentation.
- Command identity, acknowledgement and retry semantics.
- Queue limits, admission policy and which updates may be coalesced.

Our BAREWire representation can describe the typed payloads exchanged by that protocol. Application code still specifies the ordering and recovery rules. Each local graph then stabilizes the updates its owner admits.

For a device fleet, we would use the same component model to present local projections and select storage and connection policies for each deployment. A disconnected device can retain a readable snapshot and show its freshness explicitly, according to product requirements.

## Components across resource profiles

The portable vocabulary covers semantic controls, composition, editing, focus and accessibility. Native and DOM backends realize those behaviors through their own rendering and input facilities. Target extensions remain explicit where capabilities differ.

An MCU panel can use bounded partial updates and omit decorative motion. A more capable panel or desktop can admit transitions and retained composition. A WREN frontend can use browser layout and painting. We would test the domain behavior and ownership rules through each backend, including the effects of changing its motion profile.

## Workload comparisons

We would first change one of two areas and measure the affected work, then exercise layout-dependent changes and retirement.

Worker and process cases add completion during resize, close and restart. Distributed cases add packet loss, duplicate delivery, reconnect and slow clients, checking both bounded resource use and domain correctness. A larger client count is meaningful only when the workload, update rate, transport and resource envelope are stated.

For the dashboard, a useful starting point would be two charts over the same feed: one left cold when hidden and one kept current by a service observer. Reopening each would expose the latency tradeoff. Running the same comparison during reconnect would also show whether our local readiness policy survives a break in the data source.
