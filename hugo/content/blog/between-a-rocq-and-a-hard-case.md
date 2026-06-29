---
title: "Between Rocq & A Hard Case"
linkTitle: "Between Rocq & A Hard Case"
description: "How automated proofs keep the framework's highest tier available while also keeping it off the everyday path"
date: 2026-06-26T09:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Formal Verification", "Concurrency", "Analysis"]
params:
  originally_published: 2026-06-26
---

Every developer wants their code to be memory safe, the threads and processes to never deadlock, and for no buffer to overflow. Our design aims to resolve all of these considerations and more in concurrent Clef, with no annotation and no handwritten proof term to crowd the code. The language, the Program Semantic Graph (PSG), and the elaboration and saturation pipeline that builds it are in place; the proof layer that reads obligations off that graph and discharges them is upcoming work in the compiler scaffold. By design Clef Compiler Services (CCS) traverses the graph continuously, at the point of writing. At build time, CCS carries what it has established through to a re-verification in MLIR, so those properties are preserved all the way to the compiler back end. Above those everyday guarantees sits a higher tier, the relational kind that high-assurance domains like cryptography and safety-critical control reach for. It runs on a distinct but related mechanism, and the design keeps that heavier machinery off the common path so a developer who never needs the top tier still pays nothing to ship.

The scenario is specific: an engineer shipping an ML-DSA signature under FIPS 204, an avionics control under DO-178C, an infusion-pump dose calculation under IEC 62304, they all reach a point where the property that decides certification is no longer about any single point in the computation graph. It is about a pair of executions and the relationship between them. The cryptographer needs the EUF-CMA or IND-CCA2 argument: that an adversary watching the signing routine learns nothing without other compromising materials. The control engineer needs the side-channel argument: that no branch or memory access in the emitted binary depends on the secret. These challenges use terms like 'game-based', 'probabilistic', or 'relational' guarantees. What follows is how our compiler, Composer, addresses that challenge so the cryptographer does not also have to hand-write annotations from a cold start. And we also take some time to show how the generalist software developer gets all the "lower tier" proof benefits without hand-written proof annotation.

The dimensional, memory, and deadlock guarantees ride along on ordinary concurrent Clef the way MISRA-C attends safety-critical C, *on* by default and minimal time cost to author. And our relational proof tier is the domain-dependent layer above that. The same CCS compiler machinery serves both: the structural facts the everyday guarantees are made of are the premises the relational argument consumes, so the work that makes a relational proof cheap for the cryptographer is the work that makes memory safety free for everyone.

The published EUF-CMA proof for ML-DSA is about an abstract scheme. The compiler is the machinery between that design and the binary that eventually results. Any such compiler has the potential to introduce what a separate proof ceremony would never witness: a branch the source did not have, a secret-indexed table lookup, a constant-time select lowered into a conditional jump, an optimization that opens a timing channel. The only guarantee that matters is the one that holds *for the code **that runs***. This is a level that's deeper than proofs generated from code that might later be undercut in the build process.

