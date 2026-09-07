---
title: "The Foreign Pair"
linkTitle: "The Foreign Pair"
description: "Clef admits no obj and no null, and the JavaScript target changes neither: a census of what obj actually does in the F# bindings, the two foreign types that replace it, the two formal properties that make the boundary a checkpoint instead of a trap door, and the absence story that needs no new types at all."
date: 2026-08-23
authors: ["Houston Haynes"]
tags: ["Type Systems", "Interop", "JavaScript", "Design"]
weight: 22
---

Clef has no `obj`. There is no universal root in the Native Type Universe, no implicit boxing, no runtime reflection over arbitrary values, and the omission is a founding decision: the language takes its lineage from the ML family with the object layer deliberately left out, multi-paradigm but a concurrency language first. JavaScript is the target that stresses that decision hardest, because the platform's values are dynamic, its APIs traffic in loose bags and unions, and the F# path that precedes us leans on `obj` throughout its interop layer. This page establishes, with a measurement, that the pressure is survivable without a universal type, and states the tailored admission that takes its place: two foreign types, `JsValue` and `JsRef<'T>`, governed by two formal properties that keep them from infecting anything inland. It closes with the sibling exclusion, `null`, which the boundary handles with no new types at all.

## The Census

The measurement comes from the Fidelity.CloudEdge bindings, the F# predecessor whose API shapes Clef's Cloudflare library preserves. Across the hand-shaped Runtime and Core projects plus the Xantham-generated Worker context, `obj` appears 529 times. 425 of those occurrences, eighty percent, are concentrated in a single generated file, the transliteration of `@cloudflare/workers-types`. The hand-written bindings carry roughly fifty type-level uses in total, and six of the apparent hits are a local variable whose identifier happens to be `obj`. The machinery that would signal real dependence on dynamism is close to absent: zero dynamic casts, zero dynamic property access, four `createObj` calls, two `jsOptions` calls across the runtime surface.

The reading of those numbers: `obj` in this corpus is not architecture. It is the faithful transliteration of TypeScript's own dynamism, concentrated where the generator wrote it, plus one artifact of the CLR that Clef does not inherit. The hand-written code already lives essentially without it.

## Six Roles, Six Dispositions

Every census occurrence falls into one of six roles, and each role has a disposition that needs no universal type.

| Role | Census example | Clef disposition |
|---|---|---|
| TypeScript `any`/`unknown` positions | `reportError(error: obj)`, `console.log(data: obj[])` | `JsValue`, eliminated only by narrowing |
| Opaque handles, deliberate placeholders | `type AgentContext = obj`, the timer handle in `U2<float, obj>` | `JsRef<'T>`, held and passed back, never opened |
| TypeScript unions | the 636 `U2`/`U3` sites | erased unions owned by the binding layer |
| CLR top-bounds on generics | `'Type :> obj` constraints | unbounded quantification; the role vanishes |
| Options bags | `?init: obj`, `U2<obj, Headers>` | generated nominal records with `Option` fields |
| Error channels, property bags | `error: obj`, `Item: string -> obj` | `JsValue` narrowed to declared shapes |

The generic top-bound row exists only because every F# type parameter is implicitly bounded by the CLR's root, so that class disappears in Clef by construction, with nothing to replace. And the options-bag row is [the binding factory's](/docs/design/javascript-targeting/fully-informed-bindings/) work, not the type system's: each API's bag becomes a concrete generated record, which is dynamism absorbed by generation, in keeping with the framework's standing position that expressiveness is an indulgence when generation will serve.

## The Standing Art

