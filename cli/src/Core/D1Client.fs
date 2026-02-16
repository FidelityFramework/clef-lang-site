namespace ClefLang.CLI.Core

open System.Net.Http
open System.Text.Json
open Fidelity.CloudEdge.Management.D1
open Fidelity.CloudEdge.Management.D1.Types

module D1Client =

    /// Wrapper operations for D1 database management
    type D1Operations(httpClient: HttpClient, accountId: string) =
        let client = D1Client(httpClient)

        /// Get database ID by name from list response
        member private this.FindDatabaseByName(name: string) : Async<Result<string option, string>> =
            async {
                try
                    let! result = client.CloudflareD1ListDatabases(accountId, name = name)
                    match result with
                    | CloudflareD1ListDatabases.OK response ->
                        if not response.success then
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to list databases: {errorMsg}"
                        else
                            match response.result with
                            | Some resultElement when resultElement.ValueKind = JsonValueKind.Array ->
                                let databases = resultElement.EnumerateArray() |> Seq.toList
                                let found = databases |> List.tryFind (fun db ->
                                    match db.TryGetProperty("name") with
                                    | true, nameProp -> nameProp.GetString() = name
                                    | false, _ -> false)
                                match found with
                                | Some db ->
                                    match db.TryGetProperty("uuid") with
                                    | true, uuidProp -> return Ok (Some (uuidProp.GetString()))
                                    | false, _ -> return Ok None
                                | None -> return Ok None
                            | _ -> return Ok None
                with
                | ex -> return Error $"Failed to list D1 databases: {ex.Message}"
            }

        /// Create database (returns existing ID if already exists)
        member this.CreateDatabase(name: string) : Async<Result<string, string>> =
            async {
                try
                    // First check if it exists
                    let! existing = this.FindDatabaseByName(name)
                    match existing with
                    | Ok (Some id) -> return Ok id
                    | Ok None ->
                        // Create new
                        let payload = CloudflareD1CreateDatabasePayload.Create(name)
                        let! result = client.CloudflareD1CreateDatabase(accountId, payload)
                        match result with
                        | CloudflareD1CreateDatabase.OK response ->
                            if response.success then
                                // Extract UUID from result
                                match response.result with
                                | Some resultElement ->
                                    match resultElement.TryGetProperty("uuid") with
                                    | true, uuidProp -> return Ok (uuidProp.GetString())
                                    | false, _ -> return Error "Database created but UUID not found in response"
                                | None -> return Error "Database created but result was empty"
                            else
                                let errorMsg =
                                    response.errors
                                    |> List.map (fun e -> e.message)
                                    |> String.concat "; "
                                return Error $"Database creation failed: {errorMsg}"
                    | Error e -> return Error e
                with
                | ex -> return Error $"Failed to create D1 database: {ex.Message}"
            }

        /// Get database ID by name (public wrapper)
        member this.GetDatabaseByName(name: string) : Async<Result<string option, string>> =
            this.FindDatabaseByName(name)

        /// Get database by ID (returns JSON string)
        member this.GetDatabase(databaseId: string) : Async<Result<string, string>> =
            async {
                try
                    let! result = client.CloudflareD1GetDatabase(accountId, databaseId)
                    match result with
                    | CloudflareD1GetDatabase.OK response ->
                        if response.success then
                            match response.result with
                            | Some resultElement -> return Ok (resultElement.GetRawText())
                            | None -> return Ok "{}"
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to get database: {errorMsg}"
                with
                | ex -> return Error $"Failed to get D1 database: {ex.Message}"
            }

        /// Execute SQL on database
        member this.ExecuteSQL(databaseId: string, sql: string) : Async<Result<string, string>> =
            async {
                try
                    let payload = CloudflareD1QueryDatabasePayload.Create(sql)
                    let! result = client.CloudflareD1QueryDatabase(accountId, databaseId, payload)
                    match result with
                    | CloudflareD1QueryDatabase.OK response ->
                        if response.success then
                            match response.result with
                            | Some resultElement -> return Ok (resultElement.GetRawText())
                            | None -> return Ok "[]"
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"SQL execution failed: {errorMsg}"
                with
                | ex -> return Error $"Failed to execute SQL: {ex.Message}"
            }

        /// Delete database
        member this.DeleteDatabase(databaseId: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.CloudflareD1DeleteDatabase(accountId, databaseId)
                    match result with
                    | CloudflareD1DeleteDatabase.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to delete database: {errorMsg}"
                with
                | ex -> return Error $"Failed to delete D1 database: {ex.Message}"
            }
