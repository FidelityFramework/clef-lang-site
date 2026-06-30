# Atlas Kernel-Connectivity Worklist

The spec layer is the inner kernel of the corpus graph. A spec entry is "connected" when a docs or
blog page genuinely links it. **20 of 54 spec entries are connected** (8 pre-existing + 12 from the
Stage-1 cross-link pass); **34 remain isolated.**

## How to read this list (the important part)

The worklist is **not a backlog of chores** — it is a map of where the body of work has not yet been
pulled into the broader context, and reading it is a *design* activity. The graph surfaces *where* the
seams are; domain judgment decides *what each seam means*. There are two kinds, and only the author can
tell them apart:

- **Honest-boundary** — the entry is peripheral *by design*. The graph showing it loosely connected is
  the graph telling the truth about the architecture. Leave it truthfully sparse; do not pad it.
- **Genuine-gap** — the entry is isolated because the *connecting prose has not been written yet*. The
  concept belongs connected; the link is a future writing target.

**Worked example (owner's read):** `ffi-boundary` is honest-boundary *at the chapter level* — FFI is at
the boundary by design, so its sparse connection reflects its real architectural position. BUT the
flat-closure-marshaling sub-thread *within* FFI is load-bearing and genuinely belongs connected to
`closure-representation` and the memory model. So the same isolated entry contains both a correct
boundary AND a genuine gap. The graph cannot distinguish these; the author can.

## Genuine-gap candidates (connecting prose worth writing)

These name real technical concepts the corpus discusses elsewhere; a future docs/blog passage could
genuinely link them. Higher priority for the kernel-connectivity story.

- `/spec/draft/ffi-boundary/` — boundary-by-design at the chapter level, but **flat-closure marshaling**
  across the FFI boundary is a real link target → `closure-representation`, memory model. (owner-flagged)
- `/spec/draft/clef-expr/` — ClefExpr / CCS typed expression representation; the internals docs discuss
  the CCS AST and PSG; a precise link is plausible where the typed-expression layer is the subject.
- `/spec/draft/error-handling/` — Result/exception semantics; discussed in concurrency + interop docs.
- `/spec/draft/native-type-mappings/` — type→machine-layout mapping; central to the NTU/types docs.
- `/spec/draft/type-representation-architecture/` — overlaps NTU docs; needs a non-generic anchor.
- `/spec/draft/synchronous-rpc-liveness/` — wait classification; the deadlock-freedom doc is the natural site.
- `/spec/draft/incremental-computation/` — if/where the docs discuss incremental/observable recompute.
- `/spec/draft/observable-computation/` — reactive/observable thread; near reactive-signals.
- `/spec/draft/intrinsics-crypto-bits/` — the xor/post-quantum blog work touches crypto intrinsics.
- The collection-representation chapters (`list-/map-/set-/seq-/option-operations-representation`,
  `seq-representation`) — connect only where a doc genuinely discusses that representation's mechanism,
  not merely uses the collection.

## Honest-boundary / genuinely-peripheral (leave truthfully sparse)

Grammar internals, spec scaffolding, and inference-machinery chapters with no natural discussion site in
the design/blog register. Their isolation is correct; padding them would corrupt the graph.

- `front-matter`, `rfc-status`, `basic-grammar-elements`, `lexical-filtering`, `lexical-analysis`
- `inference-application-resolution`, `inference-constraint-solving`, `inference-procedures`,
  `inference-supplementary` (the formal inference-procedure chapters; the *concept* of inference is
  discussed everywhere, but these specific procedure chapters are reference material)
- `namespaces-and-modules`, `namespace-and-module-signatures`, `program-structure`,
  `program-structure-and-execution`, `expressions`, `patterns`, `special-attributes-and-types`,
  `interactive-development`, `backend-lowering-architecture`, `introduction`

  (Note: `expressions` and `patterns` are the generic-English-word false positives the Stage-1 pass
  correctly rejected — a doc saying "patterns" is not discussing the Patterns chapter.)

## Discipline

This connects to the project-memory rule that provenance/attribution edits are reader-view-scoped and
hand-tuned, not find-and-replace. Same here: a kernel link must be added ONLY where the prose genuinely
discusses the spec concept as a primary subject. A manufactured edge to connect a dot is the link-graph
equivalent of over-stamping, and it would refute the hypergraph thesis the atlas exists to demonstrate.
The honest 20/54 (rising as connecting prose is written) is the credible picture; a padded 54/54 is not.

## Note: the chaptering-fragment category (deferred spec audit)

A third category beyond genuine-gap and honest-boundary: some isolated entries may be **chaptering
artifacts**, not distinct concepts — candidates to MERGE rather than link. `program-structure` vs
`program-structure-and-execution`, the `inference-*` four-way split, and the per-collection
`*-representation` chapters are the suspects. This is a separate spec-coherence audit on the upstream spec
repo, deliberately NOT folded into the cross-link pass. See `corpus-graph-atlas-plan.md` → "Deferred:
spec-coherence (granularity) audit".
