---
title: "Breaking the P vs NP Mystique"
date: 2025-09-26T00:00:00-04:00
description: "How the Fidelity Framework Makes 'Intractable' Problems Practical Through Intelligent Compilation"
tags: ["Analysis", "Architecture", "Innovation"]
authors: ["Houston Haynes"]
params:
  originally_published: 2025-09-26
  migration_date: 2026-03-29
  updated: 2026-06-09
---

When a vendor site boasts that it can "blur the lines between P and NP," it is almost always describing something more mundane: running a workload on hardware that fits it. The mathematical complexity is untouched. Our read is that most real-world performance barriers trace to an architectural mismatch, the execution model being wrong for the shape of the work, rather than to an algorithmic wall.

There is a version of this claim that is real, and a version that is mystique, and the difference is worth getting right. Avi Wigderson, the only person to hold both a Turing Award and an Abel Prize, laid out the honest version in a recent interview, and it maps closely onto what we are building. Rather than claim to solve millennium problems, we show where intelligent compilation earns a practical speedup and where it cannot.

{{< youtube 5GUcvSAJcJw >}}

## The Control Flow Trap

To understand why so many problems seem harder than they should be, consider how modern computers execute programs. Everything flows through what we call the von Neumann bottleneck:

\[
\text{Fetch} \to \text{Decode} \to \text{Execute} \to \text{Memory} \to \text{Repeat}
\]

This sequential pipeline forces every computation, no matter how naturally parallel, through a narrow channel of step-by-step execution. When we encounter a problem whose parts are independent, we dutifully encode it as nested loops anyway, then pay for a serialization the problem never demanded. That cost is a property of how we compiled it, not of the problem.

The distinction to hold onto is between complexity and model. Worst-case complexity is real and fixed: nothing here makes an NP-complete problem stop being NP-complete. What compilation controls is the *model*, and as we explored in our [hypergraph architecture](/docs/internals/pipeline/hyping-hypergraphs/), there is a duality in how a computation can be represented. Control flow treats a program as a sequence of instructions. Data flow treats it as a network of dependencies. A problem that looks intractable under control flow can become manageable under data flow. The complexity is unchanged. What changed is that the independent work is no longer forced single-file through a sequential channel.

## The Satisfiability Example

Consider Boolean satisfiability (SAT), the canonical NP-complete problem. Given a logical formula, can we find variable assignments that make it true? Traditional approaches compile this to control flow:

\[
\text{Time} = O(2^n) \text{ where } n = \text{number of variables}
\]

