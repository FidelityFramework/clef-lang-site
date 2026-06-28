# Known Art: The Solution Space Already Open to Coexponential Inference

The session-inference problem is reconstructing a coexponential session type from
ordinary actor code, with no developer annotation, surfacing the type only when the
analysis needs help. The coexponential is the stateful-server connective of Qian,
Kavvos, and Birkedal; it gives the inference a definite semantic target to
reconstruct. Whether that reconstruction is feasible at all is the open question.
This document does not claim it is. It catalogues the solution space the Fidelity
Framework already has standing, the parts of this shape that are shipped or
implemented, so the open question is approached from evidence rather than from a
blank page.

The thesis is narrow and optimistic. Two avenues onto the session-inference problem
are already open to us. The first is HelloArty, which is shipped and proves that
inferring a structural property, checking it against a budget at design time, and
surfacing a steerable diagnostic is a working pattern. The second is the liveness
analyzer, which is designed and worked through to the lowering seam, and which
demonstrates that an analyzer path exists for a slice of exactly this problem space:
it already reconstructs the wait-for rank, which is the projection of the session
type onto the liveness question. The width-inference coeffect in our Alex middle end
is the third piece, the implemented mechanism that shows how an inferred structural
fact settles during analysis and is carried to lowering without a second pass.

Each of these is described below with the concrete detail that makes it
load-bearing. The discipline throughout is absolute: the precedents are shipped or
implemented and are stated in present tense; the session inference itself is unbuilt
and is stated only in architectural verbs.

## Avenue one: HelloArty proves the pattern, and it ships

HelloArty is a blinky-LED design for the Digilent Arty A7-100T, compiled through our
Clef toolchain. It is the worked proof that the three-step pattern at the center of
session inference, infer a structural property, check it against a budget at design
time, surface a steerable diagnostic, is a working pattern and not a hope. The blog
post `blog/fpga-and-hardware-inference.md` and the repository at
`github.com/FidelityFramework/HelloArty` are the record.

### Machine classification is inferred, not declared

The Moore/Mealy/Mixed distinction is not a design choice in HelloArty. It is a
property of the dependency graph, present before any annotation. A
`MachineDependencyAnalysis` nanopass walks the step function body in our PSG and,
for each output field, traces whether any transitive dependency reaches an input
parameter. If one does, that output is a Mealy path; if all dependencies resolve to
state or constants, it is a Moore path.

HelloArty comes out classified Inferred Mixed. The LED outputs depend on switch
inputs, so they are Mealy; the UART report depends only on state, so it is Moore.
The editor surface reports it directly:

```
ℹ Design 'helloArtyTop': Inferred Mixed
  Mealy paths:
    Outputs.Leds depends on Inputs.Sw0..Sw3 (color selection)
    Outputs.Leds depends on Inputs.Btn0..Btn3 (cadence control)
  Moore paths:
    Outputs.UartReport depends on state only
  Suggestion: Consider [<MachineModel(Mixed)>] to document intent
```

Nobody declared the design Mixed. That is what the code says, and the analysis reads
it off the structure. The relevance to session inference is direct. A coexponential
session type is also a structural property of actor code, the conversation the
channel conducts over its lifetime, present in the message ordering and the state
threading before anybody writes it down. Machine classification is the existence
proof that a structural property of this kind is recoverable from the graph and
reportable in the editor.

### Width is inferred from operations, with no annotation

The second inference HelloArty ships is bit width. On a CPU `int` means whatever the
register holds. On FPGA every wire is exactly as wide as the design requires, so
width is a property of the code. An `IntervalAnalysis` nanopass walks the enriched
PSG after type resolution and propagates intervals through the graph: constants have
exact intervals, modulus `x % K` bounds the result to `[0, K-1]`, clamp tightens
from one side, addition and subtraction propagate through operands, DU tags take
`[0, numCases - 1]`. The minimum width falls out as `ceil(log2(M + 1))` for an
unsigned maximum `M`.

