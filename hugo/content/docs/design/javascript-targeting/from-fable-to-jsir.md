---
title: "From Fable to JSIR: The Back-End Transition for JavaScript Targeting"
linkTitle: "Fable to JSIR"
description: "How Composer's JavaScript-emission back-end transitions from Fable + npm bundlers to JSIR + MLIR, while the .NET-hosted compiler persists across the change"
date: 2026-05-04
authors: ["Houston Haynes"]
tags: ["Architecture", "Compilation", "JavaScript", "JSIR", "Design"]
weight: 30
---

The working F#/Fable path gives this design something valuable: programs, bindings and runtime tests we can use now. Composer's proposed JSIR backend would give Clef a different route to JavaScript through its own semantic graph and MLIR middle-end. The two efforts can inform each other without requiring F# users to migrate.

This page keeps the transition as a set of engineering waypoints. It does not describe a completed Composer backend: the specification's [JavaScript Substrate profile](/spec/draft/javascript-boundary/) is design-stage, with no conforming implementation yet. The [central worked example](../jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) gives the acceptance sequence that each new lowering should follow.

## Two Models, Two Source Languages

Fable compiles F# through its own intermediate AST and target transformations. Its JavaScript route produces Babel-shaped output and supports interop declarations, including `[<Emit>]` templates. The compiler is available as a [.NET tool](https://fable.io/docs/getting-started/your-first-fable-project.html); a project's optional Node-based bundler is a separate choice. Describing Fable as a Node-resident source walker without an IR misses both its implementation and its contribution.

Composer owns Clef's Program Semantic Graph. The proposed route carries its facts through Alex toward JSHIR/JSIR and JavaScript. Earlier drafts considered a separate JavaScript AST route that bypassed Alex. Moving the design into the MLIR fan-out offers a common place to express and check lowering obligations; it does not automatically prove those obligations.

The difference is therefore the owner of the language semantics and preservation work. F# continues through Fable. Clef would use Composer. Both can target the same host APIs, and both can be checked against the same external contract where their source semantics overlap.

## What Fable Does Well, What Composer Needs to Add

Fable preserves F# idioms through its intermediate representation, has a mature interop model and supplies years of useful implementation experience. Partas.Solid, WrenHello's WebView layer and FSharp.CloudEdge bindings make that experience relevant to this design. The existing BAREWire Fable target supplies byte-level examples and runtime tests immediately.

Composer's additional work is specific:

