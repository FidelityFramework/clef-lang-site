---
title: "Counting the Cost of Coordination"
linkTitle: "Counting the Cost of Coordination"
description: "Concurrency coordination is expensive at the CPU cache line. Our actor and arena model treats that cost as a structural property instead of leaving it to be hand-tuned away."
date: 2026-06-19T10:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Concurrency", "Performance", "Architecture"]
params:
  originally_published: 2026-06-19
---

Concurrency has been the through-line of our work from the start. We describe Clef as a concurrent programming language that happens to use ML-family semantics inside our Native Type Universe, and the ordering in that sentence is deliberate. The functional core, the immutability, the parametric types, the computation expressions, is the material we build with. What we are building is a language whose first concern is how programs run alongside one another safely and efficiently.

That concern has two faces, and we have written about both. The first is correctness: whether a concurrent program stays safe and makes progress. We took that up in [Fearless Concurrency Gets Real](/blog/fearless-concurrency-gets-real/), prompted by an interview where a Rust maintainer described that language's borrow checker fighting the developer, and in that entry we argued that our compiler should carry that burden, not the person at the keyboard. 

> This post is the second face: performance. 

Concurrency is a wider domain than correctness alone, and the cost of coordinating concurrent work is its own structural problem. We have addressed it the same way, by building the answer into how a program is organized, so that the cost is not left for a developer to tune away by hand.

A recent talk states that performance problem better than we could, so we will start there.

Jon Gjengset gave it this spring, "The Cost of Concurrency Coordination," and it walks the cost of multi-threaded coordination all the way down to the CPU cache line. The slide framing is "Are Mutexes Slow?", and the answer he builds toward is that the question was never really about mutexes. It is worth watching in full, because the reasoning is careful and the conclusion lands somewhere more interesting than the title suggests.

{{< youtube tND-wBBZ8RY >}}

The talk is not about Rust, and Gjengset says so at the top: the material maps to whatever language you work in. That is the frame we take as well. The thing being measured is hardware, not a language, and the structural response we take to it is a property of how a program is organized, not of any one syntax.

## Where the cost actually lives

Gjengset's benchmark is deliberately plain: a shared counter that many threads only ever read, never write. Reading shared data should be cheap, and with one thread it is, around 250 million reads per second. Add a second thread and throughput drops to a tenth of that. Add more and it flattens out near the floor. A reader-writer lock, the tool usually recommended for read-heavy work, does no better, and as readers pile up it gets worse than a plain mutex.

