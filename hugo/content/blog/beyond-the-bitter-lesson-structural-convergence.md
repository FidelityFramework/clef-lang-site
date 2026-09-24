---
title: "Beyond the Bitter Lesson: Structural Convergence"
linkTitle: "Beyond the Bitter Lesson"
description: "Convergence and construction form a continuum, not a binary choice"
date: 2026-08-26T00:00:00+00:00
authors:
  - SpeakEZ
tags: ["machine-learning", "geometric-algebra", "architecture", "formal-verification"]
draft: false
---

For years the argument in machine learning was framed as a question of scale, when the real split was always about where structure comes from: whether a model discovers it on its own or has it supplied by construction. Deep learning bet on discovery: a large enough model fed enough data would find that structure itself, and whatever engineers added by hand was scaffolding to pull away once the data grew large enough. Andrej Karpathy gave that position its clearest public statement, in his 2017 essay [Software 2.0](https://karpathy.medium.com/software-2-0-a64152b37c35). "Neural networks are not just another classifier," he wrote, "they represent the beginning of a fundamental shift in how we develop software. They are Software 2.0." He was not unique in espousing it, only unusually willing to say it in public, early and often, which makes him the concrete example for a stance most of the field shared. It hardened past software into a claim about inference itself, that emergence from scale was *somehow* the **one** road to a correct answer, and that anything built by hand was a confession you did not yet have enough data. The claim split the field. Some took it as liberation. Others took it as a refusal to model what was plainly in front of them, and those who took that view moved on to pursue other avenues of research.

{{< x user="khoiiiind" id="2092449669234528453" >}}

And as we now know, the story Karpathy advanced did not survive contact with the frontier it described. The capability wall was a reliability wall. The labs slipped reinforcement learning into their large language models for the desired result that deep learning alone had not produced. Mistral put sparse mixtures of experts into the mainstream vocabulary, and with them the idea that a model should route to a specialist and run only the part it needs was a revelation. Each model sub-set entered as an efficiency or an alignment fix, and each was structure smuggled in the side door, an ersatz neurosymbolic AI in the clothes of "staged deep learning," soon to be pervasive, yet remaining unnamed.

> In the US, we call it "moving the goal posts."

The sharpest evidence, though, is not in the pipelines the field bolted on. It is in the pure-convergence results we sometimes see put forward as "proof" that structure is somehow unnecessary.

[Mamba-3](https://arxiv.org/abs/2603.15569) spent a model generation and a large compute budget to rediscover a rotor. Its central advance was to bring complex-valued state transitions back, and the object it converged onto, the complex rotational generator its authors bridge to RoPE, is to our eyes a grade-2 bivector exponential, an exact algebraic thing that could have been written down on the first day. The field treats results like this as a triumph of learning over structure. We see them as a missed learning opportunity of another sort. Convergence, when it works, is often the expensive stochastic rediscovery of an object you could have constructed from extant meta-theory.

That claim meets a reflex. Building the structure by hand is what Sutton warns against, so a typed, exact, by-construction substrate sounds like heresy before it gets a hearing. The reflex has a real track record behind it, and it deserves fair treatment in resolving the false dilemma.

## The Bitter Lesson is about answers, not invariants

[Sutton's argument](https://www.incompleteideas.net/IncIdeas/BitterLesson.html) pertained specifically to ***pre-supplying* the solution**. Every case it draws on has the same shape. Someone encoded what the answer is, the good chess move, the correct parse, the useful edge detector, and a system that learned the answer from data and compute won instead.

An invariant is not an answer. A grade-type does not force-feed a model an answer about the domain. It states what any solution must obey, that a conserved quantity stays conserved and that dimensions stay consistent, because a value that violates them is unrepresentable. And from that, we find that **the learning *still* happens**. An Adaptive Domain Model is a learned model. The construction narrows the hypotheses to the admissible elements within defined constraints, and convergence finds the answer among those bounds.

> What Sutton warned against was hand-supplying the answer. 

His original work said nothing against specifying the constraints the learned answer must satisfy. He has since [argued this point](https://www.dwarkesh.com/p/richard-sutton) in interviews, and it's notable that it's still a struggle to get the point across.

The field already shows signs of this progression without identifying its true source. A clear example is convolution, which is a hand-built translation-equivariance constraint. "Attention" is permutation equivariance. RoPE is a fixed rotational structure on position. Every result that scaled past hand-built vision features was computed on top of convolution's baked-in symmetry. None of these was subsumed by the next scaling run, because none was a guess about the answer. Each encodes a true symmetry of its domain, and true-symmetry structure survives scaling and is amortized across every model that makes use of those features, while over-specific structure does not. Our grade-types as designed in Clef sit with convolution, not with the chess heuristics.

## Structure is meaningful at both ends

The thesis with the Fidelity Framework was never that construction beats convergence. It is that they reside in one continuum, and that structure carries material utility along its range. The key is choosing which area along that spectrum is best suited for a given case.

At one end the structure is exact and typed. A domain model carries its grade, its dimension, and its equivariance as invariants discharged before training and held through it. At the other end the structure cannot be typed at all, and it is arranged statistically instead. [Alex Zhang's recent analysis of language-model harnesses](https://alexzhang13.github.io/blog/2026/harness/), the article behind the post above, is a direct measurement of that far end. Compositional generalization is a property of the harness around the learner, not of the monolith's weights, and the harness gets it by keeping every call locally in-distribution.

> His locally-in-distribution is the statistical shadow of what a type makes exact.

Where we would keep a message in-contract, he keeps a call in-distribution, and both are the same discipline at two resolutions.

Our [graph-query worker](/blog/retrieving-fidelity/#grammar-examples-and-meaning) receives worked query examples for a narrow task, and its graph queries pass gateway validation before execution.

So the axis is not structure against no-structure. It is the form the structure takes, exact and constructed where the invariant is known, statistical and arranged where it is not. Structural convergence is our name for the axis: convergence that runs inside a constructed frame, and settles faster when it is a product of principle and not lossy discovery.

## A Matter of Phase

That axis is older than the near-term findings. [Harper, Mitchell, and Moggi](https://dl.acm.org/doi/10.1145/96709.96744) drew it in 1990 as the phase distinction: the split between the static, compile-time part of a program and the dynamic, run-time part. We found the connection through a call-out around [Verse](https://simon.peytonjones.org/assets/pdfs/verse-conf.pdf). [Paul Snively](https://youtu.be/VBT0j14rn5c) put Tim Sweeney's reading of it to us in passing - that "type" and "term" are more a matter of phase than category. A type is a term settled early, a value a term settled late, and which of the two is observed turns on when its fact is 'fixed'.

Read that way, our two ends are two phases of one computation. Construction is the part fixed at the static phase, by the frame, before a gradient step. Convergence is the part fixed at the dynamic phase, once the data arrives to settle what the frame left open. A grade-type is pinned and erased before the binary exists, and the value fused inside it is settled only at runtime. The exact and the statistical are one structure caught at two times.

## The continuum in our pre-prints

This disposition is not new to our work. [Decidable by Construction]({{< ref "/docs/guides/_index.md" >}}) is the capstone of the base type-theoretic framework, and the foundation on which our scaffolding continues. A property worth having is structural, the proof carried by the construction rather than as an emergent property.

Our [Adaptive Domain Models](https://arxiv.org/abs/2603.18104) pre-print turns that disposition on the task of learning, and it articulates the continuum in a single artifact. An Adaptive Domain Model is a learned model, so *convergence* is doing real work. Its structure is a typed invariant, so construction is material over the same object. It learns inside its admissible space and never pre-supplies an answer, which is exactly why the Bitter Lesson holds nothing over our work. The block-diagonal generator whose off-block entries are provably absent, the sparsity that is a fact of the type rather than a learned near-zero, and the posterior the model reports because a domain answer should carry its own confidence, each is convergence running inside constructive integrity.

## Places on a Dial

The frontier is choosing, stratum by stratum, how much admits exact construction and how much is left to convergence. Our work on what we refer to as "grade axes" is our current research into that thesis. As our own frame on implementation converges, we separate grade into three segments: the part that behaves as a group, the part that behaves as a lattice, and the signature that parameterizes that specific algebra. Each part is constructed to exactly the degree it truthfully can support. Other algebraic strata sit behind this approach, and each one sets the balance differently and appropriately to its purpose. It seems a bit abstract, and it is, but the reasoning behind preserving that structure becomes apparent as its capability for "tagless" structure in the lowered artifacts comes into view.

When it comes to modeling a domain, the more a stratum is constructed without over-parameterizing the computation, the less time and energy is spent rediscovering what was present from the start. What we refer to as "negative cost of abstraction" (borrowed from Stroustrup to our own purpose) is the ambition, and it is not a rejection of learning. It is a way to give learning *less to **reinvent***. The point of finding convergence through a constructive frame was never to win an argument against convergence. It was to start the search from the invariant, so the compute goes to what is genuinely unknown rather than to rediscovering a rotor in the most expensive manner possible.

## The Probabilistic Stratum

Most working programmers meet types as labels that pin one thing down. `int` is a whole number, a sum type is one of a fixed set of shapes, and the checker's job is to confirm you are compiling the exact kind it expected. But that picture is the *shallow* end of what the machinery does. Sum and product are only the addition and multiplication of an algebra of types, and most languages stop there. [Negative and fractional types](https://arxiv.org/abs/2606.04352) are the subtraction and division that complete it, and the division half already has the shape of Bayesian conditioning.

Push past the label and a type already carries more than a point. In our [dimensional types](https://arxiv.org/abs/2603.16437) a value carries the algebra of its quantity, not just its number: a velocity is a length over a time, a force a mass times an acceleration, and the compiler cancels the units by construction and refuses any sum that crosses them. That algebra is the substrate the rest of our type universe stands on. Dimensional inference also determines the value's range, and range propagation over it produces a safety proof from the arithmetic rather than hand-rolled proof annotation or a Cartesian test suite.

Ranges are one step. The same machinery types a distribution over values as readily as a single value, an object you compose by the same rules. A point reference is the degenerate case of that object, a spike with all its mass in one place. It is the certainty limit of a spread that is, in general, wide.

So a type that carries a posterior is the type system working in its native probabilistic register, not a metaphor bolted onto it. The point-value is the special case, the one where the variance went to zero. We explore the entire space: a construction constraint reads as a prior. A grade or a conserved quantity concentrates all the probability mass on the admissible set before a single sample arrives, so the model can only sharpen its posterior inside that set, and more importantly never place weight outside it. Once a type can hold a distribution, the question stops being whether structure and learning belong together and becomes which part of the structure each component settles.

## Machine learning is a profunctor over the probability monad

The compact statement of this section came out of a conversation with [Paul Snively](https://www.youtube.com/watch?v=Cq_IstGhUv4) (the link to an interview separate from the Verse talk). Against two points this work had been pressing for a while, that the field runs on reinforcement learning while disclaiming it, and that the apparatus is Bayesian inference bought at ruinous cost, he gave the logician's compression:

> Of course Bayes' theorem goes to modus ponens in the continuum limit! It had better!

The line draws blank stares, because it presumes understanding of calculus and formal logic in what otherwise would seem to be a humorous attempt at light, nerd-flex irony. But here we will follow it forward: if Bayes collapses to modus ponens at the deterministic limit, then learning, the probabilistic interior beneath that limit, had better be a profunctor over the probability monad. The rest of this section is that consequence in unpacked form.

The continuum has an historical frame that is instructive. The Kleisli arrow dates to Heinrich Kleisli in 1965, and the probability monad it runs over goes back to Lawvere in the early 1960s, formalized by Giry in 1982. This structure was settled before backpropagation entered common use, decades before the transformer. A 'learner' is a Kleisli arrow of the probability monad, an input sent to a distribution over outputs. Organize those arrows into a bimodule and you have a profunctor, and profunctor composition is a coend, an integral over the intermediate object, which in this setting is marginalization over the latent. Composition is Bayesian updating. So Bayesian inference is not a technique a model applies. It is the composition law of the category the model already lives in, and a learner that composes correctly is a profunctor over that monad. Otherwise, it is computing the wrong thing.

Send the probabilities to zero and one and the more tame representation shows through. Distributions collapse to point masses, the Kleisli category collapses to relations, the coend collapses to relational composition, and Bayes becomes *modus ponens*. The exact logical structure is what the probabilistic extreme *eventually* reduces to in the limit, which is why Snively's line is a claim about the shape of the subject rather than simply a clever retort. The rotor that was articulated earlier in this piece is one exact object that computational convergence rediscovers at significant cost. This is a specific example of a more general case. Every learning system is an expensive instantiation of a probabilistic profunctor, and the only open question is how much of that profunctor was 'written down' and how much is left for gradient descent to discover at the whims of the data that inform it.

The 'tell' is in the training loop the field is shy to describe. Maximum-entropy reinforcement learning *is* variational inference, so the reinforcement learning the field leans on, while disclaiming it, is Bayesian inference under another name. It is the same profunctor composition once more. The standard stack spends enormous compute amortizing that coend, precomputing into weights the marginalization a typed model performs directly when it holds a given structure.

> Amortization is the price of not knowing the invariant.

Our domain models report the posterior directly because the profunctor is typed rather than fit. In short, our unique approach is the *construction* end of the continuum, in the vocabulary that renders it at once distinct from convergence-only methods while able to work the same problems.

## Structure's Return

And to his credit, Karpathy has since moved the marker, although it's hard to see how he might have stayed on that original position and remain relevant today. Talking with [Dwarkesh Patel in October 2025](https://www.dwarkesh.com/p/andrej-karpathy) about the years it took to make self-driving reliable, he named the real shape of the work, **the *march of nines***: every nine of reliability past ninety percent costs the same constant, massive effort as the one before, because each clears a rarer tail of edge cases the last did not reach. The demo was always easy. The product was always a grind. He is not singular in walking this back, but is named here as a credit for leaving enough candor on the public record to make the arc legible to those interested. And that full arc also belongs to the entire field, if the DL-ride-or-die adherents are honest enough to admit it.

The "march of nines" is our thesis in empirical dress. Reliability in Karpathy's frame costs a constant amount 'per nine' because pure convergence begins from *a cold start*, rediscovering with compute a structure that we believe would be more easily found by other, principled and more time-efficient means. That process is the most expensive possible way to back into an integrity that was there in the logic all along. 

{{< youtube lXUZvyajciY >}}

The steps the last section traced from modus ponens through the profunctor into the type were available from the start, exact and free, to anyone grounded enough to follow the progression forward rather than stumble back into it once the scaling ran dry. Following it forward, from pure logic to a domain model that discharges its own guarantees, is the work we have been doing, and it remains open to anyone with the background to see the progression, and ***follow it***.

## The Honest Middle

At the risk of overemphasizing the point, none of this is a plea to hand-code an answer, as with rules-based "expert systems" in the 1980s. A model with the answer 'baked in' is a lookup table, and the Bitter Lesson has always been right about that failure mode. Nor is *the fix* a matter of handing the problem to some magical idea of scale and waiting for truth to emerge from it. Our position is that the useful ground is along a continuum between those two extremes: a model that learns inside a frame shaped to provide constructive constraint. What it discovers based on the provided contextual data is arrived at honestly, and something out of its range is unrepresentable rather than statistically unlikely. That is the constellation we are building, domain models built with structural bounds and composed on a shared substrate, developed across our [Constrained Machine Learning]({{< ref "/docs/design/constrained-machine-learning/_index.md" >}}) design. This middle ground is where we believe that the most reliable, most safe and most efficient systems will come from.

The frame does more than rule out a *wrong* answer. It sets the distribution over the right range before the first sample, and drawing that prior into the type, so construction and probability are one object, is the next stretch of our grade-axis work. The Bitter Lesson is a historical argument, and taking it with nuance is what lets this work distinguish itself from the experiments that came before. When we refer to HPC and AI being "the same problem" with distinct emphases for each domain, we're pointing to this structural, graded landscape that informs in many directions and provides coherent solutions to problems the field finds intractable today. It's the reason we insist on unpacking the history of extant art, all of it pointing in this direction. Those hard-won lessons are valuable, and bringing them into the modern compute terrain in coherent form is, we hope, a new lesson the field will consider.
