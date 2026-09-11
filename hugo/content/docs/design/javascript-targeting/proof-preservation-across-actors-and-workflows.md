---
title: "Proof Preservation Across Actors and Workflows"
linkTitle: "Actor and Workflow Proofs"
description: "Preserving actor, arithmetic, and recovery contracts through JavaScript suspension, distributed replies, and durable workflow execution."
date: 2026-09-11
authors: ["Houston Haynes"]
tags: ["JavaScript", "Concurrency", "Verification", "Architecture"]
weight: 50
---

This design connects the [JavaScript boundary contract](/spec/draft/javascript-boundary/), [scheduler contract](/spec/draft/scheduler-contract/), and [arithmetic-construction obligations](/spec/draft/numeric-selection/#105-capacity-error-and-decomposition-obligations). It specifies the evidence a proposed Composer JavaScript realization would need across suspension, actor delivery, and durable recovery. It does not claim that JSIR actor generation, workflow lowering, or their preservation proofs are implemented.

[Carrying Proofs into JavaScript](/blog/carrying-proofs-into-javascript/) develops the narrative. The existing [boundary map](../cloudflare-agents-and-the-boundary-map/) addresses incoming values; [ledger lowering](../the-ledger-lowering/) addresses retained observations and recovery. This page supplies the join and continuation obligations that connect them.

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

At each suspension, analysis needs the continuation's live captures, actor and job identities, pending obligations, and the mutable dependencies of facts used after resumption. An immutable scalar capture can retain its established range. An immutable reference binding does not freeze the referenced object. A fact derived from storage that may change requires revalidation, an established version check, or a protocol preventing the change.

A correct resumption path establishes:

1. The incoming value satisfies its boundary shape, numeric, and representation conditions.
2. Its logical operation and actor incarnation match the pending continuation.
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
