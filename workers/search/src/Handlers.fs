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

            // Fuse results with RRF (k=60)
            let fused = Search.reciprocalRankFusion bm25Results vectorResults 60
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
            let query: string = body?query |> unbox
            let limit = validateLimit body?limit 5

            match validateQuery query with
            | Error msg ->
                return jsonResponse {| error = msg |} 400
            | Ok query ->

            // Hybrid search first
            let! bm25Results = Search.bm25Search env.DB query (limit * 2) None
            let! vectorResults = Search.vectorSearch env.AI env.VECTORIZE query (limit * 2)
            let fused = Search.reciprocalRankFusion bm25Results vectorResults 60
            let topResults = fused |> Array.truncate limit

            if topResults.Length = 0 then
                return jsonResponse {| error = "No results found for synthesis" |} 404
            else

            // Build prompt and call Workers AI (non-streaming for short synthesis)
            let prompt = Search.buildSynthesisPrompt query topResults

            let aiRequest = createObj [
                "messages" ==> [|
                    createObj [ "role" ==> "user"; "content" ==> prompt ]
                |]
                "max_tokens" ==> 256
                "temperature" ==> 0.3
            ]

            let! aiResult = env.AI.run("@cf/zai-org/glm-4.7-flash", aiRequest)

            // Workers AI non-streaming returns { response: "..." }
            let responseText: string =
                let r: obj = aiResult?response
                if not (isNullOrUndefined r) then string r
                else
                    // Some models return { result: { response: "..." } }
                    let r2: obj = aiResult?result?response
                    if not (isNullOrUndefined r2) then string r2
                    else ""

            if responseText = "" then
                return jsonResponse {|
                    query = query
                    results = topResults
                    synthesis = null
                    error = "AI returned empty response"
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

    /// POST /purge-index (auth required) — clear all indexed content
    let handlePurgeIndex (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; message = "Unauthorized" |} 401
            else

            // Delete all content_sections (triggers will clean FTS5)
            let! _ = env.DB.prepare("DELETE FROM content_sections").run<obj>()
            return jsonResponse {| success = true; message = "Index purged" |} 200
        }

    /// Handle health check endpoint
    let handleHealth () : Response =
        jsonResponse { status = "ok" } 200

    /// Handle 404 Not Found
    let handleNotFound () : Response =
        Globals.Response.Create(U2.Case1 "Not Found", !!createObj [ "status" ==> 404 ])
