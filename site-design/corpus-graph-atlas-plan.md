# Corpus-Graph Atlas — Staged Implementation Plan

Status: **design approved, Stage 1 in progress.** Not yet built.
Origin: design workflow `wf_90aacb8c-3bf` (6 grounded probes + adversarial judging + synthesis).

## Concept

A Cytoscape.js navigable graph of the whole corpus, on a dedicated `/atlas` page linked
prominently from the front page. Three concentric layers: **spec = inner kernel** (canonical),
**docs = middle ring** (exposition), **blog = outer ring** (sparse nodes, dense links — the
discursive surface). Nodes navigable with hover-tile callouts; click navigates to the page.

**Why it exists (the epistemic frame):** the framework's thesis is that a hypergraph is the
right representation of a program (PSG/PHG). Rendering the corpus *as* a navigable graph is the
corpus practicing what it preaches — it demonstrates command of the concept. To *not* show a
graph of a body of work that argues graphs are the correct structure would read as odd by
omission to a reader who knows the PHG papers. The graph must therefore be **credible**: an
87%-disconnected kernel would refute the very claim. Honest real-link density is the substance;
the rendering is the lens.

**Hosting:** the graph is *another index* alongside the existing Vectorize + D1 FTS5 search
stack — "the same fabric that answers your search query also holds the graph of how these ideas
connect." Explicit href edges live in a new D1 table; semantic-similarity edges are computed
offline from the existing Vectorize index and stored. A worker endpoint serves the merged graph.

## Current corpus graph (extracted this session, `scratchpad/linkgraph.json`)

216 nodes (spec 54, docs 109, blog 53), 465 href edges.
Edge flow: docs→docs 229, blog→docs 112, blog→blog 81, docs→blog 31, **docs→spec 7, blog→spec 4**.
**KEY PROBLEM: 46 of 54 spec entries have ZERO inbound href links** — the inner kernel is 87%
disconnected on hyperlinks alone. Connecting it honestly is the gating work (Stage 1).

## Locked decisions (judge fixes applied)

1. **Nodes are PAGE-grained.** Node id = `page_url` trailing-slash form (e.g.
   `/spec/draft/memory-regions/`). The section-grained `content_sections.id` (`{type}/{slug}#{i}`)
   is never a graph node id. This single decision dissolves the hairball, the id-churn stale-edge
   problem, and the spec-id/page_url divergence at once.
2. **`layer` is derived at node-upsert:** blog→blog, spec→spec, else (design|internals|reference|
   guides) → docs. `?layers=` query param whitelists spec|docs|blog then expands `docs` to the
   four real content_types.
3. **Both edge kinds materialized in D1.** No live Vectorize on the read path (40041 rate limit).
   Semantic edges computed offline by a rarely-run endpoint and stored.
