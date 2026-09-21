---
title: "The Lyapunov Window"
linkTitle: "The Lyapunov Window"
description: "A proposed error-limited reconstruction horizon, with explicit sensitivity assumptions, checkpoint costs and experimental acceptance criteria."
date: 2026-08-23
lastmod: 2026-09-21
authors: ["Houston Haynes"]
tags: ["Reversibility", "Numerics", "Coeffects", "Design"]
weight: 65
---

**Status: proposed analysis and experiment.** A forward/backward integration can
measure numerical reconstruction error. It does not, by itself, prove an inverse,
physical accuracy or an advantage for one representation. No ThreeBody experiment
comparing IEEE FP64, posit or b-posit arithmetic has been completed for this design.

The proposed *Lyapunov window* is the span over which a stated reconstruction
procedure meets a stated error tolerance under established assumptions. Its
useful length depends on the method, step schedule, state scaling, arithmetic,
input errors and dynamics. A Lyapunov estimate can inform that investigation;
it is not automatically a certified maximum error bound.

## The Reversibility Horizon

Separate three properties:

- A continuous physical model can have time-reversal symmetry.
- A discrete method can be symmetric in exact arithmetic: applying its negative
  step undoes its positive step under the method's assumptions.
- A finite-state implementation can have an exact inverse on its admitted states.

