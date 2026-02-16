namespace ClefLang.CLI.Core

open System.Net.Http
open System.Text
open System.Text.Json

module VectorizeClient =

    /// Parse Cloudflare API response envelope, returning (success, result, errorMsg)
    let private parseResponse (content: string) =
        use doc = JsonDocument.Parse(content)
        let root = doc.RootElement
        let success =
            match root.TryGetProperty("success") with
            | true, el -> el.GetBoolean()
            | false, _ -> false
        let result =
            match root.TryGetProperty("result") with
            | true, el -> Some (el.GetRawText())
            | false, _ -> None
        let errorMsg =
            match root.TryGetProperty("errors") with
            | true, el when el.GetArrayLength() > 0 ->
                el.EnumerateArray()
                |> Seq.map (fun e ->
                    match e.TryGetProperty("message") with
                    | true, m -> m.GetString()
                    | false, _ -> "Unknown error")
                |> String.concat "; "
            | _ -> ""
        (success, result, errorMsg)

    /// Wrapper operations for Vectorize index management via direct HTTP calls
    type VectorizeOperations(httpClient: HttpClient, accountId: string) =

        let baseUrl =
            let origin = httpClient.BaseAddress.OriginalString.TrimEnd('/')
            $"{origin}/accounts/{accountId}/vectorize/v2/indexes"

        /// Build a full URI for a sub-path
        member private _.Uri(path: string) = System.Uri(baseUrl + path)

        /// POST JSON and return response body
        member private this.PostJson(path: string, jsonBody: string) : Async<string> =
            async {
                let content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                let request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, this.Uri(path), Content = content)
                let! response = httpClient.SendAsync(request) |> Async.AwaitTask
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return body
            }

        /// GET and return response body
        member private this.Get(path: string) : Async<string> =
            async {
                let request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, this.Uri(path))
                let! response = httpClient.SendAsync(request) |> Async.AwaitTask
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return body
            }

        /// Create Vectorize index with a preset (idempotent — returns Ok if already exists)
        member this.CreateIndex(indexName: string, preset: string) : Async<Result<string, string>> =
            async {
                try
                    // Check if index already exists
                    let! getBody = this.Get($"/{indexName}")
                    let (success, _, _) = parseResponse getBody
                    if success then
                        return Ok $"Index '{indexName}' already exists"
                    else
                        return! this.DoCreateIndex(indexName, preset)
                with
                | _ ->
                    return! this.DoCreateIndex(indexName, preset)
            }

        member private this.DoCreateIndex(indexName: string, preset: string) : Async<Result<string, string>> =
            async {
                try
                    let json = $"""{{ "name": "{indexName}", "config": {{ "preset": "{preset}" }} }}"""
                    let! body = this.PostJson("", json)
                    let (success, _, errorMsg) = parseResponse body
                    if success then
                        return Ok $"Index '{indexName}' created"
                    else
                        let msg = if errorMsg <> "" then errorMsg else body
                        return Error $"Index creation failed: {msg}"
                with
                | ex -> return Error $"Failed to create Vectorize index: {ex.Message}"
            }

        /// Create metadata index for filtering on a property
        member this.CreateMetadataIndex(indexName: string, propertyName: string) : Async<Result<unit, string>> =
            async {
                try
                    let json = $"""{{ "propertyName": "{propertyName}", "indexType": "string" }}"""
                    let! body = this.PostJson($"/{indexName}/metadata_index/create", json)
                    let (success, _, errorMsg) = parseResponse body
                    if success then
                        return Ok ()
                    else
                        let msg = if errorMsg <> "" then errorMsg else body
                        return Error $"Metadata index creation failed: {msg}"
                with
                | ex -> return Error $"Failed to create metadata index: {ex.Message}"
            }

        /// Delete Vectorize index
        member this.DeleteIndex(indexName: string) : Async<Result<unit, string>> =
            async {
                try
                    let request = new HttpRequestMessage(System.Net.Http.HttpMethod.Delete, this.Uri($"/{indexName}"))
                    let! response = httpClient.SendAsync(request) |> Async.AwaitTask
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    let (success, _, errorMsg) = parseResponse body
                    if success then
                        return Ok ()
                    else
                        let msg = if errorMsg <> "" then errorMsg else body
                        return Error $"Failed to delete index: {msg}"
                with
                | ex -> return Error $"Failed to delete Vectorize index: {ex.Message}"
            }
