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

With the grade, the non-infection guarantee is structural instead of conventional. `JsValue` and `JsRef<'T>` participate in no subtyping relationships in the NTU: they are foreign types, not tops or bottoms, so no subsumption edge exists for dynamism to travel along. Contact with them is a capability coeffect, carried in the signature of any function that touches the pair and composed through the same abelian substrate that already carries dimensions, placement, and the other capability facts. A module whose boundary grade is zero is proven free of foreign contact, and a project can require grade-zero everywhere but its designated interop layer. This is the contrast with the escape-hatch tradition worth stating plainly: an `unsafe` block is policed by convention and review, while a boundary grade is computed by the compiler and visible in every type it touches. The loosening the JavaScript target demands turns out not to be a loosening of the language at all. It is a permission with a paper trail.

## Absence at the Boundary

Clef refuses one other value family, and the refusal needs less machinery than `obj` did. There is no `null` in the language: absence is structure, an `Option` case or a union case, so there is nothing to lower. Null and undefined may appear in emitted JavaScript only as boundary representations selected by generated code, the way a C sentinel appears only inside a [Farscape](/docs/design/interop/library-binding/) marshaling stub. The C precedent carries the design intact: the generated wrapper layer converts sentinels to `Option` and error codes to `Result`, the flat closure gives every crossing value an explicit slot, and the conversion's totality is checkable because the layout is explicit, with no hidden channel through which a sentinel could reach inland code.

JavaScript raises the difficulty from C's one-state absence to a three-state alphabet: a property can be absent, present holding `undefined`, or present holding `null`, and the platform assigns meanings to the differences. KV's `get` returns null on a miss, Durable Object storage returns undefined on one, D1 carries SQL NULL as null, a TypeScript optional marked `?` is possibly-undefined while `| null` is declared separately, and JSON admits null with no undefined at all.

Inbound, absence is a narrowing outcome. The default maps all three states to `None` for a declared `Option<'T>`, per the [narrowing rules](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#boundary-1-type-erasure), and the default is wrong in exactly one situation: an API whose semantics distinguish the states, with JSON merge-patch as the canonical case, null meaning clear and absence meaning keep. There the binding generator emits a three-case union for the site, `Keep`, `Clear`, and `Set of 'T` in shape, selected from the declared distinction between `| null` and optional, and the [disagreement channel](/docs/design/javascript-targeting/fully-informed-bindings/) flags an SDK body that treats the states differently than its declaration admits. Outbound, `None` has no single lowering: the witnessing rule selects an omitted key, null, or undefined per site, which is representation selection applied to absence.

Away from the boundary, the open decision is the representation of `Option` inside emitted code. Fable erases by convention, `None` as undefined, with documented edge cases at nested options. Composer would erase by proof: erased where the middle-end proves nesting cannot occur, reified where it can, so null and undefined appear in the artifact only where a rule or a proven-safe erasure selected them. The conformance harness inherits one duty from this: option-representation normalization joins the differences the JSHIR comparison treats as equivalent.

Set the two exclusions side by side. `obj` required two foreign types and a grade, because something genuinely foreign had to be held. Absence requires zero new types, since `Option` and generated unions carry it inland natively. The hazards differ in kind, infection for dynamism and lossy collapse for absence, and so do the cures: subsumption denied by structure for the first, generation informed by declared and measured distinctions for the second.

## Below the Surface

The architectural root of the difference with the F# path: Fable needs `obj` liberally because Fable is a library-level compiler, and a library can only express interop in the host language's own types, so the host language must contain an escape valve. Composer owns the pipeline end to end, which moves the dynamic manipulation below the type surface, into witnessing rules and JSIR ops where no Clef type denotes it. Under that split, the census's 425 generated occurrences mostly dissolve instead of translating: the top-bounds vanish, the unions become erased binding constructs, the handles become lumps, the bags become generated records, and the honest `JsValue` count that remains is the set of positions where TypeScript itself wrote `any` and meant it. The constraint holds, the language stays closed, and the boundary gets a vocabulary that is small, foreign, explicit, and graded: two types, two properties, and no trap doors behind them.
