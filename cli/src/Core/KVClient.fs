namespace ClefLang.CLI.Core

open System.Net.Http
open Fidelity.CloudEdge.Management.KV
open Fidelity.CloudEdge.Management.KV.Types

module KVClient =

    /// KV Namespace operations via Fidelity.CloudEdge Management KV client
    type KVOperations(httpClient: HttpClient, accountId: string) =
        let client = KVClient(httpClient)

        /// Create namespace (returns existing ID if already exists)
        member this.CreateNamespace(title: string) : Async<Result<string, string>> =
            async {
                try
                    // First check if it exists
                    let! existing = this.GetNamespaceByTitle(title)
                    match existing with
                    | Ok (Some id) -> return Ok id
                    | Error e -> return Error e
                    | Ok None ->
                        // Create new
                        let payload = ``workers-kvcreaterenamenamespacebody``.Create(title)
                        let! result = client.WorkersKvNamespaceCreateANamespace(accountId, payload)
                        match result with
                        | WorkersKvNamespaceCreateANamespace.OK response ->
                            match response.result with
                            | Some resultObj ->
                                return Ok resultObj.id
                            | None -> return Error "KV namespace created but ID not found in response"
                        | WorkersKvNamespaceCreateANamespace.BadRequest failure ->
                            return Error $"Failed to create KV namespace: {failure.errors}"
                with
                | ex -> return Error $"Failed to create KV namespace: {ex.Message}"
            }

        /// Get namespace ID by title
        member this.GetNamespaceByTitle(title: string) : Async<Result<string option, string>> =
            async {
                try
                    let! result = client.WorkersKvNamespaceListNamespaces(accountId)
                    match result with
                    | WorkersKvNamespaceListNamespaces.OK response ->
                        let found =
                            response.result
                            |> Option.bind (fun namespaces ->
                                namespaces |> List.tryFind (fun ns -> ns.title = title))
                            |> Option.map (fun ns -> ns.id)
                        return Ok found
                    | WorkersKvNamespaceListNamespaces.BadRequest failure ->
                        return Error $"Failed to list KV namespaces: {failure.errors}"
                with
                | ex -> return Error $"Failed to list KV namespaces: {ex.Message}"
            }

        /// List all namespaces
        member this.ListNamespaces() : Async<Result<string, string>> =
            async {
                try
                    let! result = client.WorkersKvNamespaceListNamespaces(accountId)
                    match result with
                    | WorkersKvNamespaceListNamespaces.OK response ->
                        match response.result with
                        | Some namespaces ->
                            let names = namespaces |> List.map (fun ns -> $"{ns.title} ({ns.id})") |> String.concat ", "
                            return Ok names
                        | None -> return Ok "[]"
                    | WorkersKvNamespaceListNamespaces.BadRequest failure ->
                        return Error $"Failed to list KV namespaces: {failure.errors}"
                with
                | ex -> return Error $"Failed to list KV namespaces: {ex.Message}"
            }

        /// Delete namespace
        member this.DeleteNamespace(namespaceId: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.WorkersKvNamespaceRemoveANamespace(namespaceId, accountId)
                    match result with
                    | WorkersKvNamespaceRemoveANamespace.OK _ ->
                        return Ok ()
                    | WorkersKvNamespaceRemoveANamespace.BadRequest failure ->
                        return Error $"Failed to delete KV namespace: {failure.errors}"
                with
                | ex -> return Error $"Failed to delete KV namespace: {ex.Message}"
            }
