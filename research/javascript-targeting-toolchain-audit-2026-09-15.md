# JavaScript targeting: toolchain framing audit

**Reviewed September 15, 2026.** Scope: the JavaScript Targeting design section, its Composer implementation notes and a light WrenHello documentation check. This reviews architecture, tool responsibilities and implementation status; it does not re-prove the numerical arguments or requalify every external host API in the section.

## Result

Add a dedicated [JS / JSX toolchain chapter](../hugo/content/docs/design/javascript-targeting/javascript-jsx-toolchain.md). Keep the existing JSIR architecture and proof chapters: JSX supplies a framework-specific handoff within the JavaScript target, while their computation and preservation obligations still apply.

The central framing is:

```text
Clef semantic lowering and retained UI intent
  → proposed JSHIR JSX extension and native Babel-AST bridge
  → Babel prints JSX
  → Solid's Babel-based compiler consumes JSX
  → Vite bundles assets
  → WREN embeds them; the WebView executes them
```

WrenHello already exercises the downstream path with F#/Partas.Solid and Fable as the producer. Composer currently compiles its native host. That is reusable infrastructure and concrete reference behavior, not evidence that Clef's JavaScript/JSX lowering is complete.

## Findings and disposition

| Finding | Disposition |
|---|---|
| The section lacked a clear home for JSX, Babel, Solid, Vite and native embedding. | Added the toolchain chapter and linked it from the section index, JSIR, transition, bindings and reliability pages. |
| The transition page's title and description implied that Composer was replacing its Fable backend and npm bundlers. | Reframed it as Fable and Composer serving F# and Clef, with potentially shared Solid/Vite stages. Kept its URL for existing links. |
| JSX could be mistaken for missing Babel functionality. | Located the missing representation in JSIR's native AST/IR bridge. Distinguished Babel AST from strict ESTree, JSX printing from framework transformation, and Babel from Bazel. |
| An older JSIR pin and reverse pass names could imply a ready MLIR-input CLI emitter. | Inspected upstream `d5322bda6e1311357ead5e20376e28461c8cbc2a`; updated pins and documented source-initialized CLI input versus conversion APIs or a new driver. |
| Extending TableGen alone could be presented as the complete JSX implementation. | Documented generated AST/IR/JSON/conversion machinery and the absent JavaScript schema input in the reviewed public tree. Added regeneration and semantic acceptance gates. |
| AST emission could be confused with inherently bypassing Alex. | Distinguished the historical bypass proposal from a structured exporter after portable witnessing. Kept Alex's five-dialect boundary explicit. |
| Proof claims could stop at JSX or normalized IR. | Extended the stated obligations through Solid, bundling and embedding to final assets and dependencies; added reactive-read, event and disposal cases and shared-oracle caveats. |
| Whole-library recovery was declared outside the roadmap despite newer Composer notes. | Aligned the bindings and JSIR pages with owned Clef SDK/supporting-library development through bounded acceptance and deferred inference. Retained versioned complete package graphs independently of artifact reachability. No automatic general translation or completed integration is claimed. |
| Older Composer WebView guides described `composer build` orchestration as implemented and Fable as a Clef compiler. | Marked them as historical proposals, corrected source-language attribution and linked current executable WrenHello instructions and the new Composer guide. |
| WrenHello abbreviated Fable → JSX → Solid as Fable → JavaScript. | Light README update: named the Solid/Babel transform, preserved the current build commands and pins, and added a short future-Clef/bundling note. Clarified that its native UI gate consumes the existing embedded page. |

## Section inventory

| Page | Review outcome |
|---|---|
| `_index.md` | Added the toolchain entry and existing/proposed distinction. |
| `jsir-javascript-as-mlir-backend.md` | Updated upstream pin/driver facts, portable boundary, JSX handoff and final-artifact scope. Retained its computation, carrier and proof discussion. |
| `javascript-jsx-toolchain.md` | New focused chapter for compiler roles, missing bridge support, reactive semantics, WREN packaging and future Atelier views. |
| `from-fable-to-jsir.md` | Corrected title/description, shared downstream stages, driver claims and witnessing ownership; preserved the URL. |
| `fully-informed-bindings.md` | Aligned fused analysis/deferred recovery direction, clarified Fable's reference role and added the JSX comparison boundary. |
| `design-time-spec-runtime-reliability.md` | Added UI-toolchain preservation through final embedded assets. |
| `the-foreign-pair.md` | No JSX-specific change needed. Foreign values, narrowing, absence and effects remain obligations for the UI target too. |
| `cloudflare-agents-and-the-boundary-map.md` | No JSX-specific change needed. Worker/Agent entry and lifecycle contracts remain separate from frontend framework compilation. |
| `streaming-inference-through-the-actor-pipeline.md` | No toolchain change needed. Transport, frame and correlation contracts do not depend on the UI authoring syntax. |
| `the-ledger-lowering.md` | No toolchain change needed. Retained observations and recovery remain stated design work. |
| `constructed-witnesses.md` | No toolchain change needed. Existing preserve-or-recheck discipline applies to the additional transformations. |
| `proof-preservation-across-actors-and-workflows.md` | No JSX-specific change needed. Numerical, continuation and join obligations remain applicable independently of view compilation. |

## Companion notes and implementation boundary

Composer's [new canonical toolchain guide](../../Composer/docs/javascript-targeting/10_jsx_and_webview_toolchain.md) contains the extension and acceptance details. Its overview, tooling, source-path, deployment-context and validation chapters now point to that guide. Existing uncommitted additions to the versioned dependency-graph design were preserved. The matching notes live in Composer; this review did not add a competing backend design to the Clef source repository.

[WrenHello's README](../../WrenHello/README.md) remains the executable build reference. Its scripts, `vite.config.js`, weld and native UI gate were inspected. The review changed documentation only; it did not regenerate its frontend, update packages or run GUI acceptance. Its current single-file configuration is not an implemented router, multi-entry asset system or multiwindow shell.

Atelier's multi-WebView design motivates future asset and lifetime contracts. This review distinguishes a document, native window and renderer process: a separate WebView does not guarantee a separate process on every platform. Atelier's broader per-view process-isolation claims still require platform qualification. Its design documents were not rewritten as part of this task.

The older Composer WebView guides retain illustrative historical APIs under explicit proposal status. Their snippets are not promoted to current implementation evidence. WrenHello's introductory performance comparison also remains outside this tooling-framing review; this work supplies no new benchmark measurements.

## Source checks

- Upstream JSIR was checked independently of the old local fork: CLI pass names and source input initialization, conversion APIs, native AST/IR definitions, embedded Babel bridge, AST generator/schema inventory, placeholder `JsirAny`, AST-conversion verification and disabled transformation-runner verification. Source links are pinned in the updated chapters.
- Babel's official parser/generator documentation and monorepo JSX definitions establish existing JSX support and Babel AST differences.
- Solid's Vite integration and WrenHello's actual configuration establish the downstream framework transform. Vite's build documentation establishes the multiple-HTML-entry capability; native resource resolution remains application work.
- New WrenHello and Atelier source links use their reachable Forge repositories rather than stale GitHub locations.

## Validation

- Hugo production build passed: 467 pages, output directed to a temporary directory. Existing configuration/theme deprecation and math font-metric warnings were reported.
- New/changed relative links were checked against local files and rendered page anchors; added external source URLs were checked for reachability.
- `git diff --check` passed in Composer, clef-lang-site and WrenHello. Existing dependency-graph additions were checked against a pre-edit snapshot.
- No deployment, commit, compiler implementation or runtime conformance claim is part of this documentation update.
