---
title: Numerics
weight: 55
---

Numeric selection determines which representation satisfies a computation's range and accuracy requirements. Arithmetic construction determines how operations on that representation are carried out; placement determines where the resulting work and state reside.

These articles connect the [normative numeric-selection contract](/spec/draft/numeric-selection/) to Composer's proposed analysis and lowering machinery. They distinguish mathematical guarantees, target requirements, implementation evidence, and measured cost.

Capacity, numerical error, and reproducibility are separate obligations. Integer range analysis supplies part of the foundation; fixed-point scale and rescaling, floating-point rounding, and parallel merge laws require additional evidence. The specification requires applicable checks during ordinary compilation, without opt-in wrappers, while the companions identify which mechanisms are implemented and which remain proposed.

- [Arithmetic Construction and Placement](arithmetic-construction-and-placement/) explains the integer, fixed-point, and floating-point obligations, analysis responsibilities, and hardware realizations.
- [Rounding on Real Hardware](/docs/design/types/rounding-on-real-hardware/) connects rescaling and enclosure requirements to instruction and circuit capabilities.
- [Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/) introduces the engineering questions and the proposed ThreeBody experiment in a conversational form.
