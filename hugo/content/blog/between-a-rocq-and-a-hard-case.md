---
title: "Between Rocq & A Hard Case"
linkTitle: "Between Rocq & A Hard Case"
description: "How automated proofs keep the framework's highest tier available while also keeping off the everyday path"
date: 2026-06-26T09:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Formal Verification", "Concurrency", "Analysis"]
params:
  originally_published: 2026-06-26
---

A developer wants the program to be memory safe, the threads to never deadlock, and buffers to never outlive their owner. Our design delivers all three (and more) in concurrent Clef, with no annotation and no handwritten proof term to crowd the code. The language, the Program Semantic Graph (PSG), and the elaboration and saturation pipeline that builds it are in place; the proof layer that reads obligations off that graph and discharges them is the next work to commence in the compiler scaffold. It aims to verify those facts on the PSG continuously and carry them through to a build-time re-check. There is another guarantee above that, the kind a cryptographer or a control engineer reaches for, supported by a distinct but sympathetic mechanism. The discipline that keeps that level of proof machinery off the everyday developer's desk is the same discipline that makes the everyday guarantees *free*, which is why the more demanding tier is worth understanding on its own merits.

There is a further reach, and this blog entry explores it in detail. The scenario is specific: an engineer shipping an ML-DSA signature under FIPS 204, an avionics control under DO-178C, an infusion-pump dose calculation under IEC 62304, they all reach a point where the property that decides certification is no longer about any single point in the computation graph. It is about a pair of executions and the relationship between them. The cryptographer needs the EUF-CMA or IND-CCA2 argument: that an adversary watching the signing routine learns nothing it could not have computed without the key. The control engineer needs the side-channel argument: that no branch or memory access in the emitted binary depends on the secret. These challenges use terms like 'game-based', 'probabilistic', or 'relational' guarantees. What follows is how our compiler, Composer, addresses that challenge so the cryptographer does not also have to hand-write it from a cold start. And we also take some time to show how the generalist software developer gets all the "lower tier" proof benefits without a single hand-written proof annotation.

The dimensional, memory, and deadlock guarantees ride along on ordinary concurrent Clef the way MISRA-C rules ride along on safety-critical C, *on* by default and costing nothing to author. The relational tier is the emergent, case-based layer above that, reached only by the signing routine, the key-exchange step, the calculation whose timing must not leak. The same machinery serves both: the structural facts the everyday guarantees are made of are the premises the relational argument consumes, so the work that makes a relational proof cheap for the cryptographer is the work that makes memory safety free for everyone.

---

Stay with the ML-DSA signing routine, because it shows what "discharged against the code" affords. The published EUF-CMA proof is about an abstract scheme. Between that scheme and the binary on the device sit the implementation choices and the compiler: a branch the source did not have, a table lookup indexed by a secret byte, a lowering pass that turns a constant-time select into a conditional jump. Any one of those ***reopens*** the channel the paper proof *closed*. The only guarantee that matters is the one that holds for the code that runs. This is a level that's much deeper than proofs for an abstract code description that might be compromised in the process of lowering.

