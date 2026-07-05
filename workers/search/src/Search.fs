namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.D1
open Fidelity.CloudEdge.Vectorize

module Search =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Sanitize a user query for FTS5 MATCH.
    /// The tokenizer uses `tokenchars .-_#+` so F#, C#, F*, C++, .NET are valid tokens.
    /// We quote each whitespace-delimited term so FTS5 treats them as phrase literals
    /// rather than interpreting operators like # (column filter) or * (prefix).
    /// Double-quotes inside input are stripped since they control FTS5 phrase syntax.
    /// Also: length limit, control char / null byte stripping.
    let private sanitizeFts5Query (raw: string) : string =
        let query = if raw.Length > 500 then raw.Substring(0, 500) else raw
        // Only strip characters that are dangerous even inside FTS5 quotes,
        // or that have no place in search input at all.
        // Preserve: # + - . _ * (these are tokenchars in our FTS5 index)
        let mustStrip (c: char) =
            match c with
            | '"' -> true                      // controls FTS5 phrase syntax
            | '(' | ')' -> true                // FTS5 grouping
            | '{' | '}' -> true                // FTS5 aux syntax
            | '\x00' -> true                   // null byte
            | c when Char.IsControl(c) -> true // control chars
            | _ -> false
        query.Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun token ->
            let cleaned =
                token.ToCharArray()
                |> Array.filter (fun c -> not (mustStrip c))
                |> System.String
            // Wrap in double quotes to make FTS5 treat it as a literal phrase.
            // This neutralizes operators like # : ^ ~ * when they appear in tokens.
            if cleaned.Length > 0 then
                sprintf "\"%s\"" cleaned
            else "")
        |> Array.filter (fun s -> s <> "")
        |> String.concat " "

    /// BM25 full-text search using D1 FTS5
    /// bm25() returns negative scores (lower = better match), we negate for ranking
    let bm25Search (db: D1Database) (query: string) (limit: int) (contentType: string option) : JS.Promise<SearchResult array> =
        promise {
            let ftsQuery = sanitizeFts5Query query
            if ftsQuery = "" then return [||]
            else

            let sql, args =
                match contentType with
                | Some ct ->
                    """
                    SELECT
                        cs.id, cs.page_title, cs.section_title, cs.page_url,
                        cs.content_type, cs.summary, cs.published_at,
                        substr(cs.content, 1, 300) as snippet,
                        bm25(content_fts, 10.0, 5.0, 1.0) as score
                    FROM content_fts
                    JOIN content_sections cs ON content_fts.rowid = cs.rowid
                    WHERE content_fts MATCH ?
                      AND cs.content_type = ?
                    ORDER BY score
                    LIMIT ?
                    """,
                    [| box ftsQuery; box ct; box limit |]
                | None ->
                    """
                    SELECT
                        cs.id, cs.page_title, cs.section_title, cs.page_url,
                        cs.content_type, cs.summary, cs.published_at,
                        substr(cs.content, 1, 300) as snippet,
                        bm25(content_fts, 10.0, 5.0, 1.0) as score
                    FROM content_fts
                    JOIN content_sections cs ON content_fts.rowid = cs.rowid
                    WHERE content_fts MATCH ?
                    ORDER BY score
                    LIMIT ?
                    """,
                    [| box ftsQuery; box limit |]

            let stmt = db.prepare(sql)
            let bound = stmt.bind(args)
            let! result = bound.all<obj>()

            let rows =
                match result.results with
                | Some r -> r |> Seq.toArray
                | None -> [||]

            return rows |> Array.map (fun row ->
                {
                    id = string row?id
                    pageTitle = string row?page_title
                    sectionTitle = string row?section_title
                    pageUrl = string row?page_url
                    contentType = string row?content_type
                    snippet = string row?snippet
                    publishedAt = string row?published_at
                    score = -(float row?score) // Negate: FTS5 bm25() returns negative
                })
        }

    /// Generate embedding using Workers AI
    let generateEmbedding (ai: Ai) (text: string) : JS.Promise<float array> =
        promise {
            let truncated = if text.Length > 512 then text.Substring(0, 512) else text
            let request = createObj [
                "text" ==> [| truncated |]
            ]
            // The embedding model can stall the same way inference does: accept the
            // request and never resolve, hanging every /search/hybrid and
            // /synthesize-stream request until the client aborts. Race it against a
            // timeout so a stalled embedder throws fast and the handler's top-level
            // catch returns a clean 500 instead of holding the socket open. 8s sits
            // well above a healthy embedding (sub-second in practice) and far below
            // the client's cap. bge is a light model, so a genuine call never nears it.
            let embedCall = ai.run("@cf/baai/bge-base-en-v1.5", request)
            let timeout : JS.Promise<obj> =
                emitJsExpr () "new Promise(function(_, reject){ setTimeout(function(){ reject(new Error('EMBED_TIMEOUT')); }, 8000); })"
            let! result = Promise.race [ embedCall; timeout ]
            let data: obj array = result?data |> unbox
            return data.[0] |> unbox<float array>
        }

    /// Vector similarity search using Vectorize
    let vectorSearch
        (ai: Ai)
        (vectorize: VectorizeIndex)
        (query: string)
        (limit: int)
        : JS.Promise<(string * float) array> =
        promise {
            let! embedding = generateEmbedding ai query
            let options = Helpers.fullQueryOptions limit false true
            let! matches = vectorize.query(embedding, options)

            return
                matches.matches
                |> Seq.toArray
                |> Array.map (fun m -> (m.id, m.score))
        }

    /// Look up section metadata from D1 for IDs not already in a result set
    let hydrateVectorResults
        (db: D1Database)
        (vectorResults: (string * float) array)
        (existingIds: Set<string>)
        : JS.Promise<SearchResult array> =
        promise {
            let missingIds = vectorResults |> Array.map fst |> Array.filter (fun id -> not (existingIds.Contains(id)))
            if missingIds.Length = 0 then return [||]
            else

            // Build parameterized IN clause
            let placeholders = missingIds |> Array.map (fun _ -> "?") |> String.concat ", "
            let sql =
                $"""SELECT id, page_title, section_title, page_url, content_type, summary, published_at,
                    substr(content, 1, 300) as snippet
                    FROM content_sections WHERE id IN ({placeholders})"""

            let stmt = db.prepare(sql)
            let bound = stmt.bind(missingIds |> Array.map box)
            let! result = bound.all<obj>()

            let rows =
                match result.results with
                | Some r -> r |> Seq.toArray
                | None -> [||]

            return rows |> Array.map (fun row ->
                {
                    id = string row?id
                    pageTitle = string row?page_title
                    sectionTitle = string row?section_title
                    pageUrl = string row?page_url
                    contentType = string row?content_type
                    snippet = string row?snippet
                    publishedAt = string row?published_at
                    score = 0.0
                })
        }

    /// One fused candidate, carrying enough provenance for salience gating:
    /// whether BM25 matched it lexically and the raw vector cosine if it was a
    /// vector hit. The RRF score alone only encodes rank, not absolute relevance,
    /// so the synthesis gate needs these signals to drop loosely-related neighbors.
    type FusedCandidate = {
        result: SearchResult
        rrf: float
        bm25Hit: bool
        vectorScore: float option
    }

    /// Recency boost added on top of a fused RRF score. RRF scores live around
    /// 0.016-0.033 (1/(60+rank)), so this caps at ~0.012 — comparable to one or two
    /// rank positions. It nudges newer content above similarly-relevant older content
    /// without letting recency override a real relevance gap. A page from `refYear`
    /// or later gets the full bonus; it decays linearly to zero over `spanYears`
    /// before that; undated content (empty published_at) gets nothing.
    let private maxRecencyBoost = 0.012
    let private recencyRefYear = 2026.0
    let private recencySpanYears = 5.0

    /// Parse a YYYY-MM-DD published_at into a recency boost in [0, maxRecencyBoost].
    let private recencyBoost (publishedAt: string) : float =
        if isNullOrUndefined publishedAt || publishedAt.Length < 4 then 0.0
        else
            match Int32.TryParse(publishedAt.Substring(0, 4)) with
            | true, year ->
                let y = float year
                let normalized = (y - (recencyRefYear - recencySpanYears)) / recencySpanYears
                let clamped = max 0.0 (min 1.0 normalized)
                clamped * maxRecencyBoost
            | false, _ -> 0.0

    /// Reciprocal Rank Fusion to combine BM25 and vector results
    /// RRF(d) = sum( 1 / (k + rank_i(d)) ) for each ranking i
    let reciprocalRankFusion
        (bm25Results: SearchResult array)
        (vectorResults: (string * float) array)
        (vectorHydrated: SearchResult array)
        (k: int)
        : SearchResult array =

        // Build lookup from BM25 results + hydrated vector-only results
        let resultMap = System.Collections.Generic.Dictionary<string, SearchResult>()
        for r in bm25Results do
            resultMap.[r.id] <- r
        for r in vectorHydrated do
            if not (resultMap.ContainsKey(r.id)) then
                resultMap.[r.id] <- r

        // Compute RRF scores
        let rrfScores = System.Collections.Generic.Dictionary<string, float>()

        // Score from BM25 ranking
        bm25Results |> Array.iteri (fun rank result ->
            let score = 1.0 / (float k + float (rank + 1))
            rrfScores.[result.id] <- score
        )

        // Add score from vector ranking
        vectorResults |> Array.iteri (fun rank (id, _vectorScore) ->
            let score = 1.0 / (float k + float (rank + 1))
            match rrfScores.TryGetValue(id) with
            | true, existing -> rrfScores.[id] <- existing + score
            | false, _ -> rrfScores.[id] <- score
        )

        // Add the recency boost, then sort. Only return results we have metadata for.
        rrfScores
        |> Seq.toArray
        |> Array.choose (fun kv ->
            match resultMap.TryGetValue(kv.Key) with
            | true, result ->
                let boosted = kv.Value + recencyBoost result.publishedAt
                Some { result with score = boosted }
            | false, _ -> None)
        |> Array.sortByDescending (fun r -> r.score)

    /// Fuse with RRF but keep provenance (BM25 hit + raw vector cosine) per result.
    /// Mirrors reciprocalRankFusion's scoring; used only by the synthesis path,
    /// which needs salience signals the plain fusion discards.
    let fuseWithProvenance
        (bm25Results: SearchResult array)
        (vectorResults: (string * float) array)
        (vectorHydrated: SearchResult array)
        (k: int)
        : FusedCandidate array =

        let resultMap = System.Collections.Generic.Dictionary<string, SearchResult>()
        for r in bm25Results do resultMap.[r.id] <- r
        for r in vectorHydrated do
            if not (resultMap.ContainsKey(r.id)) then resultMap.[r.id] <- r

        let bm25Ids = bm25Results |> Array.map (fun r -> r.id) |> Set.ofArray
        let vectorScoreMap = dict vectorResults

        let rrfScores = System.Collections.Generic.Dictionary<string, float>()
        bm25Results |> Array.iteri (fun rank r ->
            rrfScores.[r.id] <- 1.0 / (float k + float (rank + 1)))
        vectorResults |> Array.iteri (fun rank (id, _) ->
            let score = 1.0 / (float k + float (rank + 1))
            match rrfScores.TryGetValue(id) with
            | true, existing -> rrfScores.[id] <- existing + score
            | false, _ -> rrfScores.[id] <- score)

        // `rrf` stays the pure relevance signal so the salience gate's drop-ratio
        // isn't muddied by recency. The recency boost goes into the ordering score
        // (and the sort), so newer content rises among similarly-relevant results
        // without changing which results clear the gate.
        rrfScores
        |> Seq.toArray
        |> Array.choose (fun kv ->
            match resultMap.TryGetValue(kv.Key) with
            | true, r ->
                let boosted = kv.Value + recencyBoost r.publishedAt
                Some {
                    result = { r with score = boosted }
                    rrf = kv.Value
                    bm25Hit = bm25Ids.Contains(kv.Key)
                    vectorScore =
                        match vectorScoreMap.TryGetValue(kv.Key) with
                        | true, s -> Some s
                        | false, _ -> None
                }
            | false, _ -> None)
        |> Array.sortByDescending (fun c -> c.result.score)

    /// Minimum cosine for a vector-only candidate (no lexical hit) to enter
    /// synthesis. bge-base-en-v1.5 cosines run ~0.5-0.9 for on-topic neighbors;
    /// loosely associated pages sit below this. Tuned conservatively.
    let private vectorOnlyMinCosine = 0.62

    /// Keep results by salience rather than a fixed top-N. Always keep the most
    /// relevant hit; keep the rest while their pure RRF stays within `dropRatio` of
    /// the highest RRF. A candidate that matched only via vector (no BM25 hit) must
    /// also clear `vectorOnlyMinCosine` — this is what drops the "loosely related"
    /// neighbors (e.g. UI/layout pages surfacing on a memory-architecture query).
    /// Gating is on pure `rrf` (relevance), not the recency-boosted score, so a
    /// newer-but-irrelevant page can't sneak through; recency only affects ordering.
    let selectSalient (candidates: FusedCandidate array) (maxResults: int) : FusedCandidate array =
        if candidates.Length = 0 then [||]
        else
            let dropRatio = 0.5
            let topRrf = candidates |> Array.map (fun c -> c.rrf) |> Array.max
            let cutoff = topRrf * dropRatio
            candidates
            |> Array.filter (fun c ->
                let passesSalience = c.rrf >= cutoff
                let passesVectorBar =
                    c.bm25Hit ||
                    (match c.vectorScore with
                     | Some s -> s >= vectorOnlyMinCosine
                     | None -> false)
                passesSalience && passesVectorBar)
            |> Array.truncate maxResults

    /// Fetch full section bodies for the selected results, ordered to match the
    /// salience ranking, and assembled within a total character budget. glm-4.7-flash
    /// has a 131k-token window, so ~40k chars (~10k tokens) is well within context;
    /// the budget just bounds prompt size so a handful of large pages can't balloon
    /// the request. Full sections beat the old 300-char fragments. Sections past the
    /// budget are dropped (count returned for logging).
    let fetchFullContent
        (db: D1Database)
        (selected: SearchResult array)
        : JS.Promise<{| sections: (SearchResult * string) array; truncated: int |}> =
        promise {
            if selected.Length = 0 then
                return {| sections = [||]; truncated = 0 |}
            else

            let ids = selected |> Array.map (fun r -> r.id)
            let placeholders = ids |> Array.map (fun _ -> "?") |> String.concat ", "
            let sql =
                $"SELECT id, content FROM content_sections WHERE id IN ({placeholders})"
            let stmt = db.prepare(sql)
            let bound = stmt.bind(ids |> Array.map box)
            let! result = bound.all<obj>()

            let contentById = System.Collections.Generic.Dictionary<string, string>()
            match result.results with
            | Some rows ->
                for row in rows do
                    contentById.[string row?id] <- string row?content
            | None -> ()

            // Reassemble in the salience order of `selected`, applying the budget.
            let charBudget = 40000
            let kept = ResizeArray<SearchResult * string>()
            let mutable used = 0
            let mutable truncated = 0
            for r in selected do
                match contentById.TryGetValue(r.id) with
                | true, body ->
                    let bodyLen = body.Length
                    if used + bodyLen <= charBudget then
                        kept.Add((r, body))
                        used <- used + bodyLen
                    else
                        // Take a partial slice if meaningful room remains, else drop.
                        let room = charBudget - used
                        if room >= 800 then
                            kept.Add((r, body.Substring(0, room)))
                            used <- charBudget
                        truncated <- truncated + 1
                | false, _ ->
                    // No full body found; fall back to the snippet we already have.
                    if used + r.snippet.Length <= charBudget then
                        kept.Add((r, r.snippet))
                        used <- used + r.snippet.Length
            return {| sections = kept.ToArray(); truncated = truncated |}
        }

    /// Classify the user's input so the synthesis prompt frames its task correctly:
    /// a bare term ("CXL") asks the model to describe what the corpus covers, while
    /// a question or request ("Tell me about CXL") asks it to answer directly.
    /// Heuristic only — no model call.
    let isQuestionOrRequest (rawQuery: string) : bool =
        let q = rawQuery.Trim().ToLowerInvariant()
        if q = "" then false
        elif q.Contains("?") then true
        else
            let starters =
                [| "what"; "why"; "how"; "when"; "where"; "who"; "which"
                   "is "; "are "; "can "; "does "; "do "; "should "; "could "
                   "tell me"; "explain"; "describe"; "compare"; "summarize"
                   "give me"; "show me"; "list "; "find " |]
            let startsWithRequest = starters |> Array.exists (fun s -> q.StartsWith(s))
            // Multi-word natural-language input also reads as a request, not a term.
            let wordCount = q.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries).Length
            startsWithRequest || wordCount >= 6

    /// Build synthesis prompt from full-content sections grouped under their pages.
    /// `request` is the raw user input; `answerMode` switches the task verb between
    /// answering a question and describing what the corpus covers.
    /// Does this synthesis touch the four-tier verification architecture? Checked
    /// against the request and the assembled evidence so the canonical-tier guard
    /// is injected only when relevant — non-verification summaries stay lean.
    let private touchesVerificationTiers (request: string) (evidence: string) : bool =
        let haystack = (request + " " + evidence).ToLowerInvariant()
        let signals =
            [| "tier 1"; "tier 2"; "tier 3"; "tier 4"; "four-tier"; "four tier"
               "qf_lia"; "qf_bv"; "prhl"; "rocq"; "hoare"; "trusted computing base"
               "rejection-sampling"; "rejection sampling"; "proof obligation"
               "free theorem"; "decidab"; "verification tier"; "compilation byproduct" |]
        // "z3" alone is too broad (appears in general compiler prose); require it to
        // co-occur with a tier or proof signal, handled by the list above already.
        signals |> Array.exists (fun s -> haystack.Contains(s))

    /// Canonical four-tier mapping, treated as GROUND TRUTH over any ambiguous
    /// excerpt phrasing. Verified against
    /// docs/design/categorical-foundations/formal-verification-compilation-byproduct.md.
    /// This exists because small models scramble the tier→mechanism mapping under
    /// summarization pressure (e.g. attributing Z3 to Tier 1, which is wrong).
    let private fourTierGuard =
        """ACCURACY GUARD — the four-tier verification architecture. Getting this exactly right is critical: the tier→mechanism and tier→trusted-computing-base pairings are the framework's core formal claim, and a wrong pairing (e.g. describing Tier 1 properties as requiring Z3-discharged assertions, or putting Rocq in the trusted computing base before Tier 4) is a serious correctness error that misrepresents the architecture to a formal-methods audience. When the synthesis makes any claim about which tier uses which proof mechanism or which trusted computing base, the mapping below is authoritative. If an excerpt's wording seems to conflict with it, follow THIS mapping. Do not assign a mechanism to a tier unless it matches this table, and do not state a tier→mechanism pairing this table does not support.

- Tier 1 — Compilation byproducts. Dimensional types, memory lifetimes, grades, escape analysis, and parametricity-derived (free) theorems, carried through abelian group structure and computed at ZERO annotation cost during normal compilation. Free theorems apply at Tier 1 only. The QF_LIA/QF_BV assertion mechanism is Tier 2, not Tier 1; do not describe Tier 1 properties as requiring assertion annotations.
- Tier 2 — Scoped Hoare assertions. Bounds, invariants, and lifetime orderings via the [<Requires>]/[<Ensures>] attributes, discharged by Z3 over QF_LIA (dimensional algebra, range bounds) and QF_BV (bit-level and word-width reasoning from representation selection). This is where Hoare-logic vocabulary correctly belongs.
- Tier 3 — Restricted probabilistic fragment. Library-instantiated lemmas (e.g. rejection-sampling termination, transcendental/range bounds), discharged through Z3 alone. Rocq is NOT in the trusted computing base at Tier 3.
- Tier 4 — Probabilistic Relational Hoare Logic (pRHL). Relational proofs for cryptographic indistinguishability, type-checked by the Composer's pRHL type checker against a Rocq-proved rule library; Z3 handles only the arithmetic leaves. Rocq enters the trusted computing base ONLY at Tier 4.

Trusted computing base: Z3 alone for Tiers 1–3; Rocq is added only at Tier 4."""

    let buildSynthesisPromptFull
        (request: string)
        (answerMode: bool)
        (sections: (SearchResult * string) array)
        : string =
        let evidence =
            sections
            |> Array.mapi (fun i (r, body) ->
                let heading =
                    if String.IsNullOrWhiteSpace(r.sectionTitle) then r.pageTitle
                    else $"{r.pageTitle} — {r.sectionTitle}"
                $"--- EXCERPT {i + 1}: {heading} ---\n{body}")
            |> String.concat "\n\n"

        // Inject the canonical-tier guard only for verification-topic syntheses.
        let guardBlock =
            if touchesVerificationTiers request evidence then "\n\n" + fourTierGuard
            else ""

        let task =
            if answerMode then
                "Answer the USER REQUEST directly, using only the SOURCE EXCERPTS as evidence. Lead with the answer. Let the length follow the evidence: a sharp question backed by one strong excerpt deserves two or three sentences, a broad request backed by several may warrant a short paragraph. Do not pad."
            else
                "The USER REQUEST is a topic, not a question. In two to four sentences, describe what the SOURCE EXCERPTS say about it and how the relevant pieces connect. Do not pad beyond what the excerpts support."

        $"""You are a documentation assistant for the Clef programming language and the Fidelity framework (clef-lang.com).

Clef is a hard-forked F# compiler that targets native code through MLIR for CPUs, GPUs, NPUs, FPGAs, and spatial accelerators. The Fidelity framework around it spans dimensional type systems, deterministic memory management, coeffect-based escape analysis, design-time verification through Z3, categorical foundations (sheaf theory, cellular sheaves on the compilation pipeline), Hoare logic at multiple tiers, probabilistic relational reasoning for cryptography, posit arithmetic, forward-mode automatic differentiation, neuromorphic targets, and physics-informed compilation. Subject matter that sounds purely mathematical (sheaves, functors, parametricity, free theorems, group actions, Hoare triples, lattice cryptography, geometric algebra) is first-class here, not off-topic background.

USER REQUEST:
"{request}"

SOURCE EXCERPTS:

{evidence}

TASK:
{task}{guardBlock}

Rules:
- Use only information present in the SOURCE EXCERPTS. Do not invent details, names, or claims.
- If an excerpt does not bear on the USER REQUEST, ignore it. Do not force unrelated excerpts into the answer.
- Quote specific named concepts and connect excerpts where the connection is visible in the text.
- Clef is the present language of the framework. F#, F* (F-star), Scheme, OCaml, and Erlang are LINEAGE and INSPIRATION only, never the framework's present language. When an excerpt traces an idea to one of them, attribute the capability to Clef or the Fidelity framework and name the other language only as origin or inspiration ("a model Clef inherits from F#", "inspired by Erlang"). Never present F#'s (or F*'s, Scheme's, Erlang's) features as if they are Clef's current capabilities, and never imply the framework compiles or runs F#. If an excerpt itself uses heritage wording ("descends from", "inherits", "carries forward"), preserve that framing; do not flatten it into a present-tense feature of F#.
- Do not preface with phrases like "the search results describe", "based on the excerpts", or "the documentation says". Deliver the synthesis directly."""

    /// Build synthesis prompt from ranked search results (for smart-search worker)
    let buildSynthesisPrompt (query: string) (results: SearchResult array) : string =
        let contextParts =
            results
            |> Array.mapi (fun i r ->
                $"[{i + 1}] {r.pageTitle} — {r.sectionTitle}\n{r.snippet}")
            |> String.concat "\n\n"

        $"""You are a documentation assistant for the Clef programming language and the Fidelity framework (clef-lang.com).

Clef is a hard-forked F# compiler that targets native code through MLIR for CPUs, GPUs, NPUs, FPGAs, and spatial accelerators. The Fidelity framework around it covers a wide span of topics: dimensional type systems, deterministic memory management, coeffect-based escape analysis, design-time verification through Z3, categorical foundations including sheaf theory and cellular sheaves on the compilation pipeline, Hoare logic at multiple tiers, probabilistic relational reasoning for cryptography, posit arithmetic, forward-mode automatic differentiation, neuromorphic targets, and physics-informed compilation. Topics that may sound purely mathematical (sheaves, functors, parametricity, free theorems, group actions, Hoare triples, lattice cryptography, geometric algebra) are first-class subject matter for this site, not off-topic background. Treat them as such.

The user's query: "{query}"

Top search results, each with a snippet from the source page:

{contextParts}

Write a substantive synthesis of what these results say about the query, in 4 to 6 sentences. Quote specific concepts and named results by their content. Connect the snippets to one another where the connections are visible in the text. Use only information present in the snippets; do not invent details. If a snippet directly answers the query, lead with that answer rather than describing the snippet. Do not preface the synthesis with phrases like "the search results describe" or "based on the snippets" — just deliver the synthesis directly. Do not declare the results irrelevant; if the connection to the query is loose, explain what the results actually cover instead."""
