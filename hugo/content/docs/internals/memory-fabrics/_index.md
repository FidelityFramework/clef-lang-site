---
title: Memory Fabrics
weight: 65
---

One memory model reaching across substrates. BAREWire fixes a value's layout at compile time, and that same layout is what a cache line holds on a CPU, what a coherent link shares between a CPU and an accelerator, and what an RDMA read pulls across a network. These articles follow that model outward from a single machine to the fabrics that connect many.

The through-line is that the boundary between local and remote memory is a design choice, not a fixed cost. A unified-memory desktop, a CXL-coherent link, and an RDMA network are three points on the same axis: how far a typed buffer travels before the program has to copy it. Where the type system already knows a buffer's layout and registration state, that distance is something the compiler can reason about rather than something the developer manages by hand.