Symplecticity preserves a geometric structure; it does not imply either of the
last two properties. Symmetric and symplectic methods are related but distinct
subjects in [geometric numerical integration](https://www.unige.ch/~hairer/preprints/gnicodes.html).
Ordinary rounded implementations of a symmetric method need not be bitwise
reversible.

A common sensitivity model is

\[\varepsilon(t) \approx C\varepsilon_0 e^{\lambda t}.\]

If λ is positive, Cε₀ is nonzero, and tolerance δ exceeds Cε₀, this model suggests

\[T^{*} \approx \frac{1}{\lambda}\log\!\left(\frac{\delta}{C\varepsilon_0}\right).\]

This estimate treats error as an initial perturbation. A simulation also injects
error during subsequent operations. A quire may reduce some of those injections;
it does not turn every operation's error into one initial constant or remove
integration error. Effective precision is magnitude- and operation-dependent,
so substituting a format's storage width for ε₀ is not generally justified.

A more useful conditional bound tracks every step. Suppose the admitted state
domain and a specified scaled norm establish an amplification bound Aᵢ and a
local error bound ρᵢ. Then a proposed error analysis can use

\[E_{i+1} \le A_i E_i + \rho_i.\]

For constant bounds A ≥ 1 and ρ ≥ 0, induction gives

\[E_N \le A^N E_0 + \rho\frac{A^N-1}{A-1}\quad(A>1),\]

and Eₙ ≤ E₀ + Nρ when A = 1. This is a mathematical consequence of the stated
bounds, not evidence that those bounds have been established for ThreeBody.
Forward and reverse operations both need coverage. If the reference is the
continuous trajectory, ρ must also cover discretization error; comparison with a
specified discrete reference has a different error budget.

The state norm must account for units and scales. Combining position and momentum
errors as bare numbers produces a tolerance whose meaning changes with units.
An initial-condition set, a norm, a time interval and a reference procedure are
therefore part of the proposed contract.

<a id="three-fixed-points"></a>
## Three distinct techniques

[JANUS](https://arxiv.org/abs/1704.07715) combines integer and floating-point
arithmetic to construct a bitwise time-reversible N-body integrator. It supplies
a different computational property from merely reducing floating-point error.
Its exact reversal depends on its discrete construction and admissible execution
conditions; it is not a numerical method with just a larger approximate horizon.

[Stam's reversible integrator](https://research.nvidia.com/labs/prl/stam2023reversible/reversible2022.pdf)
also uses fixed/integer state with floating-point force calculations to obtain
bitwise reconstruction. Exact return does not establish an exact solution of the
continuous equations, unlimited range or unconditional stability.

A [posit or b-posit quire](/docs/design/types/posit-arithmetic/) instead provides
exact accumulation of represented products while its finite capacity conditions
hold. Inputs, non-accumulation operations and the final rounding still matter.
Whether that improves a particular forward/backward residual over FP64 is an
experimental question. A comparison should include relevant exact-accumulator
or compensated IEEE alternatives, not assume ordinary summation is the only
IEEE implementation.

Checkpointing stores states and trades storage against recomputation.
[Bennett's reversible simulation](https://epubs.siam.org/doi/10.1137/0218053)
is an information-preserving construction with time/space trade-offs. Checkpoint
scheduling does not itself supply a physically accurate integrator or a certified
sensitivity bound. These techniques can be combined only after their distinct
premises are made explicit.

[Revolve](https://users.fmi.uni-jena.de/~autodiff/?id=griewank2000ara&module=Publications&submenu=list+publications)
addresses checkpoint scheduling for reverse/adjoint differentiation. Its scheduling
optimality is relative to its cost model and assumptions. It does not certify the
error of approximately reversing a chaotic trajectory, and should not be described
as assuming that all numerical arithmetic is exact.

<a id="the-entropy-bill"></a>
## Information and saved state

Chaotic sensitivity does not imply that an invertible evolution literally erases
its fine-grained state information at a universal bit rate. Statistical entropy
results do not, without a specified model and assumptions, provide the minimum
number of checkpoint bytes for a finite computation. A bijective finite-state
update can retain its predecessor exactly; a rounded many-to-one update generally
cannot be inverted from its result alone.

Restoring a checkpoint reproduces its saved bits. It does not recover precision
missing from those bits or prove that the preceding approximate reverse calculation
was correct. A checkpoint may already differ from the intended physical trajectory.
Resetting reconstruction error relative to that saved state therefore does not
reset its physical error to zero.

More checkpoints can extend practical replay or navigation coverage, given enough
storage or recomputation. They do not create unlimited numerical precision or
unbounded guarantees at fixed resource cost. Recording a restored state as a
successful inverse would confuse storage recovery with arithmetic reversibility.

## The Window as a Coeffect

The proposed graph evidence would relate:

- the admitted initial states, units and norm;
- range and representation requirements from
  [Numeric Selection](/spec/draft/numeric-selection/);
- a specific integrator, force evaluation and step schedule;
- sensitivity and local-error bounds, including their provenance and validity
  domain;
- a required reconstruction tolerance and resource budget.

Composer could then compare candidate representations and schedules *when those
premises are available*. A measured finite-time exponent would remain estimated
evidence; a verified bound would retain its justification and applicable domain.
The result should identify which kind of evidence supports it. An unresolved
numerical obligation must not become a guaranteed horizon through a default
constant or an assumed effective bit count.

This proposed analysis would need numerical transfer functions and consumers
that establish the sensitivity and local-error evidence. The
[arithmetic construction and placement](/docs/internals/numerics/arithmetic-construction-and-placement/)
design identifies how that evidence would constrain eligible realizations.

The relation can describe a whole computation region. Its inverse recipe and
numerical evidence do not imply a saved dual beside every intermediate value.
An admitted exact inverse can reconstruct from current state; an approximate
inverse needs its error envelope; replay needs its inputs; a checkpoint policy
needs its retention and recomputation budget. The selected realization determines
which execution values remain live. [A Path Less Traveled](/blog/a-path-less-traveled/)
connects that distinction to native bidirectional composition.

Forward-mode differentiation adds another, separate choice. A fixed number of
current tangents can move through a streaming calculation without retaining all
earlier tangents. Their storage follows the live schedule and tangent count.
They carry sensitivity information, not a predecessor history or an inverse
certificate. Selecting a recovery strategy therefore needs the primal dynamics
and arithmetic contract even when its gradient computation has no reverse tape.

## Local Exponents and Global Claims

An asymptotic Lyapunov exponent describes long-time perturbation behavior under
its defining assumptions. A finite-time estimate samples a trajectory and interval.
Neither automatically bounds every perturbation admitted by a program's input
domain. Close encounters and near-singular force evaluation require explicit
separation or other domain constraints, and can invalidate a previously adequate
step/error model.

A runtime estimator may inform an adaptive schedule. A guaranteed schedule would
also need a monitor or validated domain condition, a safe response before a bound
is exceeded, and evidence that adaptation preserves the chosen numerical method's
requirements. Merely observing a larger local exponent after an encounter does
not validate the earlier interval retroactively.

Approximate forward/backward agreement is a test result for the tested states.
It does not prove injectivity or surjectivity over the admitted state space. Exact
finite-state inversion requires its own argument; closeness to the physical
trajectory, conservation behavior and shadowing require different evidence.
Consequently, a reversibility type or structural composition rule cannot by
itself certify the numerical residual of a rounded implementation.

## Checkpoint Economies

A usable checkpoint must contain enough state for the selected recovery behavior:
positions and momenta may be insufficient if replay also depends on an adaptive
step controller, random state, cached history or external inputs. Deterministic
recomputation needs the same arithmetic semantics and relevant execution choices.
Checkpoint integrity, lifetime and retention must be accounted for separately
from reconstruction accuracy.

The execution design assigns analysis and selection to Composer, arena ownership
and orchestration to Prospero, work to Olivier actors and ready-turn scheduling
to Ariel. Those components could carry a selected checkpoint policy; they do not
make its numerical assumptions true. This organization does not presume a managed
runtime or garbage collector.

An initial ThreeBody experiment should fix its initial-condition set, force law,
collision/separation treatment and step policy. Record forward error against an
appropriate reference, forward/backward residual, invariant drift, boundary events,
execution time, transfer costs and checkpoint storage separately. Compare arithmetic
choices at stated storage, accuracy or throughput budgets, and report failures as
well as successful cases. Restored checkpoints must be distinguished from computed
reverse states in both data and visualization.

The result would test whether a useful reconstruction window exists under those
conditions and whether an adaptive policy helps. It would not establish an
ordering in which posit always outlasts FP64. See
[Rounding on Real Hardware](/docs/design/types/rounding-on-real-hardware/) for
operation-level obligations and
[Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/) for the
broader design discussion.