Here is where the honest account and the mystique part ways, and it is worth being exact. Throwing raw parallelism at that exponent does not defeat it. A thousand lanes divide the search by a thousand, and a thousand is roughly `2^10`; against `2^n` that buys back about ten variables and then the wall returns. Wigderson makes the point in [his own terms](https://youtu.be/5GUcvSAJcJw?t=1385), that parallel time is one resource among many and for some problems more processors "maybe doesn't help at all." Anyone selling exponential speedup from parallel hardware alone is selling the mystique.

Two real levers remain, and both are about fit rather than force. The first is the *form* of the parallelism. As we argue in [Weaving the Braid]({{< ref "weaving-the-braid" >}}), parallelism is not one thing: GPU lane-parallelism is a single narrow shape, and most programs express crossings between control and data that no lane model can hold. Matching the form of concurrency to the structure of the problem, and lowering each region to the substrate that actually fits it, is where our [interaction nets](/docs/design/concurrency/dcont-inet-duality/) and spatial targets earn their speedups. The second lever is the instances themselves, which the next section takes up.

SAT stays NP-complete throughout. What moves is wall-clock time on the instances that carry structure, and it moves because the model fit the work, not because the complexity gave way.

It is worth being honest about our own tools here, because they make the point better than the abstraction does. SAT is the canonical hard *shape*, but our compiler's verification work does not go hand to hand with raw SAT. It discharges obligations through an SMT solver, Z3, in the fragments where it is decidable and fast in practice: quantifier-free linear integer arithmetic and bit-vectors for widths and bounds, and linear real arithmetic once our [negative and fractional types](/docs/design/types/negative-fractional-types/) ([pre-print](https://arxiv.org/abs/2606.04352)) bring reals into the obligation space. Across those fragments the solver is sound and complete. That is the same lever again, one level up: an SMT solver is a SAT core wrapped in theory solvers, and the theories are exactly the structure that a bare SAT encoding would throw away. We do not fight the worst case. We work in the fragment where the decision procedure is complete, which is where a real compiler's obligations actually live.

## What the Vendors Are Actually Doing

When companies claim breakthrough performance on NP-complete problems, they are typically doing one of three things, and none of them is magic.

**1. Hardware specialization**: building circuits that express the problem's structure directly. Our design for [ternary models on FPGAs](/blog/unified-vision-ternary-models/) reconfigures the fabric to match the computation rather than routing it through a fixed instruction set.

**2. Approximate solutions**: finding "good enough" answers in polynomial time. Many applications do not need the optimum, only an acceptable answer, and this relaxation is the natural escape from an NP-hard optimization. It has a proven ceiling, though. Wigderson [walks through the boundary](https://youtu.be/5GUcvSAJcJw?t=1145): for the canonical constraint problem a random guess already satisfies seven eighths of the clauses, and doing measurably better than that is itself NP-hard. So approximation is legitimate and bounded, which is exactly the register in which an LLM approximates a generative task: useful, and not a guarantee.

**3. Exploiting problem structure**: real instances of NP-complete problems carry properties their worst case does not. This is the lever Wigderson leans on hardest. He notes that [an NP-complete problem is a family of instances](https://youtu.be/5GUcvSAJcJw?t=630), and the ones that arise in practice are a far narrower, more structured set than the adversarial worst case. Protein folding is [provably NP-hard as energy minimization](https://youtu.be/5GUcvSAJcJw?t=725), and yet the body folds proteins constantly, because evolution selected sequences that fold efficiently and there are not exponentially many of them. The simplex method is [exponential in the worst case but runs in near-linear time in practice](https://youtu.be/5GUcvSAJcJw?t=820), because the linear systems real problems produce carry extra structure. A delivery route is NP-complete in theory, but an actual road network is close to planar, and that is a property a compiler can exploit.

Our Fidelity framework takes up all three. Our [coeffect analysis](/docs/internals/mlir/context-aware-compilation/) is where we identify which lever applies to each region of a program and compile accordingly.

## The Constraint Satisfaction Pattern

Many "hard" problems share a common pattern: they involve finding assignments that satisfy multiple constraints. Whether it's scheduling employees, routing deliveries, or optimizing portfolios, the underlying structure is similar. Traditional compilation forces us to check constraints sequentially:

\[
\text{Check}_1 \to \text{Check}_2 \to ... \to \text{Check}_n
\]

But constraints don't inherently require sequential checking. Our [Program Hypergraph](/blog/abstract-machine-model-paradox/) is where we detect that a set of constraints is independent, and it lowers them to a data-flow architecture where those checks run at once. This is the reason all of these problems reduce to the same shape. Wigderson explains that [every NP-complete problem reduces to satisfiability because computation is local](https://youtu.be/5GUcvSAJcJw?t=1610): a machine changes state by local operations, and a whole computation is a web of few-variable local constraints. That locality is precisely what a hypergraph represents, and it is why the constraint view and the data-flow view are the same view. The speedup here is bounded by how independent the constraints actually are, which is a property of the instance, not of the hardware.

## Real-World Impact

Let's be concrete about what this means for actual applications:

**Scheduling systems**: a hospital roster for 100 nurses under real constraints is not the worst-case scheduling instance. Its constraints are local and blocky, most nurses interacting with few others, and that structure is what makes a schedule reachable at all. Compiling it to interaction nets on an FPGA lets the independent constraints resolve concurrently instead of single-file, so a search that ran for hours can finish far faster. The instance was tractable because of its structure, and the compilation is what stopped squandering that structure on a sequential pipeline.

**Financial optimization**: portfolio optimization across thousands of assets looks prohibitive in the abstract, but real covariance structure is sparse and banded. As we detailed in our [unified vision for heterogeneous computing](/blog/unified-vision-ternary-models/), compiling to accelerators that match that structure is what makes near-real-time optimization reachable.

**Route planning**: delivery routing reads as exponential, yet a real road network is nearly planar and locally clustered. Compiled to a spatial architecture that respects that layout, the problem decomposes along its own seams into subproblems that resolve in parallel.

In each case we are not defeating mathematics, and we are not claiming the worst case got easier. The instances that arise in practice carry structure, and the win is a compilation that stops flattening that structure through a sequential pipeline.

## Where the Speedups Come From

Where we expect a real speedup on a "hard" problem, we can say exactly why, and the reasons are these four:

1. We compile to the execution model the work wants, data flow or control flow, by its structure.
2. We match the target to the workload: CPUs for control flow, FPGAs for data flow, GPUs for wide parallel work. CGRA, neuromorphic, and quantum are targets our architecture is designed for as they mature.
3. We preserve the mathematical properties an optimizer needs and carry them to the hardware the workload runs on.
4. We exploit the structure real instances carry, the near-planar road network, the blocky schedule, the sparse covariance, the formula with local dependency.

This does not transcend computational complexity. It engineers *through* it. As we explored in [categorical deep learning](/blog/categorical-deep-learning/), the same structure that makes a problem hard in one representation can make it tractable in another.

## Democratizing Performance

The mystique leaves engineers assuming their problem is hard when the real trouble is a solution poorly matched to it. A small business optimizing delivery routes needs neither a quantum computer nor "P-NP blurring" technology. It needs its routing algorithm to run on hardware that fits the shape of its road network.

Our Fidelity framework is built to make "hard" problems practical by compiling each one to the execution model it wants. Your code does not change. The mathematical problem does not change. By lowering to interaction nets, delimited continuations, or spatial architectures as the structure warrants, we stop imposing the artificial barriers that make these problem classes look more daunting than the instances in front of us actually are.

## Moving Beyond the Mystique

The path forward is not about solving P vs NP or reaching quantum supremacy on classical hardware. A vendor claiming esoteric mathematical acumen is usually doing what we do, matching a workload to hardware that fits it, and simply declining to say so. We would rather say so. Where we expect a real speedup, we can point at the execution model that produced it, and where a problem stays hard, we can say which structure it lacks.

What keeps drawing us back to this is a conviction that the worst case has been doing too much rhetorical work. The instances the world actually hands us carry structure, and most of the difficulty engineers fight is not the essential complexity of their problem but the friction of a compiler flattening that structure into a shape the hardware was never going to run well. Removing that friction is the work in front of us, and it leaves only the complexity the problem genuinely holds.
