---
title: "Breaking the P vs NP Mystique"
date: 2025-09-26T00:00:00-04:00
description: "How the Fidelity Framework Makes 'Intractable' Problems Practical Through Intelligent Tooling"
tags: ["Analysis", "Architecture", "Innovation"]
authors: ["Houston Haynes"]
params:
  originally_published: 2025-09-26
  migration_date: 2026-03-29
  updated: 2026-06-09
---

When a vendor site boasts that it can "blur the lines between P and NP," it is almost always describing something more mundane: running a workload on hardware that fits it. The mathematical complexity is untouched. Our read is that most real-world performance barriers trace to an architectural mismatch, the execution model being wrong for the shape of the work, rather than to an algorithmic wall.

Part of this claim is real and part is mystique. Avi Wigderson, the only person to hold both a Turing Award and an Abel Prize, laid out the real version in a recent interview, and it maps closely onto what we are building. Rather than claim to solve millennium problems, we show where intelligent compilation earns a practical speedup and benefit real-world solutions.

{{< youtube 5GUcvSAJcJw >}}

## The Control Flow Trap

To understand why so many problems seem harder than they should be, consider how modern computers execute programs. Everything flows through what we call the von Neumann bottleneck:

\[
\text{Fetch} \to \text{Decode} \to \text{Execute} \to \text{Memory} \to \text{Repeat}
\]

This sequential pipeline forces every computation, no matter how naturally parallel, through a narrow channel of step-by-step execution. When we encounter a problem whose parts are independent, we dutifully encode it as nested loops anyway, then pay for a serialization the problem never presented. That cost is a consequence of how we built the workload.

Two things are in play, complexity and model. Worst-case complexity is real and fixed: nothing here makes an NP-complete problem stop being NP-complete. We choose the *model* at compile time, and as we explored in our [hypergraph architecture](/docs/internals/pipeline/hyping-hypergraphs/), there is a duality in how a computation can be represented. Control flow treats a program as a sequence of instructions. Data flow treats it as a network of dependencies. 

> A problem that looks intractable under control flow can become manageable under data flow. 

The complexity is unchanged. What changed is that the independent work is no longer forced single-file through a sequential channel.

## The Satisfiability Example

Consider Boolean satisfiability (SAT), the canonical NP-complete problem. Given a logical formula, can we find variable assignments that make it true? Traditional approaches compile this to control flow:

\[
\text{Time} = O(2^n) \text{ where } n = \text{number of variables}
\]