Two concrete results carry the point. The `Counter` field resets via modulus against
`maxCounterTicks`, approximately 400 million, so its range is `[0, 399,999,999]`,
which fits in 29 bits down from 64. The `PeriodMs` field is clamped by
`clampPeriod` to `[100, 2000]`, which fits in 11 bits. Neither width is declared
anywhere. Each is a consequence of the operations, recovered from the structure.

This is the mechanism the session inference would lean on most directly. The
deadlock-freedom design states the link in its own words: width is inferred from
type structure and surfaced only when inference needs help, and the
synchronous-action priority is the same inference one axis over. The session type is
that inference several axes over again, reading conversation structure where the
width analysis reads value ranges.

### Computation and timing budget are checked at design time

Width reduction is not only about area; it determines whether the design meets
timing, and HelloArty checks that budget before synthesis runs. This is the part of
the precedent that most resembles checking a reconstructed structural property
against a budget at design time, and it ships as a two-layer model.

Layer 1 is a structural depth heuristic that runs during compilation. A
`DepthAnalysis` pass walks the PSG with the same semantic-edge-following traversal
that drives code generation, counting weighted combinational operation depth between
register boundaries, with multiplies and divides at weight 2 and adds and compares
at weight 1. The threshold is not hardcoded:

```
threshold = floor(clock_period_ns / ns_per_weight_unit)
```

The Arty A7-100T binding declares `ns_per_weight_unit = 1.6`. When depth exceeds the
threshold the compiler emits a `CCS0100` warning before synthesis, with a two-sided
diagnostic naming both knobs the developer can turn:

```
Behavior.clef:100: warning CCS0100: Combinational depth 12 exceeds threshold 6 (100 MHz)
  Chain: op_Multiply → op_Multiply → op_Multiply → op_Division → op_Subtraction → op_Division → op_Addition
  Hint: either reduce depth to ≤ 6, or relax clock to ≤ 52 MHz (currently 100 MHz).
        To reduce: break the arithmetic/DSP chain with register stages
```

Layer 2 is Vivado's post-route Worst Negative Slack, the ground truth. HelloArty's
smoothstep chain produces WNS = -2.635 ns at 100 MHz on Artix-7: the design needs
12.635 ns and has 10 ns. Layer 1 flagged it before synthesis ever ran. The
`ns_per_weight_unit` constant is calibrated against the Layer 2 ground truth, and
HelloArty is the first calibration point; the feedback loop from real Vivado runs
back-annotates the structural constant over time, with separate constants for
different fabrics.

With `--warnaserror` the `CCS0100` warning promotes to an error and stops
compilation before Verilog is generated. The developer chooses: fix the depth, relax
the clock, or proceed to synthesis knowing the design will likely violate timing.

### The pattern, stated plainly

HelloArty establishes one repeatable shape: infer a structural property from the
code, check it against a budget at design time, and surface a steerable diagnostic
that names the fix, the relaxation, and the proceed-knowingly options. The structural
property can be machine classification, bit width, or combinational depth; the budget
can be a timing constraint or a width limit; the diagnostic is two-sided and the
developer steers. This is shipped, and it is the template the session inference would
reconstruct in its own domain, with the coexponential as the structural target, the
liveness floor and protocol fidelity as the budget, and a wait-class-style diagnostic
as the surface.

## Avenue two: the liveness analyzer already reconstructs the projection

The second avenue is the deadlock-freedom analyzer, designed in
`deadlock-freedom-as-an-obligation.md` and described for a general audience in
`blog/fearless-concurrency-gets-real.md`. It matters here because it is an analyzer
path that already exists for a slice of the session-inference problem space. The
wait-for rank it reconstructs is the projection of the coexponential session type
onto the single liveness question, the one back-edge from a blocked client to the
server it waits on. The analyzer is the part of the fuller object that is already
worked through, lattice to seam.

### The wait-for relation and the may-wait over-approximation

A synchronous reply expectation across actors is the one construct that carries the
deadlock hazard. A `PostAndReply` suspends the caller's continuation until the callee
answers, and that suspension is an edge in a wait-for relation `W` over actor
behaviors. A fire-and-forget `Tell` adds no edge. The wait-for edges already live on
the joint-constraint axis of our Program Hypergraph, the same axis that holds region,
lifetime, and actor-lifetime hyperedges, and the same axis the coexponential session
hyperedge would ride.