- Carry its own dimensions, range facts, boundary grades and other obligations to the target operations that rely on them.
- Characterize reusable binding-shape lowerings, including receiver binding, optional arguments, callbacks and failure behavior.
- Establish preservation or re-checks at affected lowering edges, under [Conformance §6](/spec/draft/conformance/#6-the-preservation-obligation-through-lowering).
- Meet the whole JavaScript boundary profile before claiming conformance, including generated narrowing and host-error conversion.

An `[<Emit>]` template is an interop tool with a contract to test. A generated MLIR operation also has a contract to establish. Replacing one with the other changes where we can analyze and reuse that work; it does not remove the work.

## What JSIR Brings

The proposed pipeline is:

```
Clef source → PSG → Alex / MLIR → JSHIR/JSIR → JavaScript
                       ├──────→ LLVM → native binary
                       ├──────→ CIRCT → FPGA
                       └──────→ other declared targets
```

The semantic metadata remains in PSG/codata through the reasoning and affected lowering edges that need it. It can be erased after those obligations are fulfilled. A common BAREWire derivation can select the same encoding for several backends, while each backend establishes that its operations implement that choice. The final payload stays untagged with respect to type, schema, dimension and proof metadata.

JSIR supplies operations and an analysis substrate, along with source regeneration. Its reported round-trip success is empirical fidelity, not a proof that Composer's serializer emits the right bytes. Upstream verification is also path-specific: AST conversion invokes MLIR verification, while the reviewed transformation runner disables pass-manager verification pending an IR fix. The [pinned upstream details](../jsir-javascript-as-mlir-backend/#boundary-2-lowering-fidelity) belong in the tool qualification, not in an assumption that every pass is verified.

The proposed Library of Alexandria would collect reusable lowering rules and their contracts. A rule can then be applied across call sites whose premises it covers. That is a useful unit for tests and eventual proof work. Unknown shapes and external behavior still need explicit treatment.

## Direct Tools and the Supply Chain

A direct `jsir_gen` invocation can reduce the orchestration needed for one emission stage. It is still a tool built from dependencies: the reviewed upstream embeds Babel and QuickJS. Removing a Node launcher does not remove those implementations from the trust base. The exact revision also matters: current upstream spells the routes `source2ast,ast2jsir` and `jsir2ast,ast2source`, while the April checkout used `ast2hir` and `hir2ast`. [Pinned driver](https://github.com/google/jsir/blob/1488d9bd408ec9163ac7051252dfe80e40a4e26a/maldoca/js/ir/jsir_gen.cc), [embedded dependencies](https://github.com/google/jsir/blob/1488d9bd408ec9163ac7051252dfe80e40a4e26a/maldoca/js/quickjs_babel/BUILD).

The artifact's runtime dependencies are another question. Type-only declarations for a host API need not ship JavaScript. A wrapper SDK can ship substantial behavior in the bundle, even when there is no `node_modules` directory at runtime. Bindings describe that code; they do not absorb or verify it. [Fully Informed Bindings](../fully-informed-bindings/) separates host surfaces, wrapper libraries and management clients.

Build dependencies also matter because they can change emitted bytes. The useful supply-chain contract records tools, input packages, generated artifacts and the transitive code actually shipped, with pins and reproducible provenance where available. NuGet, npm and a native binary are packaging choices, not trust proofs. Xantham's compiler transport and the deployment client should be described from the chosen configuration rather than treated as an unavoidable npm runtime chain.

## Composer as .NET Host Across the Transition

Composer's .NET host can invoke native MLIR tooling; adopting a JavaScript dialect does not require changing that host. The planning split is:

| Component | Working reference or present role | Proposed Clef path |
|---|---|---|
| Compiler host | Fable and Composer have .NET tooling | Composer remains .NET-hosted |
| F# JavaScript compilation | Fable's own IR and target transforms | Continues independently |
| Clef JavaScript compilation | Backend design and implementation work | PSG → Alex → JSHIR/JSIR with preservation evidence |
| Bindings | Xantham analysis and F# generation | A Clef consumer plus matching boundary and lowering metadata |
| Bundling and deployment | Selected per application and host | Selected per emitted imports and deployment contract |

Self-hosting Composer is outside this transition. The intended destination is a supported Clef JavaScript backend alongside the F# ecosystem, with dependencies and conformance claims stated precisely.

## The Transition Arc as Waypoints

These are acceptance milestones rather than a release calendar. Upstreaming JSIR into MLIR is not a prerequisite for a pinned, out-of-tree experiment.

### Waypoint 1: The Working Reference Path

**State.** F# libraries and applications compile through Fable. FSharp.CloudEdge and BAREWire provide useful API and runtime test material. Composer's JSIR path remains proposed work.

**What this gives us.** A corpus of concrete behaviors: property access, class imports, callbacks, optional values, asynchronous calls and exact bytes. A passing corpus is evidence for those cases, not a declaration that all bindings are correct.

**Next gate.** Pin tools and inputs, lift a small Worker-shaped program, regenerate source and run its original assertions. Record unsupported constructs and discrepancies against the API contract.

### Waypoint 2: A Reproducible Reference Corpus

**State to earn.** Binding regeneration, compiler execution and runtime assertions are reproducible for a documented set of packages and shapes. Cases include negative and boundary behavior, not only successful compilation.

**What this gives us.** An executable reference that can expose a changed import, lost argument or incorrect receiver. A discovered defect in the reference is corrected against the host and source-language contracts, not adopted as the desired behavior.

**Next gate.** Select one Clef operation and give its lowering an explicit contract and premises. Use the [shared acceptance sequence](../jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact), without treating a textual match as success.

### Waypoint 3: One Supported JSIR Lowering

**State to earn.** A simple Clef operation reaches JavaScript through Composer, with an identified source obligation, a target operation and preservation or re-check evidence at the affected edges.

**What this gives us.** A bounded supported subset, backed by structural checks and execution tests. It does not yet confer the entire JavaScript Substrate profile.

**Next gate.** Extend to foundational binding operations, such as a Promise-returning call, a class method and an inbound callback. Add narrowing and typed failure handling together with the successful path.

### Waypoint 4: First Clef-Native Bindings

**State to earn.** A Clef binding generator consumes declaration analysis and produces matching boundary metadata and lowering rules for a selected SDK surface. F# generation continues independently.

**What this gives us.** Two implementations that can be compared for the agreed observable contract. BAREWire byte compatibility is tested and connected to the common encoding obligations; it is not inferred solely from their shared origin.

**Next gate.** Grow the shape inventory through additional libraries and operation families, retaining explicit unknowns and package provenance. A `.d.ts` signature alone cannot supply a library's effect, persistence or lifecycle semantics.

### Waypoint 5: A Supported Clef JavaScript Backend

**State to earn.** Composer supports the documented target and operation set, and satisfies the full requirements of any profile it claims. Lowering evidence and runtime gates cover the emitted artifacts and dependency closure.

**What this gives us.** Clef and F# programs can coexist on the same hosts. Fable remains the F# route; Composer owns the Clef route. They can share BAREWire endpoint contracts without embedding type or proof metadata in final payloads.

**Next gate.** Expand through measured demand. New rules, SDKs and host features bring their own acceptance obligations; a mature library of rules does not make arbitrary JavaScript behavior decidable.

## What Each Path Owns at Maturity

- **Fable** owns its F# translation and runtime representation choices.
- **Composer** owns Clef's semantics, narrowing and preservation through its target lowerings; **JSIR** supplies the JavaScript IR infrastructure it uses.
- **Xantham** supplies declaration analysis to binding consumers; declarations remain claims about foreign interfaces.
- **BAREWire** connects memory, IPC and network contracts through explicit representations and untagged final payloads.
- **Conclave** supplies the Cloudflare platform design; **Cloudflare and shipped libraries** supply the external implementations and host contracts on which execution depends.

## Fable Output as an Executable Reference

A TypeScript declaration, Fable output and Composer output have different roles. The declaration describes the foreign surface. The Fable program is one implementation of an application using it. The Composer program would be another. Correctness is agreement with the source and boundary semantics over admitted inputs, not equality of emitted JavaScript text.

For a supported operation, compare the observations that matter: results and exact bytes, errors and rejected inputs, mutations and object identity, receiver binding, property reads with effects, callback arguments, and promise completion order. Numeric cases need their selected contract, including wide integers, rounding, `NaN` and signed zero where observable. A test need not cover every category, but the chosen contract must say which it covers.

Lifting both outputs to JSHIR and normalizing can make a difference easier to inspect. It does not establish semantic equivalence by itself. Normalization must preserve evaluation order, effects, scope and the observations under comparison. If one implementation erases an `Option` and the other reifies it, an explicit representation relation must distinguish `None`, `Some` of an absence-like host value and nested options. Collapsing them in a comparison would hide a bug. The [option representation specification](/spec/draft/option-operations-representation/) governs the proposed Clef path.

This is familiar compiler engineering: a reference corpus supplies examples and regression gates, while the semantic contract and preservation argument explain what correctness means beyond those examples. The js_of_ocaml and Fable precedents give us useful implementations to study. Every binding tested today can deliver value now and sharpen tomorrow's lowering contract.

## Cross-references

- [JSIR: JavaScript as an MLIR Backend](../jsir-javascript-as-mlir-backend/): architecture, pinned upstream scope and the worked acceptance sequence
- [Fully Informed Bindings](../fully-informed-bindings/): declared interfaces, implementation analysis and bounded comparison
- [Design-Time Specification for Runtime Reliability](../design-time-spec-runtime-reliability/): current checks and proposed preservation work
- [TypeScript Binding via Xantham](../../interop/typescript-binding-via-xantham/): declaration analysis
- [Library Binding for C/C++](../../interop/library-binding/): the native binding counterpart
- [Atelier Transcribe](https://github.com/speakeztech/Atelier/blob/main/docs/10_transcribe.md): the proposed design-time ingestion layer
