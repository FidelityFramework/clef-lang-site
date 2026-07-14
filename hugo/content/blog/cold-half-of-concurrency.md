---
title: "The Cold Half of Concurrency"
linkTitle: "The Cold Half of Concurrency"
description: "Incremental's ML lineage, from adaptive functional programming to industrial stabilizers, and the runtime graph a well-articulated actor system already draws"
date: 2026-07-14T11:00:00-04:00
draft: true
authors: ["Houston Haynes"]
tags: ["Concurrency", "Design", "Analysis"]
params:
  originally_published: 2026-07-14
---

The first incremental system most of us ever touched was a spreadsheet. Change one cell and the dependents recompute, in dependency order, and nothing else recalculates. Nobody schedules that recalculation by hand, nobody writes a callback, and no cell recomputes twice. The machinery underneath has a name in the research literature and a decades-long debt to the ML family, and it is the half of our concurrency story this corpus has not yet given its own account.

The hot half already has one. [Fidelity.Rx](/blog/fidelityrx-native-reactivity/) covered the push model: `Observable`, events arriving whether or not anyone is ready, the producer setting the pace. Its dual is `Incremental<'T>`, the cold, pull-based side our [concurrency model](/blog/ode-to-erlang/) holds as an intrinsic, where nothing computes until something downstream demands a value. Cold is where the spreadsheet lives. It is also the side with a lineage that deserves to be named properly, because we intend to draw on every part of it.

## Bred in the ML Family

The paper trail starts with [adaptive functional programming](https://www.cs.cmu.edu/~guyb/papers/popl02.pdf), Acar, Blelloch, and Harper at POPL 2002, working in Standard ML: run a program once, record its dynamic dependence graph, then propagate an input change through just the affected region instead of rerunning the program. Acar's thesis developed the idea into a field under the name self-adjusting computation. The mechanisms that matter were all present at the start: the dependence graph is discovered from execution rather than declared, change propagation follows it selectively, and memoization decides where propagation stops.

Industrial maturity came in OCaml. Jane Street's [Incremental](https://blog.janestreet.com/introducing-incremental/) hardened the theory into a library that keeps a trading firm's derived state current: every node carries a height; a stabilization pass processes dirty nodes in height order, so a diamond dependency computes once per wave; an observer is how demand enters the system; and a cutoff stops propagation when a recomputed value is unchanged. The vocabulary that library settled, stabilize, observe, cutoff, is the vocabulary of the cold half everywhere, and their retrospective on its seven implementations is one of the best accounts in print of how much design space there is inside "just recompute what changed."

In the F# world we descend from, the line continues as [FSharp.Data.Adaptive](https://github.com/fsprojects/FSharp.Data.Adaptive), and our async lineage [credits](/blog/dotnet-to-fidelity-concurrency/) the cold-side design sources it drew on. Clef's `Incremental<'T>` is that inheritance made intrinsic: demand, staleness, and stabilization are specified in our [incremental computation](/spec/draft/incremental-computation/) spec rather than supplied by a package.

## The Graph the Actors Already Draw

This post began as one observation in a design conversation: a well-articulated actor system already contains this graph, as standing structure rather than resemblance.

Read an actor system through the process-shaped glasses the Erlang and Akka traditions supply and you see mailboxes, supervision, and delivery. Read the same system cold and a different structure surfaces. Our [actor behaviors are pure functions](/docs/design/concurrency/the-three-layer-actor-contract/) from state and message to effects. Take the pure fraction: actor state is a memoized node value, message receipt is an input change, the behavior is the recompute function, and the articulation of actor references is the dependence graph, standing in memory at runtime. The effectful residue is the part incremental computation cannot express, and the reason the system is an actor system rather than a spreadsheet. So the honest claim is a projection, in the same sense our three-layer work calls the wait-for edge a projection of the session type onto the liveness question. The incremental graph is the projection of the actor system onto the demand-and-validity axis.

The projection is worth naming because it carries three resources the mailbox traditions have no seat for, by design, since unconditional delivery is their model:

**Demand.** Nothing in a mailbox records whether anyone observes an actor's output. Under the projection, an effect-free actor whose outputs no consumer demands is never dispatched at all. Our spec already places demand registration for actor-based incremental nodes with Prospero, so the junction between the actor system and the cold graph is a standing commitment rather than a proposal.

**Cutoff.** Actors forward messages regardless of whether the derived state changed. A cutoff at the actor boundary would wake no dependents when recomputation produces an unchanged value, which is backpressure by equality.

**Order.** A stabilization pass dispatches the dirty, demanded fragment in dependency order, which is why incremental systems compute a diamond once per wave. Mailbox order can compute it twice. The graph that would settle the order is already in our compiler's possession.

## One Contract for Both Temperatures

This is where the cold half meets [the scheduler we recently gave formal standing](/blog/naming-the-scheduler/). The [scheduler contract](/spec/draft/scheduler-contract/) was drafted substrate-neutral on purpose, and its determinism clause admits more than mailbox dispatch: a stabilization pass over the demanded, dirty fragment is a conforming implementation for the cold side, dispatching in dependency order what the hot side dispatches by resume. Ariel is the junction where the two temperatures interleave, [under Prospero's policy either way](/docs/design/concurrency/ariel-under-prospero/).

The proposed `Dormant` reference state reads naturally under the same projection. Hydration is a memo restore, the cached node value mapped back from its BAREWire layout. Restart after a fault is invalidation, recompute from inputs. The identity line we drew there, preserved across sleep and re-minted across failure, is the cache-hit-versus-invalidation line that every incremental system already enforces, which gives that design a second ancestry with no activation framework anywhere in it.

We are still early on the actor-side reading, and the imagining frame belongs on it: we imagine demand and cutoff surfacing at actor granularity through the same inference-with-override posture the rest of the boundary uses, never as annotations a developer threads by hand. The spec commitments named above are the parts already standing. The projection is the direction of the design work underway.

## An Inheritance Worth Claiming

The engineers who watched these ideas mature, in Standard ML seminar rooms and then in OCaml at industrial scale, already carry the discipline this post describes. They built the graph as a library, wired demand through observers, and learned to trust a stabilizer with the order of the world's recomputation. Our design intent is that the graph they assembled by hand is the graph a well-articulated actor system already draws, carried by the same contract that schedules everything else. The hot half got its account in Fidelity.Rx. This is the cold half's opening chapter, and we will keep reporting as we carry the projection from reading into machinery.
