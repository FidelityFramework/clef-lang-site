namespace ClefLang.CLI.Core

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open Fidelity.CloudEdge.Management.Workers
open Fidelity.CloudEdge.Management.Workers.Types

module WorkersClient =

    /// Worker binding types for deployment
    type WorkerBinding =
        | R2Bucket of name: string * bucketName: string
        | D1Database of name: string * databaseId: string
        | AIBinding of name: string
        | VectorizeIndex of name: string * indexName: string
        | PlainText of name: string * value: string

    /// Worker deployment metadata
    type WorkerMetadata = {
        MainModule: string
        CompatibilityDate: string
        CompatibilityFlags: string list
        Bindings: WorkerBinding list
    }

    /// Wrapper operations for Workers management
    type WorkersOperations(httpClient: HttpClient, accountId: string) =
        let client = Fidelity.CloudEdge.Management.Workers.WorkersClient(httpClient)

        /// Build metadata JSON for worker bindings (ES module format)
        let buildMetadataJson (metadata: WorkerMetadata) =
            let bindings =
                metadata.Bindings
                |> List.map (fun binding ->
                    match binding with
                    | R2Bucket (name, bucketName) ->
                        $"""{{ "type": "r2_bucket", "name": "{name}", "bucket_name": "{bucketName}" }}"""
                    | D1Database (name, databaseId) ->
                        $"""{{ "type": "d1", "name": "{name}", "id": "{databaseId}" }}"""
                    | AIBinding name ->
                        $"""{{ "type": "ai", "name": "{name}" }}"""
                    | VectorizeIndex (name, indexName) ->
                        $"""{{ "type": "vectorize", "name": "{name}", "index_name": "{indexName}" }}"""
                    | PlainText (name, value) ->
                        $"""{{ "type": "plain_text", "name": "{name}", "text": "{value}" }}"""
                )
                |> String.concat ","

            let bindingsArray = $"[{bindings}]"
            $"""{{ "main_module": "{metadata.MainModule}", "compatibility_date": "{metadata.CompatibilityDate}", "bindings": {bindingsArray} }}"""

        /// Get account subdomain for workers.dev
        member this.GetSubdomain() : Async<Result<string option, string>> =
            async {
                try
                    let! result = client.WorkerSubdomainGetSubdomain(accountId)
                    match result with
                    | WorkerSubdomainGetSubdomain.OK response ->
                        if response.success then
                            return Ok (Some response.result.subdomain)
                        else
                            return Ok None
                    | WorkerSubdomainGetSubdomain.BadRequest _ ->
                        return Ok None
                with
                | ex -> return Error $"Failed to get subdomain: {ex.Message}"
            }

        /// List all workers
        member this.ListWorkers() : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.WorkerScriptListWorkers(accountId)
                    match result with
                    | WorkerScriptListWorkers.OK _ -> return Ok ()
                    | WorkerScriptListWorkers.BadRequest failure ->
                        return Error $"Failed to list workers: {failure.errors}"
                with
                | ex -> return Error $"Failed to list workers: {ex.Message}"
            }

        /// Delete a worker
        member this.DeleteWorker(scriptName: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.WorkerScriptDeleteWorker(accountId, scriptName)
                    match result with
                    | WorkerScriptDeleteWorker.OK _ -> return Ok ()
                    | WorkerScriptDeleteWorker.BadRequest failure ->
                        return Error $"Failed to delete worker: {failure.errors}"
                with
                | ex -> return Error $"Failed to delete worker: {ex.Message}"
            }

        /// Upload worker module with bindings using multipart form data
        member this.UploadWorkerWithBindings
            (scriptName: string)
            (workerCode: string)
            (metadata: WorkerMetadata)
            : Async<Result<unit, string>> =
            async {
                try
                    let url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/workers/scripts/{scriptName}"

                    use content = new MultipartFormDataContent()

                    // Add metadata part
                    let metadataJson = buildMetadataJson metadata
                    let metadataContent = new StringContent(metadataJson, Encoding.UTF8, "application/json")
                    content.Add(metadataContent, "metadata")

                    // Add worker script part with ES module content type
                    let workerContent = new StringContent(workerCode, Encoding.UTF8, "application/javascript+module")
                    content.Add(workerContent, metadata.MainModule, metadata.MainModule)

                    use request = new HttpRequestMessage(HttpMethod.Put, url)
                    request.Content <- content

                    let! response = httpClient.SendAsync(request) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        return Ok ()
                    else
                        // Try to parse error from response
                        try
                            use doc = JsonDocument.Parse(responseBody)
                            let mutable errorsElement = Unchecked.defaultof<JsonElement>
                            let errors =
                                if doc.RootElement.TryGetProperty("errors", &errorsElement) then
                                    errorsElement.EnumerateArray()
                                    |> Seq.map (fun e ->
                                        let mutable msgElement = Unchecked.defaultof<JsonElement>
                                        if e.TryGetProperty("message", &msgElement) then
                                            msgElement.GetString()
                                        else "Unknown error")
                                    |> String.concat "; "
                                else
                                    responseBody
                            return Error $"Upload failed ({response.StatusCode}): {errors}"
                        with _ ->
                            return Error $"Upload failed ({response.StatusCode}): {responseBody}"
                with
                | ex -> return Error $"Failed to upload worker: {ex.Message}"
            }

        /// Upload worker module (legacy - uses generated client, doesn't support bindings)
        member this.UploadWorkerModule(scriptName: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.WorkerScriptUploadWorkerModule(accountId, scriptName)
                    match result with
                    | WorkerScriptUploadWorkerModule.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Upload failed: {errorMsg}"
                    | WorkerScriptUploadWorkerModule.BadRequest failure ->
                        return Error $"Upload failed: {failure.errors}"
                with
                | ex -> return Error $"Failed to upload worker: {ex.Message}"
            }

        /// Get worker versions
        member this.ListVersions(scriptName: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.WorkerVersionsListVersions(accountId, scriptName)
                    match result with
                    | WorkerVersionsListVersions.OK _ -> return Ok ()
                    | WorkerVersionsListVersions.BadRequest _ -> return Error "Failed to list versions"
                with
                | ex -> return Error $"Failed to list versions: {ex.Message}"
            }

        /// Create/update subdomain
        member this.CreateSubdomain(subdomain: string) : Async<Result<unit, string>> =
            async {
                try
                    let body = ``workersschemas-subdomain``.Create(subdomain)
                    let! result = client.WorkerSubdomainCreateSubdomain(accountId, body)
                    match result with
                    | WorkerSubdomainCreateSubdomain.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to create subdomain: {errorMsg}"
                    | WorkerSubdomainCreateSubdomain.BadRequest failure ->
                        return Error $"Failed to create subdomain: {failure.errors}"
                with
                | ex -> return Error $"Failed to create subdomain: {ex.Message}"
            }

        /// Enable workers.dev subdomain for a specific script
        member this.EnableWorkersDevSubdomain(scriptName: string) : Async<Result<unit, string>> =
            async {
                try
                    let url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/workers/scripts/{scriptName}/subdomain"
                    let payload = """{ "enabled": true, "previews_enabled": false }"""
                    use content = new StringContent(payload, Encoding.UTF8, "application/json")
                    use request = new HttpRequestMessage(HttpMethod.Post, url)
                    request.Content <- content

                    let! response = httpClient.SendAsync(request) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        return Ok ()
                    else
                        return Error $"Failed to enable workers.dev subdomain: {responseBody}"
                with
                | ex -> return Error $"Failed to enable workers.dev subdomain: {ex.Message}"
            }
