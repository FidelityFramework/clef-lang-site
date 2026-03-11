namespace ClefLang.CLI.Core

open System.Net.Http
open Fidelity.CloudEdge.Management.R2
open Fidelity.CloudEdge.Management.R2.Types

module R2Client =

    /// Wrapper operations for R2 bucket management
    type R2Operations(httpClient: HttpClient, accountId: string) =
        let client = R2Client(httpClient)

        /// Check if bucket exists by name
        member private this.BucketExists(bucketName: string) : Async<bool> =
            async {
                try
                    let! listResult = client.R2ListBuckets(accountId, nameContains = bucketName)
                    match listResult with
                    | R2ListBuckets.OK response ->
                        match response.result.buckets with
                        | Some buckets ->
                            return buckets |> List.exists (fun b -> b.name = Some bucketName)
                        | None -> return false
                    | R2ListBuckets.BadRequest _ -> return false
                with
                | _ -> return false
            }

        /// Create bucket (idempotent - checks if exists first)
        member this.CreateBucket(bucketName: string) : Async<Result<unit, string>> =
            async {
                try
                    // First check if bucket exists
                    let! exists = this.BucketExists(bucketName)
                    if exists then
                        return Ok ()
                    else
                        let payload = R2CreateBucketPayload.Create(bucketName)
                        let! createResult = client.R2CreateBucket(accountId, payload)
                        match createResult with
                        | R2CreateBucket.OK response ->
                            if response.success then
                                return Ok ()
                            else
                                let errorMsg =
                                    response.errors
                                    |> List.map (fun e -> e.message)
                                    |> String.concat "; "
                                // Check if "already exists" error
                                if errorMsg.ToLower().Contains("already exists") then
                                    return Ok ()
                                else
                                    return Error $"Bucket creation failed: {errorMsg}"
                        | R2CreateBucket.BadRequest failure ->
                            let errorMsg =
                                failure.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            if errorMsg.ToLower().Contains("already exists") then
                                return Ok ()
                            else
                                return Error $"Bucket creation failed: {errorMsg}"
                with
                | ex when ex.Message.Contains("already exists") ->
                    return Ok ()
                | ex ->
                    return Error $"Failed to create bucket: {ex.Message}"
            }

        /// List all buckets (returns comma-separated names for display)
        member this.ListBuckets() : Async<Result<string, string>> =
            async {
                try
                    let! result = client.R2ListBuckets(accountId)
                    match result with
                    | R2ListBuckets.OK response ->
                        match response.result.buckets with
                        | Some buckets ->
                            let names = buckets |> List.choose (fun b -> b.name) |> String.concat ", "
                            return Ok names
                        | None -> return Ok "[]"
                    | R2ListBuckets.BadRequest failure ->
                        let errorMsg = failure.errors |> List.map (fun e -> e.message) |> String.concat "; "
                        return Error $"Failed to list buckets: {errorMsg}"
                with
                | ex -> return Error $"Failed to list buckets: {ex.Message}"
            }

        /// Get bucket details (returns bucket name for display)
        member this.GetBucket(bucketName: string) : Async<Result<string, string>> =
            async {
                try
                    let! result = client.R2GetBucket(accountId, bucketName)
                    match result with
                    | R2GetBucket.OK response ->
                        if response.success then
                            return Ok (response.result.name |> Option.defaultValue bucketName)
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to get bucket: {errorMsg}"
                    | R2GetBucket.BadRequest failure ->
                        let errorMsg = failure.errors |> List.map (fun e -> e.message) |> String.concat "; "
                        return Error $"Failed to get bucket: {errorMsg}"
                with
                | ex -> return Error $"Failed to get bucket: {ex.Message}"
            }

        /// Delete bucket
        member this.DeleteBucket(bucketName: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.R2DeleteBucket(bucketName, accountId)
                    match result with
                    | R2DeleteBucket.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to delete bucket: {errorMsg}"
                    | R2DeleteBucket.BadRequest failure ->
                        let errorMsg = failure.errors |> List.map (fun e -> e.message) |> String.concat "; "
                        return Error $"Failed to delete bucket: {errorMsg}"
                with
                | ex -> return Error $"Failed to delete bucket: {ex.Message}"
            }
