---
title: "The Lyapunov Window"
linkTitle: "The Lyapunov Window"
description: "A returnability horizon computed from a declared sensitivity bound, and a checkpoint cadence derived from it: reversibility as a joint constraint between numeric selection and the dynamics of the computation itself."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Reversibility", "Numerics", "Coeffects", "Design"]
weight: 65
---

Run a three-body integration forward for a while, then run it backward. With a symplectic integrator the equations permit an exact return to the initial state, and the arithmetic decides whether the program achieves one. In float64 the bodies come home for a time and then miss, and the miss grows until the reversed trajectory has no visible relationship to the forward one. With a [bounded posit and the quire](/docs/design/types/rounding-on-real-hardware/), the return stays faithful far longer, because exact accumulation removes the per-step rounding that the reversal replays in the wrong order. That comparison is the demonstration our ThreeBody work is designed to make watchable. This page is about the ceiling both versions eventually hit, and about a way to schedule around that ceiling that turns out to have deep roots in standing art.

## The Reversibility Horizon

A chaotic system amplifies any state error at an exponential rate set by its largest Lyapunov exponent. An initial representation error \(\varepsilon_0\) grows as \(\varepsilon_0\, e^{\lambda t}\), so a reversal that must land within tolerance \(\delta\) of the starting state holds only while the accumulated error stays under that tolerance. The returnable span is

\[T^{*} \approx \frac{1}{\lambda}\,\ln\frac{\delta}{\varepsilon_0},\]

and in terms of an effective precision of \(p\) bits with a safety margin \(m\),

\[T^{*} \approx \frac{p\,\ln 2 - \ln m}{\lambda}.\]

That exchange rate governs numeric selection under chaos. Each additional bit of precision extends the horizon by a fixed increment, \(\ln 2 / \lambda\), and the error growth is exponential in time: representation adds returnable time linearly against a loss that compounds. The quire improves \(\varepsilon_0\) by a large constant, which is why the posit trajectory comes home long after the float64 trajectory has lost the thread, and no representation moves the \(\lambda\) in the denominator. The horizon is a property of the system being simulated, not of the arithmetic simulating it.

## Three Fixed Points

The synthesis this page proposes is that three results, developed independently for different problems, are one mechanism seen from three sides: exactness of the step, precision of the span, and scheduling of the anchors. Each covers the axis the other two leave open.

[JANUS](https://arxiv.org/abs/1704.07715) (Rein and Tamayo, 2018) is the exactness result. It is an explicit, formally symplectic N-body integrator that combines integer and floating-point arithmetic so that time-reversal symmetry holds bit-wise: reversing the integration reproduces the forward states exactly, with no error to amplify. [Stam's 2022 integrator](https://research.nvidia.com/labs/prl/stam2023reversible/reversible2022.pdf) reaches the same property by its own route. The cost of exactness in this family is the fixed range of the integer phase space. The [b-posit](https://arxiv.org/abs/2603.01615) and quire path trades a bit of that exactness for tapered precision over a wide dynamic range, with the quire's single-rounding discipline keeping the loss per step small. And the checkpointing tradition, [Bennett's](https://doi.org/10.1137/0218053) reversible-simulation pebble games and [Griewank and Walther's](https://doi.org/10.1145/347837.347846) revolve schedules for reverse-mode differentiation, is the scheduling result: reversal over a long span by recomputation between sparsely stored anchors, with the storage-versus-recompute balance solved optimally.

None of the three ties its schedule to the dynamics. JANUS and the posit path each extend the horizon's constant. Bennett and Griewank place anchors by memory economics under arithmetic they assume exact. What remains open is the cadence question for arithmetic that is *not* exact, on a system whose divergence rate is known: where the anchors go when the budget being spent is precision.

## The Entropy Bill

The instinct behind checkpointing before the horizon reads at first as a workaround, storing state to escape a limit that reversibility purists would respect. The information theory says otherwise. A chaotic system destroys information about its initial conditions at a fixed rate, the Kolmogorov-Sinai entropy, which under the standard conditions equals the sum of the positive Lyapunov exponents ([Pesin's identity](https://en.wikipedia.org/wiki/Pesin%27s_identity)). A finite-precision simulation of such a system loses the corresponding bits per unit time no matter how the arithmetic is arranged. A reversal scheme must bank state at least at that rate or fail at the horizon. There is no third option.

Checkpointing at a cadence derived from \(\lambda\) is therefore the minimal payment: anchors stored at the rate the dynamics destroy information, with structural reversal carrying every step between them. Each window reverses to an exact stored state, the error resets to zero at each anchor with nothing compounding across windows, and total returnability becomes unbounded in the number of windows. Within a window the reversal is the [negative-typed](/docs/design/types/negative-fractional-types/) structural operation, recomputation with no stored record. At the anchors it is a sparse tape, sized by the entropy rate and no larger.

## The Window as a Coeffect

We imagine the sensitivity bound entering the type discipline the way range already does. The [numeric-selection](/spec/draft/numeric-selection/) chain runs dimension to range to representation to width, with the capability gate resolving each choice per target. A declared or estimated \(\lambda_{\max}\) extends that chain by one link: representation and precision fix \(p\), the margin fixes \(m\), and the checkpoint cadence

\[T_c \approx \frac{p\,\ln 2 - \ln m}{\lambda_{\max}}\]

falls out as a derived quantity, carried on the Program Semantic Graph as codata beside the dimensional and placement facts it depends on. Sensitivity, representation, and cadence form one hyperedge, and the solve runs in either direction. Given a representation, the graph yields the window and the schedule. Given a required window, it yields the representations that reach it, which is numeric selection graded by the dynamics of the computation being compiled. On targets where the [capability gate](/docs/design/types/rounding-on-real-hardware/) resolves the same source to different arithmetic, the window resolves differently too, and the [cross-substrate diagnostic](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) gains a row: returnable span per target, computed before deployment, so a reversible computation headed for a float64-only substrate shows its shorter window at design time.

## Local Exponents and Global Claims

Two honest boundaries keep the claim precise. A single global \(\lambda_{\max}\) is conservative for systems whose divergence rate varies along the trajectory, and the three-body problem is the canonical example: close encounters spike the local rate far above the long-run average. The practical form is adaptive, a running finite-time Lyapunov estimate from tangent-space integration tightening the cadence through an encounter and relaxing it after, at the cost of carrying the tangent system alongside the state. The declared \(\lambda_{\max}\) then serves as the static bound the compiler schedules against, with the runtime estimate refining inside it.

The second boundary separates returnability from truth. A reversal that lands within tolerance certifies that the computation is a faithful bijection over the span, and the [shadowing results](https://doi.org/10.1103/PhysRevLett.59.2891) of Hammel, Yorke, and Grebogi govern the different question of how long a numerical chaotic trajectory tracks any true orbit of the underlying system. The window bounds the bookkeeping claim, and the shadowing time bounds the physical one. A page that conflated them would be claiming more than the mechanism delivers.

## Checkpoint Economies

Bennett and Griewank optimize storage against recompute under exact arithmetic. This page's schedule optimizes storage against precision under chaotic dynamics. The same structure appears a third way in event-sourced software, where the journal holds the effects no recomputation can reproduce. In each case the same quantity is being metered: irreversibility, paid for at the rate it actually occurs, whether the rate is set by a memory ceiling, an entropy production, or an external effect. The ThreeBody demonstrator is designed to make the middle case visible on real hardware, float64 and posit side by side, each with its own computed window, each coming home for exactly as long as its arithmetic can afford. Settling the cadence derivation as a compiled discipline, from declared bound to witnessed schedule, is the claim this page stakes.
