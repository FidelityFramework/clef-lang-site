---
title: "Graph-Direct Parallelism"
linkTitle: "Graph-Direct Parallelism"
description: "How the independent side of the duality lowers from the saturated graph to primitive operations, and how the braid records the crossings where parallel work rejoins the sequential spine"
date: 2026-08-24T00:00:00-04:00
weight: 45
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
---

Most parallel frameworks put machinery between the program and the hardware: a thread pool, a task graph with a work-stealing scheduler, a parallel runtime, or a parallel intermediate representation with its own operations. The machinery exists because the framework discovers parallelism late, at runtime or deep in lowering, and needs somewhere to hold what it discovered. Our design discovers parallelism early, on the saturated graph, and that changes what needs to exist downstream. The parallel structure of a Clef program is a property the Program Semantic Graph and its hypergraph extension settle before emission begins, and the lowering emits primitive operations directly. No dialect of ours and no runtime host sits between.

This continues the elevation story that [The DCont/Inet Duality](/docs/design/concurrency/dcont-inet-duality/#upward-migration) tells for both regimes. That piece covers the classification and what became of the interim dialects. This one takes the independent side in detail: what independence is as graph structure, what each parallel shape leaves behind in the emitted code, and where the braid records the one decision that belongs to the sequential spine.

## Independence as Structure

Two operations are independent when no dependency edge connects them and no effect orders them. The pairwise fact is an absent edge, and a conventional dataflow compiler reads that much. The facts that carry the interesting parallelism are joint: a set of redexes can fire together, a set of operations must share one tile, a reduction's sharing structure permits copying here and forbids it there. Those are multi-way facts, and they ride the [Program Hypergraph](/docs/design/structure-and-performance/coupling-and-cohesion/) as hyperedges, where a set of pairwise edges would assert strictly less.

Saturation discharges the joint facts once. Confluence is established over the rule system, a property of the logic checked once rather than an obligation each program incurs. Co-location feasibility is checked against the target's resources. What survives into emission is the consequence: a schedule and a set of annotations, recorded as codata on the nodes the emission traversal visits, or reified onto the operations it emits when a downstream pass still needs the joint fact, the form a tile assignment takes for spatial targets. The traversal itself reads what saturation settled and computes none of it, the discipline the duality piece states as a standing law.

## Three Shapes, Three Residues

The independent side splits by the shape of the work, and each shape leaves a different residue in the emitted code.

Dense, rectangular work lowers through the tensor path into the standing tensor and structured-loop dialects: `linalg` and `tosa` for the algebra, `affine`, `scf`, and `vector` for loop and lane structure, the GPU dialect toward NVVM and AMDGPU, MLIR-AIE for NPU tile arrays. Skipping high-level MLIR means our concepts add no dialect of their own. The tensor path's dialects are external standing infrastructure, target vocabulary in the same sense the LLVM dialect is target vocabulary, and the emission writes into them directly from the graph's schedule. The residue is parallel loops and vector lanes, with nothing of ours between the graph and them.

Irregular reduction, the genuine interaction-net workload, leaves the trio the duality piece describes: rule bodies compiled as ordinary functions over node records, the net itself as runtime data, and a worklist of active pairs that the compiled kernels consume. The worklist runs uncoordinated because confluence was discharged over the rules, so two workers consuming distinct active pairs converge on the same net in either order. The residue is functions, records, and a loop, in `func`, `memref`, `scf`, and `cf`.

Parallelism across actors is the third shape, and it is dispatch rather than emission. Each actor's turn is the sequential regime's aggregate, a continuation state machine, and running many actors at once is a scheduling fact that lives in [Ariel under Prospero](/docs/design/concurrency/ariel-under-prospero/), as data structures the scheduler owns. No parallel construct appears in the emitted code of any single actor, which is what keeps a turn auditable as straight-line sequential logic.

## The Crossing Record

The monoidal bookkeeping on the independent side is free: associativity regroups work across cores, braiding reorders independent operations for locality, and both are semantic no-ops the reordering machinery can apply without obligation. The one decision left to the crossing record is the observable order at the seam, the moment a spawned result threads back into the sequential spine:

```fsharp
let pipeline data = async {
    let! raw = fetchBatch data          // sequential spine: a suspension point
    let scored = query {                // independent region: no ordering among rows
        for row in raw do
        where (valid row)
        select (score row) }
    do! publish scored                  // the crossing: the result rejoins the spine
}
```

The `query` region is free to compute its rows in any order and any grouping. The `do!` that follows is different in kind: the independent region's result enters the effectful spine, and from that point the program's observable behavior depends on the order in which crossings occur. A crossing is where the two regimes meet, and it is a graph point by construction: the node where an independent region's result feeds a suspension's resume. Because suspension points are enumerated in the continuation aggregate and independent regions are delimited by classification, the set of crossings is design-time structure, enumerated the way capture sets and suspension indices are.

That enumeration is what a braid treatment needs. The sequence of crossings is the program's crossing record, its abelian projection (how many crossings, which strands) comes free with the reordering bookkeeping, and only the non-abelian content, which crossing precedes which, raises an obligation. We treat that obligation in [the braid as a fourth sheaf](/docs/design/categorical-foundations/braid-as-a-fourth-sheaf/) as proposed design, and nothing about it requires a parallel IR: the obligations discharge at design time against the enumerated crossing set, and at emission a crossing is primitive, a resume delivery into a state machine, a state-index write, a worklist completion. The braid is a record the graph holds and the verification side reads. It is never an operation the backend must understand.

## Direct Descent

The range this gives us runs from cooperative scheduling control flow at one end to embarrassingly parallel data flow at the other, with the pipelined and actor-structured points between, and every point lowers from the same classification to the same primitive vocabulary. The per-call-site realization choice that [The Continuation Preservation Paradox](/docs/design/concurrency/the-continuation-preservation-paradox/) develops for the sequential side applies here symmetrically: a dense region may realize as SIMD lanes on native targets, as warp-aligned kernels through the GPU path, or as tile assignments consuming the reified co-location annotations on spatial hardware, selected per site from the same graph.

What makes the descent direct is that every decision with cross-node scope has already been made when emission starts. The graph holds the classification, the schedule, the crossing record, and the annotations, and the witness spends its locality budget on bookkeeping: names, blocks, and SSA threading. A parallel runtime holds structure because the compiler discovered it too late to compile. We are designing the pipeline so there is no such leftover structure to hold, and the emitted program carries only what executes.
