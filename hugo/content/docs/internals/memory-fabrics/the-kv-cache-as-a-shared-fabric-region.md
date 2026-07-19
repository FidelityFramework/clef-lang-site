---
title: "The KV Cache as a Shared Fabric Region"
linkTitle: "The KV Cache as a Shared Fabric Region"
description: "When the attention cache lives in a coherent CXL pool, its placement becomes a compile-time decision and its traffic a typed, observable surface"
date: 2026-07-12
authors: ["Houston Haynes"]
tags: ["Design"]
---

An autoregressive model spends most of inference reading back what it already computed. The attention keys and values for every prior token, the KV cache, are held so the next token does not recompute them. That cache is the memory-bound half of inference, and as context lengths and concurrency rise it outgrows GPU memory and then host DRAM.

Samsung's [CXL memory-pooling study](https://semiconductor.samsung.com/news-events/tech-blog/breaking-ai-memory-limits-with-cxl-memory-pooling/) (July 2026) measured the direct answer on production silicon: a coherent CXL pool, built from CMM-D modules behind a CXL switch and fronting NVIDIA Blackwell GPUs, carried the KV cache at roughly 92% of DRAM performance across eight GPUs while scaling capacity well past what host DRAM held. A DRAM-only baseline fell off once the cache outgrew it, paying the recompute cost the pool avoided. The pooled cache is near-DRAM capacity for state the model would otherwise recompute or evict.

That result is the measured hardware this design targets. The [companion coherence note](/docs/internals/memory-fabrics/next-generation-memory-coherence/) argues that CXL 3.0 turns memory topology into the thing a program reasons about, and the KV cache is a concrete case of that argument.

## Placement the Compiler Proves

The prevailing stacks reach the pool through a cache layer. [vLLM](https://github.com/vllm-project/vllm) and [LMCache](https://github.com/LMCache/LMCache) decide at runtime which cache blocks stay in GPU memory, which spill to the pool, and which are evicted, driven by heuristics over an untyped region of bytes. The heuristics are good and the measured performance shows it. What they cannot do is carry a guarantee about where a block resides across the boundary, because the pool is a `void*` with no type-level record of which coherence domain a block sits in.

Our BAREWire contract records that fact in the type. A cache block's residency is a coeffect, `SharedBuffer<'T, cxl_mem>` against `unified` against `gpu_mem`, checked when the access is admitted at compile time. Placement would then be a decision the compiler discharges against the value's dimensional range and access pattern, using the [numeric-selection and coeffect machinery](/docs/design/types/dimensional-type-safety/) that also places a quire. A block proven hot stays in GPU memory, and a block proven cold-but-live resides in the CXL pool. A block that would cross a domain it cannot reach is a design-time diagnostic rather than a runtime stall.

The [b-posit quire placement](/docs/design/types/posit-arithmetic/) makes this decision for an accumulator, one level up: keep a sequential reduction with its compute and move one result, or move every operand. For the KV cache the decision is whether attention state stays with the GPU that reads it most or resides in the pool the other agents share. Either way the trade weighs local-access cost against transport cost, and it would resolve at design time against a typed region rather than at runtime against a byte buffer.

## Fabric-Wide Addressability

A coherent CXL pool holding the KV cache is not a GPU appliance. Coherent means every agent on the fabric addresses the same region. A CPU orchestrator, an NPU running a lighter model, and an FPGA doing a fixed transform all access the same attention state without a copy. The Samsung study measured the GPU case because that is where the immediate KV-cache pressure sits. The coherence it rests on extends to every other agent class on the fabric.

Our [heterogeneous compute design](/docs/design/memory/native-memory-management/) places each part of a workload on the processor whose structure fits it, and a pooled KV cache lets those processors share the one piece of state a multi-agent inference pipeline contends over. An orchestration CPU could read the cache to route a request, a specialist NPU could consume the same blocks to score a continuation, and the write-back would be a typed region both interpret by construction. The cache stops being the GPU's private memory and becomes the fabric's shared working set, which is the coherent-memory form of the [Layer-2 FPGA weld](/blog/building-bulletproof-ebpf-programs/), where a link joins a substrate into a computation.

## Watching the Cache Traffic

A shared cache pool is only trustworthy if its traffic can be observed, and CXL.mem transactions are exactly the peer-to-peer accesses the network stack cannot see. The [observability inversion](/docs/internals/memory-fabrics/next-generation-memory-coherence/) applies here without changes. The residency and access-kind contract that admits a cache access at compile time supplies the predicate a kernel probe checks at runtime. Three conditions would then surface as witnessed events against the type the compiler already reasoned about: a block read from the wrong domain, a coherence-boundary crossing, or a hot block that should have been resident. The monitor that watches the pool would be generated from the same contract that placed the cache, not authored by hand against an opaque interconnect.

## The Runnable Experiment

Pooled KV cache at near-DRAM latency is measured external fact. This note proposes treating residency as a compile-time coeffect and letting the compiler discharge placement against a typed region. From that one cache type, the pool becomes a surface every agent can address and the monitor is generated rather than authored by hand. The claim under test is that the state an agentic inference pipeline contends over, the KV cache, is best treated as a typed region on a coherent fabric, placed by the compiler and watched by a probe the compiler generates. The hardware to run that experiment now exists.
