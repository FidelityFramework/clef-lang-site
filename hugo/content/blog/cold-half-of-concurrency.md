---
title: "The Cold Half of Concurrency"
linkTitle: "The Cold Half of Concurrency"
description: "Incremental's ML lineage, from adaptive functional programming to industrial stabilizers, and its place in actor-owned reactive graphs"
date: 2026-07-14T11:00:00-04:00
lastmod: 2026-09-21
draft: false
authors: ["Houston Haynes"]
tags: ["Concurrency", "Design", "Analysis"]
params:
  originally_published: 2026-07-14
---

A spreadsheet is often our first encounter with incremental computation. Change a cell and the dependent formulas update. The useful expectation is that the application retains valid results and recomputes the affected calculations in the right order. We want that discipline throughout our Fidelity framework, including the work an application defers while nobody needs its result.

Our default for derived state is `Incremental<'T>`. It caches a result and tracks the inputs used to produce it, with recomputation when the result is both stale and demanded. Producer-driven events remain part of the model through `Observable<'T>`. A sensor reading can invalidate an incremental derivation while an unobserved chart leaves its calculations deferred. The [native reactivity account](/blog/native-reactivity-in-clef/) describes how the two compose.

## The ML lineage

[Adaptive functional programming](https://www.cs.cmu.edu/~guyb/papers/popl02.pdf), developed by Acar, Blelloch and Harper in Standard ML, records dependencies during execution and uses them to propagate changes selectively. That work is part of the research lineage we draw on for Clef's intrinsic model.

Jane Street's [Incremental](https://blog.janestreet.com/introducing-incremental/) developed the approach in OCaml around observers and stabilization. An observer establishes demand, and a stabilization pass processes the affected graph in dependency order. A cutoff stops further propagation from a recomputed value when its result is unchanged. We use that vocabulary in our [incremental computation specification](/spec/draft/incremental-computation/).

In F#, [FSharp.Data.Adaptive](https://github.com/fsprojects/FSharp.Data.Adaptive) offers another reference for demand-driven values and changing collections. Jimmy Byrd's [IcedTasks](https://github.com/TheAngryByrd/IcedTasks) was a direct influence on our choice of cold execution. Its reusable cold-task factory defers starting work. An incremental value adds caching and dependency invalidation, so the two have different uses even when both begin with deferred work.

Those libraries provide their abstractions within their host language and runtime. In Clef we can specify cold execution and incremental computation as language intrinsics, allowing Composer to retain their semantics during lowering. That is the architectural choice we are making for Fidelity.

[A Path Less Traveled](/blog/a-path-less-traveled/) considers the same move for bidirectional composition. A quieter application interface still needs explicit dependency, resource and execution semantics in the compiler. Deferring a calculation can avoid unnecessary work; it does not make captured state free or establish that the calculation has an inverse.

## Demand and readiness

Cold execution moves some cost to the point of request. A hidden chart can consume little computation while closed and still take time to prepare when opened. That tradeoff should be explicit, particularly on hardware where we can measure a defined deployment profile.

An application could keep selected time-series calculations current through a background observer. Closing the chart would remove visual demand while the service continued its own work. Alternatively, it could retain a stale cache or prewarm the chart before displaying it. These policies have different memory and processing costs, even though all use the same demand machinery.

The distinction also applies during construction. `Incremental<'T>` defers a calculation, while `Cold<Incremental<'T>>` additionally defers constructing the subgraph. An active effect has demand of its own. Our cold UI descriptions therefore need to defer effect creation until an owner activates them.

## Actor-owned graphs

An actor can own many incremental nodes. Its message handler admits input changes, and local stabilization computes the demanded results before publication. We can use the pure calculations within an [actor behavior](/docs/design/concurrency/the-three-layer-actor-contract/) as candidates for incremental evaluation when the relevant reads and effects are known.

The ownership boundary also preserves required event handling. An unobserved temperature chart can leave its derived plot stale while the same actor continues recording readings. A command must retain its occurrence semantics even when processing it leaves a visible value unchanged.

Cutoff applies to individual derived results. If one input produces the same value, a dependent still needs to account for changes to its other inputs. Across actors, messages require an explicit delivery contract. Within an owner, the graph requires dependency-ordered stabilization. Our [specification](/spec/draft/incremental-computation/#12-relationship-to-actors) keeps those responsibilities separate.

Our [scheduler contract](/spec/draft/scheduler-contract/) provides a place to specify the admission and scheduling of this work. For a local graph, that includes ordering stale, demanded computations. For cross-owner updates, it includes delivery and the point at which a received revision becomes visible locally.

## Chart readiness

The useful comparison is a pair of charts over the same data feed. Leave one cold when hidden and keep the other's selected calculations observed in the background. Reopen both under load and measure the time to a current frame, alongside the work and memory each consumed while closed.

The browser study in [Pitch, touch and demand](/blog/pitch-slew-and-demand/) makes one part of that distinction interactive: hide the plot while the tone continues, and the audio still consumes the sampled and smoothed control values. Visual demand can end while another consumer keeps the calculation active.

We can then add prewarming and compare how much preparation was reusable after a resize or a new input revision. On a fixed instrument or kiosk deployment, those measurements would let us choose a readiness policy against a known switching-latency budget. That is the practical reason we favor incremental computation as our starting point: the application can ask for more readiness where it needs it.
