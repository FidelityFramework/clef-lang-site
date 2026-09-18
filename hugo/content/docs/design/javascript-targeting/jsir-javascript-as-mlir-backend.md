---
title: "JSIR: JavaScript as an MLIR Backend"
linkTitle: "JSIR"
description: "How Google's JavaScript IR for MLIR changes the Clef compilation story"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation", "Design"]
weight: 10
---

The Fidelity framework reaches toward hardware and hosted runtimes in one design. Cloudflare Workers give its actors an edge home; native processes and accelerators give them other places to compute. JavaScript belongs in that picture. The interesting question is how much of the compiler's reasoning can follow a program there.

Google's April 2026 [JSIR RFC](https://discourse.llvm.org/t/rfc-jsir-a-high-level-ir-for-javascript/90456) offers a practical opening: represent JavaScript inside MLIR, with source regeneration as well as analysis. Google's production uses include deobfuscation and decompilation. Its reported round-trip fidelity is substantial empirical evidence, not a semantic translation proof.

**Status, September 2026.** The F#/Fable path and BAREWire's JavaScript implementation can be exercised today. Composer's Clef → JSHIR/JSIR path and the associated proof-preservation work remain design and implementation work. The [JavaScript Substrate profile](/spec/draft/javascript-boundary/) explicitly has no conforming implementation yet. This page describes the architecture and the checks that would earn that claim.

## The Problem JSIR Solves

Composer's design lowers the Program Semantic Graph through Alex and MLIR toward LLVM, CIRCT and other target backends. The PSG carries facts about dimensions, ranges, effects, escape and representation. Sharing that information makes target-specific reasoning possible; it does not make every analysis or transformation valid for every target.

JavaScript was the exception.

The intended actor network spans native processes, shared memory and IPC, and Cloudflare Workers. BAREWire is the glue across memory layout, IPC and network contracts. Conclave is the platform for intelligent distributed systems on Cloudflare. These roles meet at a declared contract, while each substrate retains its own allocation, scheduling and host constraints.

The original plan for JavaScript emission was to bypass the Alex middle-end entirely. Clef's PSG would lower to an Oak-like JavaScript AST (analogous to Fable's approach for F#) and emit JavaScript directly. This meant that every optimization and verification pass written against Alex would not apply to the JavaScript target. JavaScript would be a side door, separate from the MLIR pipeline, maintained independently, verified independently.

```
Previous architecture:

  Clef PSG ──▶ Alex MiddleEnd ──▶ LLVM ──▶ native binary
                                          (native lowering)

  Clef PSG ──▶ (bypass Alex) ──▶ Oak-like JS AST ──▶ JavaScript
                                          (separate lowering)
```

That bypass was a choice in the earlier plan, not an inherent property of structured AST emission. A Babel-AST exporter could also consume the result of portable witnessing and retain the same preservation obligations.

Two paths can share a language contract and still require separate preservation work. The opportunity is to share more of that work before the target-specific decisions begin.

## What JSIR Changes

JSIR places JavaScript inside MLIR as a first-class dialect. It is structurally the same kind of thing as EmitC, the MLIR dialect already upstream that lowers MLIR to C source code. EmitC established the pattern: an MLIR dialect can serve as a source language emission target, not just an analysis or optimization substrate. JSIR applies that pattern to JavaScript.

The proposed architecture becomes:

```
Proposed JSIR path:

  Clef PSG ──▶ Alex MiddleEnd ──▶ LLVM ──▶ native binary

  Clef PSG ──▶ Alex MiddleEnd ──▶ JSIR ──▶ JavaScript source
```

Alex witnesses `func`, `scf`, `arith`, `memref` and `index`; target-specific JSHIR/JSIR realization follows that boundary with useful PSG/codata retained. Both paths would go through Alex and use MLIR's pass infrastructure. A pass can preserve a carried property, or a check at its output can establish it again. That is the obligation in [Conformance §6](/spec/draft/conformance/#6-the-preservation-obligation-through-lowering); merely placing a pass in MLIR establishes neither.