`W` is a may-wait over-approximation. An actor that can call either of two callees
depending on message content contributes an edge to each. The over-approximation
keeps the analysis sound: it never claims deadlock freedom an execution could
violate, at the cost of sometimes flagging a cycle no run reaches. This is the same
soundness posture our escape analysis holds, where `EscapeKind` over-approximates
escape and never under-approximates it.

### Acyclicity as a rank function, discharged QF_LIA

For the fragment where every callee is a statically resolvable actor reference, `W`
is a finite directed graph and deadlock freedom reduces to its acyclicity.
Acyclicity encodes as a rank function: an integer `r` per actor behavior such that
every edge `u -> v` has `r(u) < r(v)`, which is satisfiable exactly when the relation
has no cycle. That constraint is QF_LIA, the same fragment as our interval and bound
checks, and Z3 discharges it as an ordinary Tier 2 obligation. The unsat core is the
cycle, returned as the minimal set of edges that cannot be jointly ranked, which is
the same object the front-end diagnostic names.

### The WaitClass three-case ladder

Every synchronous RPC call site gets a wait classification, deliberately the same
shape as the escape classification managed mutability assigns to every mutable
binding:

```fsharp
type WaitClass =
    // callee static, W acyclic: guaranteed, silent
    | AcyclicStatic
    // callee static, in a connection cycle but
    //   priority-orderable: guaranteed, priority inferred
    | OrderedCyclic of priority: int
    // callee value-carried: visible downgrade to
    //   supervised timeout, diagnostic emitted
    | Unresolved of routing: RoutingKind
```

`AcyclicStatic` is the common case. The check ran, the graph was acyclic, and the
developer hears nothing. `OrderedCyclic` is the case the tree restriction of
Classical Processes wrongly forbids: two actors hold references to each other and
call in both directions, forming a cycle in the connection graph but not in the
wait-for graph, because the calls are ordered so no execution waits around the loop.
The order is the rank, and in the common case the solver finds it; the priority a
textbook Priority CP developer would hand-write is inferred, at zero ceremony.
`Unresolved` is the genuinely dynamic case: the callee is value-carried through
`Actor.self()`-passing, content-based routing, or a runtime-spawned handle.
Acyclicity over such routing is undecidable in general, so the program is neither
forbidden nor silently admitted. The call site drops out of the static guarantee
with a visible diagnostic naming which call dropped and why, and falls back to
supervised execution with a timeout.

### The doc links the two avenues itself

The deadlock-freedom design draws the connection between this avenue and the first
one in its own text, on the `OrderedCyclic` case: width is inferred from type
structure and surfaced only when inference needs help, and the synchronous-action
priority is the same inference one axis over. That sentence is the load-bearing
claim of this whole document, written by the precedent rather than asserted on its
behalf. The width inference and the wait-for priority inference are the same
operation read on different graph edges, and the coexponential session type is the
next edge over again. The wait-for rank the analyzer already reconstructs is the
liveness projection of that session type; the analyzer is a worked instance of
reconstructing part of the object the session inference would reconstruct in full.

The boundary is honest. The wait-for analyzer is sound and present in the design as
the liveness floor, and it stays available whenever the fuller session type is not
inferred, so declining to reconstruct the protocol never weakens the liveness
guarantee. The analyzer reconstructs the projection; it does not reconstruct the
session type. That gap is the open question, and the analyzer is the evidence that
its lower edge is already crossed.

## The implemented mechanism: width inference as a coeffect in Alex

The third piece is not an avenue onto the problem but the mechanism the avenues would
reuse, and it is implemented in our Alex middle end today. The fixed-point
scaffolding pre-print (`fixed-point-scaffolding.md`) documents it. Width inference is
recorded as a coeffect on the PSG during design-time analysis, and the lowering pulls
that coeffect and fixes the integer representation from it, with no second analysis.

The pre-print shows the actual middle-end code. `narrowType` pulls the design-time
width-inference coeffect and fixes the representation from the inferred range:

