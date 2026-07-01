# Semantic Layer — Reconciliation Design

Status: **design only, nothing built.** Scope-it-first per owner: produce a study report before
committing to any edge treatment or ontology vocabulary.
Origin: conversation, June 2026, after the literal-link graph reached full connectivity (0-inbound
17→0, `{{< ref >}}` extractor fix, 988 edges).

## Concept

The corpus now has a complete **literal-link graph** (authored `href`/`cites` edges over page
nodes). This plan adds a **semantic layer**: edges manufactured from embedding-space geometry, over
the same node set, to surface conceptual adjacencies that direct provenance does not show.

The literal graph gives the semantic layer a **leg up** (owner's framing): the authored links are a
validation set. A semantic edge that coincides with a literal link is *confirmed*; a strong semantic
edge where **no literal link exists** is a *latent* edge — the auto-generated ontology worklist, the
next round of the "write the unwritten relation" work the owner has been doing by hand.

## The two indices are different objects (the crux)

The owner's key realization: this is **not one index at two resolutions.** The search index and the
graph index are different kinds of thing, and reconciling them is the design work.

| | **Graph index** (`graph_nodes`/`graph_edges`, D1) | **Search index** (Vectorize `clef-content-dev` + FTS5) |
|---|---|---|
| Unit | whole **page** (1 node = 1 entry) | **section** (H2-split; 1 page → many rows) |
| Scope | spec + docs + blog + **pre-prints + external papers** | spec + docs + blog **only** |
| Structure | relational (nodes + typed edges) | geometric (points in embedding space, **no edges**) |
| Built for | navigation / provenance | retrieval / ranking |

Three mismatches to reconcile:
1. **Granularity — DON'T coarsen to fit the old graph; promote the graph to the vectors' resolution.**
   Owner's sharpened target: keep the **sections as first-class nodes** and build a new **section-
   granular** vector store where semantic edges connect **section → section**, not page → page. This
   is strictly more useful than the page-level out-navigation the literal-link graph does:
   - Page-level links are coarse ("this article relates to that article" — reader still hunts for
     *where* inside the target).
   - Section-level semantic edges are precise ("*this* discussion of the quire → *that* discussion of
     exact accumulation") — lands the reader on the exact passage.
   A section-to-section web is finer than authored links could ever be **by construction**: no one
   would hand-author links at that density, but embeddings can. The section index already exists at
   this granularity (H2-split, already in Vectorize), so there is **no aggregation loss** — use the
   sections as-is. The literal graph stays page-level provenance; the semantic graph is section-level
   conceptual adjacency, a genuinely different and richer object.
2. **Scope** — the graph has pre-print/external nodes the vector index does not (papers have no
   indexed body text). **Decision (owner): let pre-prints + external papers fall dark naturally** in
   the semantic layer. Semantic edges connect *our writing*; papers connect via `cites` only. Spec
   IS in the vector set (the CLI `classifyContent` indexer covers `spec/`), so spec+docs+blog all
   participate.
3. **Object** — the vector index has no edges; edges are **manufactured from geometry** (cosine
   threshold). "What treatment do semantic edges get" is really "what geometric relationships
   correspond to meaningful ontological relations" — **unanswerable until the geometry is seen.**
   Hence scope-it-first.

## The overlap is PAID FOR by section-to-section precision (the value justification)

The semantic layer partly re-derives what the literal page-graph already encodes: a *confirmed* edge
means both indices "know" the same two pages relate. On its own that overlap would be waste — why
compute what was already authored?

**But the overlap is not free redundancy — it is paid for by the extra resolution.** Where the
page-graph says "A relates to B," the section index says "A's *quire* discussion relates to B's
*exact-accumulation* discussion." The confirmed edges are not wasted recomputation; they are the
page-link **plus its coordinates** — *where inside each page* the relationship lives (deep-link
targets that did not exist before). You pay overlap to get the relationship **localized to the
passage**, not to get the same fact twice.

This reframes the tags economically:
- **Confirmed** = the overlap, earning its keep by pinning a page-link to specific sections.
- **Latent** = pure new value — adjacencies the page-graph never had.
- **Bridge** = a page-link whose sections *don't* strongly align — a warning the link may be
  structural/aspirational, worth a second look.

So the section store is not "the page-graph again, finer." It is a **strict superset**: everything
the page-graph knows, localized, plus the latent section web. The overlap is the price of admission
for the localization; the localization is the product.

## The pass (mirrors the existing Vectorize indexing pass)

Owner: "it should be like the other Vectorize pass" — a real pipeline step using the embeddings
already in Vectorize (`@cf/baai/bge-base-en-v1.5`), **not a local recompute** that could drift. The
embeddings already exist (the search-index pass wrote them); the semantic pass **reads them back**,
it does not re-embed.

1. Read existing section vectors from Vectorize (`getByIds` / `query`) — same index, same model.
2. **Section-to-section** cosine similarity — the sections are the nodes; edges connect sections
   directly. No roll-up to page-pairs (that was the coarsening the owner rejected).
3. For each section, top-K nearest sections → candidate semantic edges (each edge carries the two
   section anchors: page + H2, so a UI can deep-link to `/page/#section`).
4. Cross-reference against the page-level `graph_edges` for TAGGING (the literal graph is page-level;
   a section-edge "agrees" if its two pages have a literal link):
   - section-pair high-sim **+ their pages have a literal link** → **confirmed** (the page link is
     realized at this specific section pair — evidence of *where* the relationship lives)
   - high-sim **+ no page-level literal link** → **latent** (the discovery set — a section-level
     adjacency the authored page graph never captured)
   - a page has a literal link but its sections show only **low** cross-similarity → **bridge** (the
     link connects distant regions; structurally interesting)
5. Output = a **study report**: latent section-pairs ranked by score, tagged by page-level agreement,
   grouped so the owner sees the *patterns* (where section-adjacency agrees vs. surprises the
   page-level provenance).

The literal graph **calibrates the threshold**: what cosine score reliably corresponds to a real
relationship is learned from the confirmed set, not guessed. (bge-base-en-v1.5 cosines run ~0.5–0.9
for on-topic neighbors per the search worker's own notes.)

## Sequencing (why the ontology comes LAST)

Owner: "I'm really not sure how to treat the semantic edges until I see enough of pattern similarity
and differences to make proper notes." This inverts the naive build order:

1. **NOW — observation, not ontology.** The pass produces the study report. Untyped. The owner reads
   it and takes notes on what the clusters actually are.
2. **LATER — typed ontology, from evidence.** Once the patterns are seen, define the relation
   vocabulary (candidate types raised in discussion: `extends`, `formalizes`, `supersedes`,
   `is-substrate-of`, `historical-precedent-of`, `critiques`) FROM the data, and classify the
   strongest edges. This is "semantic web" in the Berners-Lee/RDF-triple sense and the PSG/PHG
   thesis applied to the corpus itself — but it cannot be the first build. Defining the vocabulary
   before looking is the premature-ontology trap the owner is explicitly avoiding.

## Open tension the section-level target creates (resolve during study)

Section-nodes are a **different node set** from the atlas's page-nodes. The atlas is ~150 page-nodes;
the section index is many more (every H2 across every page). So the semantic graph is NOT a simple
overlay on the existing Atlas — it is potentially its own, denser graph. Two ways this could go, TBD
after the study shows the density:
- **A) A distinct section-level view** — a second mode of the Atlas (or its own surface) where nodes
  ARE sections, showing the fine semantic web. The page-level literal Atlas and the section-level
  semantic Atlas are two lenses on the corpus.
- **B) Project section-edges onto the page graph** — keep the Atlas page-nodes, but a page-to-page
  semantic edge exists if ANY section pair is strongly similar, and clicking it reveals which
  sections. Keeps one graph; loses some of the section-node richness in the main view.
The study report (untyped, section-to-section) is what tells us which is worth building.

## Visualization target: the 3D cityscape (deferred; needs the section data first)

Owner's image: a **cube / cityscape on a lazy susan** — spinnable, tiltable, navigated like a
miniature. This is not decoration; it is the honest rendering of the section-level superset, because
it uses the one axis the page-level 2D atlas *cannot* show.

Axis assignment:
- **Vertical (top → bottom) = position WITHIN an article** (owner: "top = beginning, bottom = how
  they play out"). Each article becomes a **tower**: a column of section-nodes stacked in reading
  order — H1/intro at the top, descending through H2s to the conclusion. Tower height = section count.
  This vertical axis IS the section granularity made spatial.
- **Horizontal ground plane = the corpus map** — the existing 2D atlas layout (concentric bands:
  spec inner, docs mid, blog outer / or the grouped connectivity layout) becomes the *floor plan* the
  towers rise out of. The skyline is a cityscape of articles.
- **Edges = section-to-section arcs landing on FLOORS, not rooftops.** A page-level literal link is
  tower-to-tower; a section-level semantic edge connects a *specific floor* of one tower to a specific
  floor of another ("3rd floor of the quire article → 5th floor of exact-accumulation"). You literally
  *see* the localization the section store buys — the arcs land on the passages.

So the cube visualizes exactly what the section-level superset adds over the page graph: the
intra-article axis.

**The keystone: the 2D atlas IS the cityscape seen from directly overhead.** These are not two
different visualizations sharing data — they are **one spatial model at two camera positions**:
- **2D atlas (Concentric/Grouped) = the top-down / plan view.** Camera straight down the vertical
  axis, looking at the *rooftops*. The floor plan (concentric bands, the corpus map) is visible and
  each article collapses to a single point — its footprint, the top of its tower. The intra-article
  axis is compressed to nothing because you look straight down it.
- **3D cityscape (Spatial) = the camera lowered and orbiting.** The towers gain height, the floors
  separate, and the section structure hidden *inside* each rooftop point unfolds into a column.

The 2D view is the **degenerate case** of the 3D view (elevation 0, looking down) — but this is a
**conceptual** relationship, not a camera path the modes animate through. Operationally the two are
separate renderers each locked to its natural stance: **2D is always overhead** (that is simply what
2D is), and **3D ALWAYS renders as the perspective cityscape** — you do NOT enter Spatial at top-down
and tilt up. You enter and it is already the angled cityscape with a live camera, towers at height,
ready to orbit. The "seen from above" framing explains *why* the two views are coherent; it is not a
transition the user passes through.

**3D camera default:** on entering Spatial, a **one-time intro sweep** — a brief automatic
establishing orbit that announces the dimensionality and resolves depth (motion parallax) — then it
**settles into a high-angle bird's-eye position: elevated, looking DOWN ACROSS the cityscape at a
downward tilt** (not a low street-level shot). This is the sweet spot: high enough to read the whole
floor-plan (the concentric bands) and the overall layout, but angled enough that the towers still
show their HEIGHT — the section stacks — rather than collapsing to rooftops. Overview and the third
dimension in one frame. The user navigates (orbits / pans / zooms) from that resting pose; drag =
lazy-susan turntable spin. The bird's-eye also makes the projection relationship visible in the pose
itself: it is the top-down 2D atlas tilted just a few degrees off straight-down, enough to see the
towers rise. The sweep gives the "it's a cityscape" moment once; after it settles, no camera motion
the user did not ask for. (Rejected: perpetual idle auto-spin — too demo-like for a navigation tool;
and low street-level — loses the overview the bird's-eye keeps.)

This also dissolves the A/B tension above: the
page-level literal atlas is not a *different* graph from the section-level semantic one — it is the
section graph **viewed from above**, each article's column of sections stacked into one visible
footprint. A page-level link (tower-to-tower) is what a bundle of section-edges *looks like from
overhead*: all the floor-to-floor arcs between two towers projected down onto one rooftop line.

The confirmed/latent/bridge tags even get a visual reading in this frame: a **bridge** (page-link,
weak section alignment) is a rooftop line with almost no arcs beneath it; a **confirmed** edge is a
rooftop line with a dense bundle of floor-arcs underneath. From directly above they look identical;
tilting the camera up reveals which is substantial and which is a thin thread.

**The Spatial control RE-SPAWNS the view — it is NOT a third layout toggle.** Concentric and Grouped
are peers: two 2D Cytoscape *layouts* run on the same `cy` instance over the same ~150 page-nodes,
instant to switch. Spatial is a different kind of control entirely — clicking it **disposes the 2D
Cytoscape view and mounts a separate WebGL cityscape** built from the section-level data. Three
reasons it must re-spawn, not overlay:
1. **Different renderer** — Cytoscape 2D vs. Three.js/WebGL; you cannot toggle a Cytoscape layout and
   a WebGL scene on one instance. Tear down one, mount the other.
2. **Different node set** — the 2D views share page-nodes; the cityscape's nodes are *sections* (many
   more). Not the same graph re-laid-out — a different graph.
3. **Different data source** — 2D reads page-level `graph.json`; the cityscape reads the section-level
   semantic store.
The mental model: Concentric/Grouped is re-centering a **map**; Spatial is switching from the map to a
**globe**. In the control bar, Spatial sits apart from the two layout toggles (or in a distinct
view-mode group), and carries its own dispose/mount lifecycle.

**RENDERER DECIDED: `3d-force-graph`** (Three.js under the hood). Confirmed by a working prototype on
real corpus data (`scratchpad/cityscape-3d.html`): concentric ground-plane grid, each article a real
3D box-tower (height = actual H2 count, 1–17), threading edges through the volume, orbit/zoom, and the
one-time intro sweep → high-angle bird's-eye settle — all working. Owner confirmed the model reads.
Cytoscape is NOT a candidate — it is 2D-only, no camera, no orbit; the 2D atlas stays Cytoscape but
the cityscape is a wholly separate WebGL path. Other libs considered and ruled out: ECharts-GL
(charting abstraction, no arbitrary edge-between-bars), Plotly-3D (scientific-plot viewer, weak graph
interactions), raw Three.js (reinvents the graph scaffolding `3d-force-graph` gives free).
`3d-force-graph` is the fit: Three.js power + graph scaffolding done (orbit camera, node
picking/hover/click, `nodeThreeObject` custom geometry = the tower, controllable fixed positions).
Loaded lazily only when Spatial is entered.

Prototype implementation notes (what worked, for the real build):
- **Fixed positions, force sim OFF** — set `fx/fz` from the concentric ring layout, base `fy=0`;
  `d3Force('charge'/'link'/'center', null)` + `cooldownTicks(0)` so the plane doesn't scramble.
- **Tower = `nodeThreeObject`** returning a `BoxGeometry(w, floors*HSCALE, w)` with `position.y = h/2`
  so the base sits on the plane and the tower rises. Papers = short translucent stubs (fall dark).
- **Camera** — `cameraPosition({x:0,y:620,z:820},{x:0,y:40,z:0}, 1600)` for the sweep→bird's-eye.
- **Still placeholder in the prototype:** (a) ground-plane placement is hash-ring spread, not yet the
  balanced concentric distribution the 2D atlas has; (b) threading is base-to-base (tower-to-tower)
  because section-level semantic edges don't exist yet — floor-to-floor threading is the payoff that
  lights up once the semantic pass runs. The prototype is the STAGE those threads will play on.
- **3D graphs are easy to make beautiful, hard to make usable** — occlusion (towers hiding towers),
  depth ambiguity, untraceable edges through the volume. The lazy-susan spin helps (motion parallax
  resolves depth). The cityscape metaphor is a point in its favor: buildings-on-a-ground-plane is a
  spatial arrangement humans already parse.
- **Needs the section data to exist.** Empty towers with no floors are pointless. Build order: the
  semantic pass + study report FIRST (proves the section-edge density is worth a 3D view), then the
  renderer. This is the concrete form of **option A** (the section-level view) in the A/B tension above.

## Surfacing (decided in principle, deferred to post-study)

Owner wants BOTH, once the treatment is understood:
- **Graph overlay** — semantic edges as a toggleable edge class (a 4th toggle beside Links/Citations),
  latent edges styled distinctly (dashed?) so the discovery set is visually obvious. (Which graph —
  page-level projection or section-level view — is the A/B above.)
- **Review queue** — a ranked list of high-sim section-pairs whose pages lack a literal link, surfaced
  as concrete authoring suggestions (accept → write a See-also or a section-anchored inline link;
  dismiss). Section granularity makes these suggestions *precise* — it names the exact passage to
  link, not just the page. Turns the semantic layer into the ontology-worklist generator that drives
  the manual shelving, at section resolution.

## Division of labor / constraints

- Anything reading the deployed Vectorize index needs `CLOUDFLARE_ACCOUNT_ID` + `CLOUDFLARE_API_TOKEN`.
  Per standing rule, **the owner runs all deploys / live-Cloudflare passes.** Claude builds the pass
  code (F#, structured like `workers/search/src/Indexing.fs`); the owner runs it against the live index.
- No re-embedding — reuse the existing `clef-content-dev` vectors. Same model, same index the search
  pass wrote, so no drift.

## Also queued (unrelated, small): the EDGES control

The control-bar "EDGES" is a `<span>` label (section divider), not a button — reads like a dead
button. Decision: make it a real **"toggle all edges"** master switch (Links + Citations + the coming
Semantic), lit when edges shown, dimmed when hidden. Build alongside the semantic overlay so the new
edge class has its toggle and the master switch has something to master.
