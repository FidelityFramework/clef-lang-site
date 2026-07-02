# Design: the equipotence companion to "Weaving the Braid"

A structural proposal for a blog entry, not a draft. Anchored on Simon Peyton Jones's
limestone/granite quote and the owner's thread on Church-Turing equipotence and bounded
lessons. The braid post argues that substrates differ in which structures they seat; this
post argues that difference is the only negotiable one, because equipotence fixes
everything else.

## Title candidates

- "The Granite Under the Limestone" (carries the attribution in the image)
- "Granite Under Limestone"
- "The Coordinate-Free Machine"

Owner picks. The SPJ image should appear in the opening regardless of title.

## The anchor

SPJ, in a recent interview [owner to supply venue and link]: "when the limestone of
imperative programming has worn away, the granite of functional programming will be
revealed underneath." The post reads the quote as a claim about what is underneath the
syntax, not as partisanship for a syntax. Before drafting, verify the exact quote wording
against the interview source; the thread's rendering is the working text until then.

## The arc

1. **The quote and its claim.** Regardless of the syntax in use, the underlying formalism
   is unavoidable. Some say the formalism is not needed; the working question is whether
   it is worth the cost incurred by throwing it away.

2. **Equipotence, stated precisely.** Turing machines, register machines, S-K-I
   reduction, a CGRA dataflow fabric: the equivalences among the formalisms are theorems
   (Church, Turing, Kleene); their extension to physical substrates is the Church-Turing
   thesis. Keep the two separate; calling the thesis a theorem is the error a careful
   reader will catch first. What is fixed is the class of computable functions. Substrate
   choice is an engineering decision, not a foundational one.

3. **The lambda calculus as coordinate-free description.** It is the destination nothing
   needs to "rise to" because every substrate already embodies the class in some form.
   That part is not negotiable. Equipotence says nothing about cost, complexity, or which
   structures survive expression without being flattened into encoding; that is the braid
   post's territory, and the two posts split the argument along exactly that line.

4. **Bounded lessons.** SPJ's own "don't put the interpreter in hardware" held while
   general-purpose scaling made compiling-to-x86 the winning move every time. The lesson
   was indexed to a decision space: hardware capability, power budget, time, willingness
   to trade one form of efficiency for another. It was not an ever-present truth, and the
   Lisp-machine history is the worked example the site already carries
   (cross-link: hardware-lessons-from-lisp). As the space shifted, so did the answer.

5. **The shift underway.** NextSilicon and Efficient lowering C onto reconfigurable
   dataflow fabrics are not escaping the class; they are re-expressing the same
   computable functions in a different equipotent model. Mechanism-level and respectful:
   the GUPS demo-selection critique already lives in the braid post and is not re-run
   here; this post's angle is the decision space, not the demo.

6. **What readiness means.** When the decision space shifts, is the framework ready?
   CPU-only and CPU/GPU-dominated frameworks will face a shift that is not minor error
   correction. Our answer from the compiler side is the MLIR fan-out (LLVM, CIRCT,
   MLIR-AIE, further targets under design), with demonstrated-vs-designed verbs applied
   exactly: CIRCT/FPGA is the demonstrated companion; the rest is architecture.
   Cross-link: proofs-to-silicon, the braid post's loom section.

## Thesis to land

Equipotence fixes what every substrate can compute and leaves open only where a program's
structure and cost land. A framework is ready for a substrate shift exactly to the degree
its compilation discipline treats the substrate as a decision rather than a foundation.

## Cross-links, both directions

Inbound (this post links out):
- /blog/weaving-the-braid/ at the structure-seating claim (the braid post now carries an
  equipotence guard sentence in its diagnosis paragraph; this post is its expansion)
- /blog/hardware-lessons-from-lisp/ at the interpreter-in-hardware history
- /blog/abstract-machine-model-paradox/ at multiple AMMs selected by context
- /blog/the-return-of-the-compiler/ at the compiler as the locus of the decision
- /blog/fpga-and-hardware-inference/ at the CIRCT companion
- /docs/design/concurrency/dcont-inet-duality/ and /docs/internals/verification/proofs-to-silicon/
  for the fan-out mechanics
- SPJ interview [link TBD]

Outbound (edits to existing files once this post exists):
- weaving-the-braid: forward-link from the equipotence guard sentence ("equipotence is
  settled") to this post; tight forecast-to-delivery pair
- hardware-lessons-from-lisp: closing forward-link where the bounded-lesson framing is
  foreshadowed
- abstract-machine-model-paradox: forward-link at the AMM-selection claim

## Failure modes

- Calling the Church-Turing thesis a theorem. The formalism equivalences are the
  theorems; the thesis is the extension to physical machines.
- Re-running the braid post's critique. GUPS and demo selection belong there; overlap
  makes both posts weaker and the cross-link pointless.
- Letting "readiness" drift into marketing. Demonstrated-vs-designed verbs throughout;
  the framework's posture is quantum-ready-not-quantum-shipping applied to dataflow.
- Driving the limestone/granite metaphor past one beat. It opens the post and may close
  it with a single callback; nothing in between.
- Attribution drift. The quote is SPJ's; the equipotence framing and the bounded-lesson
  reading are the owner's; NextSilicon and Efficient are external engineering treated at
  mechanism level.
- F# as present language. F# is lineage; Clef is the language the argument speaks for.

## Register

Blog: voiced, first-person, expert-technical. The quote invites a reflective opening;
the equipotence core is stated plainly and briefly, with links doing the textbook work.
Enrichment budget in the braid post's spirit: one table (formalism / proven-equivalent-by
/ what varies) is likely the only figure this piece needs; it is an argument, not a
pipeline.

## Open questions for the owner

- Interview source and link for the SPJ quote; verify exact wording before drafting.
- Title choice from the candidates above.
- Whether to embed the SpeakEZ thread itself or paraphrase it as the post's spine.
- Date and position relative to the braid post (same week reads as a deliberate pair).
