namespace ClefLang.AskAI

open Fable.Core
open Fable.Core.JsInterop
open CloudFlare.Worker.Context

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

    /// Main fetch handler - entry point for the Cloudflare Worker
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

                        | "POST", "/synthesize" ->
                            // Synthesize summary from pre-ranked BM25 results
                            let! response = Handlers.handleSynthesizeRequest request env ctx
                            return Handlers.withCORS env origin response

                        | "POST", "/synthesize-stream" ->
                            // Streaming synthesis (SSE)
                            let! response = Handlers.handleSynthesizeStreamRequest request env ctx
                            return Handlers.withCORS env origin response

                        | "GET", "/health" ->
                            return Handlers.handleHealth ()

                        | "GET", "/" ->
                            // Root - return info
                            let info = createObj [
                                "name" ==> "Clef Ask AI Worker"
                                "version" ==> "0.1.0"
                                "description" ==> "Synthesizes summaries from BM25 search results via Workers AI"
                                "endpoints" ==> [|
                                    createObj [ "method" ==> "POST"; "path" ==> "/synthesize"; "description" ==> "Generate summary from search results" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/synthesize-stream"; "description" ==> "Stream summary from search results (SSE)" ]
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
