---
title: Categorical Foundations
weight: 60
---

Our Fidelity framework grew from engineering requirements: preserving dimensional meaning through compilation, determining memory placement, and supporting different targets from a shared source. We want an engineer to express a physical operation once and retain its constraints while choosing how it runs.

[Categorical deep learning](/blog/categorical-deep-learning/) helped us recognize a related problem in model design: connecting a model's required structure with its parameterized implementation. Our [adjoint correspondence entry](/docs/design/categorical-foundations/categorical-deep-learning-adjoint-correspondence/) develops that connection through parameter sharing and differentiation, keeping the laws of each construction explicit.

We are extending this inquiry to the relationships between compilation stages and reasoning modes. A library result should remain usable when its premises hold for the transformed operation. Numeric representation and memory layout add conditions that must be checked at their respective boundaries. Our [compilation sheaf design](/docs/design/categorical-foundations/the-compilation-sheaf/) explores how to organize that evidence across the program and its target realizations.
