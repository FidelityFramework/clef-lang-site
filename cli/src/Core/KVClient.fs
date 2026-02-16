namespace ClefLang.CLI.Core

open System.Net.Http
open System.Text
open System.Text.Json

module KVClient =

    /// KV Namespace operations via direct Cloudflare API
    /// Note: Fidelity.CloudEdge doesn't have KV Management bindings yet
    type KVOperations(httpClient: HttpClient, accountId: string) =

        let baseUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces"

        /// Create namespace (returns existing ID if already exists)
        member this.CreateNamespace(title: string) : Async<Result<string, string>> =
            async {
                try
                    // First check if it exists
                    let! existing = this.GetNamespaceByTitle(title)
                    match existing with
                    | Ok (Some id) -> return Ok id
                    | Ok None ->
                        // Create new
                        let payload = JsonSerializer.Serialize({| title = title |})
                        use content = new StringContent(payload, Encoding.UTF8, "application/json")

                        let! response = httpClient.PostAsync(baseUrl, content) |> Async.AwaitTask
                        let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                        if response.IsSuccessStatusCode then
                            use doc = JsonDocument.Parse(responseBody)
                            let id = doc.RootElement.GetProperty("result").GetProperty("id").GetString()
                            return Ok id
                        else
                            return Error $"Failed to create KV namespace: {responseBody}"
                    | Error e -> return Error e
                with
                | ex -> return Error $"Failed to create KV namespace: {ex.Message}"
            }

        /// Get namespace ID by title
        member this.GetNamespaceByTitle(title: string) : Async<Result<string option, string>> =
            async {
                try
                    let! response = httpClient.GetAsync(baseUrl) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        use doc = JsonDocument.Parse(responseBody)
                        let ns =
                            doc.RootElement.GetProperty("result").EnumerateArray()
                            |> Seq.tryFind (fun n -> n.GetProperty("title").GetString() = title)
                            |> Option.map (fun n -> n.GetProperty("id").GetString())
                        return Ok ns
                    else
                        return Error $"Failed to list KV namespaces: {responseBody}"
                with
                | ex -> return Error $"Failed to list KV namespaces: {ex.Message}"
            }

        /// List all namespaces
        member this.ListNamespaces() : Async<Result<string, string>> =
            async {
                try
                    let! response = httpClient.GetAsync(baseUrl) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        return Ok responseBody
                    else
                        return Error $"Failed to list KV namespaces: {responseBody}"
                with
                | ex -> return Error $"Failed to list KV namespaces: {ex.Message}"
            }

        /// Delete namespace
        member this.DeleteNamespace(namespaceId: string) : Async<Result<unit, string>> =
            async {
                try
                    let url = $"{baseUrl}/{namespaceId}"
                    let! response = httpClient.DeleteAsync(url) |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        return Ok ()
                    else
                        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                        return Error $"Failed to delete KV namespace: {body}"
                with
                | ex -> return Error $"Failed to delete KV namespace: {ex.Message}"
            }