Here is where the real claim and the mystique part ways. Throwing raw parallelism at that exponent does not resolve it. A thousand lanes divide the search by a thousand, and a thousand is roughly `2^10`. Against `2^n` that buys back about ten variables, and then the wall returns. Wigderson makes the point in [his own terms](https://youtu.be/5GUcvSAJcJw?t=1385), that **parallel time is one resource *among many*** and for some problems more processors "maybe doesn't help at all." 

> Anyone selling exponential speedup from parallel hardware **alone** is selling the mystique.

Two real gains remain, and both come from matching the work to the machine rather than piling on hardware. The first is the *form* of the parallelism. As we argue in [Weaving the Braid]({{< ref "weaving-the-braid" >}}), parallelism is not one thing: GPU lane-parallelism is a single narrow shape, and most programs express crossings between control and data that an individual lane model cannot properly deal with on its own. Matching the form of concurrency to the structure of the problem, and lowering each region to the substrate that actually fits it, is where we expect the speedups from our implementation of [interaction nets](/docs/design/concurrency/dcont-inet-duality/) onto spatial targets to deliver the highest gains. 

This is where the theory makes its way to the silicon substrate. What is referred to as a 'satisfiability problem' stays exactly as hard in principle, NP-complete from start to finish. What ***moves*** is wall-clock time in resolving that structure. That time collapses because the model fit the work, not because the complexity yielded to some mystical power.

Our own tools make this point better than waving our hands around the abstraction. SAT is the canonical hard *shape*, the plain yes-or-no question of whether a formula can be made true, but our compiler's verification work does not go hand to hand with it in that raw form. Our Composer Compiler is designed to discharge obligations through an SMT solver in the 'fragments' where the decision procedure is decidable and fast in software engineering problem domains: quantifier-free linear integer arithmetic and bit-vectors for widths and bounds, and linear real arithmetic once our [negative and fractional types](/docs/design/types/negative-fractional-types/) bring reals into the obligation space. Across those fragments the solver is sound and complete. That is the same lever again, one level up: an SMT solver is a SAT core wrapped in theory solvers, and the theories are exactly the structure that a bare SAT encoding would throw away. We do not fight the worst case. We work in the fragment where the decision procedure is complete, which is where a real compiler's obligations ***actually* live**.

## What Some Vendors Are Actually Doing

When companies claim breakthrough performance on NP-complete problems (and actually deliver it beyond simple marketing hype), they are typically doing one of three things, and none of them qualify as magic.

**1. Hardware specialization**: building circuits that express the problem's structure directly. Our design for [ternary models on FPGAs](/blog/unified-vision-ternary-models/) assumes this position to reconfigure the fabric to match the computation rather than routing it through a fixed instruction set.

**2. Approximate solutions**: finding "good enough" answers in polynomial time. Many applications do not need the optimum, only an acceptable answer, and this relaxation is the natural escape from an NP-hard optimization. It has a proven ceiling, though. Wigderson [walks through the boundary](https://youtu.be/5GUcvSAJcJw?t=1145): for the canonical constraint problem a random guess already satisfies seven eighths of the clauses, and doing measurably better than that is itself NP-hard. So approximation is legitimate and bounded, which is exactly the register in which an LLM approximates a generative task: useful, and not a guarantee.

**3. Exploiting problem structure**: real instances of NP-complete problems carry properties their worst case does not. Wigderson presses this point hardest. He notes that [an NP-complete problem is a family of instances](https://youtu.be/5GUcvSAJcJw?t=630), and the ones that arise in practice are a far narrower, more structured set than the adversarial worst case. Protein folding is [provably NP-hard as energy minimization](https://youtu.be/5GUcvSAJcJw?t=725), and yet the body folds proteins constantly, because evolution selected sequences that fold efficiently and there are not exponentially many of them. The simplex method is [exponential in the worst case but runs in near-linear time in practice](https://youtu.be/5GUcvSAJcJw?t=820), because the linear systems real problems produce carry extra structure. A delivery route is NP-complete in theory, but an actual road network is close to planar, and that is a property an intelligent compiler can exploit.

Our Fidelity framework takes up all three of these tenets in a way that makes programs efficient given the available resources. Our [coeffect analysis](/docs/internals/mlir/context-aware-compilation/) is where we identify which one applies to each region of a program and compile accordingly.

## The Constraint Satisfaction Pattern

Many "hard" problems share a common pattern: they involve finding assignments that satisfy multiple constraints. Whether it's scheduling employees, routing deliveries, or optimizing portfolios, the underlying structure is similar. Traditional compilation forces us to check constraints sequentially:

\[
\text{Check}_1 \to \text{Check}_2 \to ... \to \text{Check}_n
\]

But constraints don't inherently require sequential checking. Our [Program Hypergraph](/blog/abstract-machine-model-paradox/) is designed to detect when a set of constraints is independent and lower them to a data-flow architecture where those checks resolve ***together***. All of these problems reduce to the same shape because computation is local. Wigderson explains that [every NP-complete problem reduces to satisfiability](https://youtu.be/5GUcvSAJcJw?t=1610) for that reason: a machine changes state by local operations, and any computation is a web of few-variable local constraints. That locality is precisely what a hypergraph represents, and it is why the constraint view and the data-flow view are the same view. The speedup here is bounded by how independent the constraints actually are, which is a property of the problem (and solution) space, not a direct factor of hardware.

## Real-World Impact

Let's be concrete about what this means for actual applications:

**Scheduling systems**: a hospital roster for 100 nurses under real constraints is not the worst-case scheduling instance. Its constraints are local and blocky, most nurses interacting with few others, and that structure is what makes a schedule reachable at all. Shifting this from control flow on a CPU to a 'weave' of interaction nets on an FPGA lets the independent constraints resolve concurrently instead of single-file, so a search that ran for hours previously can finish far faster. The instance is now tractable because of its structure, and the compilation can be what stops squandering that structure on an unnecessarily sequential pipeline.

**Financial optimization**: portfolio optimization across thousands of assets looks prohibitive in the abstract, but real covariance structure is sparse and banded. As we detailed in our [unified vision for heterogeneous computing](/blog/unified-vision-ternary-models/), compiling to accelerators that match that structure is what makes near-real-time optimization reachable.

**Route planning**: delivery routing reads as exponential, yet a real road network is nearly planar and locally clustered. Compiled to a spatial architecture that respects that layout, the problem decomposes along its own seams into subproblems that resolve in parallel.

In each case we are not defeating mathematics, and we are not claiming the worst case got easier. The instances that arise in practice carry structure, and the win is a compilation that stops flattening that structure through a sequential pipeline.

## Where the Speedups Come From

Where we expect a real speedup on a "hard" problem, we can say exactly why, and the reasons are these four:

1. We compile to the execution model that is advantaged, data flow or control flow, by its structure.
2. We match the target to the workload: CPUs for control flow, FPGAs for data flow, GPUs for wide parallel work. CGRA, neuromorphic, and quantum are targets our architecture is designed to eventually serve as those substrates mature.
3. We preserve the mathematical properties an optimizer needs and carry them through the compiler's middle-end so the lowering respects them. This is both an integrity and efficiency benefit.
4. We exploit the structure real instances carry, the near-planar road network, the blocky schedule, the sparse covariance, the formula with local dependency. Respecting the problem space is recognition that one size *does **not*** fit all.

This won't 'transcend' computational complexity as some technology marketing will claim without shame. Our appraoch engineers *through* it. As we explored in [categorical deep learning](/blog/categorical-deep-learning/), the same structure that makes a problem hard in one representation can make it tractable in another. Not only does the math *not lie*, it actually in many ways *shows the way* if we choose to recognize it.

## Democratizing Performance

Evangelizing the 'mystique' leaves engineers assuming their problem is intractable. The real "trouble" is surrendering to mystique before arriving at the principled solution that meets the problem equally. A small business optimizing delivery routes needs neither a quantum computer nor "P-NP blurring" technology. It needs its routing algorithm to run on the available hardware, making the best fit in the shape of its road network data.

Our Fidelity framework is built to make "hard" problems practical by compiling each one to the execution model it in-effect *demands*. The source code that articulates the algorithm does not change. The mathematical problem does not change. By lowering to interaction nets, delimited continuations, or spatial architectures as the structure warrants, we stop imposing the artificial barriers that make these problem classes look more daunting than they need to be in the context of modern computer hardware.

## Moving Beyond the Mystique

The path forward is not about ***"solving"*** P vs NP or reaching 'quantum supremacy' on classical hardware. A vendor claiming esoteric mathematical acumen is usually doing what we do, matching a workload to hardware that fits it, and simply declining to make the plain case. We would rather replace opacity with *clarity*. Where we expect a real speedup, we can point at the execution model that produced it, and where a problem stays difficult given an architectural choice, we can say which structure it lacks.

What keeps drawing us back to this is a conviction that the "worst case" framing has been doing too much rhetorical work. The instances the world actually hands us carry structure, and most of the difficulty engineers fight is not the essential complexity of their problem but the friction of their legacy tooling *flattening* that structure into a shape the hardware was never going to run well. Removing that friction is the work in front of us, and it the *real* efficiency play to focus on the essential complexity any problem genuinely holds.
