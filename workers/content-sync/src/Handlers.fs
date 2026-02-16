namespace ClefLang.ContentSync

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.Worker.Context
open Fidelity.CloudEdge.Worker.Context.Globals
open Fidelity.CloudEdge.R2

module Handlers =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Verify API key authorization
    let private verifyAuth (request: Request) (env: WorkerEnv) : bool =
        let authHeader = request.headers.get("Authorization")
        match authHeader with
        | Some header when header.StartsWith("Bearer ") ->
            let token = header.Substring(7)
            token = env.SYNC_API_KEY
        | _ -> false

    /// Create JSON response using Response.json for proper serialization
    let jsonResponse (data: obj) (status: int) : Response =
        Response.json(data, !!createObj [ "status" ==> status ])

    /// Sync response type
    type SyncResult = { success: bool; key: string; message: string }

    /// Sync a single post to R2
    let private syncPost (env: WorkerEnv) (post: SyncRequest) : JS.Promise<SyncResult> =
        promise {
            try
                // Build R2 key - use slug directly for clean idempotency
                let key = $"{post.slug}.md"

                // Build custom metadata
                let summary =
                    if isNullOrUndefined post.summary || String.IsNullOrEmpty(post.summary) then ""
                    else post.summary

                let tagsStr =
                    if isNullOrUndefined post.tags || post.tags.Length = 0 then ""
                    else String.concat "," post.tags

                let context = $"Content titled '{post.title}'. URL: {post.url}"

                let metadata = createObj [
                    "title" ==> post.title
                    "date" ==> post.date
                    "summary" ==> summary
                    "tags" ==> tagsStr
                    "url" ==> post.url
                    "context" ==> context
                ]

                let httpMeta = createObj [
                    "contentType" ==> "text/markdown"
                ]

                let putOptions: R2PutOptions = !!createObj [
                    "customMetadata" ==> metadata
                    "httpMetadata" ==> httpMeta
                ]

                // Upload to R2
                let! _ = env.CONTENT.put(key, U3.Case2 post.content, putOptions)

                return { success = true; key = key; message = "Content synced" }

            with ex ->
                return { success = false; key = $"{post.slug}.md"; message = ex.Message }
        }

    /// Handle single sync request
    let handleSync (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            // Verify authorization
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; key = ""; message = "Unauthorized" |} 401
            else

            try
                // Parse request body (interface type works with plain JS objects)
                let! body = request.json<SyncRequest>()

                let! result = syncPost env body
                let status = if result.success then 200 else 500
                return jsonResponse result status

            with ex ->
                return jsonResponse {| success = false; key = ""; message = $"Parse error: {ex.Message}" |} 400
        }

    /// Handle batch sync request
    let handleBatchSync (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            // Verify authorization
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; synced = 0; failed = 0; results = [||] |} 401
            else

            try
                // Parse request body (interface type works with plain JS objects)
                let! body = request.json<BatchSyncRequest>()

                // Sync each post
                let! results =
                    body.posts
                    |> Array.map (syncPost env)
                    |> Promise.all

                let synced = results |> Array.filter (fun r -> r.success) |> Array.length
                let failed = results |> Array.filter (fun r -> not r.success) |> Array.length

                let status = if (failed = 0) then 200 else 207  // 207 Multi-Status for partial success
                return jsonResponse {| success = (failed = 0); synced = synced; failed = failed; results = results |} status

            with ex ->
                return jsonResponse {| success = false; error = ex.Message; synced = 0; failed = 0; results = [||] |} 400
        }

    /// Handle purge - delete all objects from R2 bucket
    let handlePurge (request: Request) (env: WorkerEnv) : JS.Promise<Response> =
        promise {
            // Verify authorization
            if not (verifyAuth request env) then
                return jsonResponse {| success = false; deleted = 0; message = "Unauthorized" |} 401
            else

            try
                let mutable deleted = 0
                let mutable hasMore = true
                let mutable cursor: string option = None

                while hasMore do
                    // List objects with optional cursor for pagination
                    let listOptions: R2ListOptions = !!createObj [
                        "limit" ==> 1000
                        if cursor.IsSome then "cursor" ==> cursor.Value
                    ]

                    let! listResult = env.CONTENT.list(listOptions)

                    // Delete each object
                    for obj in listResult.objects do
                        do! env.CONTENT.delete(obj.key)
                        deleted <- deleted + 1

                    // Check for more pages
                    hasMore <- listResult.truncated
                    cursor <- listResult.cursor

                return jsonResponse {| success = true; deleted = deleted; message = "Bucket purged" |} 200

            with ex ->
                return jsonResponse {| success = false; deleted = 0; message = $"Purge error: {ex.Message}" |} 500
        }

    /// Handle health check
    let handleHealth () : Response =
        jsonResponse {| status = "ok"; service = "content-sync" |} 200

    /// Handle not found
    let handleNotFound () : Response =
        jsonResponse {| error = "Not Found" |} 404
