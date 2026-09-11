---
title: Numerics
weight: 55
---

Numeric selection determines which representation satisfies a computation's range and accuracy requirements. Arithmetic construction determines how operations on that representation are carried out; placement determines where the resulting work and state reside.

These articles connect the [normative numeric-selection contract](/spec/draft/numeric-selection/) to Composer's proposed analysis and lowering machinery. They distinguish mathematical guarantees, target requirements, implementation evidence, and measured cost.

- [Arithmetic Construction and Placement](arithmetic-construction-and-placement/) follows functional source through compensated and exact arithmetic to CPU, GPU, spatial accelerator, and FPGA realizations.
- [Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/) introduces the engineering questions and the proposed ThreeBody experiment in a conversational form.
