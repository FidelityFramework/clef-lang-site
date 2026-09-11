---
title: "Proof Preservation Across Actors and Workflows"
linkTitle: "Actor and Workflow Proofs"
description: "Preserving actor, arithmetic, and recovery contracts through JavaScript suspension, distributed replies, and durable workflow execution."
date: 2026-09-11
authors: ["Houston Haynes"]
tags: ["JavaScript", "Concurrency", "Verification", "Architecture"]
weight: 50
---

This design starts with [arithmetic-construction obligations](/spec/draft/numeric-selection/#105-capacity-error-and-decomposition-obligations) that can be established within JavaScript's numerical model. It then connects them to the [JavaScript boundary contract](/spec/draft/javascript-boundary/) and [scheduler contract](/spec/draft/scheduler-contract/) so suspension, actor delivery, and durable recovery preserve the arithmetic premises. The mathematical arguments below are proof sketches and cited results, not machine-checked certificates for a completed Composer JSIR implementation.

[Carrying Proofs into JavaScript](/blog/carrying-proofs-into-javascript/) develops the narrative. The existing [boundary map](../cloudflare-agents-and-the-boundary-map/) addresses incoming values; [ledger lowering](../the-ledger-lowering/) addresses retained observations and recovery. This page supplies the numerical arguments and the join and continuation obligations that preserve their premises.

## Numerical guarantees within JavaScript

The following arguments concern admitted primitive numeric values and explicit operation graphs. Host arithmetic semantics are premises in the TCB; generated code must implement the analyzed operations without changing rounding points or introducing coercions. Availability and cost of a construction must be established for the configured JavaScript target. These arguments do not assume access to a native FPU control register, an FMA instruction, or FPGA quire hardware from ordinary JavaScript.

### Exact integer ranges in a Number carrier

**Claim.** A graph of integer addition, subtraction, and multiplication produces exact integer values when its admitted inputs and every mathematical intermediate have magnitude at most \(2^{53}\), and lowering preserves those operations. This concerns integer values; any observable signed-zero policy is an additional requirement.

**Argument.** Each such integer is representable in binary64. A correctly rounded operation whose exact result is representable returns that result. Induction over the operation graph establishes exactness. Division needs a separate divisibility and definedness argument. Number bitwise operations must not be substituted as general integer operations without justifying their narrower semantics. [ECMAScript numeric semantics](https://tc39.es/ecma262/multipage/ecmascript-data-types-and-values.html#sec-ecmascript-language-types-number-type).

For an arbitrary tree of disjoint partial sums, the sufficient bound \(\sum_i |x_i|\le 2^{53}\) covers every subtree. The triangle inequality bounds each subtotal by that same quantity; induction then establishes exactness for every permitted tree. This is stronger than a proof for one sequential order and can permit dynamic partitioning without changing the result.

Admission must establish that input conversion preserved the intended integer. The endpoint \(2^{53}\) is exactly representable, but an adjacent larger integer can round to it. A range check performed only after a lossy conversion cannot establish the original value. This representability argument does not redefine JavaScript's safe-integer predicate.

### Fixed-point exactness and quantization

**Claim.** For carriers \(X,Y\) at scales \(2^{-f},2^{-g}\), the exact product is \((XY)2^{-(f+g)}\). Rescaling to \(2^{-h}\), where \(d=f+g-h>0\), is exact if \(2^d\) divides \(XY\), provided the carrier operations and scale realization themselves remain exact.

**Argument.** The destination carrier is \(XY/2^d\). Divisibility makes it an integer, so no fractional carrier information is discarded. Otherwise nearest rounding to output spacing \(\Delta=2^{-h}\) gives

\[
|\widehat{xy}-xy|\le\frac{\Delta}{2}.
\]

This follows from choosing a nearest point on a uniform grid, with no clipping and a specified tie rule. It is a local quantization bound, not an end-to-end error bound. The emitted rounding sequence needs its own preservation argument; a convenient host rounding function must not be assumed to have the desired tie rule. [Rounding §6.1](/spec/draft/rounding/#61-fixed-point-scale-and-error) governs the construction.

### Error-free residuals using ordinary floating-point operations

**Claim.** TwoSum returns components \(h,\ell\) satisfying \(h+\ell=a+b\) as a real-value equality for finite represented operands, round-to-nearest/ties-to-even, gradual underflow, and no intermediate overflow.

**Justification.** This is the error-free transformation in Ogita, Rump, and Oishi's [Algorithm 3.1 and Theorem 3.4](https://ogilab.w.waseda.jp/ogita/math/doc/2005_OgRuOi.pdf). The six additions/subtractions in the [functional construction](/docs/internals/numerics/arithmetic-construction-and-placement/#functional-residual-arithmetic) are expressible with primitive JavaScript Number arithmetic. The compiler must establish the domain and preserve the sequence. A two-component result establishes one exact sum, not an unlimited accumulator or an associative merge algorithm.

### A bound for a rounded reduction tree

**Claim.** Let represented finite inputs \(x_i\) be reduced by a binary addition tree of maximum leaf depth \(d\). Suppose each addition satisfies the relative-error model \(\operatorname{fl}(a+b)=(a+b)(1+\delta)\), with \(|\delta|\le u=2^{-53}\), and \(du<1\). Then, for exact sum \(s\) and computed sum \(\widehat{s}\),

\[
|\widehat{s}-s|\le\gamma_d\sum_i|x_i|,
\qquad \gamma_d=\frac{du}{1-du}.
\]

**Argument.** Expand the tree symbolically. Each input is multiplied by at most \(d\) factors \((1+\delta)\) along its path to the root. The deviation of each product from one is bounded by \(\gamma_d\); the triangle inequality gives the displayed absolute bound. The premise excludes overflow and operations for which underflow invalidates the relative-error model; a broader domain needs an additional absolute-error treatment. Cancellation can make relative error with respect to \(s\) large even when this absolute bound holds.

Fixing the tree preserves its rounded result under changing callback completion order, provided the same represented inputs reach the same operations. Allowing another tree requires its own bound and does not establish bitwise equality. Neither claim includes errors already present in the inputs or the numerical method.

### Exact binary64-term accumulation and merge

**Claim.** Every finite binary64 value is an integer multiple of \(2^{-1074}\). Therefore an exact sum of represented inputs can be constructed by decoding each \(x_i\) to an integer coefficient \(k_i\), accumulating those coefficients exactly, and performing one justified final rounding:

\[
x_i=k_i2^{-1074},\qquad
K=\sum_i k_i,\qquad
r=\operatorname{RN}_{64}(K2^{-1074}).
\]

An internal BigInt-based realization or a proved multiword implementation is a possible carrier, subject to platform policy and resource bounds. This introduces no source-level numeric wrapper. For at most \(N\) finite terms, a conservative coefficient bound is

\[
|K_{\mathrm{partial}}|
\le\sum_i|k_i|
\le N(2^{53}-1)2^{2045}.
\]

The last factor comes from the largest finite binary64 value expressed in units of \(2^{-1074}\). It bounds every disjoint partial sum, including unfavorable sign groupings. A bounded integer realization must cover that range; an extensible carrier still needs an allocation and execution-budget policy.

**Merge argument.** For state interpretation \(D(K)=K2^{-1074}\), exact integer addition gives \(D(K_A+K_B)=D(K_A)+D(K_B)\). Associativity and commutativity follow at the interpreted-value level. Disjoint partitions covering the same terms therefore produce the same exact sum; a deterministic finalization rule gives the same output. This is a mathematical construction argument. The coefficient decoder, carrier operations, merge, and finalizer each require implementation evidence.

Converting the wide coefficient to Number before applying the scale is not a valid general finalizer: it can round or overflow prematurely. Finalization must use the exact state to determine the output exponent, significand, and rounding decision, including subnormal, zero, and overflow rules. A finite-result contract additionally requires final coverage. NaNs, infinities, and observable signed zero require explicit policies outside the finite real-value argument.

This construction sums represented terms exactly. An exact dot product needs exact product formation and a correspondingly finer accumulator scale; substituting rounded worker products changes the contract. Sending rounded subtotals instead of exact partial states also changes it.

### What the compiler and the host contribute

The mathematical laws above are independent of callback scheduling. The host supplies the declared primitive arithmetic and execution semantics. The proposed compiler identifies eligible computation regions, establishes their numerical premises, realizes the chosen construction, and preserves its operation-level argument through lowering. It must also preserve represented inputs and partial states across admitted serialization and recovery paths.

Automatic proof generation is part of that proposed compilation mechanism. For recognized constructions, analysis derives the required capacity, rounding, error, and merge obligations from the computation graph and numeric contract. Construction theorems supply reusable proof structure; inferred program facts and declared boundary conditions instantiate their premises. The configured verification mechanisms discharge supported obligations, and lowering must preserve the resulting evidence. Developers need not write a proof annotation for each arithmetic operation or reconstruct the construction theorem. Domain-specific accuracy requirements and unavailable input facts remain explicit contracts; the compiler cannot infer arbitrary application intent. An unresolved obligation remains unresolved, even when its generation is automatic.

The acceptance evidence must separate these mathematical results from completed implementation proofs. Existing integer analysis and codec checks are foundations; automatic real-error analysis, this exact-accumulator realization, and their artifact-bound JSIR proofs are not claimed implemented here. The actor and workflow obligations below maintain the terms, multiplicities, and state on which the numerical guarantees depend.

## Execution domains and authority

A Cloudflare Worker instance can process concurrent requests through one JavaScript event loop. Awaiting an asynchronous operation allows other requests to execute; successive requests need not reach the same instance. Single-threaded callback execution therefore does not make an entire asynchronous invocation atomic. [Cloudflare execution model](https://developers.cloudflare.com/workers/reference/how-workers-works/).

The scope of each obligation must be explicit:

| Domain | Required local evidence | Additional boundary evidence |
|---|---|---|
| Synchronous turn | Exclusive actor-state access; operation semantics; termination or budget handling | Host callback and reentrancy behavior |
| Suspension and resumption | Captured values, state dependencies, continuation identity, readiness | Completion routing, cancellation, stale replies, host scheduling assumptions |
| Cross-isolate work | Valid local actor execution | Logical job identity, payload fidelity, delivery policy, no shared-address assumption |
| Durable workflow | Valid reconstructed control and data state | Persisted observations, accepted attempts, code compatibility, effect recovery |

Olivier actors perform work. Prospero describes supervision, lifecycle, and orchestration responsibilities; Ariel names the dispatch contract across targets. On Cloudflare, the existing platform supplies execution and dispatch. This design introduces no separate Ariel scheduler, dispatch loop, or worker pool there. Generated code uses the available host APIs and preserves application-level actor and continuation invariants within their execution model.

Where supported, platform facilities can implement application-defined orchestration, including dependency sequencing, result collection, and declared retry or recovery policies. That mapping must identify the policies the host can express and the guarantees it supplies; unsupported requirements remain capability or assumption findings. A local JavaScript closure and a durable continuation record are different representations requiring separate lowering evidence.

Cloudflare Durable Objects add identity and storage facilities. Their input/output gates have specific semantics; they do not make arbitrary sequences containing remote awaits transactional. Generated actor bindings need the applicable gate and reentrancy contract, not an inference from the phrase “single-threaded.” [Durable Object rules](https://developers.cloudflare.com/durable-objects/best-practices/rules-of-durable-objects/).

## Facts that survive suspension

The general JavaScript obligations below are specialized to each supported host realization. On Cloudflare, proof scope is the admitted generated application patterns and their declared foreign boundaries, using existing platform facilities. It is not verification of arbitrary JavaScript, workerd, or Cloudflare's distributed infrastructure.

### Host premises and application invariants

The Cloudflare realization records host services in its trusted computing base (TCB). A proof conditional on a host contract establishes correct application use of that contract; it does not verify the service implementation. The compiler should reuse sufficient host guarantees rather than generate redundant identity, dispatch, or coordination machinery.

| Property | Platform contribution admitted as a premise | Remaining application or lowering obligation |
|---|---|---|
| Object identity and delivery | Durable Object addressing and the selected call's completion semantics | Correct target selection and correspondence to the current job, input version, and partition |
| State access | Host execution model and documented input/output gate behavior | Correct use of those facilities; invariants across permitted non-storage awaits and callbacks |
| Consistent acceptance | The selected storage operation's persistence and atomicity contract | The transaction or reconstruction protocol keeps accepted identities and contributions consistent |
| Durable resumption | Workflow step-state retention and recovery semantics | Sufficient retained observations, stable logical step mapping, compatible code, and preserved arithmetic |
| Scheduling and progress | Platform dispatch, budgets, and service behavior | An explicit assumption manifest and application failure policy; no inferred global liveness guarantee |

Durable Object identity follows the [host model](https://developers.cloudflare.com/durable-objects/concepts/what-are-durable-objects/), including its [replacement and uniqueness conditions](https://developers.cloudflare.com/durable-objects/platform/known-issues/). A stable object ID is not automatically a Clef actor-incarnation identifier or a logical job version. The binding must state whether host reactivation is transparent to the application's lifecycle, and introduce additional epochs only where that lifecycle contract requires them.

Evidence should distinguish properties discharged by application analysis, conditions established by generated checks, and guarantees assumed from the configured host. Supported host APIs and operation contracts constrain which realizations are admissible. If a required property lies outside that scope, it remains unresolved or unsupported; the existence of a broader JavaScript implementation strategy does not establish its availability on Cloudflare.

### Continuation validity

At each suspension, analysis needs the continuation's live captures, actor and job identities, pending obligations, and the mutable dependencies of facts used after resumption. An immutable scalar capture can retain its established range. An immutable reference binding does not freeze the referenced object. A fact derived from storage that may change requires revalidation, an established version check, or a protocol preventing the change.

A correct resumption path establishes:

1. The incoming value satisfies its boundary shape, numeric, and representation conditions.
2. Its logical operation and any incarnation or epoch required by the declared lifecycle match the pending continuation, using host correspondence guarantees where sufficient.
3. The operation has not already completed, been canceled, or been superseded under the declared policy.
4. The required actor-state exclusivity and remaining premises hold when the turn begins.

These conditions concern generated control flow and state transitions as well as types. Unknown foreign effects remain explicit. Synchronous reentrancy through a host callback also needs a contract; event-loop serialization alone is insufficient.

## Partition and join contract

A dynamically created job needs a logical input version and a partition rule. Runtime task count is compatible with a static argument when each generated partition satisfies an invariant. For an all-results join, the partition protocol must establish a closed expected set, either directly or through a justified completion/accounting protocol. A counter reaching zero is meaningful only if task creation and completion update it under that protocol.

Each logical partition must identify its covered inputs, admitted computation, and required output contract. A record containing a numeric value of the right type is insufficient: it may belong to another snapshot, repeat an accepted partition, or omit some inputs. Where authentic origin is required, an identifier alone is not authentication; the transport or verification contract must establish that premise.

Let \(P\) be the finite expected partition set, \(A\) the accepted set, and \(v_p\) the agreed represented contribution for partition \(p\). For an exact additive construction with accumulator interpretation \(D\), a useful join invariant is

\[
A\subseteq P,\qquad
D(q)=\sum_{p\in A}v_p.
\]

This uses a zero initial state. A source-specified nonzero initial value must contribute once to the complete reduction. Acceptance adds an eligible partition to \(A\) and incorporates its contribution in \(q\) as one logical state transition. A duplicate leaves both unchanged; a conflicting result for an accepted identity follows an explicit error or reconciliation policy. For deterministic jobs, silently accepting whichever conflicting value arrives first would invalidate the claim.

Completion for this join requires \(A=P\). Quorum, first-success, streaming, and partial-result joins require different contracts; they must not inherit an all-results theorem. The invariant establishes multiplicity and correspondence, not eventual arrival.

For crash recovery, accepted identities and accumulator state must be recoverable consistently. A supported atomic storage update can retain both; an alternative can retain accepted contributions and reconstruct the accumulator under established arithmetic semantics. A volatile duplicate set is insufficient when recovery can forget an already applied contribution. Deduplication retention must cover the retry/replay horizon, or the protocol must reject expired job epochs.

## Arithmetic and transport fidelity

The numerical contract determines whether the collector can use a fixed input-indexed tree, arbitrary admitted merges, or an explicitly order-sensitive operation. Exact and rounded constructions are distinguished in [Arithmetic Construction and Placement](/docs/internals/numerics/arithmetic-construction-and-placement/). Associativity and commutativity do not establish idempotency: replaying a contribution changes an ordinary sum even when its accumulator is exact.

Partition boundaries also matter. Replacing individual terms with rounded worker subtotals can change a fixed-tree result or violate an exact-accumulation contract. Serialization must preserve the required partial state or justify any loss. Binary64 capacity, fixed-point scale, wide integer encoding, non-finite values, and signed zero each follow the actual boundary contract; a generic host serialization facility supplies no blanket numerical-fidelity theorem.

BAREWire can express agreed request, contribution, and recovery layouts. Job identifiers, partition indices, and versions are application fields where required. They do not require self-describing proof, dimension, or schema tags in every payload. Deployment/session agreement still determines how untagged bytes are interpreted. Shape checks establish decoded structure; they do not prove the remote worker computed the right answer. That premise requires an admitted implementation contract, validated result, or appropriate computation evidence.

## Durable reconstruction and effects

Cloudflare Workflows documents recovery across engine lifetimes using returned step state and deterministic step names. The design must map logical operation identities to those facilities and retain the observations needed to reconstruct decisions. Mutating a local object is not a substitute for persisted state. [Workflow rules](https://developers.cloudflare.com/workflows/build/rules-of-workflows/).

Step attempts can be retried, and retry exhaustion can terminate the workflow with an error. A successful-result preservation argument does not guarantee eventual success. [Workflow retry behavior](https://developers.cloudflare.com/workflows/build/sleeping-and-retrying/).

The proposed durable continuation record includes the control position or reconstructible path, compatible program version, logical job/input version, expected and accepted partitions, numerical state or reconstruction inputs, and cancellation/failure state. References to target-local resources need rebinding or explicit rejection after recovery. Persisting an ordinary V8 closure is not assumed.

A repeated external write needs an effect-specific protocol. Recording a local step and performing a remote action are not atomic merely because both have identifiers. The effect destination must honor an idempotency key, participate in a supported transaction, or provide another justified recovery protocol. Deduplicating replies at the collector does not undo duplicated external effects. These are the recovery distinctions already made in [ledger lowering](../the-ledger-lowering/#the-write-side-boundary-map).

The [proof-composition design](/docs/internals/verification/proof-composition-and-tooling/#a-numerical-result-needs-a-protocol-to-remain-the-same-result) develops automatic elaboration of these joint numerical and protocol obligations using reusable Rocq foundations. Host-provided scheduling and durability remain explicit premises. The proposed integration preserves this document's arithmetic and recovery requirements; importing a protocol theorem cannot replace them.

## Compiler artifacts and open premises

The proposed implementation should produce reviewable artifacts rather than infer correctness from an API name:

| Artifact | Evidence it needs |
|---|---|
| Continuation and actor state machine | Capture validity, exclusive turns, eligible transitions, stale-reply handling |
| Partition/join descriptor | Input coverage, logical multiplicity, readiness, failure/cancellation policy |
| Arithmetic construction | Term formation, intermediate capacity, permitted trees, merge and finalization laws |
| Boundary codecs and checks | Agreed layout, decoding preconditions, numerical fidelity, identity correspondence |
| Recovery plan | Retained observations, consistent acceptance, compatible code, effect protocol |
| Assumption manifest | Host scheduling, storage and delivery contracts, budgets, external completion premises |

Source types contribute to these artifacts, but do not determine all their behavioral properties. Facts may be established by graph analysis, accepted laws, supported solver obligations, and generated checks at open boundaries. Metadata can be consumed after its preservation role is complete. Neither carrying metadata nor generating an SMT query proves the emitted operation implements the modeled behavior.

The host engine, host APIs, and persistence mechanisms remain assumptions of the realization unless independently verified. The manifest should name their relevant configuration and contracts. Single-threadedness does not establish scheduler fairness, bounded service latency, or supervision that can interrupt a non-yielding callback. Host termination on budget exhaustion must be distinguished from a locally dispatched cancellation turn.

## Bounded acceptance sequence

Start with a finite partitioned calculation over a fixed input snapshot. Keep its effect policy simple and establish the arithmetic construction independently. Then inspect the emitted JavaScript and exercise:

- Every result arrival order, with stable logical partition membership.
- Duplicate, conflicting, stale, malformed, and wrong-version replies.
- Suspension followed by mutation of a fact's dependency.
- Interruption before and after the persistent acceptance transition, followed by recovery.
- Cancellation, exhausted retries, refused admission, and unavailable external completion.
- Fixed-tree and exact-merge numerical controls, including rounded-subtotal rejection where exact state is required.

These are implementation acceptance cases, not substitutes for the invariant and lowering argument. Extend from the [existing contract-to-artifact sequence](../jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact); keep Fable characterization tests distinct from proofs of a new Composer backend. This proposal supplies no automatic workflow adapter or completed distributed verification pass.