This is not hypothetical. The Symbolic Software audit of the [hax pipeline](https://symbolic.software/blog/2026-04-07-cryspen-hax/), which translates a Rust subset into an external proof assistant, found that it verified panic freedom and functional correctness of an ML-DSA implementation while missing the zero-knowledge property, because the rejection-sampling loop extracted to a proof-inert form. The verification ran against a model the extraction had quietly severed from the security-critical behavior, and the gap is the kind that recovers the secret key from roughly a thousand signature passes. A proof about a model is only as good as its integrity to the computation substrate, and an extraction step in standard proof mechanics is where that integrity is lost.

### Maintaining the Chain

The signing routine shown below is spawned from ordinary Clef. The constant-time primitives transcribe directly from the scrutinized references, because the operations are the same branchless bit manipulations, and control flow never depends on a secret.

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

The rejection-sampling loop is the one hax could not verify, and it is the case our design is built to handle without the export disconnect. Our process runs until enough candidates are accepted, acceptance depends only on the public candidate value, and the write index advances branchlessly so the loop's control flow carries no secret. That branchless structure is exactly what hax *lost*: its extraction turned the loop into a proof-inert form, so the verifier signed off without ever seeing whether a secret reached a branch. We take the opposite path and never extract the loop into a separate model. The branchless control flow stays on the graph, where it can be re-checked on the lowered code, so that the property confirmed is a property of the instructions in the compute graph.

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

In the design, the compiler is the one that establishes the security property, so the developer never writes the proof by hand to get its benefits. The framework reads the relational obligation off the routine's structure, builds the proof terms automatically, and re-checks them against the emitted binary. That posture, where the compiler carries the burden of proof and the developer writes the solution, is the one [Fearless Concurrency Gets Real](/blog/fearless-concurrency-gets-real/) develops for memory and liveness, here carried up to the most demanding tier the framework reaches.

While we have a strong belief in providing maximum reach for the developer, we also seek out every opportunity available to provide sensible automation that helps streamline the design-time and build-time experience.

## Where the difference surfaces

What makes this framework different is not the usual verification pitch. The usual pitch is that proof is a cost the developer can be talked into paying: write the annotations, accept the slowdown, collect the warrant. We took the opposite position. A large share of the proof work is automated as structural support the compiler derives from the program already written, and that automation is an accelerant rather than a tax.

It is meant to move development forward, because the obligations will be discharged as the program is elaborated, surfaced in the editor at the point of writing. We want the most common proof terms to be a design-time element in order to support correctness, a property that identifies issues immediately instead of after a separate verification pass, a failed audit, or worse, a production incident. The feedback loop that formal methods usually defer to the far end of a project sits at the moment of authorship instead. Our approach should also lighten the annotation burden, because the structural facts that higher obligations consume, the dimensions and lifetimes and escape behavior and grade, are inferred from the code's own structure rather than declared by hand. The annotations a conventional verified-programming workflow makes the developer write are, in the cases our algebra covers, derived by the compiler instead of requiring the developer to hand-roll it directly.

That difference carries all the way to the top tier. The relational, probabilistic guarantee there is non-trivial, but we expect our design to be supportive of those furthest reaches: the local facts those "higher" proof tiers consume were automated for free, and the discharge and the artifact re-check are carried by the compiler. The developer's contribution is the domain encoding that had to be written anyway. The automated proof elements at tiers 1, 2, and 3 do the structural work, which is what would let a domain expert reach a relational guarantee without becoming a proof engineer, and it is the same machinery that lets a generalist warrant memory safety and dimensional correctness without writing a single annotation.

## What spans, and what stays local

Most of what the compiler will check is "local" to the code being expressed. The dimension of a value, the escape class of an allocation, the width of an integer: each is a property of a single point in the program, and each lands in a narrow decidable theory, integer-linear arithmetic or bit-vectors, the kind a solver settles fast enough to surface in the editor at the point of writing. [The Decidability Sweet Spot](/docs/internals/verification/decidability-sweet-spot/) is the account of why staying inside that fragment is what makes verification in our framework both seamless and computationally cheap, and [Formal Verification as Compilation Byproduct](/docs/design/categorical-foundations/formal-verification-compilation-byproduct/) is the wider tour of what the compiler is meant to hand the developer 'for free'.

By contrast, some properties are not local. Whether a set of actors can deadlock is a fact about how the whole program fits together, and no single operation can see the cycle, because the cycle is a relationship among many. The framework is designed to handle this without leaving the decidable fragment. As [Deadlock Freedom as an Obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/) lays out, each synchronous call carries only its own wait-for edge as a local fact, the acyclicity obligation rides on the enclosing region, gathers every edge, and proves a ranking exists. The rank is an integer assigned to each actor so that every wait-for edge strictly increases it, which exists exactly when the relation is acyclic. That constraint, \(r(u) < r(v)\) for every edge, is integer-linear arithmetic, the same kind of obligation as an interval check, so it lowers into the SMT dialect where the solver would discharge it like any other.

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

A signer that fans out to leaf workers has a rank: the workers sit above the signer in the ordering and never call back, so the solver would find \(r\) immediately. The priority a developer would otherwise hand-write is that rank, and the common case never asks for it. No actor carries the whole-program fact; each carries its own wait edge, and the region's obligation gathers them.

This is the distinction that bears some expansion here. Deadlock-freedom is a spanning concern: it reaches across the whole program and consumes local facts as premises. And it stays inside the decidable fragment. 

> 'Spanning' is not the same as 'exotic' in that conceptual frame. 

The semantic graph preserves information and stays inside theories a solver decides in microseconds. The local and the spanning compose through one structure, local facts on the operations and the spanning obligation on the region that encloses them, which is the cell-complex discipline [The Compilation Sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/) makes precise: local sections checked on the edges, composing into a global section over the whole. This reads as particularly nerdy, but the net effect is that the compiler front end is doing a lot more work than lexing and parsing code into an abstract syntax tree. We believe it's a unique benefit that our framework provides, and calling out the automation is not only a *flex* of sorts but also our way of submitting that we're making a novel contribution to the field with this effort.

## Where the target set widens

The narrow theories the framework targets are not fixed once and forgotten. They widen as additional portions of the Native Type Universe are engaged, and they widen within the same decidable discipline. Integer-linear arithmetic is the workhorse. When negative and fractional types enter, carrying dimensional exponents that are rational rather than integer, the obligations reach down into the reals, and the target set admits quantifier-free linear real arithmetic beside the integer fragment. That widening is the subject of the [negative and fractional types pre-print on arXiv](https://arxiv.org/abs/2606.04352), and the SpeakEZ companion post [Getting Real with Fidelity Framework](https://speakez.tech/blog/getting-real-with-fidelity-framework/) takes a more conversational path from the integers into the rationals. The reason for combining dimensions is that addition stays linear no matter how many reciprocals are stacked. In proof theoretic terms, the fragment stays linear, quantifier-free, decidable in polynomial time, with principal solutions. In an absolute sense the solver tier got wider. However it did not get *heavier* in kind. The same structural integrity reaches into the rationals through those types, a continuity the [Fixed-Point Scaffolding pre-print](https://arxiv.org/abs/2606.02854) develops as it carries the dual structure of negative and fractional values through lowering. Developers get considerable new tools for building algorithms that ride the normal lowering and proof machinery.

So neither 'spanning' nor reaching into *the reals* steps away from the proof terms the compiler is designed to discharge automatically. This commitment to staying inside the decidable fragment is something we arrived at on our own, but it is not without precedent. Dafny reached it first, by a different road. 

Though Dafny was an influence on the development of the F\* language, Dafny itself has no dependent types, no kernel, no interactive proof fallback: every obligation is discharged directly by an SMT solver, and when the solver stalls the developer does not drop into a proof assistant, they insert an intermediate assertion that gives the solver a stepping stone. We arrived at a substantially similar decidability-first commitment, by the abelian-group route that our dimensional type algebra allowed. Our system reaching a similar place that Dafny established by a different discipline is the strongest evidence to us that we're on the right track. The benefit is precise: on a recent verified-coding benchmark, an SMT-backed system in Dafny's lineage succeeds where a dependent-typed one in the proof-assistant lineage does not, because the gradient tracks how much of the discharge a decision procedure can carry.

The intermediate assertion that Dafny uses is the right primitive, and we have a similar mechanism that has type-elemental underpinnings that matter. Dafny's `assert` is a cut within a single verification stratum: it splits one hard obligation into two the solver can each discharge, and it is placed by the developer's intuition about where the solver stalls. By contrast, our tier architecture registers that same cut as the degenerate case of a typed transition, a [mode shift](/docs/internals/verification/mode-shifts/) whose source and target tier coincide, carrying a splitting obligation but no change in verification strength. The genuine transitions are the ones that do change strength, between the decidable tiers and the tier *above* them. Those are not placed by intuition; Baker identifies them from a structural signal in the Program Semantic Graph. Dafny has one flat stratum and a manual cut. We have a stack of informed strata with typed shifts between them, and the manual cut is absorbed as the identity shift inside a stratum.

Dafny also marks, by negative example, exactly where the SMT discipline stops. A Dafny program is verified and then handed to an unverified compiler that emits to a managed runtime, which decides memory behavior *the proof **never** witnessed*. The strongest SMT-backed verifier still severs the proof from the artifact at the compiler boundary. Closing that separation, carrying the established facts through lowering and re-checking them on the binary, is the concern we address through the rest of this post. It is not a refinement of Dafny, but a different commitment entirely.

## The relational concern

A 'relational' proof property, such as one that shows a signing routine leaks *nothing* about the secret, is about a pair of executions and the relationship between them, with a coupling between the two runs' random choices as its content. It is not a property of any *single* point in the program, so it is not "local" in the sense we've been using that term here. This is another point where precision matters, because ***not*-local** is not disqualifying on its own. Deadlock-freedom is not local either, and yet it stays in the decidable tier.

> The relational concern is different on a second axis. 

It is relational *and **probabilistic***, and the probabilistic half is the one that does the disqualifying. The relational structure, a pair of executions, is not itself the problem; deadlock-freedom relates a whole set of actors and stays decidable. The problem is the coupling between two runs' *distributions*. A quantifier-free linear theory reasons about one assignment of values at a time, so it has no vocabulary for a distribution, let alone a coupling that relates two of them. Establishing such a judgment is the construction of a derivation in a probabilistic relational proof system, proof search, which is not a decision procedure and carries no polynomial bound. The original designs for the solver tier were scoped, on purpose, to the quantifier-free linear fragment in order to stay clear of that open-ended search. The probabilistic component is exactly the class that scoping was designed to keep *out*.

The difference in kind is also why the implementation and expected audience is narrow. A specialist reaches for a relational guarantee because the programmatic use case demands it. The specialist states the property in the domain's own terms, an EUF-CMA game, a constant-time requirement, and the design hands the work of building the derivation that discharges it to the compiler. At this level of engineering, stating the property and building its proof are separate tasks, and the framework is meant to take the second one off the specialist's plate.

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

That lemma reaches the edge and stops. It can equate the values the two runs return, which holds only when sampling is deterministic, and the real routine is not. The security property is about the *distributions* of the two runs, and the coupling between their random choices that makes those distributions equal, and a refinement type has nowhere to put a distribution or a coupling. That is the wall the cryptography world hits with refinement types, and the reason it reaches instead for probabilistic relational Hoare logic and tools like EasyCrypt. The Clef surface keeps F\*'s statement shape and adds the two clauses that cross the wall.

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

The premises cross the tier boundary as typed shifts, and the developer deals with none of them. There is no `shift` keyword in Clef. The shift is internal compiler machinery, and the plan reads like this: Baker would read the saturated graph, recognize from the loop's branchless structure that a Tier 2 fact admits a Tier 3 termination claim, and emit the shift that carries the obligation. The termination fact climbs from Tier 2 to Tier 3 that way, and the relational judgment sits above both on a second shift the same elaboration emits, each one carrying the obligation that the lower tier's structure admits the higher tier's claim. What the developer sees through all of it is the `sampleUniform` loop from before, unchanged.

To emphasize the distinction from prior art, a Dafny `assert` cuts within one stratum and is placed by the developer at design-time, where their experience shows the solver will stall. The Clef shift crosses between strata, and the design places it from a structural signal in the code rather than by hand. The same cut that is manual in Dafny becomes derived *and **typed*** here. The Tier 3 termination it discharges is itself a library-carried lemma, proved once and instantiated against the loop's concrete bound, so the loop does not re-derive it. That lemma is library code, written by whoever builds the cryptographic lemma library, not by the engineer writing the signing routine.

```fsharp
// from a lemma library.
[<Tier3.Lemma>]
let rejectionTerminates<'w when 'w : Numeric> (bound: 'w) (n: int) =
    requires (bound > zero<'w>)
    ensures  (terminatesAlmostSurely (rejectionLoop bound n))
    ensures  (expectedIterations (rejectionLoop bound n)
              = n * widthCardinality<'w> / toRational bound)
    discharge_via NegativeBinomial
```

So the relational concern is not a fourth rung on the same ladder as the local tiers, and it is not pushed out by spanning or by the reals. It is a different kind of obligation on a different axis: a whole-computation property in a relational, probabilistic logic the decidable theories do not express. It does rely on and leverage the benefit of lower-tier facts, the constant-time fact, the termination fact, the bounds, as premises, and binds them into one statement about the program's whole behavior. The local guarantees are what the higher discipline can be built from. The spanning relation is what ties them into a behavioral whole, where that level of proof machinery is needed. 

We also contemplate some automation would emerge at the probabilistic/relational 'Tier 4' as well. Where the 'lower' tiers hand their obligation to an SMT solver and carry the verdict directly in the graph, this could be checked against a probabilistic rule library proved once in an external proof assistant, with the solver still discharging the arithmetic leaves at design and build time. The trusted base moves exactly once across the whole scaffold: the SMT solver alone carries the lower tiers, and using that 'established art' the proof assistant's kernel can enter to provide its warrants per the demands of the project.

## Bracketing pRHL with the lowering machinery

Because the relational concern is not in the fragment the lowering pipeline carries directly, it lives where the framework's structural work has always lived, and it is designed to be exercised at two checkpoints with the entire lowering between them.

The pipeline itself carries the local, decidable facts. MLIR has one proof dialect, the SMT dialect, and it carries exactly the quantifier-free fragment targeted, including the spanning-but-decidable obligations like acyclicity that ride on their enclosing region. This is the division the [Fixed-Point Scaffolding pre-print](https://arxiv.org/abs/2606.02854) draws when it describes the framework as having "New Jersey wheels under an MIT frame": MLIR is the structure-carrying vehicle, while the formal structure is settled in our Program Semantic Graph during design-time analysis. The probabilistic relational conditional is not in what that vehicle carries, so it is established separately, and the developer will be able to exercise that option at two distinct phases of compilation.

In our current designs, the first checkpoint is the end of the front end. After elaboration produces the saturated graph and before lowering begins, the relational property would be established on the source model, the whole program as written, with the local facts it consumes already present on the graph. This is part of the design-time experience. The transition from the decidable tiers up to the relational concern is the kind of typed coercion our [Mode Shifts](/docs/internals/verification/mode-shifts/) proposal sketches: an obligation that names the lower-tier facts it consumes and the backend it dispatches to, carried as graph structure so the editor could report its status, established or in question or pending, as a live property of the program, even where establishing it is heavier than a solver query. That proposal is design exploration rather than shipping infrastructure, and it is the shape the tier-to-tier transition is being designed to take.

Then the lowering runs. The local facts travel through it in the SMT dialect, re-checked at each pass because a transformation could break them. However, the probabilistic relational conditional does not travel. There is no vehicle for it, and nothing for it to do per pass, because the spanning relation it established does not change as the code is transformed. It waits at the far end.

The second checkpoint is after lowering, on the emitted artifact. When the back end has emitted the binary together with its verification certificates, the relational discipline is designed to preserve the option to engage a second time, now with the compiled artifact. This is the final checkpoint, because it closes the gap between "the property holds in the code I designed" and "the property holds in the program that shipped." The mechanism is the one [From Proofs to Silicon](/docs/internals/verification/proofs-to-silicon/) develops: at `clef build --release` the graph freezes, a global verification run produces a mathematical witness, and the back end hashes the binary alongside that witness into a release certificate, embedded in a `.proofcert` file or a dedicated ELF section (or perhaps both). That certificate carries the tier label of the obligation it records, so a reconciliation step can check that the shipped binary is a faithful realization of the proven structure. The same re-check is designed to run in two contexts: as the compiler's own final stage, and as an external re-verification by an auditor or certification lab over the shipped artifact and its certificates, needing no access to the compiler's internals because everything required is in the emitted evidence.

What an auditor accepts depends on the lower-tier premises within the audit artifact. A certification lab does not trust a vendor's assertion that a property holds; the lab re-runs the proof in its own kernel. This is a bit of "proof term insider" detail, but it goes to how the verification holds under adversarial scrutiny. If the dimensional, lifetime, and constant-time premises entered *as **axioms***, the re-check would confirm a *conditional*, **not a fact**, and `Print Assumptions` on the final theorem would expose the axioms a sophisticated lab reads as a proof resting on unverified machinery. The commitment is that the premises enter as ***proved*** *lemmas* instead: the linear-arithmetic premises are re-proved by the proof assistant's own tactics, and the bit-vector premises are reconstructed from the solver's certificate so the kernel re-verifies each step, leaving the solver outside the trusted base. The lab re-runs the proof, inspects the assumptions, and finds only the assistant's base axioms and standard logic underneath.

The second checkpoint is also where a property that exists only at the artifact gets established. A timing side channel, a secret-dependent branch the source did not have but a compilation stage introduced, has no source-level fact to carry it, because at the source level there was nothing to carry. The fact exists only in the machine code, which is where the second checkpoint would have its reach: a check on the emitted code, that no secret-dependent branch or memory access survived lowering, would feed into the relational confirmation. The first checkpoint could not see this. The second is designed to exercise this critical verification, as part of the coherence of this unique design, again, as the case warrants.

## Why 'TCB size' matters

Our approach is to do everything we can to keep the set of components that must be correct for the relational guarantee to hold as small as possible, and the two-checkpoint shape is why. The guarantee rests on the proof kernel that establishes and re-confirms the relational conditional, plus the machinery that carries the local facts through the lowering and emits them as certificates. It does not rest on the relational correctness of the lowering passes, because no relational reasoning passes through them. A global section that consistent annotations exist at every stage is necessary but not sufficient on its own; sufficiency comes from the reconciliation step confirming the binary realizes that section. If a lowering pass misbehaves, the local facts it was supposed to preserve fail their re-check, and the relational conditional re-confirmed at the second checkpoint does not hold over the broken facts. The lowering does not have to be trusted to be relationally correct. The design goal of this framework is to halt as early as possible when it is not, and offer supportive information to get building the application back on track as quickly as possible.

## The shape of things to come

The design has the framework span the program routinely and stay in the decidable fragment while it does, with deadlock-freedom as the proof of concept. The fragment widens into the reals as fractional and negative types require, and stays decidable. The one concern that exits is the relational, probabilistic one, and it exits on the probabilistic axis, because that is a different kind of logic, not because it spans and not because it reaches the reals. It would live at the proof kernel, established once at the end of the front end on the source model and re-established once after lowering on the emitted artifact, by the compiler as its final stage and re-runnable by anyone over the shipped binary. The lowering runs between the two, carrying the local facts the conditional consumes and nothing probabilistic itself, because the only proof vehicle the pipeline is given is scoped to exactly the decidable fragment the framework targets.

The relational guarantee is meant to be a property of the binary rather than a model of it. The failures that only appear during compilation would be caught where the information to catch them finally exists. The compiler stays small and the trusted base stays small, because the guarantee is re-confirmed on the output rather than carried through the machinery. And the developer would not write the proof by hand to collect any of it.

For the specialist, the signing routine is in a familiar domain, and the compiler is the one that would derive the relational obligation, build the proof terms, and re-check them on the binary. The coupling proof that a high-assurance audit would otherwise demand by hand is the one piece the specialist is meant to be spared. For the generalist who rarely reaches this narrow passage, it is still worth seeing how the framework reasons about its furthest reach, because that same design is what shapes the everyday surface, with the lower proof layers generated automatically underneath.

A word on Rocq, which we expand on here for the first time. Rocq, the kernel formerly known as Coq, is where our "Tier 4" proof obligation comes to rest, and that is a recent decision in the framework's development. We chose it for the reason a certification lab would: it is the kernel under CompCert and the Verified Software Toolchain, a standard bearer that high-assurance engineering, research, and academic work already lean on, so a relational guarantee discharged in Rocq is one an auditor can re-check in a tool they understand rather than one they would have to learn and trust from a cold start. The other reason is closer to our own design. Rocq's foundations sit in sympathy with where our semantic structure already lives, the constructive, type-theoretic ground our dimensional and grade algebra is built on, so the probabalistic tier reads as a continuation of the framework rather than a foreign appendage bolted to its top. The title of this post is a small joke on the name, while the choice behind it is quite straight-forward: when the hard case demands a kernel, it should be one the field already relies on.


The deeper treatments live in the framework's design documentation and pre-prints, collected in [A Deeper Dive]({{< ref "/docs/guides/_index.md" >}}): the [decidability sweet spot](/docs/internals/verification/decidability-sweet-spot/), the [deadlock freedom as an obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/), the [compilation sheaf](/docs/design/categorical-foundations/the-compilation-sheaf/), the [mode-shifts proposal](/docs/internals/verification/mode-shifts/), and the [artifact certificate](/docs/internals/verification/proofs-to-silicon/).

## References

[1] K. R. M. Leino, "Dafny: An automatic program verifier for functional correctness," in *Logic for Programming, Artificial Intelligence, and Reasoning (LPAR-16)*, pp. 348-370, Springer, 2010.

[2] G. Barthe, B. Grégoire, and S. Zanella-Béguelin, "Formal certification of code-based cryptographic proofs," in *Proceedings of the 36th ACM SIGPLAN-SIGACT Symposium on Principles of Programming Languages*, pp. 90-101, 2009.

[3] Cryspen and Symbolic Software, "Verifying ML-DSA with hax: panic freedom, functional correctness, and the limits of extraction," [symbolic.software](https://symbolic.software/blog/2026-04-07-cryspen-hax/), 2026.

[4] X. Leroy, "Formal verification of a realistic compiler," *Communications of the ACM*, vol. 52, no. 7, pp. 107-115, 2009.

[5] P. Wadler, "Theorems for free!" in *Proceedings of the Fourth International Conference on Functional Programming Languages and Computer Architecture*, pp. 347-359, ACM, 1989.

[6] H. Haynes, "Fixed-Point Scaffolding in the Clef Programming Language," [arXiv:2606.02854](https://arxiv.org/abs/2606.02854), 2026.

[7] H. Haynes, "Negative and Fractional Types in the Fidelity Framework," [arXiv:2606.04352](https://arxiv.org/abs/2606.04352), 2026.
