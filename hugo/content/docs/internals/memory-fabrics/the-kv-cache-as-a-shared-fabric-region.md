---
title: "The KV Cache as a Shared Fabric Region"
linkTitle: "The KV Cache as a Shared Fabric Region"
description: "When the attention cache lives in a coherent CXL pool, its placement becomes a compile-time decision and its traffic a typed, observable surface"
date: 2026-07-12
authors: ["Houston Haynes"]
tags: ["Design"]
---

An autoregressive model spends most of inference reading back what it already computed. The attention keys and values for every prior token, the KV cache, are held so the next token does not recompute them. That cache is the memory-bound half of inference, and as context lengths and concurrency rise it outgrows GPU memory and then host DRAM. The state that decides the model's latency stops fitting where the model runs.

Samsung's [CXL memory-pooling study](https://semiconductor.samsung.com/news-events/tech-blog/breaking-ai-memory-limits-with-cxl-memory-pooling/) (July 2026) measured the direct answer on production silicon: a coherent CXL pool, built from CMM-D modules behind a CXL switch and fronting NVIDIA Blackwell GPUs, carried the KV cache at roughly 92% of DRAM performance across eight GPUs while scaling capacity well past what host DRAM held. A DRAM-only baseline fell off once the cache outgrew it, paying the recompute cost the pool avoided. The pooled cache is not a slower fallback. It is near-DRAM capacity that the model would otherwise have to recompute or evict.

That result is the hardware this design was waiting for. The [companion coherence note](/docs/internals/memory-fabrics/next-generation-memory-coherence/) argues that CXL 3.0 turns memory topology into the thing a program reasons about, and the KV cache is the workload that makes the argument concrete.

## Placement the Compiler Proves

The prevailing stacks reach the pool through a cache layer. [vLLM](https://github.com/vllm-project/vllm) and [LMCache](https://github.com/LMCache/LMCache) decide at runtime which cache blocks stay in GPU memory, which spill to the pool, and which are evicted, driven by heuristics over an untyped region of bytes. The heuristics are good and the measured performance shows it. What they cannot do is carry a guarantee about where a block resides across the boundary, because the pool is a `void*` with no type-level record of which coherence domain a block sits in.

Our BAREWire contract records that fact in the type. A cache block's residency is a coeffect, `SharedBuffer<'T, cxl_mem>` against `unified` against `gpu_mem`, checked when the access is admitted at compile time. Placement would then be a decision the compiler discharges against the value's dimensional range and access pattern, the same [numeric-selection and coeffect machinery](/docs/design/types/dimensional-type-safety/) that places a quire. A block proven hot stays in GPU memory; a block proven cold-but-live rides the CXL pool; a block that would cross a domain it cannot reach is a design-time diagnostic rather than a runtime stall. The placement heuristic becomes a placement proof.

This is the same decision the [b-posit quire placement](/docs/design/types/posit-arithmetic/) makes for an accumulator, one level up. The accumulator asks whether to keep a sequential reduction with its compute and move one result, or move every operand. The KV cache asks whether attention state stays with the GPU that reads it most or rides the pool the other agents can also reach. Both weigh local-access cost against transport cost, and both would resolve at design time against a typed region rather than at runtime against a byte buffer.

## The Pool Is Addressable by Every Agent

A coherent CXL pool holding the KV cache is not a GPU appliance. Coherent means every agent on the fabric addresses the same region, so a CPU orchestrator, an NPU running a lighter model, or an FPGA doing a fixed transform all reach the same attention state without a copy. The Samsung result front-runs GPUs because that is the immediate KV-cache pressure, and the coherence it rests on is what opens the wider door.

This is the agentic weave reaching a shared substrate. Our [heterogeneous compute design](/docs/design/memory/native-memory-management/) places each part of a workload on the processor whose structure fits it, and a pooled KV cache lets those processors share the one piece of state a multi-agent inference pipeline contends over. An orchestration CPU could read the cache to route a request, a specialist NPU could consume the same blocks to score a continuation, and the write-back would be a typed region both interpret by construction. The cache stops being the GPU's private memory and becomes the fabric's shared working set, which is the coherent-memory form of the [Layer-2 FPGA weld](/blog/building-bulletproof-ebpf-programs/) that welds a substrate into a computation over a link.

## Watching the Cache Traffic

A shared cache pool is only trustworthy if its traffic can be observed, and CXL.mem transactions are exactly the peer-to-peer accesses the network stack cannot see. The [observability inversion](/docs/internals/memory-fabrics/next-generation-memory-coherence/) applies here without changes. The residency and access-kind contract that admits a cache access at compile time supplies the predicate a kernel probe checks at runtime, so a block read from the wrong domain, a coherence-boundary crossing, or a hot block that should have been resident would surface as a witnessed event against the type the compiler already reasoned about. The monitor that watches the pool would be generated from the same contract that placed the cache, not authored by hand against an opaque interconnect.

## Where This Sits

The pieces line up. Pooled KV cache at near-DRAM latency is measured external fact. Residency as a compile-time coeffect, placement as a proof, the pool as a shared surface for every agent, and observability as a byproduct of the cache type are the design this note proposes. None of it ships today, and the conditional voice marks the parts that are design rather than demonstration. The claim under test is that the state an agentic inference pipeline contends over, the KV cache, is best treated as a typed region on a coherent fabric, placed by the compiler and watched by a probe the compiler generates. The hardware to run that experiment now exists.
