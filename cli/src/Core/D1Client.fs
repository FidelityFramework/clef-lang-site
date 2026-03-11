namespace ClefLang.CLI.Core

open System.Net.Http
open System.Text
open System.Text.Json
open Fidelity.CloudEdge.Management.D1
open Fidelity.CloudEdge.Management.D1.Types

module D1Client =

    /// Wrapper operations for D1 database management
    type D1Operations(httpClient: HttpClient, accountId: string) =
        let client = Fidelity.CloudEdge.Management.D1.D1Client(httpClient)

        /// Get database ID by name from list response
        member private this.FindDatabaseByName(name: string) : Async<Result<string option, string>> =
            async {
                try
                    let! result = client.D1ListDatabases(accountId, name = name)
                    match result with
                    | D1ListDatabases.OK response ->
                        if not response.success then
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to list databases: {errorMsg}"
                        else
                            let found =
                                response.result
                                |> List.tryFind (fun db -> db.name = Some name)
                            match found with
                            | Some db -> return Ok db.uuid
                            | None -> return Ok None
                    | D1ListDatabases.BadRequest failure ->
                        return Error $"Failed to list databases: {failure.errors}"
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
                        let payload = D1CreateDatabasePayload.Create(name)
                        let! result = client.D1CreateDatabase(accountId, payload)
                        match result with
                        | D1CreateDatabase.OK response ->
                            if response.success then
                                match response.result.uuid with
                                | Some uuid -> return Ok uuid
                                | None -> return Error "Database created but UUID not found in response"
                            else
                                let errorMsg =
                                    response.errors
                                    |> List.map (fun e -> e.message)
                                    |> String.concat "; "
                                return Error $"Database creation failed: {errorMsg}"
                        | D1CreateDatabase.BadRequest failure ->
                            return Error $"Database creation failed: {failure.errors}"
                    | Error e -> return Error e
                with
                | ex -> return Error $"Failed to create D1 database: {ex.Message}"
            }

        /// Get database ID by name (public wrapper)
        member this.GetDatabaseByName(name: string) : Async<Result<string option, string>> =
            this.FindDatabaseByName(name)

        /// Get database by ID (returns name for display)
        member this.GetDatabase(databaseId: string) : Async<Result<string, string>> =
            async {
                try
                    let! result = client.D1GetDatabase(accountId, databaseId)
                    match result with
                    | D1GetDatabase.OK response ->
                        if response.success then
                            return Ok (response.result.name |> Option.defaultValue databaseId)
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to get database: {errorMsg}"
                    | D1GetDatabase.BadRequest failure ->
                        return Error $"Failed to get database: {failure.errors}"
                with
                | ex -> return Error $"Failed to get D1 database: {ex.Message}"
            }

        /// Execute SQL on database via direct HTTP (D1QueryDatabase doesn't accept a body payload)
        member this.ExecuteSQL(databaseId: string, sql: string) : Async<Result<string, string>> =
            async {
                try
                    let url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/d1/database/{databaseId}/query"
                    let payload = JsonSerializer.Serialize({| sql = sql |})
                    use content = new StringContent(payload, Encoding.UTF8, "application/json")

                    let! response = httpClient.PostAsync(url, content) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        use doc = JsonDocument.Parse(responseBody)
                        let root = doc.RootElement
                        let mutable successProp = Unchecked.defaultof<JsonElement>
                        if root.TryGetProperty("success", &successProp) && successProp.GetBoolean() then
                            let mutable resultProp = Unchecked.defaultof<JsonElement>
                            if root.TryGetProperty("result", &resultProp) then
                                return Ok (resultProp.GetRawText())
                            else
                                return Ok "[]"
                        else
                            return Error $"SQL execution failed: {responseBody}"
                    else
                        return Error $"SQL execution failed - HTTP {int response.StatusCode}: {responseBody}"
                with
                | ex -> return Error $"Failed to execute SQL: {ex.Message}"
            }

        /// Delete database
        member this.DeleteDatabase(databaseId: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.D1DeleteDatabase(accountId, databaseId)
                    match result with
                    | D1DeleteDatabase.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to delete database: {errorMsg}"
                    | D1DeleteDatabase.BadRequest failure ->
                        return Error $"Failed to delete database: {failure.errors}"
                with
                | ex -> return Error $"Failed to delete D1 database: {ex.Message}"
            }
