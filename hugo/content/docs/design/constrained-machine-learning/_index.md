---
title: Constrained Machine Learning
weight: 70
---

Our Adaptive Domain Model carries a domain's known structure in its types. A conserved grade, the domain's many dimensions, an equivariance: our model encodes each of these in the type, and the type directly informs the weights the model fits. Our design considers a unique Bayesian approach that allows for inference with confidence intervals, and opens the door for continuous learning. 

The payoff compounds: we imagine a well-structured domain model that is more precise, smaller, and cheap enough for simple hardware, and a constellation of them carries the work a monolithic transformer carries weakly and expensively. It's a complex landscape, but one we believe is worth exploring theoretically and researching with aim to make it a next-generation intelligence platform.

This section builds that constellation end to end, as a research program and sequence of proposals that fit the theoretical framing. The motivating vision, specialized models composing on a shared hypergraph substrate rather than one monolithic transformer, was sketched earlier in [A Vision For Unified Cognitive Architecture]({{< ref "/blog/unified-cognitive-architecture" >}}); what follows is where that sketch is worked out. It rests on our [ADM pre-print](https://arxiv.org/abs/2603.18104) and our framework's [other formal work]({{< ref "/docs/guides/_index.md" >}}), and reads the white-box program of [Buchanan, Pai, Wang, and Ma](https://ma-lab-berkeley.github.io/deep-representation-learning-book/) and their open [CRATE](https://github.com/Ma-Lab-Berkeley/CRATE) derivation as a specification that in many cases supports our approach, even as we reach beyond their reading.