The two-type answer has thirty years of standing art behind it, and the art locates `obj`'s danger precisely. The danger is subsumption, the implicit conversion that lets any value be treated as the universal type without a mark in the source, and not dynamism itself, the mere existence of values whose type is unknown until runtime. Abadi, Cardelli, Pierce, and Plotkin's [dynamic typing in a statically typed language](https://dl.acm.org/doi/10.1145/103135.103138) gives the founding form: `Dynamic` as a type, never a top. Injection is explicit, elimination is `typecase`, and no subsumption exists, so nothing becomes `Dynamic` silently and a `Dynamic` is usable as nothing until narrowed. `JsValue` is that construction with schema-directed narrowing as its `typecase`. Matthews and Findler's [multi-language semantics](https://dl.acm.org/doi/10.1145/1190216.1190220) supplies the second type and the vocabulary for the pair: their lump embedding, a foreign value carried opaquely and returned unopened, is `JsRef<'T>`, and their natural embedding, conversion at the boundary, is the narrowing path. For higher-order crossings, callbacks entering Clef from SDK internals, the [contract-and-blame discipline](https://doi.org/10.1007/978-3-642-00590-9_1) governs, with binding-declared signatures and narrowing at entry.

TypeScript itself ran the controlled experiment on the failure mode. Its `any` participates in subtyping in both directions and so propagates through every operation it touches. Its later repair, `unknown`, admits no operations until narrowed and does not spread. The design law that falls out is the one this page rests on: **infection is a property of subsumption, not of dynamism.** A dynamic type spreads when values enter it implicitly and leave it unchecked. Deny it subtyping edges and it cannot spread at all.

The neighboring ecosystems mark the alternatives. js_of_ocaml kept `Js.Unsafe` as its escape hatch and ReScript kept its own, both policed by review convention. Elm held the hard line with ports alone, workable because its platform surface stayed small. Clef's position is available to neither: the binding factory makes the hard line affordable at the scale of the Cloudflare surface by generating the typed layer, and the grade discipline below makes whatever residue remains measurable.

## The Checkpoint Regime

Four properties separate a checkpoint from a trap door, and the pair is designed against all four. A trap door is implicit: values enter it by subsumption, with no mark in the source. Injection into `JsValue` and `JsRef` is explicit and occurs only in boundary functions the bindings declare. A trap door is ubiquitous: `obj` is reachable from every expression in a CLR language. The pair is local to the interop layer, and no inland API mentions it. A trap door is unchecked on exit: a downcast from `obj` is a runtime assertion anywhere in the program. The pair's only elimination is narrowing, which returns `Result` with the failed premise identified, the [constructed witness](/docs/design/javascript-targeting/constructed-witnesses/) discipline applied to values. And a trap door is untracked: nothing in a signature reveals that `obj` passed through a function. The pair is graded.

## The Boundary Grade

The proposed boundary grade makes foreign contact explicit in the compiler's model. `JsValue` and `JsRef<'T>` would have no subsumption edges into ordinary NTU kinds, and boundary operations would contribute a tracked coeffect. A zero grade establishes absence of modeled foreign contact only when all reachable calls have sound summaries and unknown effects cannot disappear through composition or cancellation. This grade discipline and its JavaScript lowering remain design work; a computed label alone does not prove the behavior of an imported implementation. The [contract-to-artifact path](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) states the separate obligations.

## Absence at the Boundary

Clef refuses one other value family, and the refusal needs less machinery than `obj` did. There is no `null` in the language: absence is structure, an `Option` case or a union case, so there is nothing to lower. Null and undefined may appear in emitted JavaScript only as boundary representations selected by generated code, the way a C sentinel appears only inside a [Farscape](/docs/design/interop/library-binding/) marshaling stub. The C precedent carries the design intact: the generated wrapper layer converts sentinels to `Option` and error codes to `Result`, the flat closure gives every crossing value an explicit slot, and the conversion's totality is checkable because the layout is explicit, with no hidden channel through which a sentinel could reach inland code.

JavaScript raises the difficulty from C's one-state absence to a three-state alphabet: a property can be absent, present holding `undefined`, or present holding `null`, and the platform assigns meanings to the differences. KV's `get` returns null on a miss, Durable Object storage returns undefined on one, D1 carries SQL NULL as null, a TypeScript optional marked `?` is possibly-undefined while `| null` is declared separately, and JSON admits null with no undefined at all.

Inbound, the [JavaScript boundary specification](/spec/draft/javascript-boundary/#5-absence) defines the default: a declared `Option<'T>` maps absent, undefined, and null to `None`, and narrows other values into `Some`. Where a binding declares a distinction, the generated binding preserves it with a three-case union, such as keep, clear, and set for merge-patch semantics. Those behavioral meanings belong to the declared API contract; optional and nullable syntax alone does not describe an implementation's update behavior. Outbound generation chooses omission, null, or undefined from the declared surface for each position.

Away from the boundary, `Option` representation remains an emission decision. Erasure needs a relation showing that all admitted uses preserve observable behavior, including nested options and interaction with JavaScript null/undefined values. Reification is required where the admitted erasure cannot preserve those distinctions. The conformance harness may normalize only differences justified by that relation; normalized JSHIR equality does not supply the relation itself.

Set the two exclusions side by side. `obj` required two foreign types and a grade, because something genuinely foreign had to be held. Absence requires zero new types, since `Option` and generated unions carry it inland natively. The hazards differ in kind, infection for dynamism and lossy collapse for absence, and so do the cures: subsumption denied by structure for the first, generation informed by declared and measured distinctions for the second.

## Below the Surface

Fable has its own intermediate representation and compiles the F# language and its interop conventions; it is not limited to being a library-level AST wrapper. Composer's proposed distinction is ownership of Clef's source kinds, boundary declarations, and witnessing rules through its own pipeline. Generated records and explicit foreign handles can reduce universal-object-shaped bindings without proving the imported JavaScript implementation. The intended vocabulary stays small and explicit, with accepted narrowing and effect obligations retained on the PSG until their lowering role is fulfilled.
