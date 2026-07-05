namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.Worker.Context
open Fidelity.CloudEdge.Worker.Context.Globals

module Handlers =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Create JSON response
    let jsonResponse (data: obj) (status: int) : Response =
        Response.json(data, !!createObj [ "status" ==> status ])

    // ── CORS helpers ──────────────────────────────────────────────

    /// Check if an origin is allowed based on ALLOWED_ORIGIN env var
    let isOriginAllowed (env: WorkerEnv) (origin: string option) : string option =
        match origin with
        | None -> None
        | Some requestOrigin ->
            if env.ALLOWED_ORIGIN = "*" then
                Some requestOrigin
            else
                let allowedOrigins = env.ALLOWED_ORIGIN.Split(',') |> Array.map (fun s -> s.Trim())
                if allowedOrigins |> Array.contains requestOrigin then
                    Some requestOrigin
                else
                    None

    /// Handle CORS preflight OPTIONS requests
    let handleOptions (env: WorkerEnv) (origin: string option) : Response =
        let headers = Globals.Headers.Create()
        match isOriginAllowed env origin with
        | Some allowedOrigin ->
            headers.set("Access-Control-Allow-Origin", allowedOrigin)
        | None -> ()
        headers.set("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        headers.set("Access-Control-Allow-Headers", "Content-Type, Authorization")
        headers.set("Access-Control-Max-Age", "86400")

        Globals.Response.Create(U2.Case1 "", !!createObj [
            "status" ==> 204
            "headers" ==> headers
        ])

    /// Add CORS headers to a response
    let withCORS (env: WorkerEnv) (origin: string option) (response: Response) : Response =
        let headers: Headers = response?headers
        match isOriginAllowed env origin with
        | Some allowedOrigin ->
            headers.set("Access-Control-Allow-Origin", allowedOrigin)
        | None -> ()
        response

    /// Verify Bearer token auth for indexing endpoints
    let private verifyAuth (request: Request) (env: WorkerEnv) : bool =
        let authHeader = request.headers.get("Authorization")
        match authHeader with
        | Some header when header.StartsWith("Bearer ") ->
            header.Substring(7) = env.INDEX_API_KEY
        | _ -> false

    // ── Input validation ────────────────────────────────────────────

    let private maxQueryLength = 500
    let private maxLimit = 50

    /// Validate and clamp query input
    let private validateQuery (raw: string) : Result<string, string> =
        if isNullOrUndefined raw || String.IsNullOrWhiteSpace(raw) then
            Error "query is required"
        else
            let trimmed = raw.Trim()
            if trimmed.Length > maxQueryLength then
                Ok (trimmed.Substring(0, maxQueryLength))
            else
                Ok trimmed

    /// Validate and clamp limit
    let private validateLimit (raw: obj) (defaultLimit: int) : int =
        if isNullOrUndefined raw then defaultLimit
        else
            try
                let n: int = emitJsExpr raw "Number($0) | 0"
                min (max 1 n) maxLimit
            with _ -> defaultLimit

    // ── Route handlers ────────────────────────────────────────────

    /// GET /search?q=...&limit=...&type=...
    /// Fast BM25-only search for instant results
    let handleSearch (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            let startTime = DateTime.UtcNow

            let url: obj = emitJsExpr request.url "new URL($0)"
            let searchParams: obj = url?searchParams
            let query: string = searchParams?get("q") |> unbox
            let limitStr: string = searchParams?get("limit") |> unbox
            let typeStr: string = searchParams?get("type") |> unbox
            let limit = validateLimit limitStr 10
            let contentType = if isNullOrUndefined typeStr || String.IsNullOrWhiteSpace(typeStr) then None else Some typeStr

            match validateQuery query with
            | Error msg ->
                return jsonResponse {| error = msg |} 400
            | Ok query ->

            let! results = Search.bm25Search env.DB query limit contentType
            let latencyMs = int (DateTime.UtcNow - startTime).TotalMilliseconds

            return jsonResponse {|
                query = query
                results = results
                totalResults = results.Length
                searchTimeMs = latencyMs
            |} 200
        }

    /// POST /search/hybrid { query, limit, type }
    /// Full hybrid search: BM25 + vector with RRF fusion
    let handleHybridSearch (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            let startTime = DateTime.UtcNow
            let! body = request.json<obj>()
            let query: string = body?query |> unbox
            let limit = validateLimit body?limit 10
            let contentType =
                let t: string = body?``type`` |> unbox
                if isNullOrUndefined t || String.IsNullOrWhiteSpace(t) then None else Some t

            match validateQuery query with
            | Error msg ->
                return jsonResponse {| error = msg |} 400
            | Ok query ->

            // Run BM25 and vector search in parallel
            let bm25Promise = Search.bm25Search env.DB query (limit * 2) contentType
            let vectorPromise = Search.vectorSearch env.AI env.VECTORIZE query (limit * 2)
            let! bm25Results = bm25Promise
            let! vectorResults = vectorPromise

            // Hydrate metadata for vector-only results (not found by BM25)
            let bm25Ids = bm25Results |> Array.map (fun r -> r.id) |> Set.ofArray
            let! vectorHydrated = Search.hydrateVectorResults env.DB vectorResults bm25Ids

            // Fuse results with RRF (k=60)
            let fused = Search.reciprocalRankFusion bm25Results vectorResults vectorHydrated 60
            let topResults = fused |> Array.truncate limit

            let latencyMs = int (DateTime.UtcNow - startTime).TotalMilliseconds

            return jsonResponse {|
                query = query
                results = topResults
                totalResults = topResults.Length
                searchTimeMs = latencyMs
                fusionMethod = "rrf-k60"
            |} 200
        }

    /// POST /synthesize-stream { query, limit }
    /// Hybrid search + AI synthesis via SSE
    let handleSynthesizeStream (request: Request) (env: WorkerEnv) (_ctx: ExecutionContext) : JS.Promise<Response> =
        promise {
            let! body = request.json<obj>()
            let rawQuery: string = body?query |> unbox
            // `limit` here is a salience ceiling, not a fixed count — the gate may
            // return fewer. Default 10: a sharp query still gets gated down to a few,
            // a broad one can surface up to ten when that many clear the bar.
            let limit = validateLimit body?limit 10

            match validateQuery rawQuery with
            | Error msg ->
                return jsonResponse {| error = msg |} 400
            | Ok query ->

            // Hybrid search over a wide candidate pool, then fuse with provenance.
            let candidatePool = max (limit * 3) 15
            let! bm25Results = Search.bm25Search env.DB query candidatePool None
            let! vectorResults = Search.vectorSearch env.AI env.VECTORIZE query candidatePool
            let bm25Ids = bm25Results |> Array.map (fun r -> r.id) |> Set.ofArray
            let! vectorHydrated = Search.hydrateVectorResults env.DB vectorResults bm25Ids
            let fusedCandidates = Search.fuseWithProvenance bm25Results vectorResults vectorHydrated 60

            // Salience gate replaces the blind top-N truncate: drop loosely-related
            // vector neighbors and results far below the top RRF score.
            let selected = Search.selectSalient fusedCandidates limit
            let topResults = selected |> Array.map (fun c -> c.result)

            if topResults.Length = 0 then
                return jsonResponse {| error = "No results found for synthesis" |} 404
            else

            // Pull full section bodies (within a char budget) instead of 300-char
            // snippets, so the model synthesizes from real content, not fragments.
            let! full = Search.fetchFullContent env.DB topResults
            let sections = full.sections

            // Frame the prompt around the user's actual request: a question gets
            // answered, a bare term gets described. Use the RAW query (not the
            // FTS5-sanitized form) so "Tell me about X?" reaches the model intact.
            let answerMode = Search.isQuestionOrRequest rawQuery
            let prompt = Search.buildSynthesisPromptFull rawQuery answerMode sections

            // glm-4.7-flash is a reasoning model and emits chain-of-thought by
            // default, which adds latency and risks leaking the <think> scratchpad
            // when generation is cut off mid-thought. A search-result summary needs
            // no deliberation, so disable thinking at the source. Verified against
            // the Cloudflare model input schema (chat_template_kwargs.enable_thinking).
            // Gemma is not a reasoning model and ignores the flag harmlessly, so the
            // same request body serves both. max_tokens/temperature apply to both.
            let aiRequest = createObj [
                "messages" ==> [|
                    createObj [
                        "role" ==> "system"
                        "content" ==> "Respond directly and concisely. Do not include reasoning steps, analysis frameworks, or preamble. Just provide the synthesis."
                    ]
                    createObj [ "role" ==> "user"; "content" ==> prompt ]
                |]
                "max_tokens" ==> 2048
                "temperature" ==> 0.3
                "chat_template_kwargs" ==> createObj [ "enable_thinking" ==> false ]
            ]

            // Extract the summary text from a Workers AI chat response. Both models
            // return OpenAI chat completion format { choices: [{ message: {...} }] }.
            // GLM (a reasoning model) may emit chain-of-thought as <think>...</think>
            // blocks inline within `content` or as a separate `reasoning_content`
            // field; Gemma emits neither, so its plain content passes straight
            // through. The extractor handles both:
            //   1. Strip any <think>...</think> blocks from content
            //   2. If the stripped content is non-empty, return it
            //   3. Otherwise fall back to reasoning_content (some flash modes
            //      put the entire answer there when content is empty)
            //   4. Final fallbacks: legacy `response` field and raw string
            let extractText (aiResult: obj) : string =
                if isNullOrUndefined aiResult then ""
                else
                    let content: string =
                        emitJsExpr aiResult """
                            (function(r) {
                                var stripThink = function(s) {
                                    if (typeof s !== 'string') return '';
                                    return s.replace(/<think>[\s\S]*?<\/think>/g, '').trim();
                                };
                                if (r && r.choices && r.choices[0] && r.choices[0].message) {
                                    var msg = r.choices[0].message;
                                    var stripped = stripThink(msg.content || '');
                                    if (stripped.length > 0) return stripped;
                                    if (msg.reasoning_content) {
                                        var rc = stripThink(msg.reasoning_content);
                                        if (rc.length > 0) return rc;
                                    }
                                    if (msg.content) return msg.content;
                                }
                                if (r && r.response) return stripThink(r.response) || r.response;
                                if (typeof r === 'string') return stripThink(r) || r;
                                return null;
                            })($0)"""
                    if not (isNullOrUndefined content) then content
                    else ""

            // Workers AI inference intermittently stalls and never resolves, and a
            // single model can go fully dark for days (a real Cloudflare incident
            // took glm-4.7-flash offline). Without a bound the whole request hangs
            // until the client aborts; with only one model, one outage kills every
            // summary. Race each call against a timeout so a stalled model fails fast,
            // and fall through to a second model so a single-model outage still yields
            // a summary. The timeout sits above a slow but genuine response (observed
            // up to ~26s) and below the client's 30s cap so the client gets a clean
            // body. Timeouts are budgeted so GLM + Gemma both fit under the client cap.
            let runModel (model: string) (timeoutMs: int) : JS.Promise<string> =
                promise {
                    let aiCall = env.AI.run(model, aiRequest)
                    let timeout : JS.Promise<obj> =
                        emitJsExpr timeoutMs "new Promise(function(_, reject){ setTimeout(function(){ reject(new Error('AI_TIMEOUT')); }, $0); })"
                    let! aiResult =
                        Promise.race [ aiCall; timeout ]
                        |> Promise.catch (fun (e: exn) ->
                            // Swallow a timeout OR a per-model error so the fallback
                            // gets its turn — a model that 500s should not abort the
                            // whole request when a second model might still answer.
                            ignore e
                            null)
                    return extractText aiResult
                }

            // Primary: GLM. On empty (timeout or empty body), fall back to Gemma,
            // Cloudflare's recommended alternate and outside the current incident.
            // 18s + 10s keeps the worst case under the client's 30s cap.
            let! glmText = runModel "@cf/zai-org/glm-4.7-flash" 18000
            let! responseText =
                if glmText <> "" then Promise.lift glmText
                else runModel "@cf/google/gemma-4-26b-a4b-it" 10000

            if responseText = "" then
                return jsonResponse {|
                    query = query
                    results = topResults
                    synthesis = null
                    error = "AI synthesis unavailable"
                |} 200
            else

            return jsonResponse {|
                query = query
                results = topResults
                synthesis = responseText
            |} 200
        }

    /// POST /index (auth required) — batch index content
    let handleIndex (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; message = "Unauthorized" |} 401
            else

            let! body = request.json<BatchIndexRequest>()
            let sections =
                if isNullOrUndefined body || isNullOrUndefined body.sections then [||]
                else body.sections

            if sections.Length = 0 then
                return jsonResponse {| success = true; indexed = 0; unchanged = 0; failed = 0 |} 200
            else

            let! result = Indexing.indexBatch env sections
            let status = if result.failed = 0 then 200 else 207
            return jsonResponse result status
        }

    /// POST /purge-index (auth required) — clear all indexed content and vectors
    let handlePurgeIndex (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; message = "Unauthorized" |} 401
            else

            // Collect all section IDs so we can purge Vectorize
            let! idResult = env.DB.prepare("SELECT id FROM content_sections").all<obj>()
            let ids =
                match idResult.results with
                | Some r -> r |> Seq.map (fun row -> string row?id) |> Seq.toArray
                | None -> [||]

            // Delete vectors in batches (Vectorize limit is 100 per call)
            let mutable vectorsDeleted = 0
            for batch in ids |> Array.chunkBySize 100 do
                let idList = ResizeArray(batch)
                let! _ = env.VECTORIZE.deleteByIds(idList)
                vectorsDeleted <- vectorsDeleted + batch.Length

            // Delete all content_sections (triggers will clean FTS5)
            let! _ = env.DB.prepare("DELETE FROM content_sections").run<obj>()
            return jsonResponse {| success = true; message = "Index purged"; vectorsDeleted = vectorsDeleted; sectionsDeleted = ids.Length |} 200
        }

    /// POST /reconcile (auth required) — delete the stale remainder after an index pass.
    /// The CLI sends the complete set of IDs that SHOULD exist now; this deletes every
    /// D1 row and Vectorize entry whose id is not in that set. This is the prevention
    /// sweep for orphaned vectors: when content moves (content-type/slug/section-index
    /// change its id) or is deleted, the old id is never re-indexed, so without this its
    /// vector survives and keeps surfacing in search. Idempotent; safe to run every pass.
    let handleReconcile (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; message = "Unauthorized" |} 401
            else

            let! body = request.json<ReconcileRequest>()
            let validIds =
                if isNullOrUndefined body || isNullOrUndefined body.validIds then [||]
                else body.validIds

            // Guard: an empty valid set would delete the entire index. That is what
            // /purge-index is for; reconcile refuses it so a CLI bug that sends nothing
            // cannot silently wipe everything.
            if validIds.Length = 0 then
                return jsonResponse {| success = false; message = "reconcile received an empty validIds set; refusing to delete the whole index (use /purge-index for a full wipe)" |} 400
            else

            let validSet = Set.ofArray validIds

            // Every id currently in D1
            let! idResult = env.DB.prepare("SELECT id FROM content_sections").all<obj>()
            let allIds =
                match idResult.results with
                | Some r -> r |> Seq.map (fun row -> string row?id) |> Seq.toArray
                | None -> [||]

            // Stale = present in D1 but not in the valid set
            let staleIds = allIds |> Array.filter (fun id -> not (validSet.Contains id))

            // Delete stale vectors (Vectorize limit is 100 per call)
            let mutable vectorsDeleted = 0
            for batch in staleIds |> Array.chunkBySize 100 do
                let idList = ResizeArray(batch)
                let! _ = env.VECTORIZE.deleteByIds(idList)
                vectorsDeleted <- vectorsDeleted + batch.Length

            // Delete stale D1 rows (triggers clean FTS5). Bind per-id to avoid building
            // a giant IN-clause; the stale set is small in normal operation.
            let mutable rowsDeleted = 0
            for id in staleIds do
                let! _ = env.DB.prepare("DELETE FROM content_sections WHERE id = ?").bind(id).run<obj>()
                rowsDeleted <- rowsDeleted + 1

            return jsonResponse {| success = true; message = "Reconciled"; staleVectorsDeleted = vectorsDeleted; staleRowsDeleted = rowsDeleted; validCount = validIds.Length |} 200
        }

    /// POST /graph/rebuild (auth required) — idempotent full rebuild of the corpus graph.
    /// The CLI graph extractor sends the complete node + edge set from the content walk;
    /// this replaces the stored graph. Refuses an empty node set so a CLI bug cannot wipe it.
    let handleGraphRebuild (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; message = "Unauthorized" |} 401
            else

            let! body = request.json<GraphRebuildRequest>()
            let nodes =
                if isNullOrUndefined body || isNullOrUndefined body.nodes then [||]
                else body.nodes
            let edges =
                if isNullOrUndefined body || isNullOrUndefined body.edges then [||]
                else body.edges

            if nodes.Length = 0 then
                return jsonResponse {| success = false; message = "graph rebuild received an empty node set; refusing to wipe the graph" |} 400
            else

            let! result = Graph.rebuild env nodes edges
            return jsonResponse {| success = true; result = result |} 200
        }

    /// GET /graph (public, CORS) — the Cytoscape-shaped corpus graph for the Map modal.
    let handleGraph (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            let! graph = Graph.read env
            return jsonResponse graph 200
        }

    /// Handle health check endpoint
    let handleHealth () : Response =
        jsonResponse { status = "ok" } 200

    /// Handle 404 Not Found
    let handleNotFound () : Response =
        Globals.Response.Create(U2.Case1 "Not Found", !!createObj [ "status" ==> 404 ])
