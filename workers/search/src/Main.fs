namespace ClefLang.Search

open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.Worker.Context

module Main =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// URL helper to parse request path
    [<Global>]
    type URL
        [<Emit("new URL($0)")>]
        (url: string) =
        member _.pathname: string = jsNative
        member _.searchParams: obj = jsNative

    /// Main fetch handler — entry point for the Cloudflare Worker
    [<ExportDefault>]
    let handler =
        createObj [
            "fetch" ==> fun (request: Request) (env: WorkerEnv) (ctx: ExecutionContext) ->
                promise {
                    let url = URL(request.url)
                    let path = url.pathname
                    let method = request.method

                    // Get Origin header for CORS handling
                    let origin =
                        let headers: Headers = request?headers
                        let originValue = headers.get("Origin")
                        if isNullOrUndefined originValue then None else Some (string originValue)

                    try
                        match method, path with
                        | "OPTIONS", _ ->
                            // CORS preflight
                            return Handlers.handleOptions env origin

                        | "GET", "/search" ->
                            // Fast BM25-only search
                            let! response = Handlers.handleSearch request env
                            return Handlers.withCORS env origin response

                        | "POST", "/search/hybrid" ->
                            // Hybrid BM25 + vector search with RRF
                            let! response = Handlers.handleHybridSearch request env
                            return Handlers.withCORS env origin response

                        | "POST", "/synthesize-stream" ->
                            // Hybrid search + AI synthesis SSE
                            let! response = Handlers.handleSynthesizeStream request env ctx
                            return Handlers.withCORS env origin response

                        | "POST", "/index" ->
                            // Batch index content (authenticated)
                            let! response = Handlers.handleIndex request env
                            return response

                        | "POST", "/purge-index" ->
                            // Clear all indexed content (authenticated)
                            let! response = Handlers.handlePurgeIndex request env
                            return response

                        | "GET", "/health" ->
                            return Handlers.handleHealth ()

                        | "GET", "/" ->
                            // Root — return service info
                            let info = createObj [
                                "name" ==> "Clef Search Worker"
                                "version" ==> "0.1.0"
                                "description" ==> "Hybrid BM25 + vector search via D1 FTS5 and Vectorize"
                                "endpoints" ==> [|
                                    createObj [ "method" ==> "GET"; "path" ==> "/search?q=&limit=&type="; "description" ==> "BM25 full-text search" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/search/hybrid"; "description" ==> "Hybrid BM25 + vector search with RRF fusion" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/synthesize-stream"; "description" ==> "Hybrid search + AI synthesis (SSE)" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/index"; "description" ==> "Batch index content (authenticated)" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/purge-index"; "description" ==> "Clear all indexed content (authenticated)" ]
                                    createObj [ "method" ==> "GET"; "path" ==> "/health"; "description" ==> "Health check" ]
                                |]
                            ]
                            return Handlers.jsonResponse info 200

                        | _ ->
                            return Handlers.handleNotFound ()

                    with ex ->
                        let error = createObj [
                            "error" ==> ex.Message
                        ]
                        return Handlers.jsonResponse error 500
                }
        ]
