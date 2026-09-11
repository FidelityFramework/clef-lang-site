---
title: On Hardware
weight: 60
---

Hardware contracts and bring-up across Fidelity targets. Each page distinguishes current implementation evidence from proposed extensions and from behavior verified on a device.

- [Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/): direct RA6M5 startup, MMIO, and the HelloBlinky vector milestone.
- [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/): substrate requirements and the actual Farscape/toolchain boundary.
- [CPU cache design](/docs/internals/hardware/cache-aware-compilation-cpu/) and [GPU cache design](/docs/internals/hardware/cache-aware-compilation-gpu/): layout and ownership facts, proposed optimizations, and measurement limits.
- [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/): hosted Ariel evidence and the remaining freestanding contract.
- [Storage on Metal](/docs/internals/hardware/storage-on-metal/): durable-storage design and target atomicity/recovery obligations.
- [Bring-Up Beyond the CPU](/docs/internals/hardware/bring-up-beyond-the-cpu/): FPGA, NPU, GPU, and display artifact/acceptance boundaries.

The memory-across-substrates discussion continues in [Memory Fabrics](/docs/internals/memory-fabrics/). Hardware facts belong in versioned platform declarations with manual or schematic provenance. Build and device results belong with the exact source, toolchain, and artifact they validate; these explanatory pages link that evidence rather than silently generalizing it.
