namespace ClefLang.ContentSync

open Fable.Core
open Fable.Core.JsInterop
open CloudFlare.Worker.Context

module Main =

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

                    try
                        match method, path with
                        | "POST", "/sync" ->
                            // Single post sync
                            return! Handlers.handleSync request env

                        | "POST", "/batch" ->
                            // Batch sync multiple posts
                            return! Handlers.handleBatchSync request env

                        | "POST", "/purge" ->
                            // Purge all objects from R2 bucket
                            return! Handlers.handlePurge request env

                        | "GET", "/health" ->
                            return Handlers.handleHealth ()

                        | "GET", "/" ->
                            // Root - return info
                            let info = createObj [
                                "name" ==> "Clef Content Sync Worker"
                                "version" ==> "0.1.0"
                                "endpoints" ==> [|
                                    createObj [ "method" ==> "POST"; "path" ==> "/sync"; "description" ==> "Sync single post" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/batch"; "description" ==> "Batch sync posts" ]
                                    createObj [ "method" ==> "POST"; "path" ==> "/purge"; "description" ==> "Purge all content from R2" ]
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