This is not hypothetical. The Symbolic Software audit of the [hax pipeline](https://symbolic.software/blog/2026-04-07-cryspen-hax/), which translates a Rust subset into an external proof assistant, found that it verified panic freedom and functional correctness of an ML-DSA implementation while missing the zero-knowledge property, because the rejection-sampling loop extracted *to a **proof-inert*** form. The verification ran against a model the extraction had **silently** severed from the security-critical behavior, and the gap is the kind that recovers the secret key from roughly a thousand signature passes. A proof about a model is only as good as its integrity to the computation substrate, and an extraction step in standard proof mechanics is where that potential loss is ever-present. We had argued for this exact failure mode shortly before the audit appeared: that a formal-methods winter follows when extraction-based tools are sold past what they check. The warning is in [The Dangers of Unearned Press](https://speakez.tech/blog/dangers-of-unearned-press/), and the account of its confirmation, three days later in this same audit, in [Case Studies in Consequence](https://speakez.tech/blog/case-studies-in-consequence/).

## Maintaining the Chain

The signing routine shown below is spawned from ordinary Clef. The constant-time primitives transcribe directly from the scrutinized references, because the operations are branchless bit manipulations, and control flow never depends on a secret.

```fsharp
module Clef.Cryptography.ConstantTime

// all-ones mask -> a, all-zeros mask -> b. selection is arithmetic,
// control flow does not depend on the secret.
let ctSelect (mask: uint32) (a: uint32) (b: uint32) : uint32 =
    b ^^^ ((a ^^^ b) &&& mask)

// 0xFFFFFFFF if x < bound, else 0. no early exit.
let ctBelow (x: uint32) (bound: uint32) : uint32 =
    let diff = x - bound
    let borrow = (((x ^^^ bound) &&& (x ^^^ diff)) ^^^ x) >>> 31
    0u - borrow
```

The rejection-sampling loop is the one hax could not verify, and it is the case our design is built to handle without the export disconnect. Our process runs until enough candidates are accepted, acceptance depends only on the public candidate value, and the write index advances branchlessly so the loop's control flow carries no secret. That branchless structure is exactly what hax *lost*: its extraction turned the loop into a *proof-inert* form, so the verifier signed off without ever seeing whether a secret reached a branch. We take the opposite path and never extract the loop into a separate model. The branchless control flow stays on the graph, where it can be re-checked on the lowered code, so that the property confirmed is a property of the instructions in the compute graph.

```fsharp
// fill `output` with `n` samples uniform in [0, bound). acceptance is a
// mask, not a branch; the index advances 0 or 1 with no secret-dependent jump.
let sampleUniform (stream: unit -> uint32) (bound: uint32)
                  (n: int) (output: uint32[]) : unit =
    let mutable filled = 0
    while filled < n do
        let cand = stream ()
        let accept = ctBelow cand bound
        output.[filled] <- ctSelect accept cand output.[filled]
        filled <- filled + int (accept &&& 1u)
```

In our design, the compiler derives the security property, so the developer isn't required to write the proof by hand to get its benefits. CCS would read the relational obligation off the routine's structure, and assemble the proof terms automatically, then re-checks them against the emitted binary, all on the developer's behalf. That posture, where the compiler carries the burden of proof and the developer writes the solution, is the one [Fearless Concurrency Gets Real](/blog/fearless-concurrency-gets-real/) develops for memory and liveness, here carried up to the most demanding tier the framework reaches.

## Negative-Cost Verification

Stroustrup's now-famous "negative-cost abstraction" says the high-level construct yields better machine code than hand-rolling, because structure feeds the optimizer, where significant machine-level knowledge has been invested. Our use of "negative-cost verification" is the same claim for correctness. The facts the proof establishes (dimensions, ranges, escape, grade) are the facts our pipeline reads to choose representation and placement for a given hardware substrate. In our design Composer verifies for safety, and the binary emerges with stronger guarantees optimized to the target, in a [Native Type Universe](/docs/design/types/) that resolves the compute graph and proofs quickly. 

The usual pitch is that proof is a cost the developer accepts directly: write the annotations, cycle proof terms on top of debugging, and accept the slowdown, then eventually collect the warrant. We took the opposite position. A large share of the proof work is meant to be automated as structural support the compiler derives from the program already written, and that automation is an accelerant rather than a tax.

It is meant to move development forward, because the obligations will be discharged as the program is elaborated, surfaced in the editor at the point of writing. We want the most common proof terms to be a design-time element in order to support correctness, a property that identifies issues immediately instead of after a separate verification pass, a failed audit, or worse, a production incident. The feedback loop that formal methods usually defer to the far end of a project sits at the moment of authorship instead. We saw this in F\*'s design and, absent of the annotation burden, we really appreciated the design-time feedback it provided. Along those lines, our approach should lighten the annotation burden, because the structural facts that higher obligations consume, the dimensions and lifetimes and escape behavior and grade, are inferred from the code's own structure where a conventional workflow demands them as explicit declarations. The annotations a conventional verified-programming workflow makes the developer write are, in the cases our algebra covers, derived by the compiler instead of requiring the developer to hand-roll the full assembly of proof terms directly.

That difference carries all the way to the top tier. The relational, probabilistic guarantee there is non-trivial, but we expect our design to be supportive of those furthest reaches: the local facts that those "higher" proof tiers consume are automated. Our philosophy is that developer's contribution should remain with the code that had to be written anyway. Our automated proof elements at tiers 1, 2, and 3 (with some support from library lemmas at tier 3) do the structural work that lets a domain expert reach a relational guarantee without becoming a proof engineer, and that same machinery lets a generalist warrant memory safety and dimensional correctness without writing annotations.

## What spans, and what stays local

Most of what the compiler will check directly in the PSG is "local" to the code being expressed. The dimension of a value, the escape class of an allocation, the width of an integer: each is a property of a single point in the program, and each lands in a narrow decidable theory, integer-linear arithmetic or bit-vectors, the kind a solver settles fast enough to surface in the editor at the point of writing. [The Decidability Sweet Spot](/docs/internals/verification/decidability-sweet-spot/) is the account of why staying inside that fragment is what we expect will keep verification in our framework both interactive at the point of writing and computationally cheap, and [Formal Verification as Compilation Byproduct](/docs/design/categorical-foundations/formal-verification-compilation-byproduct/) is the wider tour of what the compiler is meant to provide the developer 'for free' in the cognitive load-bearing sense.

Some properties are *not* local, though. Whether a set of actors can deadlock is a fact about how the broader program fits together, and no single operation can see the cycle, because the cycle is a relationship among many. The framework is designed to handle this without leaving the decidable fragment. As [Deadlock Freedom as an Obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) lays out, each synchronous call carries only its own wait-for edge as a local fact. The rank is an integer assigned to each actor so that every wait-for edge strictly increases it, which exists exactly when the relation is acyclic. That constraint, \(r(u) < r(v)\) for every edge, is integer-linear arithmetic, the same kind of obligation as an interval check, so it lowers into the SMT dialect where the solver would discharge it like any other.

The signing service from before is the right place to see this. A signer actor fans the work out: it asks a sampler worker for the rejection-sampled vector and a hasher for the challenge, and it blocks on each reply. Nothing here is annotated for liveness.

```fsharp
let sampler = spawn SamplerActor
let hasher  = spawn HasherActor

let signRequest (msg: Message) (sk: SecretKey) = actor {
    let! y = sampler.PostAndReply (Sample sk.bound sk.n)   // signer -> sampler
    let! c = hasher.PostAndReply  (Challenge msg y.commit)  // signer -> hasher
    return assemble y c sk
}
```

Each `let! … = callee.PostAndReply …` suspends the signer until the callee answers, so each is one wait-for edge with the callee at its head. Reading the saturated graph, the compiler would collect those edges over the enclosing region and emit the acyclicity obligation. No edge is written by the developer; each falls out of a `PostAndReply` the code already contains.

```mlir
// witnessed from the PSG read of signRequest and its callees.
%e0 = wait_edge { from = @signer, to = @sampler }   // from `let! y = sampler.PostAndReply ...`
%e1 = wait_edge { from = @signer, to = @hasher  }   // from `let! c = hasher.PostAndReply ...`
smt.assert (forall (u v) (=> (wait %u %v) (lt (rank %u) (rank %v))))
smt.check   // sat: a rank exists, the service is deadlock-free.
            // unsat: the core is the cycle, named back as the wait-for path.
             
```

A signer that fans out to leaf workers has a rank: the workers sit above the signer in the ordering and never call back, so the solver would find \(r\) immediately. The priority a developer would otherwise hand-write is that rank. No actor carries the broader-program fact; each carries its own wait edge, and our analysis gathers them at the enclosing region.

Deadlock-freedom is a spanning concern: it reaches across the compute graph and consumes local facts as premises. And it stays inside the decidable fragment. 

> 'Spanning' is not the same as 'exotic' in that conceptual frame. 

The semantic graph preserves information and stays inside theories a solver decides in microseconds. The local and the spanning compose through one structure, local facts on the operations and the spanning obligation on the region that encloses them, which is the cell-complex discipline [The Compilation Sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) makes precise: local sections checked on the edges, composing into a global section over the enclosing complex. That theoretical framing can be a demanding read, but the net effect is that the compiler front end is doing significantly more work than 'the standard' lexing and parsing code into an abstract syntax tree.

## Where the target set widens

The theories the framework targets are not fixed. They widen as additional portions of the Native Type Universe are engaged, and they widen within the same decidable discipline. Integer-linear arithmetic is the workhorse. When negative and fractional types enter, carrying dimensional exponents that are rational instead of integer, the obligations reach down into the reals, and the target set admits quantifier-free linear real arithmetic beside the integer fragment. That widening is the subject of the [negative and fractional types pre-print on arXiv](https://arxiv.org/abs/2606.04352), and the SpeakEZ companion post [Getting Real with Fidelity Framework](https://speakez.tech/blog/getting-real-with-fidelity-framework/) takes a more conversational path from the integers into the rationals. The reason for combining dimensions is that addition stays linear no matter how many reciprocals are stacked. In proof theoretic terms, the fragment stays linear, quantifier-free, decidable in polynomial time, with principal solutions. In an absolute sense the solver tier got wider. However it did not get *heavier* in kind. The same structural integrity reaches into the rationals through those types, a continuity the [Fixed-Point Scaffolding pre-print](https://arxiv.org/abs/2606.02854) develops as it carries the dual structure of negative and fractional values through lowering. Developers get considerable new tools for building algorithms that ride the normal lowering and proof machinery.

So neither 'spanning' nor reaching into *the reals* steps away from the proof terms the compiler is designed to discharge automatically. This commitment to staying inside the decidable fragment is something we arrived at on our own, but it is not without precedent. The [Dafny](https://dafny.org/) language reached it first, by a different road. 

As anyone who has followed our work knows, a significant influence beyond F\# is the [F\* language](https://fstar-lang.org/). Through our research we saw side-long references to Dafny as a separate SMT-backed verification tradition. We didn't realize the sympathies between our work and the established art in Dafny until quite recently. The comparison that matters here is narrow: Dafny does not use dependent types, has no kernel, no interactive proof fallback, contrasts that the F\* authors themselves draw. Every obligation in Dafny is translated through the Boogie intermediate language and discharged by an SMT solver, *but* when the solver stalls the developer does not drop into a proof assistant. By practice, when a proof falters, the practice with Dafny is to manually insert an intermediate assertion that gives the solver a stepping stone. We arrived at a substantially similar decidability-first commitment, by the abelian-group route that our dimensional type algebra allowed. Our system reaching a similar place that Dafny established by a different discipline is evidence to us that we've been on the right track. The benefit is precise: on a verified-coding benchmark, an SMT-backed system in Dafny's tradition succeeds where a dependent-typed one in the proof-assistant tradition does not, because the gradient tracks how much of the discharge a decision procedure can carry.

The intermediate assertion that Dafny uses is the right primitive, and we have a similar mechanism that has type-elemental underpinnings that are unique and material to our design. Dafny's `assert` is a cut within a single verification stratum: it splits one hard obligation into two the solver can each discharge, and it is placed by the developer's intuition about where the solver stalls. Our tier architecture registers that same cut differently, as the degenerate case of a typed transition, a [mode shift](/docs/internals/verification/mode-shifts/) whose source and target tier coincide, carrying a splitting obligation but no change in verification strength. The genuine transitions are the ones that do change strength, between the decidable tiers and the tier *above* them. Those are not placed by intuition; as our Baker component in CCS identifies them from a structural signal in the Program Semantic Graph. Dafny has one flat stratum and a manual cut that the developer must supply. We have a stack of informed strata with typed shifts between them, and the cut is absorbed as the identity shift inside a stratum.

Dafny also draws a clear line around where its SMT discipline applies. Dafny verifies the source, then a compiler emits to a managed runtime that decides memory behavior the proof scope was never meant to reach. The proof and the artifact meet at the compiler and runtime boundary, and Dafny's guarantee is scoped to the source side of it. That boundary is where our own design goes further: the obligations travel through Composer's middle-end, re-checked at each lowering pass in MLIR's SMT dialect, so a property established at the source level is confirmed to survive every transformation the compiler applies. The lessons Dafny offers confirm our choices on decidability, and they also affirm our choice to keep the verification machinery running through lowering rather than stopping at the source boundary.

## The relational concern

A 'relational' proof property, such as one that shows a signing routine leaks *nothing* about the secret, is about a pair of executions and the relationship between them, with a coupling between their random choices as its content. It is not a property of any *single* point in the program, so it is not "local" in the sense we've been using that term here. This is another point where precision matters, because ***not*-local** is not disqualifying on its own. Deadlock-freedom is not local either, and yet it stays in the decidable tier.

> The relational concern is different on a second axis. 

It is relational *and **probabilistic***, and the probabilistic half is the challenge. The relational structure, a pair of executions, is not itself significant; deadlock-freedom relates an entire set of actors and stays decidable. The problem is the coupling between two runs' *distributions*. A quantifier-free linear theory reasons about one assignment of values at a time, so it has no vocabulary for a distribution, let alone a coupling that relates two of them. Establishing such a judgment is the construction of a derivation in a probabilistic relational proof system, proof search, which is not a decision procedure and carries no polynomial bound. The original designs for the solver tier were scoped, with this specific choice, to the quantifier-free linear fragment in order to stay clear of that open-ended search. The probabilistic component is exactly the class that scoping at certain proof tiers was designed to keep *out*. We wanted to be certain that only when it's ***needed*** is it brought into a project.

The difference in kind is also why the implementation and expected audience is comparatively narrow. A specialist reaches for a relational guarantee because the programmatic use case demands it. The specialist states the property in the domain's own terms, an EUF-CMA game, a constant-time requirement, and the design hands the work of building the derivation that discharges it to the compiler. There may be some hand-annotation that is required of the developer, but the case is self-selective: a developer who reaches for a relational guarantee is already familiar with the domain practice that demands it. Stating the property and building its proof are separate tasks, and the framework is meant to take the second one off the specialist's plate, and we're leaving open the future possibility for lemma libraries or analyzers to help automate portions of this tier as well.

A relational obligation is written as a named statement with clauses, the shape Dafny established, while in our case it carries content that reaches past what F#, F\*, or Dafny was built to state. F\* is the right yardstick, because it layers Dafny-style refinement annotations onto a dependent type theory and goes the furthest of the three. Aim it at the same property the Clef block below states, that two runs differing only in the secret agree on their output, and it reaches a lemma over the two runs.

```fsharp
// F*: the closest the refinement world gets. a lemma over two runs.
val sample_independent_of_secret:
    sk1:secret_key -> sk2:secret_key -> stream:rng -> bound:uint32 -> n:nat ->
    Lemma (requires bound > 0ul)
          (ensures  sample sk1 stream bound n == sample sk2 stream bound n)
//                  ^ equality of the returned VALUES, deterministic case only.
//                    no vocabulary for the two DISTRIBUTIONS or the coupling.
 
```

That lemma reaches the edge and stops. It can equate the values the two runs return, which holds only when sampling is deterministic, and the real routine is not. The security property is about the *distributions* of the two runs, and the coupling between their random choices that makes those distributions equal, and a refinement type has nowhere to put a distribution or a coupling. That is a "wall" the cryptography world hits with refinement types, and the reason it reaches instead for probabilistic relational Hoare logic and tools like EasyCrypt. The Clef surface keeps a form of F\*'s statement shape and adds the two clauses that cross the figurative wall.

```fsharp
[<Tier4.Relational>]
let signatureIndependentOfSecret =
    relating  (run_left  = sampleUniform stream bound n)   // two runs, not one
              (run_right = sampleUniform stream bound n)
    requires  (secret_left <> secret_right)        // differ only in the secret
    requires  (stream_left = stream_right)         // same public randomness
    couples   (accept_left ~ accept_right)         // the coupling F* cannot state
    consumes  Tier3.rejectionTerminates            // termination premise
    consumes  Tier2.constantTime sampleUniform     // constant-time premise
    ensures   (distribution run_left = distribution run_right)
    dispatch_to Rocq
```

The `relating` clause names the pair, which is the irreducibly relational element a value-refinement type cannot express. The `couples` clause specifies how the two runs' random choices are paired, the machinery that makes the distributional reasoning tractable. The `consumes` clauses name the lower-tier premises, so the dependency is explicit and the reconciliation can confirm those premises were actually discharged. The `dispatch_to` clause says where the obligation goes, so the tier boundary is admitted in the surface and never silently assumed. The specialist writes the `relating`, `requires`, `couples`, and `ensures` that state the judgment; the `consumes` and the dispatch are meant to be filled by the compiler from the obligations already on the graph.

The premises cross the proof 'tier boundary' as typed shifts, and our expectation is that the developer wouldn't have to deal directly with any of them. The shift is internal compiler machinery, and the plan reads like this: at a certain point in the nanopass structure in CCS, the Baker component would read the saturated graph, recognize from the loop's branchless structure that a Tier 2 fact admits a Tier 3 termination claim, and would saturate the graph with the shift that carries the obligation. The termination fact climbs from Tier 2 to Tier 3 that way, and the relational judgment sits above both on a second shift the same elaboration would emit, each one carrying the obligation that the lower tier's structure admits the higher tier's claim. What the developer sees through all of it is the `sampleUniform` loop from before, unchanged.

A Dafny `assert` cuts within one stratum and is placed by the developer at design-time, where their experience shows the solver will stall. The Clef shift crosses between strata in the PSG, and the design places it from a structural signal in the code and the resulting graph. The same cut that is manual in Dafny becomes derived *and **typed*** here. We expect that the Tier 3 termination it discharges would itself be a library-carried lemma, proved once and instantiated against the loop's concrete bound, so the loop does not re-derive it. 

```fsharp
[<Tier3.Lemma>]
let rejectionTerminates<'w when 'w : Numeric> (bound: 'w) (n: int) =
    requires (bound > zero<'w>)
    ensures  (terminatesAlmostSurely (rejectionLoop bound n))
    ensures  (expectedIterations (rejectionLoop bound n)
              = n * widthCardinality<'w> / toRational bound)
    discharge_via NegativeBinomial
```

So the relational concern is not a fourth rung on the same ladder as the local tiers, and it is not pushed out by spanning or by the reals. It is a different kind of obligation on a different axis: a broad computation property in a relational, probabilistic logic the decidable theories do not express. It does rely on and leverage the benefit of lower-tier facts, the constant-time fact, the termination fact, the bounds, as premises, and binds them into one statement about the program's  behavior within that scope. 

We also contemplate some automation would emerge at the probabilistic/relational 'Tier 4' as well. Where the 'lower' tiers hand their obligation to an SMT solver and carry the verdict directly in the graph, this could be checked against a probabilistic rule library proved once in an external proof assistant, with the solver still discharging the *'arithmetic leaves'* at design and build time. The trusted base moves exactly once across the entire scaffold: the SMT solver alone carries the lower tiers, and using that 'established art' the proof assistant's kernel can enter to provide its warrants per the demands of the project.

## Bracketing pRHL with the lowering machinery

Because the relational concern is not in the fragment the lowering pipeline carries directly, it is designed to be exercised at two checkpoints that are compiler pipeline adjacent.

The pipeline itself is meant to carry the local, decidable facts. MLIR has one proof dialect, the SMT dialect, and it can carry exactly the quantifier-free fragment targeted, including the spanning-but-decidable obligations like acyclicity that ride on their enclosing region. This is the division the [Fixed-Point Scaffolding pre-print](https://arxiv.org/abs/2606.02854) draws when it describes the framework as having "New Jersey wheels under an MIT frame": MLIR is the structure-carrying vehicle, while the formal structure is settled in our Program Semantic Graph during design-time analysis. The probabilistic relational conditional is not in what that vehicle carries, so it is established separately, and the developer will be able to exercise that option at two distinct phases of compilation.

In our current designs, the first checkpoint is the end of the front end, when CCS has completed its work. After elaboration produces the saturated graph and before lowering begins, the relational property would be established on the source model. The transition from the decidable tiers up to the relational concern is the kind of typed coercion our [Mode Shifts](/docs/internals/verification/mode-shifts/) proposal sketches. At this point Rocq has the entire program as written to that point, with the local facts it consumes already present on the graph. While this is considered as part of the design-time experience, we imagine it to be purely optional and only enabled as the domain demands.

As such, we continue to lower through Composer's "MiddleEnd" and MLIR. The local facts travel through it in the SMT dialect, re-checked at each pass because a transformation could break them. However, the probabilistic relational conditional does not "travel" in the same sense. There is no vehicle for it, and nothing for it to do per pass, because the spanning relation it established does not change as the code is transformed. 

The second checkpoint is after lowering, on the emitted artifact. When the back end has emitted the binary together with its verification certificates, the relational discipline is designed to preserve the option to engage a second time, now with the compiled artifact. This is the final checkpoint, because it closes the gap between "the property holds in the code I designed" and "the property holds in the program that was built." The mechanism is the one [From Proofs to Silicon](/docs/internals/verification/proofs-to-silicon/) develops: at `clef build --release` the graph freezes, a global verification run produces a mathematical witness, and the back end hashes the binary alongside that witness into a release certificate, embedded in a `.proofcert` file or a dedicated ELF section (or perhaps both). That certificate carries the tier label of the obligation, so a reconciliation step can check that the shipped binary is a faithful realization of the proven structure. The same re-check is designed to run in two contexts: as the compiler's own final stage, and as an external re-verification by an auditor or certification lab over the shipped artifact and its certificates. Our design intent is to ensure that the process would need no access to the compiler's internals because everything required is in the emitted evidence.

How the lower-tier premises enter the audit artifact is, in our experience with these reviews, what a careful re-check turns on. A certification lab will re-run the proof in its own kernel. That re-check is only as strong as the premises: if the dimensional, lifetime, and constant-time facts were carried in merely *as **axioms***, the kernel would confirm a *conditional* rather than a fact, and `Print Assumptions` on the final theorem would list all of those axioms. This would not be desirable. So our design carries the premises in as ***proved*** *lemmas* instead. In our pipeline the linear-arithmetic premises are in essence re-established directly by the proof assistant's own tactics, and the bit-vector premises are reconstructed from the solver's certificate so the kernel re-verifies each step, which keeps the solver out of the trusted base. A party who re-runs that proof and inspects its assumptions would find only the kernel's base axioms and standard logic underneath. This is a clean result that labs would look for.

That second checkpoint is also where the design is meant to catch a hazard introduced after the source. The mechanism we are building toward is preservation by re-check, not a fresh audit of the artifact: if a back-end stage were to lower a constant-time select into a secret-dependent path, the constant-time fact it was meant to preserve would fail its re-check against the emitted artifact, and the relational confirmation that consumed that fact would no longer hold. The first checkpoint cannot see a hazard that did not exist yet at the source. The second is where we intend preservation to be confirmed on the generated artifact, so a property broken in lowering surfaces as a failed re-check instead of passing silently.

Reaching past a re-check sits a use case we recognize: an artifact-level verification pass that re-derives the security property directly from the emitted binary or netlist, including the synthesis stages that leave our IR. That would represent a significant body of work, and it is both hardware-dependent and domain-specific, since what counts as a leak differs by target: a secret-dependent branch on a CPU, a data-dependent path delay on an FPGA. We see it as a beneficial engineering track, and one our Fidelity framework would readily support. But the demand for such a mechanism would need to be present in the community before an effort in that category is undertaken.

## Why 'TCB size' matters

A guarantee is only as trustworthy as the parts that hold, known as the trusted computing base (TCB). Our two-checkpoint design is how the process keeps the TCB as small as practical. The relational guarantee rests on two parts: the proof kernel that establishes and re-confirms the relational conditional, and the machinery that carries the local facts through lowering and emits them as certificates. Showing that consistent annotations exist at every stage is necessary but not enough on its own; the reconciliation step supplies the rest by binding the certificate to the shipped artifact and re-validating the carried facts against it. In the negative case where a lowering pass might have misbehaved, the local facts it was supposed to preserve would fail their re-check, and the relational conditional we re-confirm at the second checkpoint would no longer hold over broken facts. So our lowering does not have to be assumed correct, as the mechanism signals when it is not. The fact that this is done at multiple stages, at design time and at build time, with supportive diagnostics, is the crux of our design.

## The shape of things to come

In proof-theoretic terms, our design reaches across the entire program without leaving the decidable fragment. As we've shown in our writing on this site and in our arXiv pre-prints, the fragment widens into the reals as fractional and negative types require, and stays decidable. The one concern that exits is a narrower relational, probabilistic case. The lowering runs between the intrinsic and extrinsic proof obligations, carrying the local facts the conditional consumes.

What stays open is the reach past that re-check, the artifact-level pass we would build in conjunction with customers and the community when the demand is there.

For the current design scope, the signing routine should be a familiar activity for the specialist, as Composer would derive the relational obligation, build the proof terms, and re-check them on output. A hand-built coupling proof demanded by a high-assurance audit would then become a significantly reduced engineering burden relative to standard practice. And for the generalist who rarely reaches for those domains, it is still worth seeing how the framework reasons about its furthest reach, because that same design is what shapes the everyday surface. The lower proof layers generated automatically in the PSG provide "negative-cost verification" for safety and efficiency in every application, for any hardware target.

Rocq, the kernel formerly known as Coq, does double duty of a sort. It is where our "Tier 4" proof obligation is exercised, but it's also the "full final pass" for all obligations in the final review and audit scenarios. And we chose it in part for the reason a lab would: it is the kernel under CompCert and the Verified Software Toolchain, a standard bearer that high-assurance engineering, research, and academic work already rely on, so a lemma or guarantee discharged in Rocq by a developer as a capstone step to the release build is also the activity an auditor can perform in a tool they know. But there's also another reason that's more conceptual than mechanical. Rocq is an ML-family language descended from OCaml, the same lineage Clef occupies. Even though it's not directly material to how Rocq is used with the Fidelity Framework, we find the relation gratifying. Its foundations sit in sympathy: the constructive, type-theoretic ground Rocq stands on is adjacent to the ground our dimensional and grade algebra is built on, so the probabilistic tier reads as a continuation of the framework rather than a foreign tool with impedance mismatches to go with it. And so the title of this post is a small joke on the name, while the choice behind it is quite straight-forward: when the hard case demands an independent kernel, we found the "obvious" choice is also the one closest to home.


The deeper treatments live in the framework's design documentation and pre-prints, collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}): the [decidability sweet spot](/docs/internals/verification/decidability-sweet-spot/), the [deadlock freedom as an obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/), the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/), the [mode-shifts proposal](/docs/internals/verification/mode-shifts/), and the [artifact certificate](/docs/internals/verification/proofs-to-silicon/).

## References

[1] K. R. M. Leino, "Dafny: An automatic program verifier for functional correctness," in *Logic for Programming, Artificial Intelligence, and Reasoning (LPAR-16)*, pp. 348-370, Springer, 2010.

[2] G. Barthe, B. Grégoire, and S. Zanella-Béguelin, "Formal certification of code-based cryptographic proofs," in *Proceedings of the 36th ACM SIGPLAN-SIGACT Symposium on Principles of Programming Languages*, pp. 90-101, 2009.

[3] Cryspen and Symbolic Software, "Verifying ML-DSA with hax: panic freedom, functional correctness, and the limits of extraction," [symbolic.software](https://symbolic.software/blog/2026-04-07-cryspen-hax/), 2026.

[4] X. Leroy, "Formal verification of a realistic compiler," *Communications of the ACM*, vol. 52, no. 7, pp. 107-115, 2009.

[5] P. Wadler, "Theorems for free!" in *Proceedings of the Fourth International Conference on Functional Programming Languages and Computer Architecture*, pp. 347-359, ACM, 1989.

[6] H. Haynes, "Fixed-Point Scaffolding in the Clef Programming Language," [arXiv:2606.02854](https://arxiv.org/abs/2606.02854), 2026.

[7] H. Haynes, "Negative and Fractional Types in the Fidelity Framework," [arXiv:2606.04352](https://arxiv.org/abs/2606.04352), 2026.

[8] N. Swamy, C. Hriţcu, C. Keller, A. Rastogi, A. Delignat-Lavaud, S. Forest, K. Bhargavan, C. Fournet, P.-Y. Strub, M. Kohlweiss, J.-K. Zinzindohoué, and S. Zanella-Béguelin, "Dependent Types and Multi-Monadic Effects in F\*," in *Proceedings of the 43rd ACM SIGPLAN-SIGACT Symposium on Principles of Programming Languages (POPL '16)*, pp. 256-270, 2016.