```fsharp
// Alex middle end. narrowType pulls the design-time width-inference coeffect and
// fixes the integer representation from the inferred range.
let narrowType (coeffects: TransferCoeffects) (nodeId: NodeId) (ty: MLIRType) : MLIRType =
    match coeffects.WidthInference with
    | ...
        | TInt (IntWidth 0) ->                                 // representation not yet fixed
            match Map.tryFind (NodeId.value nodeId) result.NodeWidths with
            | Some inferred -> TInt (IntWidth inferred.Bits)   // fixed from the inferred range
            | None -> failwith "error FPGA0001: range unobservable; the source must annotate the width"
        | _ -> ty   // concrete integers pass through; struct fields are narrowed field by field (elided)
```

The behavior on the unobservable case is the posture the session inference would
inherit. A node whose range the analysis cannot observe is not guessed but reported,
and the `FPGA0001` error asks the source for an annotation. The analysis never
invents a width to keep moving; it states what it could not determine and hands the
ambiguity back to the developer. That is the inferred-with-override discipline at the
mechanism level, the thing a session inference would have to honor when it reaches a
conversation structure it cannot reconstruct.

The coeffect discipline this rides is the one of Petricek, Orchard, and Mycroft,
where a coeffect is what a computation requires from its context. The pre-print
states the three properties that make it carry. The requirement settles during
analysis. It is recorded as codata on the immutable PSG, beside the dimension, the
grade, and the escape class. A navigational pass over the graph, in the manner of
Huet's zipper, witnesses the recorded coeffect at lowering and elides to MLIR
accordingly, so there is no second analysis at lowering time. Settle in analysis,
record as PSG codata, witness with the zipper at lowering. A coexponential session
type, if it can be inferred at all, would be recorded and carried exactly this way:
as codata settled in analysis, witnessed at the seam, never recomputed.

## The inferred-with-override template the whole family shares

Across width, escape, and wait classification the framework applies one template,
worked out for escape in `memory-coeffect-algebra.md`. It is the shape the session
inference would have to fit:

- A tentative classification is derived from the binding site.
- The required classification is computed at each use site.
- The value is promoted, or the relation is ranked, when any use demands more than
  the tentative assignment: `if lambda_required(v, use_i) > lambda_tentative(v) then
  promote`.
- The discharge is QF_LIA over a finite lattice, a single linear inequality per use
  site decided by Z3.
- The common case is zero-annotation and silent.
- Otherwise a visible diagnostic fires and an opt-in override is offered.

Lattice and Composer surface the inference in the editor, presenting the promotion,
the escape path, or the wait-for cycle as navigable structure the developer can
steer. The soundness posture is uniform: `EscapeKind` and `WaitClass`
over-approximate and never under-approximate, so the analysis never claims a
guarantee an execution could break. A session inference built to this template would
over-approximate the conversation structure, discharge what it can over a finite
lattice, stay silent in the common case, and surface a steerable diagnostic with an
override when it cannot reconstruct the type.

## What is open, stated plainly

The two avenues are open; the destination is not reached. HelloArty proves the
infer-check-steer pattern works and ships it for machine class, width, and timing.
The liveness analyzer proves an analyzer path exists for the liveness projection of
the session type and works it through to the seam. The Alex coeffect proves the
carry mechanism is implemented and that the unobservable case is reported rather than
guessed. None of these is the coexponential session inference. Reconstructing a
coexponential session type from actor behavior, without annotation, is not a solved
problem. The fragment that can be inferred cheaply, the fragment that needs developer
help, and the fragment that falls back to the wait-for slice already in place is the
boundary still to map.

The honest framing is that building the inference is part of finding out whether it is
possible. This may require the Fidelity Framework itself to become the expression
mechanism that validates whether the inference is feasible: the PSG that carries the
conversation structure, the coeffect discipline that records it, the tier seam that
discharges what it can, and the editor surface that shows the developer the result.
We do not yet know the answer. What this document establishes is that the question is
approached from a framework that already infers structural properties, already checks
them against budgets at design time, already reconstructs the liveness projection of
the very object in question, and already carries inferred coeffects to lowering
without a second pass. Two avenues are open. The work is to walk them and see how far
they reach.
