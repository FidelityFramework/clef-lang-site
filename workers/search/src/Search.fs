namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.D1
open Fidelity.CloudEdge.AI.Generated
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
                        cs.content_type, cs.summary,
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
                        cs.content_type, cs.summary,
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
                    score = -(float row?score) // Negate: FTS5 bm25() returns negative
                })
        }

    /// Generate embedding using Workers AI
    let generateEmbedding (ai: Ai<obj>) (text: string) : JS.Promise<float array> =
        promise {
            let truncated = if text.Length > 512 then text.Substring(0, 512) else text
            let request = createObj [
                "text" ==> [| truncated |]
            ]
            let! result = ai.run("@cf/baai/bge-base-en-v1.5", request)
            let data: obj array = result?data |> unbox
            return data.[0] |> unbox<float array>
        }

    /// Vector similarity search using Vectorize
    let vectorSearch
        (ai: Ai<obj>)
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

    /// Reciprocal Rank Fusion to combine BM25 and vector results
    /// RRF(d) = sum( 1 / (k + rank_i(d)) ) for each ranking i
    let reciprocalRankFusion
        (bm25Results: SearchResult array)
        (vectorResults: (string * float) array)
        (k: int)
        : SearchResult array =

        // Build lookup from BM25 results
        let resultMap = System.Collections.Generic.Dictionary<string, SearchResult>()
        for r in bm25Results do
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

        // Sort by combined RRF score descending, only return results we have metadata for
        rrfScores
        |> Seq.toArray
        |> Array.sortByDescending (fun kv -> kv.Value)
        |> Array.choose (fun kv ->
            match resultMap.TryGetValue(kv.Key) with
            | true, result -> Some { result with score = kv.Value }
            | false, _ -> None)

    /// Build synthesis prompt from ranked search results (for smart-search worker)
    let buildSynthesisPrompt (query: string) (results: SearchResult array) : string =
        let contextParts =
            results
            |> Array.mapi (fun i r ->
                $"[{i + 1}] {r.pageTitle} — {r.sectionTitle}\n{r.snippet}")
            |> String.concat "\n\n"

        $"""You are a helpful assistant for the Clef programming language documentation site (clef-lang.com).
Clef is a concurrent systems language targeting CPU, GPU, NPU, FPGA, and other accelerators with proof-carrying capabilities for safe realtime systems.

The user searched for: "{query}"

Here are the top search results with snippets:

{contextParts}

Provide a concise 2-3 sentence summary that synthesizes these results in relation to the user's query.
Reference specific results by their content but do not use numbered citations.
If the results don't seem relevant to the query, say so briefly.
Do not make up information not present in the snippets."""