The cause is not the lock. It is the [cache coherence protocol](https://youtu.be/tND-wBBZ8RY?t=2079) underneath it. A CPU keeps recently used memory in caches close to each core, tracked in 64-byte chunks called cache lines, and a protocol named MESI negotiates which core may read or write each line. A write to shared data requires coordination across cores. When one core wants to modify a line that others hold, it has to tell every other core to drop its copy first. That cross-core conversation costs roughly thirty nanoseconds per transfer, against about one nanosecond for an access already in L1.

A reader-writer lock loses here for a reason that surprises people. Taking the read lock increments a counter inside the lock, and incrementing is a write. 

> So every reader writes to the same shared field, and that field bounces from core to core, exclusive then shared then exclusive, all the way around the ring. 

The "read" lock is sequentializing on a hidden write. Gjengset's summary is blunt: "[it was never about locks](https://youtu.be/tND-wBBZ8RY?t=2079). Coordination is expensive because of cache coherence." The lock is just the interface the cost happens to arrive through. His other warning is the one worth pinning up: "[lock-free does not mean contention-free](https://youtu.be/tND-wBBZ8RY?t=1696)." A lock-free structure can still drag if two threads keep writing to the same cache line, even when they touch different fields in it. That last case has a name, "false sharing", and it is the part of the talk that our entry here turns on.

## The fix, and who pays for it

Gjengset shows a structure called left-right that scales linearly with readers, and the way it gets there is instructive: it gives every reader its own cache line. No reader writes to memory any other reader touches, so no reader has to coordinate. The readers run fully in parallel because the cores running them never have to talk.

Getting there is expert work. Left-right keeps two full copies of the data, tolerates stale reads, allows only a single writer, and requires a deterministic operation log to keep the copies in sync. Gjengset is careful that it is not a drop-in anything. And the false-sharing bug he hit while building it was a ten-times slowdown traced to two counters landing on the same 64-byte line, fixed by hand with a 64-byte alignment. His closing advice is explicit: you have to "[pick the algorithm that best suits the data transfer patterns your application has](https://youtu.be/tND-wBBZ8RY?t=1980)," exploiting every oddity of your workload to shave the coordination cost.

That is true, and it is the crux. The cost is real, it is structural, and in the world the talk describes it is paid by an expert who knows about MESI, reaches for the right algorithm, and remembers to align the counter. The knowledge sits with the person, and the fix is applied by hand, per data structure, per workload, and re-checked with a profiler after the fact.

## Making isolation a property instead of a discipline

The question we have been asked in our early designs is whether that cost has to be carried the same way. The thing false sharing requires is two independently-used pieces of data sharing a cache line. The thing that prevents it is keeping unrelated mutable state in separate regions of memory. 

> In a model where unrelated state is already separated by construction, false sharing across that boundary cannot arise, because there is no shared line to contend over.

Our actor model is built to start from that separation. In its design each actor owns an arena, its own region of memory, and nothing outside the actor can address it. Messages transfer ownership between actors through typed channels; they do not share it. Two actors that do not share memory cannot share a cache line, so the cross-core coordination Gjengset measures has nothing to coordinate. When state belongs to an actor instead of living behind a shared reference, the isolation that left-right achieves by hand, one cache line per reader, is simply the default arrangement.

This is a property of our ownership model, and it is the one claim here we make in the present tense. The rest, placing arenas to fit cache tiers, sizing them to working sets, verifying isolation with hardware counters like the HITM events Gjengset's `perf c2c` analysis surfaces, is design work in progress in our compiler, Composer, and our orchestration layer, Prospero. The [cache-conscious memory work](/docs/internals/hardware/cache-aware-compilation-cpu/) lays that out for the CPU, and a [companion piece](/docs/internals/hardware/cache-aware-compilation-gpu/) does the same for the GPU. The structural account, why ownership-by-actor-boundary makes cache isolation a consequence and not a chore, is developed in our [Program Hypergraph pre-print](https://arxiv.org/abs/2603.17627).

The honest scope matters here, for the same reason it matters in the talk. Left-right is fast because its author understood the hardware and paid for the performance in design effort. The arena model does not abandon that effort. It relocates it: the alignment, the isolation, the per-region placement become decisions Composer reasons about over a known memory layout, where today they are annotations a developer remembers to write and a profiler later confirms. Where the structure can carry the decision, the developer does not have to.

## Two ends of the same problem

This is the performance companion to an argument we made about correctness in [Fearless Concurrency Gets Real](/blog/fearless-concurrency-gets-real/). There the question was liveness, whether a concurrent program makes progress, and the move was to let our compiler reason about the whole program so the developer does not thread the analysis by hand. Here the question is coordination cost, and the move is the same shape: the structure of the program places mutable state so that unrelated work does not contend, and the aligning and padding and measuring stop being the developer's to remember.

Gjengset's talk is the careful, hardware-level statement of why this matters, and it reaches its conclusion from the systems side, with a profiler and a benchmark and a deep read of the cache. We are coming at the same cost from the compiler side, asking what a language can guarantee about memory placement before the program is built. The cost he counts is real and it is not going away. The question we keep working is how much of it the structure can carry, so that fewer programs have to be hand-tuned to escape a tax that the shape of the program could have avoided from the beginning.
