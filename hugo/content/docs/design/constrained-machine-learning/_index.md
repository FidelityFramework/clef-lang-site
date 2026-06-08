---
title: Constrained Machine Learning
weight: 10
---

Our Adaptive Domain Model gives a learned model the structure of its domain as a typed invariant the verifier discharges, so it is correct by construction in its domain rather than correct on average. The model therefore provides authoritative output and can 'show its work', grounding its result. The payoff compounds: a typed domain model is more precise, smaller, and cheap enough for simple hardware, and a constellation of them carries the work a monolithic transformer carries weakly and expensively, leaving the language model a bounded interface rather than the whole system.

This section builds that constellation end to end, as a research program and sequence of proposals that fit the theoretical framing. It rests on the [ADM pre-print](https://arxiv.org/abs/2603.18104) and the framework's [other formal work]({{< ref "/docs/guides/_index.md" >}}), and reads the white-box program of [Buchanan, Pai, Wang, and Ma](https://ma-lab-berkeley.github.io/deep-representation-learning-book/) and their open [CRATE](https://github.com/Ma-Lab-Berkeley/CRATE) derivation as a specification that in many cases supports our approach, even as we reach beyond their reading.
