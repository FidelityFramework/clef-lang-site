namespace ClefLang.AskAI

open System
open Fable.Core
open Fable.Core.JsInterop
open CloudFlare.Worker.Context
open CloudFlare.Worker.Context.Globals
open CloudFlare.D1

module Handlers =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Log query to D1 for analytics
    let logQuery (db: D1Database) (entry: QueryLogEntry) : JS.Promise<unit> =
        promise {
            let sql = """
                INSERT INTO query_log (
                    id, query_text, timestamp,
                    response_latency_ms, source_urls, source_count
                ) VALUES (?, ?, ?, ?, ?, ?)
            """
            let stmt = db.prepare(sql)
            let bound = stmt.bind(
                entry.Id,
                entry.QueryText,
                entry.Timestamp.ToString("o"),
                entry.ResponseLatencyMs,
                entry.SourceUrls,
                entry.SourceCount
            )
            let! _ = bound.run<obj>()
            return ()
        }

    /// Create JSON response
    let jsonResponse (data: obj) (status: int) : Response =
        Response.json(data, !!createObj [ "status" ==> status ])

    /// Build the synthesis prompt from search results
    let private buildSynthesisPrompt (query: string) (results: SearchResult array) : string =
        let contextParts =
            results
            |> Array.mapi (fun i r ->
                $"[{i + 1}] {r.title}\n{r.snippet}")
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

    /// Handle the /synthesize POST endpoint
    /// Takes pre-ranked BM25 results and returns an AI-generated summary
    let handleSynthesizeRequest (request: Request) (env: WorkerEnv) (ctx: ExecutionContext) : JS.Promise<Response> =
        promise {
            let startTime = DateTime.UtcNow
            let queryId = Guid.NewGuid().ToString()

            // Parse request body
            let! body = request.json<SynthesizeRequest>()
            let query = body.query

            if isNullOrUndefined query || String.IsNullOrWhiteSpace(query) then
                return jsonResponse { error = "Query is required" } 400
            else

            let results = if isNullOrUndefined body.results then [||] else body.results

            if results.Length = 0 then
                return jsonResponse { error = "At least one search result is required" } 400
            else

            // Build prompt and call Workers AI
            let prompt = buildSynthesisPrompt query results

            let aiRequest = createObj [
                "messages" ==> [|
                    createObj [ "role" ==> "user"; "content" ==> prompt ]
                |]
                "max_tokens" ==> 256
                "temperature" ==> 0.3
            ]

            let! aiResult = env.AI.run("@cf/meta/llama-3.1-8b-instruct", aiRequest)
            let response: obj = aiResult
            let summary: string =
                if not (isNullOrUndefined response?response) then
                    string response?response
                else
                    "Unable to generate summary."

            let latencyMs = int (DateTime.UtcNow - startTime).TotalMilliseconds

            // Log query analytics in background
            let logEntry = {
                Id = queryId
                QueryText = query
                Timestamp = startTime
                ResponseLatencyMs = latencyMs
                SourceUrls = results |> Array.map (fun s -> s.url) |> String.concat ","
                SourceCount = results.Length
            }
            ctx.waitUntil(logQuery env.DB logEntry |> unbox)

            return jsonResponse { summary = summary; sourceCount = results.Length } 200
        }

    /// Handle streaming /synthesize-stream POST endpoint
    /// Returns SSE with AI summary chunks
    let handleSynthesizeStreamRequest (request: Request) (env: WorkerEnv) (ctx: ExecutionContext) : JS.Promise<Response> =
        promise {
            let startTime = DateTime.UtcNow
            let queryId = Guid.NewGuid().ToString()

            // Parse request body
            let! body = request.json<SynthesizeRequest>()
            let query = body.query

            if isNullOrUndefined query || String.IsNullOrWhiteSpace(query) then
                return jsonResponse { error = "Query is required" } 400
            else

            let results = if isNullOrUndefined body.results then [||] else body.results

            if results.Length = 0 then
                return jsonResponse { error = "At least one search result is required" } 400
            else

            // Build prompt and call Workers AI with streaming
            let prompt = buildSynthesisPrompt query results

            let aiRequest = createObj [
                "messages" ==> [|
                    createObj [ "role" ==> "user"; "content" ==> prompt ]
                |]
                "max_tokens" ==> 256
                "temperature" ==> 0.3
                "stream" ==> true
            ]

            let! streamResponse = env.AI.run("@cf/meta/llama-3.1-8b-instruct", aiRequest)

            // Create a TransformStream to build our SSE response
            let transformStream: obj = emitJsExpr () "new TransformStream()"
            let readable: obj = transformStream?readable
            let writable: obj = transformStream?writable
            let writer: obj = writable?getWriter()
            let encoder: obj = emitJsExpr () "new TextEncoder()"

            // Helper to write SSE event
            let writeEvent (eventType: string) (data: obj) : JS.Promise<unit> =
                let json = JS.JSON.stringify(data)
                let sseData = $"event: {eventType}\ndata: {json}\n\n"
                let encoded: obj = encoder?encode(sseData)
                writer?write(encoded) |> unbox

            // Process the AI stream in background
            let processStream () : JS.Promise<unit> =
                promise {
                    try
                        let body: obj = streamResponse?body
                        if not (isNullOrUndefined body) then
                            let reader: obj = body?getReader()
                            let decoder: obj = emitJsExpr () "new TextDecoder()"
                            let mutable isDone = false
                            let mutable fullText = ""
                            let mutable buffer = ""

                            while not isDone do
                                let! result = reader?read() |> unbox<JS.Promise<obj>>
                                let done': bool = result?``done`` |> unbox
                                let value: obj = result?value

                                if done' then
                                    isDone <- true
                                else if not (isNullOrUndefined value) then
                                    let chunk: string = decoder?decode(value) |> unbox
                                    buffer <- buffer + chunk

                                    // Process complete SSE messages (end with \n\n)
                                    let parts = buffer.Split([|"\n\n"|], StringSplitOptions.None)
                                    if parts.Length > 1 then
                                        for i in 0 .. parts.Length - 2 do
                                            let line = parts.[i]
                                            if line.StartsWith("data: ") then
                                                try
                                                    let json = line.Substring(6)
                                                    let parsed: obj = JS.JSON.parse(json)
                                                    let response: obj = parsed?response
                                                    if not (isNullOrUndefined response) then
                                                        let text = string response
                                                        fullText <- fullText + text
                                                        do! writeEvent "chunk" {| text = text |}
                                                with _ -> ()
                                        buffer <- parts.[parts.Length - 1]

                            // Send done event
                            do! writeEvent "done" {| complete = true; fullText = fullText |}

                            // Log query analytics
                            let latencyMs = int (DateTime.UtcNow - startTime).TotalMilliseconds
                            let logEntry = {
                                Id = queryId
                                QueryText = query
                                Timestamp = startTime
                                ResponseLatencyMs = latencyMs
                                SourceUrls = results |> Array.map (fun s -> s.url) |> String.concat ","
                                SourceCount = results.Length
                            }
                            ctx.waitUntil(logQuery env.DB logEntry |> unbox)

                            do! writer?close() |> unbox<JS.Promise<unit>>
                        else
                            do! writeEvent "error" {| message = "No response body" |}
                            do! writer?close() |> unbox<JS.Promise<unit>>
                    with ex ->
                        do! writeEvent "error" {| message = ex.Message |}
                        do! writer?close() |> unbox<JS.Promise<unit>>
                }

            // Start processing (don't await - let it run in background)
            processStream () |> ignore

            // Return SSE response immediately with the readable stream
            let headers = Globals.Headers.Create()
            headers.set("Content-Type", "text/event-stream")
            headers.set("Cache-Control", "no-cache")
            headers.set("Connection", "keep-alive")

            return Globals.Response.Create(!!readable, !!createObj [
                "status" ==> 200
                "headers" ==> headers
            ])
        }

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
        headers.set("Access-Control-Allow-Methods", "POST, OPTIONS")
        headers.set("Access-Control-Allow-Headers", "Content-Type")
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

    /// Handle health check endpoint
    let handleHealth () : Response =
        jsonResponse { status = "ok" } 200

    /// Handle 404 Not Found
    let handleNotFound () : Response =
        Globals.Response.Create(U2.Case1 "Not Found", !!createObj [ "status" ==> 404 ])