This matters because representation and contract metadata can remain in PSG/codata until all reasoning that needs it is complete. A common codec derivation can choose field order and byte encodings once. The native and JavaScript lowerings must then preserve that choice, including bounds, endian order and numeric conversions. Shared derivation reduces opportunities for drift and gives the checks a common reference. Cross-target byte tests and lowering evidence still have work to do.

## JSIR's Design

JSIR maps JavaScript syntax into MLIR operations, distinguishing references from values and using JSHIR regions for high-level control flow. In the upstream revision reviewed on September 15, 2026 [`d5322bd`](https://github.com/google/jsir/tree/d5322bda6e1311357ead5e20376e28461c8cbc2a), the driver exposes source, Babel AST and high-level IR. The CLI names the forward conversions `source2ast,ast2jsir` and the reverse conversions `jsir2ast,ast2source`. Its input is initialized as JavaScript source: the reverse conversions operate on JSHIR already in the representation pipeline, not an established reverse-only CLI accepting an MLIR file. Composer needs conversion-library integration or a JSHIR-input driver. [Input initialization](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/ir/jsir_gen_lib.cc), [conversion APIs](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/driver/conversion.h). The April checkout used `ast2hir` and `hir2ast`; commands must be paired with the pinned tool revision. The presence of both `jsir` and `jshir` dialects is not a promise of a separately supported low-level emission route. [Driver source](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/ir/jsir_gen.cc).

The forward route also opens an avenue beyond bindings: lift a small library fragment, recover candidate Clef code, and maintain that code locally. That would require an explicit semantic contract, preservation evidence, licensing and an update policy. Neither a successful lift nor a round trip proves that the recovered program behaves the same. [Fully Informed Bindings](/docs/design/javascript-targeting/fully-informed-bindings/) describes the broader design direction of owned Clef SDKs and supporting libraries, developed through bounded, validated replacements and deferred inference. That frontend work is separate from adopting a target emitter; a JSIR lift alone implements neither.

The work builds on MLIR's established dialect and analysis machinery. Its [design document](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/docs/intermediate_representation_design.md) is a useful starting point for that engineering, especially the distinction between faithful syntax representation and analysis of JavaScript behavior.

JSIR is not TypeScript's semantic type system. Its current type machinery includes a placeholder `JsirAny`; it does not carry Clef dimensions, range proofs or boundary grades for us. Composer must retain those facts alongside the lowering and connect them to the operations whose behavior they constrain. Erasing metadata is safe only after its preservation obligations have been fulfilled. [IR type definitions](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/ir/jsir_types.td).

## JSX as a Framework Handoff

For a Solid/WREN frontend, the proposed output can retain JSX for Solid’s compiler before Vite bundles the result. Babel already represents and prints JSX; the reviewed JSIR native AST/IR bridge does not. The [JS / JSX toolchain chapter](../javascript-jsx-toolchain/) describes the generated-definition and driver work, reactive-read preservation, and the existing WrenHello path that a Clef producer could reuse. General JavaScript remains the output for non-UI computation.

This extends the preservation chain through Solid compilation, bundling and native embedding. Evidence must concern final assets and shipped dependencies, with the affected obligations preserved or re-checked across those stages. JSX is an intermediate contract, not a proof boundary at which the argument can stop.

## The Trust Chain

The actor design maps edge actors to Durable Objects and exchanges BAREWire payloads over suitable transports. The final payload is untagged with respect to types, schemas, dimensions and proofs: the communicating parties already have the contract. A union case index or optional-value presence bit selects an alternative *within* that contract. It is not a schema identifier. Internal JavaScript objects may use tags too; this says nothing against internal tags or metadata retained during compilation. The [DU representation](/spec/draft/discriminated-union-representation/) specifies the union encoding.

This architecture creates a specific trust question: how much can you trust JavaScript that was emitted by a compiler whose verification properties are defined at the MLIR level?

The answer requires distinguishing three boundaries.

### Boundary 1: Type Erasure

JavaScript retains runtime value classifications and object properties, but those do not establish a Clef declaration's meaning. A generated JavaScript union may use a tag and fields; a final BAREWire payload uses only the encoding elements its external contract requires. Neither representation makes an arbitrary inbound value trustworthy.

BAREWire's current codecs check cursor bounds and malformed encodings, including invalid boolean and optional-value flags; framing rejects unknown envelope kinds, and full decoding checks consumption of the input. A generic union case index still needs the client codec to check membership in its declared case set. Those checks remain necessary for truncated or hostile bytes, even when both correct endpoints derive codecs from one declaration. The untagged payload cannot by itself reveal that two deployments assigned different meanings to the same layout. Contract agreement belongs to build/deployment coordination or an explicit session control exchange. It need not be paid for with a type or proof tag on every payload.

The current JavaScript implementation is concrete ground for this design: Fable compiles the shared codec and framing sources, and JavaScript tests exercise exact byte vectors, round trips and selected rejection cases, including an out-of-bounds read and a truncated frame. Other tests construct SMT formulas from platform declarations and invoke cvc5. These prove those declaration formulas, not the correctness of the JavaScript constructing them. The proof inventory in `BAREWire/docs/12 Intersection Subset.md`, §5.1, records the present scope. At source revision `14e46f6d4023b630c4d4d0f6c773cea019f4d9bc`, the Fable 5.13.0 JavaScript gate makes 22 cvc5 calls: 15 `unsat` and 7 `sat`, over Linux declaration examples and solver-emitter edge cases. These are not transport or stack proofs over emitted JavaScript.

JavaScript objects arriving from APIs, JSON, storage or callbacks need a different boundary operation: narrowing. The proposed [JavaScript boundary](/spec/draft/javascript-boundary/#3-injection-and-elimination) generates a total check from the declared target shape, returning `Result` with a failed path instead of admitting a partial record. Callback parameters receive the same check. Host throws and awaited rejections are converted to typed errors. Absence follows the binding's per-position contract; missing, `undefined` and `null` are not interchangeable for every API. Libraries such as `serde` illustrate the practical value of generated conversion code. [Constructed Witnesses](/docs/design/javascript-targeting/constructed-witnesses/) explains how such checks can supply observable premises of a larger argument.

### The Academic Twist: JavaScript's Tagged-Structure Heritage

JavaScript's early Scheme influence makes a useful connection: dynamic values retain evidence that a statically typed boundary can inspect. Property presence, value classifications and explicit null tests give narrowing code something to work with. The connection to the LISP and contract traditions is a design precedent, not a proof about V8's representation.

The Clef profile keeps that dynamic work at declared boundaries, through `JsValue`, opaque `JsRef<'T>` and narrowing. It does not introduce a universal interior `obj`. The generated checks still need to account for JavaScript behavior such as getters, proxies and thrown values; a property name alone is not a proof of a record's shape.

### Boundary 2: Lowering Fidelity

MLIR can check operation structure, region invariants and value use. These are useful checks, with a narrower scope than semantic preservation. At the pinned upstream revision, [AST-to-JSHIR conversion invokes `mlir::verify`](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/ir/conversion/utils.cc), while [the transformation runner disables pass-manager verification](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/ir/transforms/transform.cc) pending an IR-design fix. The two paths must not be described as a universally verified pipeline.

A well-formed call to `DataView.getFloat64` can still use the wrong offset or endian flag. Preservation work therefore starts at each affected lowering edge, not only at final emission. Composer would need a certified transformation or a re-check connecting the source operation to its target behavior. Source regeneration and empirical round trips are useful additional evidence; neither discharges that correspondence by itself.

The intended chain is:

```
Declared source / library / platform facts
  → PSG obligations, with premises and provenance
  → target operations, preserving or re-checking affected facts
  → JSHIR/JSIR and emitted JavaScript under a stated semantic relation
  → execution under explicit host and external-library assumptions
```

The same responsibility exists on native backends. Shared IR makes it easier to locate and reuse the contract; Composer owns the preservation argument for its lowerings.

### Boundary 3: Runtime Contract

The emitted JavaScript calls host APIs such as `fetch`, WebSocket operations and storage. A TypeScript declaration constrains the interface a binding exposes; it does not prove persistence, scheduling, purity or delivery. A library body may be available for analysis, but dynamic calls and unavailable host implementations still require conservative summaries or explicit assumptions.

Compatibility dates, pinned library versions, inspectable workerd source and runtime tests make these assumptions manageable. They do not turn a host promise into a theorem. Actor lowering must model reentrancy at suspension points and retain or await asynchronous work according to the host lifecycle; a tell-style message without an application acknowledgment still needs a valid completion policy.

### Embodying a Durable Object: Per-Instance State

An emission concern specific to the actor target is per-instance state. A Durable Object class must attach the appropriate state to each host-created instance. The contract is independent of where the host places instances or how often it recreates them.

This constrains the emission model in a way the stateless `fetch` target does not. A compiler that emits a single script with module-level state runs that script once per isolate and installs one set of state cells shared by every instance. Reaching the DO model from there means hand-building a factory protocol across the FFI boundary: the compiler-emitted code has to expose a constructor that yields a fresh, isolated state cell per instance, and the host class has to anchor that cell to `this` and route each method call back to the right instance. Done by hand, this is error-prone, and the naive version, a module-level reference shared across instances, silently bleeds state between actors in production. The structural-layout convenience of clean module output does nothing to prevent this. It is an instance-lifecycle problem, not a layout problem.

The proposed JSIR path would emit a class whose constructor owns the actor's state, with dispatch and lifecycle hooks tied to that instance. Tests with distinct object IDs, suspension and recreation would check that no mutable module-level cell accidentally becomes actor state. This generation remains upcoming work; it is not supplied by JSIR's class operations alone.

<a id="schema-identity-as-a-proxy-for-dimensional-agreement"></a>
## Agreement Before the Untagged Payload

Dimensions, ranges and message roles belong to the contract before lowering. Keep them in PSG/codata through representation selection and every affected preservation check. Once those obligations are fulfilled, the final payload can omit that metadata. That is BAREWire's useful economy: the endpoints know how to interpret the bytes without making each message describe itself.

Consider a force and a distance, each assigned a binary64 wire field by a boundary declaration. Replacing distance with time can leave exactly the same two-field encoding. Both record declarations are valid in isolation; an incompatible use is where dimensional checking has work to do. No union case index detects this semantic substitution. Contract agreement must include the declarations' meaning, not only their byte shape, and deployments must establish that agreement outside the payload.

BAREWire's framing includes a control `Hello` carrying an epoch and build text. Decoding that control frame is not the same as enforcing epoch agreement, session order or reply correlation. Those are explicit endpoint/session responsibilities. A deployment may use them to reject incompatible peers while keeping ordinary data payloads untagged.

## Representation Fidelity Across Substrates

Representation selection connects dimension, range, width and allocation, but these are joint obligations with different premises. Facts may remain pending while the graph is elaborated. A concrete lowering needs them resolved at the point it commits to a representation; no source seal or width-named numeric type is required to supply them.

JavaScript's default real carrier is binary64. Integer realization follows [Width Inference §8](/spec/draft/width-inference/#8-target-lowering): the exact host-number envelope must be respected, with a documented wide-integer realization such as `BigInt` or emulation above it. A target can advertise additional emulated capabilities under its declared policy. "Always Number" is not an adequate lowering rule, and JavaScript bitwise coercions cannot silently truncate a wider integer.

A cross-target transfer may change representation. Its contract must distinguish bit preservation of the chosen wire encoding from preservation of the original numeric value. Exact transfer needs both coverage and exact representability; a lossy transfer needs a justified error bound. Equal dimensions do not imply equal arithmetic results. [Numeric Selection §10](/spec/draft/numeric-selection/#10-the-preservation-chain-and-the-quire-pass) makes that distinction explicit.

For a posit/quire computation, exact accumulation additionally requires exact representation of each product and coverage of every reachable partial sum in the finite accumulator. The final conversion can round, and exact accumulation does not recover input error or prove a simulation's useful horizon. A target without a required exact-accumulation capability must produce a capability error. An empty coverage set is also an error. A comparative IDE display can help a developer choose among admissible targets; it cannot downgrade these requirements to advice.

A useful display would show the selected carrier, the source of range facts, transfer fidelity, required capabilities and any unresolved premises. Quantitative error claims belong there only when supported by the selected format and analysis. That keeps the design-time feedback useful without inventing precision numbers.

## What This Means for Cross-Substrate Actors

The hybrid actor network would let native processes and edge actors share contracts while doing useful work on different substrates. BAREWire supplies the glue for memory description, IPC and network encoding; Conclave supplies the Cloudflare platform. A native BFF can be as thin as forwarding or as substantial as local inference and numeric computation.

The existing F#/Fable BAREWire path already checks cross-runtime byte examples before deployment. A shared Composer derivation could strengthen that foundation by exposing the same obligations to both backends. It would still need evidence that each backend implements them.

A wire layout also differs from an in-memory ABI. Native structures can have padding, alignment and pointer or lifetime requirements. Ordinary JavaScript objects have structural property access under a managed runtime, not a specified native byte size. `ArrayBuffer`/`DataView` access does have explicit offsets and bounds; a zero-copy or shared-memory path additionally needs ownership and host capability premises. Reusing a declaration does not make these representations interchangeable.

## From Contract to Emitted Artifact

A small example makes the proposed work tangible. Suppose an endpoint contract assigns two unsigned four-byte integer fields at offsets 0 and 4 in an eight-byte payload, in little-endian order. The boundary declaration fixes the encoding; the source value must have an admitted range within `0…2³²−1`. This example separates an *access-extent* query from the stronger claim that two lowerings write the same bytes. Neither claim proves peer agreement or message delivery.

| Step | Concrete evidence to retain |
|---|---|
| Contract | Field meaning, order, offsets, encoding, eight-byte extent, admitted numeric ranges and the declared origin of each fact |
| Operation | Read/write four bytes at the chosen offset; require sufficient input/output storage; establish the declared numeric conversion |
| Query | Negate the extent claim under the operation's premises; record its source/PSG obligation and supported arithmetic fragment |
| Lowering | Relate that operation to native byte access, `DataView.setUint32` or four indexed stores with the same offset and little-endian encoding; preserve the bounds guard and conversion behavior |
| Artifact | Bind evidence to the emitted module and dependency closure, target policy and exact tool revisions; record unresolved or external premises separately |

For the fixed access, this illustrative SMT query asks whether either declared field can cross the payload extent:

```smt
(set-logic QF_LIA)
(declare-const offset Int)
(assert (or (= offset 0) (= offset 4)))
(assert (> (+ offset 4) 8))
(check-sat)
; unsat
```

The stronger byte claim is concrete too: for admitted value `v`, both a `DataView.setUint32(offset, v, true)` write and four indexed stores must produce `floor(v / 256ⁱ) mod 256` at `offset + i`, for `i` from 0 through 3, and leave every other byte unchanged. Both views must refer to the same payload origin. The value is exact as a host number within this range. A universal proof would need semantics for these target operations and their conversion behavior; the bounds query above supplies only one premise.

That result concerns mathematical integers and the declared offsets. Connecting it to a JavaScript access requires more: the actual buffer must contain the payload; the lowering must emit that offset without a truncating coercion; the view must remain valid through the access, without detachment or interfering aliases; and the encoded conversion must meet the numeric contract. Invalid values and spans can make the two JavaScript forms behave differently, so admission checks and failure behavior must be included in the contract. Exactness in this bounded example cannot be generalized to all integer widths. An unavailable premise remains pending or becomes a located failure when commitment requires it. A timeout or unknown solver result is not a proof.

The record in the final row is an acceptance requirement for the proposed Composer path, not an existing JavaScript proof-certificate format. The current BAREWire tests establish selected byte behavior and declaration-level queries; they do not yet pair a semantic proof with an emitted JS artifact.

An incremental acceptance sequence can keep this work reviewable:

1. Pin the JSIR/tool/runtime revisions and characterize one codec plus malformed, truncated and mismatched-contract inputs against the working Fable/native reference tests.
2. Carry the contract and pending obligations through PSG, then implement one target operation with a stated semantics and explicit external premises.
3. Check the relevant JSHIR path for structural validity, and establish preservation or re-check each affected lowering edge. Introduce wrong-offset, endian and truncation mutations to show the checks reject them.
4. Run native and emitted-JS behavior tests against the contract: bytes, accepted/rejected inputs, results and errors. Add numeric and asynchronous cases as their operation families enter the supported subset. Normalized IR is a comparison aid, not the equivalence theorem.
5. Bind results to the emitted artifacts and dependency closure. Expand the supported subset only with corresponding evidence; claim the JavaScript Substrate profile only when its full requirements are met.

This reaches beyond codecs. The same discipline can connect a callback's narrowing, an integer realization, a library effect summary or an actor's completion policy to the operation that implements it.

## The Precedents

JSIR does not arrive in isolation. It follows a pattern that MLIR has been establishing for several years.

**EmitC** is an MLIR dialect already upstream in MLIR core, designed for lowering MLIR to C source code. It established that source language emission is a valid MLIR use case. JSIR is structurally the same pattern applied to JavaScript.

**WAMI** (WebAssembly through MLIR) demonstrated compilation to WebAssembly through MLIR dialects without going through LLVM IR. Their paper explicitly mentions future integration with a JavaScript MLIR dialect. The research community anticipated this direction.

**The 2022 emitjs RFC** proposed an `emitjs` dialect modeled on EmitC, with the pipeline: ONNX model / C / DSL to MLIR dialects to MLIR js-Dialect to JavaScript. The community asked for prototypes. Four years later, JSIR delivers from the opposite direction: analysis-first, but with full round-trip capability enabling the emission use case.

**js_of_ocaml** is the closer architectural precedent. Jérôme Vouillon and Vincent Balat built js_of_ocaml starting in 2010 at PPS/CNRS Paris Diderot. The design commits to compiling from OCaml bytecode (the output of `ocamlc`) rather than from source AST: bytecode is lifted into an internal SSA-style IR (`Code.program`), optimization passes run over that IR (dead code elimination, tail-call optimization, flow analysis, effect-handler CPS transformation, closure generation), and JavaScript is produced through a conventional compiler back end. The commitment is, in the project's own words, that "the bytecode provides a very stable API," which makes the compiler easier to maintain than one that retargeted OCaml's source-level AST.

In 2024 the same project shipped `wasm_of_ocaml`, a WebAssembly backend. The critical detail: `wasm_of_ocaml` shares `Code.program` with `js_of_ocaml`. Both backends sit atop the same IR and consume the same upstream optimization passes. This is the multi-target-from-shared-IR pattern in miniature, one frontend and one middle-end feeding two backends, and it is structurally what our Composer generalizes over MLIR's dialect infrastructure to reach four backends (LLVM, CIRCT, MLIR-AIE, JSIR) from one Clef source. The architecture Composer adopts is the pattern js_of_ocaml has been proving at production scale for fifteen years, adapted to MLIR's substrate and extended to more targets.

**Fable** is an established F# compiler with its own intermediate AST and target transformations, including JavaScript emission through Babel-shaped output. It does not compile CLR IL to JavaScript, and it is not limited to raw source walking or string templates. Its existing interop patterns and runtime tests are valuable references for Composer. The architectural difference here is which compiler owns the semantic graph and preservation work, not the presence or absence of an IR.

**Melange** brings OCaml's module and type system to JavaScript, including direct ES module output and foreign bindings. Like a TypeScript declaration or an F# binding, an `external` declaration alone does not validate an inbound runtime value. Applications in these ecosystems can and do add validators. Composer's proposed distinction is to make the declared narrowing and preservation requirements part of its JavaScript profile, including callbacks and failure paths, rather than leaving their coverage to each application.

The transformations that both js_of_ocaml and Fable perform (pattern matching to switch/if chains, algebraic data types to object construction, tail calls, currying) are thoroughly characterized. These serve as direct blueprints for MLIR lowering passes that target JSIR.

## What Does Not Change

JSIR affects the compilation pipeline. It does not affect the actor model's design, the management API surface, or the deployment infrastructure.

Fable continues as the F#-to-JavaScript path for Partas.Solid, WrenHello's WebView layer and FSharp.CloudEdge bindings. Composer's proposed JSIR backend serves Clef code. The two paths can share useful contract tests while remaining distinct implementations.

FSharp.CloudEdge is the F# binding and management-tooling layer. Conclave is the platform for intelligent distributed systems on Cloudflare. The management client provisions resources and deploys artifacts externally; it need not care which compiler produced a Worker's JavaScript.

The actor model's semantic design (Olivier workers, Prospero supervisors, WebSocket transport, BAREWire serialization, elastic scaling with Queue pivot, event-sourced persistence) is unchanged. These are runtime patterns, not compilation patterns. They exist at the Clef source level and in the Cloudflare runtime contract. JSIR changes how the compiler produces the JavaScript that implements them. It does not change what they are.

Firetower, the monitoring tool, is similarly unaffected. It consumes management APIs and runtime WebSocket data. Its own compilation path (Avalonia for desktop, Fable for web) is independent of how Worker code is generated.

## Practical Next Steps

The [acceptance sequence above](#from-contract-to-emitted-artifact) starts with a small experiment: pin and build JSIR, lift a Worker-shaped program, and confirm the supported path back to executable JavaScript. Then connect one Composer operation to it. This can proceed while the upstream RFC remains under discussion.

Fable-compiled binding tests supply behavior to compare, not a required textual output. Keep their original API and runtime assertions so a discrepancy can be investigated against the host contract instead of assuming either compiler is the oracle. The [transition plan](../from-fable-to-jsir/) develops that comparison.

## The Adjacent Capability: WASM and Stack Switching

WebAssembly is another possible consumer of the shared middle-end, through LLVM or an MLIR-specific route. [WebAssembly Targeting](/docs/design/wasm-targeting/) and [Coroutines Versus Stack Switching](/docs/design/wasm-targeting/coroutine-versus-stack-switching/) discuss those designs. Stack switching is especially relevant to the framework's continuation model; the availability and semantics of a chosen runtime feature must be checked for the deployment target.

A WASM module hosted by a Worker also crosses a JavaScript boundary. Linear-memory bounds, host imports, suspension and numeric transfers create their own preservation obligations. BAREWire can supply the agreed encoding there too, with untagged final payloads; a common contract does not eliminate the distinction between linear memory, JavaScript objects and native ABI layouts.

## Honest Framing

JSIR gives Composer a place to express JavaScript operations within the compiler architecture it is already pursuing. BAREWire gives that work a small, useful contract to start with, spanning memory, IPC and network use without making every final payload describe its types.

The opportunity is to carry more reasoning to the point where it matters: dimensions through numeric selection, buffer extents through actual accesses, foreign-value premises through narrowing, and asynchronous effects through host completion. A pass that preserves an obligation can carry its evidence onward; a pass that can disturb it needs a re-check. Neither erasure nor a change of substrate makes an unresolved obligation disappear.

That is a practical direction for engineering: keep the current byte and runtime tests, add explicit lowering relations, and grow the supported operation set with evidence attached. JavaScript becomes another place the framework can do useful work, with the guarantees earned by its compiler and boundary implementation rather than borrowed from the name of an IR.

## See also

- [Proof Preservation Across Actors and Workflows](../proof-preservation-across-actors-and-workflows/): continuation, partition/join, arithmetic, and durable-recovery obligations for the proposed JavaScript realization.
- [Carrying Proofs into JavaScript](/blog/carrying-proofs-into-javascript/): the narrative companion to Pondering Fearless Parallelism, following a logical computation across suspension and isolate boundaries.

- [A Runtime Revolution, sort of...]({{< ref "runtime-revolution-fidelity" >}}): the blog-layer framing of this same JSIR-on-Cloudflare story, walking through why the unified middle-end matters for Workers and how BAREWire carries the contract across the erasure boundary.
- [Cloudflare Agents and the Boundary Map](/docs/design/javascript-targeting/cloudflare-agents-and-the-boundary-map/): the worked example of this mechanism against Cloudflare's AI-agent surface, where generated per-boundary narrowing spans the request, WebSocket, AI-inference, SQLite, state-sync, and RPC edges a Durable Object multiplies.
