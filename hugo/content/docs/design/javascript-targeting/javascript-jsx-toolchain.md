---
title: "JavaScript, JSX and the WREN Toolchain"
linkTitle: "JS / JSX Toolchain"
description: "Where Clef lowering, JSIR, Babel, Solid and Vite meet, and how WREN can share its existing frontend toolchain with a future Clef JSX producer."
date: 2026-09-15
tags: ["Architecture", "JavaScript", "JSX", "WREN", "Design"]
weight: 20
---

**JSX gives Clef a structured handoff to the Solid compiler.** This is particularly useful for WREN desktop applications and Atelier's editor-style interface: Clef can express view and interaction intent, while Solid realizes DOM construction and reactive updates. Ordinary JavaScript remains the target for computation, libraries and hosted services.

**Status, September 2026.** WrenHello already uses F#/Partas.Solid → Fable → JSX → Solid/Vite → embedded HTML, with a Composer-compiled native host. A Clef frontend would be an additional producer feeding that downstream toolchain. Clef-to-JSX lowering and the necessary JSIR extension are proposed work; the existing application does not establish a completed Composer JavaScript backend.

## Three Responsibilities

| Responsibility | Owner in the proposed WREN path |
|---|---|
| Realize Clef semantics and preserve view intent | Composer, through its semantic graph, portable witnessing and target lowering |
| Turn Solid-compatible JSX into DOM/reactive JavaScript | Solid's compiler, implemented through Babel plugins and `babel-preset-solid` |
| Coordinate transforms and package frontend assets | Vite, using `vite-plugin-solid` and the selected asset/bundle configuration |

The native host then embeds or serves those assets, and the system WebView executes them. Calling Vite plus Solid the **downstream frontend toolchain** is useful: it identifies work that Clef can reuse while keeping Composer's semantic responsibilities explicit. The [Solid Vite integration](https://github.com/solidjs/solid-vite-plugin) connects the transform to the build.

## Babel and Bazel