4. **No FK / ON DELETE CASCADE on edges** (D1 doesn't reliably enforce). `target_url` is plain
   TEXT; an `external INTEGER` flag marks targets not in the node set. Stale edges swept explicitly.
5. **Degree recomputed in batch at end of rebuild, HREF-ONLY.** No per-row degree triggers (they
   break under upsert). Semantic edges never inflate degree or node size — the honesty signal
   (46/54 spec nodes with zero real inbound links) must reflect href edges only.
6. **Semantic-edge generation GATED behind the prose cross-link pass shipping first.** Synthetic
   edges render as a visually distinct secondary type and never substitute for a missing href.
7. **The href extractor is NEW.** `Index.fs:66 stripMarkdown` rewrites `[text](url)→text`,
   discarding every URL before D1. `content_sections` has no edge column. A real extractor must be
   built; it does not exist outside the one-off scratchpad script.

## Stages

### Stage 1 — Real prose cross-links FIRST (data quality, owner-curated) — pure build, no deploy
The gating dependency. Worklist `scratchpad/xlink-worklist.json`: 14 concepts / 156 candidate
sites (memory-regions=60, units-of-measure=33, width-inference=17, closure-representation=12,
platform-bindings=8, type-definitions=8, then 4,4,3,2,2,1,1,1). FIND→VERIFY→APPLY: per concept,
find genuine mechanism/argument discussion where the spec concept is the sentence's primary
subject; reject generic-word false positives and incidental mentions; one anchor per concept per
page (anti-inflation); prefer docs/design + docs/internals, blog only when it moves past
assertion to mechanism. **Exclude `_index.md` from source and target** (Index.fs filters
`_`-prefixed files → no node). Thin-tail concepts (platform-predicates, access-kinds, ffi-boundary
= 1 site each) stay honestly unlinked rather than padded. Output: owner-reviewable changelist
(anchor sentence + proposed `[text](/spec/draft/slug/)` + source-layer + per-kernel-node inbound
lift). **Re-derive the kernel-lift estimate from real inputs — the probe's "+11-15 edges" used
wrong numbers.**

### Stage 2 — Href extractor + D1 graph schema (the missing pipeline) — build; owner runs migration + deploys
- **Schema** appended to `workers/search/schema.sql`: `graph_nodes(page_url PK, content_type,
  layer, page_title, tags, summary, published_at, updated_at)` + `graph_edges(source_url,
  target_url, edge_type 'href'|'semantic', weight, label, external, updated_at, PK(source,target,
  type))`. No FK, no degree triggers.
- **Extractor** in `cli/src/Commands/Index.fs`: in `parseAndSplit`, scan the RAW body (before
  stripMarkdown) with `\[([^\]]+)\]\(([^)]+)\)` capturing group 2; resolve targets against the set
  of known `page_url`s from `allSections`; strip `#anchor`; non-node targets → `external=1`. Drive
  node/edge population from the FULL `allSections` set (Index.fs:366), **not** the hash-gated
  `/index` results (which emit nothing for unchanged pages). New step in `Index.execute` after the
  `/reconcile` block (~line 502), POSTing plain-JS payloads (createObj/==>) to the new endpoints.
- **Worker**: new `workers/search/src/Graph.fs` + handlers; routes `POST /graph/rebuild` (auth,
  idempotent full rebuild, refuses empty node set like reconcile), `GET /graph` (CORS, public,
  Cytoscape shape, cached), `GET /graph/stats`. Add `Graph.fs` to the `.fsproj` compile order.

### Stage 3 — Cytoscape contract + "Map" MODAL (peer to Smart Search) — build; owner deploys
**Placement decision (revised):** the atlas is a **modal beside Smart Search**, NOT a dedicated `/atlas`
page. Search and the map are the same act from two directions — query-in vs structure-in — both entry to
the corpus by its connectivity, so they sit as peers. A modal is also lazy-loaded (Cytoscape JS + graph
fetch only on open, exactly like the search modal), giving max prominence (a button on every page) at zero
front-page load cost. This supersedes the `/atlas` page in the Concept section above.
- `GET /graph` returns Cytoscape elements: `nodes[].data{id,layer,contentType,title,summary,tags,
  publishedAt,inboundHref,outboundHref}`, `edges[].data{id,source,target,type,weight,label}`
  (edge id `src->tgt` href vs `src~tgt` semantic so both coexist), plus `stats{nodeCount,
  layerBreakdown,edgeTypeBreakdown,generation=max(indexed_at)}`. `?layers=` and `?ego=<url>` params.
- **Reuse the search-modal shell.** Real artifacts to mirror: `layouts/_partials/search.html` (the
  `hextra-search-wrapper` navbar trigger + `#clef-search-modal` backdrop/container/header markup),
  `assets/js/search.js` (the `clefSearch` object — `open()`/`close()`/Esc handling), `assets/css/custom.css`
  (`.clef-search-modal/.clef-search-backdrop/.clef-search-container`). Add a **Map** button next to the
  search input; new `#clef-map-modal` cloning the search shell; new `clefMap` JS object mirroring
  `clefSearch` but mounting Cytoscape; `fetch('<worker>/graph')` on first open (cache after).
- **Layout: concentric and grouped-by-section are the keepers; force-directed (fcose) is DROPPED** — it
  erases the spec→docs→blog stratification into a hairball. The layout must encode the layering. Offer a
  concentric/grouped toggle. Isolated spec nodes render **visible but faded** (honest-gap signal). Node
  size from inbound href degree; hover-tile from title+summary; click navigates to `page_url`; mobile
  degrades to a grouped list. Prototype lives at `scratchpad/atlas-preview.html` (concentric + grouped
  validated; fcose cut).

### Stage 4 — Semantic edges (gated, offline, page-grained) — build; owner runs manually
New `workers/search/src/SemanticEdges.fs`, `POST /graph/semantic`. **Do NOT mean-pool** — every
vector already carries `page_url` in metadata (Indexing.fs:93). Per section vector, `vectorize.query`
topK (~30-40, over-fetch since `!=` filter unsupported), drop same-page matches BEFORE aggregation,
aggregate to one page→page edge per pair keeping MAX cosine, **threshold ~0.70-0.78** (0.50 hairballs
on this domain-homogeneous corpus), cap 3-5 neighbors per source. Idempotent full recompute. Run
out-of-band, chunked, backoff on 40041, split multi-line `.bind().run()` chains (Fable gotcha).

## Owner-vs-builder split

| Action | Who |
|---|---|
| Stage 1 prose-link changelist | builder drafts → **owner curates/approves** |
| `.md` edits applied | builder (after owner approval) |
| D1 schema migration on `clef-search-dev` | **owner runs** (builder supplies DDL) |
| Worker/CLI F# code, `/atlas` page + front-end | builder |
| Every deploy (worker + Pages) and index rebuild | **owner only** |
| Manual `/graph/semantic` run | **owner** |

## Top risks

1. **Stage-1 honesty deficit (highest).** Too few genuine non-blog anchors → kernel stays sparse →
   graph undercuts the PHG claim. Mitigation: classify by source layer, report distinct-kernel-node
   lift, leave thin-tail honestly unlinked.
2. **URL canonicalization mismatch** (extractor vs stored page_url) → orphan/duplicate nodes.
   Normalize both ends to the exact trailing-slash form classifyContent emits; strip anchors.
3. **Driving population off hash-gated `/index`** reflects only churned pages. Drive from full
   `allSections` every deploy.
4. **Semantic hairball** from low threshold + section grain. Page-grain aggregation, 0.70-0.78, cap.
5. **FK/CASCADE assumption** (won't enforce on D1). No FK; explicit sweep in rebuild.
6. **Vectorize 40041 on `/graph/semantic`** (O(sections)). Out-of-band, chunked, backoff.

## Highest-leverage first step
Run the Stage-1 FIND/VERIFY pass over the 156-site worklist → owner-reviewable changelist. Pure
build, no gate, and its output tells the owner whether the kernel can be connected credibly — which
decides whether the whole atlas is worth shipping. Everything downstream is mechanical once the
corpus links honestly.

## The deeper purpose: the atlas as corpus ontology

Building the atlas well forces the corpus into an explicit **ontology** — a structured account of which
concepts exist in the framework and how they relate. This is the hypergraph thesis (the right
representation of a *program* is a typed graph) raised one level to the *body of knowledge*. The atlas's
deepest value is therefore not navigation or the virtue-signal, but that **it makes the corpus's ontology
inspectable**, and the isolated "floating" nodes are the ontology's report on its own incompleteness.

Each floating node is a prompt — "what is this concept's place in the whole, and where should that
placement be written?" — answered three ways:
1. **Genuine relation, unwritten** → write the connecting prose (grows the ontology; the cross-link worklist).
2. **Correctly peripheral** → it stays loosely connected; that IS its ontological position (FFI-as-boundary).
3. **Chaptering fragment** → the node may be an artifact of how the spec was chaptered, not a distinct
   concept; the meta-update is "should the ontology MERGE it?" — see the deferred spec-coherence audit below.

Render decisions that follow: isolated nodes stay **visible but faded** (the honest gap is diagnostic and
motivates the work). The layout must **encode the epistemic stratification** (spec→docs→blog) — concentric
and grouped-by-section do this and are the keepers; plain force-directed (fcose) erases the layering into a
hairball and is dropped.

## Deferred: spec-coherence (granularity) audit

A separate, focused future task — NOT folded into the cross-link work. Some floating spec nodes look like
**chaptering artifacts rather than distinct concepts**: `program-structure` vs
`program-structure-and-execution`; the `inference-application-resolution` / `inference-constraint-solving` /
`inference-procedures` / `inference-supplementary` split; the per-collection `list-/map-/set-/seq-/option-
operations-representation` chapters. The audit question is "does the spec's chapter structure match the
conceptual structure, or should some nodes merge?" — the ontology critiquing its own granularity. Run as its
own task on the upstream spec repo (`~/repos/clef-lang-spec`), not as part of atlas link-adding.

## Five node categories (revised from three)

The graph has FIVE node kinds, not three (owner correction):
1. **arXiv pre-prints (6)** — the APEX kernel, your research, the source everything formalizes. Gold,
   innermost. Already well-connected (32 pages cite them; ADM=9, DTS+DMM=6, NFT=6 inbound) — the true
   center radiates immediately, a stronger story than the spec ring alone.
2. **spec (54)** — formal language definition. Orange.
3. **docs (109)** — exposition. Blue.
4. **blog (53)** — discourse. Green.
5. **external citations** — the standing literature you build ON (Mehta-Hsu, Kokke, etc.). Grey/yellow.
   GATED by a citation threshold (≥2 citing pages → ~8 nodes) to keep the set small and meaningful —
   "not too many external links" (owner). Threshold is tunable (≥3 → ~3 nodes).

Pre-print and external nodes are keyed by arXiv URL (not in hugo/content). The 6 pre-print IDs:
2603.16437 (DTS+DMM), 2603.17627 (PHG), 2603.18104 (ADM), 2603.25414 (DBC), 2606.02854 (FPS),
2606.04352 (NFT).

## Three edge types (revised)

1. **href** — body cross-links (page→page). Thin grey.
2. **cites** — page→paper arXiv citations. Gold. (This is how the pre-print/external kernel connects.)
3. **tag** — shared-frontmatter-tag membership (page↔page sharing a `tags:` entry). Dashed, faint.
   Skips ubiquitous tags (>12 pages) and singletons to avoid a hairball. ~165 edges currently.

## Deferred: header meta-tagging scheme

The `tag` edges currently reuse existing frontmatter `tags:`. A richer, principled scheme is a SEPARATE
future task (owner: "design next, before the modal ships" was deferred to after the modal baseline):
the need is that **membership edges (a doc IS ABOUT a concept/paper) live at the header level, not as body
cross-links** — body extraction captures *mention*, frontmatter captures *membership*, and the
membership edges are the ontological backbone. Scheme to design: a controlled concept vocabulary, an
explicit `realizes:` / `paper:` frontmatter field tying a doc to the pre-print it expounds (so the
pre-print↔doc "realizes" edge is a fact, not an accident of an inline arxiv link), and section/topic tags.
Until then, existing `tags:` carry approximate membership.

## Layout (validated in prototype)

Concentric (5 rings, pre-print center → blog outer) and grouped-by-section are the keepers. Plain
force-directed (fcose) is DROPPED — it erases the epistemic stratification into a hairball. Isolated nodes
render faded. Prototype: `scratchpad/atlas-preview.html` (5 categories, 3 edge types, tag-edge toggle).

## Management model: the graph rides the search index pipeline (owner decision)

The corpus graph shares the **same management model as Smart Search** — it is "another index," maintained by
the same pipeline, not a parallel system. This resolves the lifecycle question:

- **Same rebuild trigger.** When content is re-indexed for search (the `index` / `reconcile` / `--force`
  pipeline the owner runs on deploy), the graph rebuilds from the same content walk. The href extractor
  hooks into `Index.fs` (Stage 2); one walk feeds both search and graph.
- **Same staleness discipline.** Moved/deleted pages that orphan search vectors also orphan graph edges;
  the same reconcile/`--force` sweep keeps both honest. No separate graph-maintenance burden.
- **Curated, not auto-generated.** The workflow is: (1) agent-assisted enrichment generates candidate edges
  (cross-link passes like the Stage-1 work), (2) the owner HAND-CURATES — approve/reject, editorial judgment
  stays the owner's (as with the 10 high-confidence links applied this session), (3) the index/graph rebuilds
  through the pipeline so curated links become real edges. This is the same human-in-the-loop model as search
  synthesis — the graph reflects curated truth, never auto-generated slop.

This ratifies "the graph is another index sharing search's fabric" at the *management* level, not just storage:
the owner runs ONE deploy/index process and both search and the map stay current.

## Polish applied this session (live modal baseline)
- Map button + `#clef-map-modal` wired into the real build (`assets/js/map.js`, `_partials/map.html`,
  `_partials/scripts/map.html`, `head-end.html` loader, `custom.css` styles, `static/graph.json`).
- Five categories with real labels (external papers titled from citing-prose: Mamba-3, Mehta & Hsu, HCP,
  etc.). Pre-prints largest + central; citations orbit them — validated as the design clarity to keep.
- Button placement: search + Map are siblings in one navbar flex group (the search box keeps `md:w-80`;
  Map button is `nowrap`, sits beside it rather than wrapping below).

## Open polish (next)
- Confirm button placement renders inline across navbar widths (mobile/desktop).
- External (grey) node titles now display — consider richer tooltips (author + short title).
- Tooltip/hover-tile refinement; click-to-paper for external/preprint nodes (open arXiv URL).

## Map controls — posture and deferred direction (do not over-build)

Current controls (reasonable baseline, settle before changing):
- Layout: Concentric / Grouped.
- Edges: three independent named toggles — **Links** (href) / **Citations** (cites) / **Themes** (tag) —
  each shows/hides its own edge type. This replaced a confusing binary "All edges / Hide tags" whose
  label didn't match what it hid. It is a CLARITY fix, not added on the theory that "graphs need edge
  filters."

Governing posture (owner-stated, June 2026):
- **Graph-UI control conventions are NOT a settled industry norm.** Do not assert "a graph panel must have
  X/Y/Z" or accrete "expected" controls speculatively. Unlike a search box, graph-viz controls vary wildly
  across products; conforming to an assumed standard would be premature.
- The visual density is good as-is; nobody is pressed to add controls.
- The owner is still feeling out the controls already in place and does NOT want to overcorrect on a first
  impression. The agent should likewise hold steady — the next control change should come from the owner's
  LIVED USE surfacing a real need, not from guessing or from re-churning the scheme each message.

Deferred direction (aspirational, use-driven — NOT a committed norm):
- Smart-Search-style **meta-filtering** to constrain/"zoom" the graph to a subgraph (facet by layer, tag,
  content-type, connectivity), echoing how the search modal narrows results. This is the controls' likely
  future destination IF use validates it — recorded as a direction of interest, explicitly not asserted as a
  graph-UI requirement. Build only when the owner's interaction shows the need.