Babel is an independent open-source toolkit for JavaScript syntax trees: parsing, traversing, transforming and printing them. Its parser already accepts JSX when enabled, its AST definitions already include JSX nodes, and its generator can print them. Babel AST is derived from ESTree with documented differences; native bridges must map the representation they actually use. [Babel parser](https://babeljs.io/docs/babel-parser#output), [JSX node definitions](https://github.com/babel/babel/blob/main/packages/babel-types/src/definitions/jsx.ts), [generator](https://babeljs.io/docs/babel-generator).

There are two limited Babel uses here:

1. **JSIR source generation:** convert the native AST representation to Babel AST and print JavaScript, or JSX after the bridge is extended. Printing JSX leaves its framework transformation for the next stage.
2. **Solid compilation:** run Solid's Babel transformation to consume that JSX and produce JavaScript using Solid's DOM/reactive conventions. A React JSX transform would select a different implementation contract.

JSIR embeds a Babel payload and executes it through QuickJS. That does not provide a full Babel source checkout or install Solid's compiler. Its [bridge source](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/js/quickjs_babel/native.js) shows the parser, traversal and generator calls.

**Bazel** is a separate build orchestrator used by JSIR. It can build the native compiler tools and could coordinate application stages if selected. It does not transform JSX, and WREN need not adopt it merely because JSIR uses it. **Bun** can contribute JavaScript analysis, execution and build facilities, but substituting it for any stage requires checking that stage's contract, especially Solid's transformation and the actual WebView runtime.

## Where JSX Is Missing

The missing representation is in **JSIR's native AST/IR bridge**, not Babel. At reviewed upstream revision [`d5322bd`](https://github.com/google/jsir/tree/d5322bda6e1311357ead5e20376e28461c8cbc2a), the native AST classes and JSHIR operation definitions do not model JSX elements, fragments, attributes or expression containers. Enabling JSX parsing in Babel alone cannot carry them through that bridge.

Extending the bridge is a concrete candidate path:

```text
Clef source and UI library contracts
  → CCS / PSG → Baker → Alex's portable MLIR
  → JavaScript/JSX realization with retained PSG/codata
  → extended JSHIR → native AST → Babel JSX AST → .jsx
  → Solid compiler through Vite → bundled JavaScript / CSS / HTML
  → native embedding → WebView
```

Alex's witnessed vocabulary remains `func`, `scf`, `arith`, `memref` and `index`. JSHIR is itself an MLIR dialect below that boundary. UI identities, captures, effects and reactive intent must remain available until target realization; JSX does not require a new Clef semantic dialect in Alex.

The extension must cover both operations and conversion machinery. JSIR's [AST generator](https://github.com/google/jsir/blob/d5322bda6e1311357ead5e20376e28461c8cbc2a/maldoca/astgen/ast_gen_main.cc) produces native AST, JSON conversion, visitors, TableGen and AST/IR conversion code from a schema. The reviewed public tree lacks the JavaScript `ast_def.textproto` named in its example, although test schemas are present. Reproducible extension therefore starts by obtaining or reconstructing that input, or defining a maintained extension mechanism; editing generated TableGen alone is insufficient.

The driver also needs qualification. `jsir2ast,ast2source` are reverse conversion names, but the current CLI starts from a JavaScript-source representation. They do not establish a reverse-only command accepting an MLIR file. Composer needs the conversion APIs or a JSHIR-input driver. The [JSIR chapter](../jsir-javascript-as-mlir-backend/#jsirs-design) records the checked revision and this distinction.

A structured Babel-AST exporter after portable witnessing remains another possible implementation choice. It would owe the same semantic and reactive preservation work. The proposed JSHIR extension offers a common MLIR analysis surface; AST emission itself does not inherently bypass Alex.

## Preserve the Reactive Read

The [UI reconsideration of 18 September 2026]({{< relref "/docs/design/user-interfaces" >}}) favors quiet functional composition over shared semantic operations, with optional computation-expression surfaces. The native undertaking is a new reactive-area update engine; WREN would realize the same view, binding, identity and lifetime contracts through DOM/Solid. Elmish can supply pure state transitions where useful without defining the rendering algorithm. Cold descriptions defer setup and effects until an owned mount establishes demand; demanded bindings or pure area computations then update affected content. The adapter must preserve that activation boundary even when its target library exposes immediately activating constructors. This shared model still needs its own library and lowering support; Fable's Partas plugin does not automatically become a Composer plugin.

For example:

```jsx
const view = <span>{total()}</span>;
```

The call's position allows Solid to arrange a tracked update. Hoisting `total()` into a plain snapshot before constructing the view can lose that behavior. Similar obligations apply to deferred props, ordered spreads, event handlers and cleanup. Valid JSX alone is insufficient. Solid's [JSX explanation](https://docs.solidjs.com/concepts/understanding-jsx) describes its compilation and reactive role.

MLIR regions can retain embedded expression structure, but JSX braces introduce no new JavaScript lexical scope. Outer captures must remain valid. A region does not by itself prove tracking, ordering or safe movement of expressions.

This is JSX's strongest contribution here: preserving a form on which the framework compiler performs meaningful transformations. Readability and reference examples help too. JSX supplies neither Clef's number and Option semantics nor its proof obligations.

## Bundled Pages and Floating Views

WrenHello's [Vite configuration](https://forge.spkez.dev/FidelityFramework/WRENHello/src/branch/main/vite.config.js) produces one self-contained HTML file: scripts and styles are inlined, CSS splitting is disabled and dynamic imports are inlined. Its [weld script](https://forge.spkez.dev/FidelityFramework/WRENHello/src/branch/main/scripts/weld.js) freezes that file into a native source literal. The build is currently scripted in the [package commands](https://forge.spkez.dev/FidelityFramework/WRENHello/src/branch/main/package.json).

Several authored pages can live in that one document, with a persistent shell and smooth transitions between sections. Source organization does not force full document navigation. Conversely, packaging several HTML documents together does not preserve their DOM or JavaScript state when navigating between them. Routing, transitions and state retention need an explicit interaction design.

[Atelier's multi-WebView design](https://forge.spkez.dev/FidelityFramework/Atelier/src/branch/main/docs/04_multi_webview.md) motivates a later asset model. Vite can build [multiple HTML entries](https://vite.dev/guide/build#multi-page-app), but native embedding would then need an asset collection, entry/chunk manifest and resource resolver. Shared bytes do not create a shared JavaScript heap, and local assets still incur initialization and rendering costs.

Docked panels are a UI/runtime concern. Separate native WebView inspectors additionally need window creation, host messaging and explicit lifetime ownership. The workload, its observation subscription and its view should have separate lifetimes: closing an inspector normally stops observation without terminating the inspected activity. Reopening can obtain a snapshot and further updates. This can be a reusable WREN capability for Atelier and standalone diagnostic applications. Renderer-process isolation is a separate, platform-dependent property.

## Carrying Proofs Through This Toolchain

This framing fits [Carrying Proofs into JavaScript](/blog/carrying-proofs-into-javascript/). The property to preserve concerns program behavior; compiler metadata need not appear in the final application payload. JSX adds a transformation edge at which preservation must be established or affected properties re-checked.

For a Solid frontend, the argument extends through the Solid transform, bundling and embedding to the **final assets and shipped dependency closure**. Record tool versions and configuration, source/obligation identities and host assumptions. Source maps and artifact hashes support provenance; they do not prove behavior. The embedded Solid runtime remains a dependency.

Fable's output supplies an executable reference over a declared shared contract. Compare JSX structure before the framework transform, supported JavaScript after it, and actual behavior in the final WebView. Two producers sharing the same Solid transform can share its failures; agreeing outputs are not independent proof of that transform. WrenHello's [native UI acceptance gate](https://forge.spkez.dev/FidelityFramework/WRENHello/src/branch/main/tests/native-ui/README.md) supplies bounded cases for its existing path, including real button updates, rejected messages and shutdown. Rebuild the frontend and weld before using the gate to validate a changed frontend.

The next milestone is a small Clef view with a changing value and an event callback that survives this complete path, with mutations exposing lost tracking or duplicated dispatch. That would establish one supported UI lowering while ordinary JavaScript, broader UI coverage and future multiwindow hosting earn their own evidence.
