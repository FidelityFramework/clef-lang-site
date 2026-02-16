namespace ClefLang.CLI.Core

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open Blake3
open Fidelity.CloudEdge.Management.Pages
open Fidelity.CloudEdge.Management.Pages.Types

module PagesClient =

    /// File info for upload
    type FileInfo = {
        RelativePath: string
        FullPath: string
        Hash: string
        Size: int64
        ContentType: string
    }

    /// File ready for upload
    type FileUpload = {
        Hash: string
        Content: byte[]
        ContentType: string
        FilePath: string
    }

    /// Get MIME type from file extension
    let getContentType (filePath: string) =
        match Path.GetExtension(filePath).ToLowerInvariant() with
        | ".html" | ".htm" -> "text/html"
        | ".css" -> "text/css"
        | ".js" -> "application/javascript"
        | ".mjs" -> "application/javascript"
        | ".json" -> "application/json"
        | ".png" -> "image/png"
        | ".jpg" | ".jpeg" -> "image/jpeg"
        | ".gif" -> "image/gif"
        | ".svg" -> "image/svg+xml"
        | ".ico" -> "image/x-icon"
        | ".woff" -> "font/woff"
        | ".woff2" -> "font/woff2"
        | ".ttf" -> "font/ttf"
        | ".eot" -> "application/vnd.ms-fontobject"
        | ".xml" -> "application/xml"
        | ".txt" -> "text/plain"
        | ".md" -> "text/markdown"
        | ".webp" -> "image/webp"
        | ".webm" -> "video/webm"
        | ".mp4" -> "video/mp4"
        | ".mp3" -> "audio/mpeg"
        | ".pdf" -> "application/pdf"
        | ".zip" -> "application/zip"
        | _ -> "application/octet-stream"

    /// Wrapper operations for Pages management
    type PagesOperations(httpClient: HttpClient, accountId: string) =
        let client = PagesClient(httpClient)

        /// Compute Cloudflare Pages hash: BLAKE3(base64(content) + extension), first 32 hex chars
        let computeFileHash (filePath: string) : string =
            let content = File.ReadAllBytes(filePath)
            let base64Content = Convert.ToBase64String(content)
            let extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant()
            let dataToHash = base64Content + extension
            let hashBytes = Hasher.Hash(Encoding.UTF8.GetBytes(dataToHash))
            // Take first 32 hex characters (16 bytes)
            BitConverter.ToString(hashBytes.AsSpan().Slice(0, 16).ToArray()).Replace("-", "").ToLowerInvariant()

        /// Build file info list from directory
        let collectFiles (directory: string) : FileInfo list =
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            |> Array.filter (fun f ->
                not (f.Contains(".git")) &&
                not (f.EndsWith(".DS_Store")) &&
                not (f.EndsWith("~")))
            |> Array.map (fun fullPath ->
                let relativePath =
                    fullPath.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                    |> fun p -> "/" + p.Replace("\\", "/")
                let hash = computeFileHash fullPath
                let size = System.IO.FileInfo(fullPath).Length
                let contentType = getContentType fullPath
                { RelativePath = relativePath
                  FullPath = fullPath
                  Hash = hash
                  Size = size
                  ContentType = contentType })
            |> Array.toList

        /// Build manifest JSON from file list
        let buildManifest (files: FileInfo list) : string =
            let entries =
                files
                |> List.map (fun f -> sprintf "\"%s\":\"%s\"" f.RelativePath f.Hash)
                |> String.concat ","
            sprintf "{%s}" entries

        /// Get upload JWT token for Pages assets
        member this.GetUploadToken(projectName: string) : Async<Result<string, string>> =
            async {
                try
                    let url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}/upload-token"
                    let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        use doc = JsonDocument.Parse(responseBody)
                        let root = doc.RootElement

                        let mutable successProp = Unchecked.defaultof<JsonElement>
                        if root.TryGetProperty("success", &successProp) && successProp.GetBoolean() then
                            let mutable resultProp = Unchecked.defaultof<JsonElement>
                            if root.TryGetProperty("result", &resultProp) then
                                let mutable jwtProp = Unchecked.defaultof<JsonElement>
                                if resultProp.TryGetProperty("jwt", &jwtProp) then
                                    return Ok (jwtProp.GetString())
                                else
                                    return Error "JWT not found in response"
                            else
                                return Error "Result not found in response"
                        else
                            return Error $"API returned failure: {responseBody}"
                    else
                        return Error $"HTTP {int response.StatusCode}: {responseBody}"
                with
                | ex -> return Error $"Failed to get upload token: {ex.Message}"
            }

        /// Check which hashes are missing from Cloudflare's storage (requires JWT auth)
        member this.CheckMissingAssets(jwt: string, hashes: string list) : Async<Result<string list, string>> =
            async {
                try
                    // Note: JWT-authenticated endpoints use /pages/assets/* without account ID
                    let url = "https://api.cloudflare.com/client/v4/pages/assets/check-missing"
                    let payload = sprintf """{"hashes":[%s]}""" (hashes |> List.map (sprintf "\"%s\"") |> String.concat ",")

                    // Use a fresh HttpClient without default auth for JWT-authenticated requests
                    use jwtClient = new HttpClient()
                    jwtClient.DefaultRequestHeaders.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt)

                    use content = new StringContent(payload, Encoding.UTF8, "application/json")
                    let! response = jwtClient.PostAsync(url, content) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        // Parse the response to get missing hashes
                        use doc = JsonDocument.Parse(responseBody)
                        let root = doc.RootElement

                        let mutable successProp = Unchecked.defaultof<JsonElement>
                        if root.TryGetProperty("success", &successProp) && successProp.GetBoolean() then
                            let mutable resultProp = Unchecked.defaultof<JsonElement>
                            if root.TryGetProperty("result", &resultProp) && resultProp.ValueKind = JsonValueKind.Array then
                                let missing =
                                    resultProp.EnumerateArray()
                                    |> Seq.map (fun e -> e.GetString())
                                    |> Seq.toList
                                return Ok missing
                            else
                                return Ok []
                        else
                            return Error $"API returned failure: {responseBody}"
                    else
                        return Error $"HTTP {int response.StatusCode}: {responseBody}"
                with
                | ex -> return Error $"Failed to check missing assets: {ex.Message}"
            }

        /// Upload a batch of files (requires JWT auth)
        member this.UploadAssetBatch(jwt: string, files: FileUpload list) : Async<Result<unit, string>> =
            async {
                try
                    // Note: JWT-authenticated endpoints use /pages/assets/* without account ID
                    let url = "https://api.cloudflare.com/client/v4/pages/assets/upload"

                    // Build the upload payload - array of objects with key, value, metadata, base64
                    let fileEntries =
                        files
                        |> List.map (fun f ->
                            let base64Content = Convert.ToBase64String(f.Content)
                            sprintf """{"key":"%s","value":"%s","metadata":{"contentType":"%s"},"base64":true}"""
                                f.Hash base64Content f.ContentType)
                        |> String.concat ","

                    let payload = sprintf "[%s]" fileEntries

                    // Use a fresh HttpClient without default auth for JWT-authenticated requests
                    use jwtClient = new HttpClient()
                    jwtClient.DefaultRequestHeaders.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt)

                    use content = new StringContent(payload, Encoding.UTF8, "application/json")
                    let! response = jwtClient.PostAsync(url, content) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        return Ok ()
                    else
                        return Error $"Upload failed - HTTP {int response.StatusCode}: {responseBody}"
                with
                | ex -> return Error $"Failed to upload assets: {ex.Message}"
            }

        /// Register all hashes for a deployment (requires JWT auth)
        member this.UpsertHashes(jwt: string, hashes: string list) : Async<Result<unit, string>> =
            async {
                try
                    // Note: JWT-authenticated endpoints use /pages/assets/* without account ID
                    let url = "https://api.cloudflare.com/client/v4/pages/assets/upsert-hashes"
                    let payload = sprintf """{"hashes":[%s]}""" (hashes |> List.map (sprintf "\"%s\"") |> String.concat ",")

                    // Use a fresh HttpClient without default auth for JWT-authenticated requests
                    use jwtClient = new HttpClient()
                    jwtClient.DefaultRequestHeaders.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt)

                    use content = new StringContent(payload, Encoding.UTF8, "application/json")
                    let! response = jwtClient.PostAsync(url, content) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        return Ok ()
                    else
                        return Error $"Upsert hashes failed - HTTP {int response.StatusCode}: {responseBody}"
                with
                | ex -> return Error $"Failed to upsert hashes: {ex.Message}"
            }

        /// List all Pages projects
        member this.ListProjects() : Async<Result<JsonElement option, string>> =
            async {
                try
                    let! result = client.PagesProjectGetProjects(accountId)
                    match result with
                    | PagesProjectGetProjects.OK response ->
                        return Ok response.result
                with
                | ex -> return Error $"Failed to list Pages projects: {ex.Message}"
            }

        /// Get a project by name
        member this.GetProject(projectName: string) : Async<Result<JsonElement option, string>> =
            async {
                try
                    let! result = client.PagesProjectGetProject(projectName, accountId)
                    match result with
                    | PagesProjectGetProject.OK response ->
                        if response.success then
                            return Ok response.result
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to get project: {errorMsg}"
                with
                | ex -> return Error $"Failed to get project: {ex.Message}"
            }

        /// Check if project exists
        member this.ProjectExists(projectName: string) : Async<bool> =
            async {
                try
                    let! result = this.GetProject(projectName)
                    match result with
                    | Ok (Some _) -> return true
                    | _ -> return false
                with
                | _ -> return false
            }

        /// Create a new Pages project
        member this.CreateProject(projectName: string, productionBranch: string) : Async<Result<unit, string>> =
            async {
                try
                    let payload = PagesProjectCreateProjectPayload.Create(projectName, productionBranch)
                    let! result = client.PagesProjectCreateProject(accountId, payload)
                    match result with
                    | PagesProjectCreateProject.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to create project: {errorMsg}"
                with
                | ex -> return Error $"Failed to create project: {ex.Message}"
            }

        /// Delete a Pages project
        member this.DeleteProject(projectName: string) : Async<Result<unit, string>> =
            async {
                try
                    let! result = client.PagesProjectDeleteProject(projectName, accountId)
                    match result with
                    | PagesProjectDeleteProject.OK response ->
                        if response.success then
                            return Ok ()
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to delete project: {errorMsg}"
                with
                | ex -> return Error $"Failed to delete project: {ex.Message}"
            }

        /// Deploy a directory to Pages using direct upload
        /// This implements the full upload flow: get-token -> check-missing -> upload -> upsert-hashes -> create deployment
        member this.DeployDirectory
            (projectName: string)
            (directory: string)
            (branch: string option)
            (commitMessage: string option)
            (commitHash: string option)
            (verbose: bool)
            (progressCallback: string -> unit)
            : Async<Result<string, string>> =
            async {
                try
                    // Step 0: Get upload JWT token
                    progressCallback "Getting upload token..."
                    let! tokenResult = this.GetUploadToken(projectName)
                    match tokenResult with
                    | Error e -> return Error $"Failed to get upload token: {e}"
                    | Ok jwt ->
                    if verbose then progressCallback $"Got JWT token ({jwt.Length} chars, starts with: {jwt.Substring(0, min 20 jwt.Length)}...)"

                    // Step 1: Collect all files and compute hashes
                    progressCallback "Collecting files..."
                    let files = collectFiles directory
                    let allHashes = files |> List.map (fun f -> f.Hash) |> List.distinct

                    if verbose then progressCallback $"Found {files.Length} files, {allHashes.Length} unique hashes"

                    // Step 2: Check which files are missing
                    progressCallback "Checking for existing assets..."
                    let! missingResult = this.CheckMissingAssets(jwt, allHashes)

                    match missingResult with
                    | Error e -> return Error $"Failed to check missing assets: {e}"
                    | Ok missingHashes ->

                    if verbose then progressCallback $"{missingHashes.Length} files need uploading"

                    // Step 3: Upload missing files in batches
                    let! uploadOk =
                        if missingHashes.Length > 0 then
                            async {
                                progressCallback $"Uploading {missingHashes.Length} files..."

                                let missingHashSet = Set.ofList missingHashes
                                let filesToUpload =
                                    files
                                    |> List.filter (fun f -> missingHashSet.Contains(f.Hash))
                                    |> List.map (fun f ->
                                        { Hash = f.Hash
                                          Content = File.ReadAllBytes(f.FullPath)
                                          ContentType = f.ContentType
                                          FilePath = f.RelativePath })

                                // Batch uploads (max 50MB or 100 files per batch)
                                let maxBatchSize = 50L * 1024L * 1024L // 50MB
                                let maxFilesPerBatch = 100

                                let rec uploadBatches (remaining: FileUpload list) (batchNum: int) =
                                    async {
                                        match remaining with
                                        | [] -> return Ok ()
                                        | _ ->
                                            // Take files up to batch limits
                                            let mutable batchSize = 0L
                                            let mutable batchCount = 0
                                            let batch, rest =
                                                remaining
                                                |> List.fold (fun (batch, rest) file ->
                                                    let newSize = batchSize + int64 file.Content.Length
                                                    if batchCount < maxFilesPerBatch && newSize < maxBatchSize then
                                                        batchSize <- newSize
                                                        batchCount <- batchCount + 1
                                                        (file :: batch, rest)
                                                    else
                                                        (batch, file :: rest)
                                                ) ([], [])
                                                |> fun (b, r) -> (List.rev b, List.rev r)

                                            if verbose then
                                                progressCallback $"Uploading batch {batchNum} ({batch.Length} files, {batchSize / 1024L}KB)..."

                                            let! uploadResult = this.UploadAssetBatch(jwt, batch)
                                            match uploadResult with
                                            | Error e -> return Error e
                                            | Ok () -> return! uploadBatches rest (batchNum + 1)
                                    }

                                return! uploadBatches filesToUpload 1
                            }
                        else
                            async {
                                progressCallback "All files already uploaded"
                                return Ok ()
                            }

                    match uploadOk with
                    | Error e -> return Error e
                    | Ok () ->

                    // Step 4: Upsert all hashes
                    progressCallback "Registering assets..."
                    let! upsertResult = this.UpsertHashes(jwt, allHashes)
                    match upsertResult with
                    | Error e -> return Error $"Failed to register assets: {e}"
                    | Ok () ->

                    // Step 5: Create the deployment
                    progressCallback "Creating deployment..."
                    let manifest = buildManifest files

                    // Use multipart form data directly to avoid generated client issues
                    let url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/pages/projects/{projectName}/deployments"

                    use formContent = new MultipartFormDataContent()
                    formContent.Add(new StringContent(manifest), "manifest")
                    match branch with
                    | Some b -> formContent.Add(new StringContent(b), "branch")
                    | None -> ()
                    match commitMessage with
                    | Some msg -> formContent.Add(new StringContent(msg), "commit_message")
                    | None -> ()
                    match commitHash with
                    | Some hash -> formContent.Add(new StringContent(hash), "commit_hash")
                    | None -> ()

                    let! response = httpClient.PostAsync(url, formContent) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        // Parse the response to extract deployment URL
                        use doc = JsonDocument.Parse(responseBody)
                        let root = doc.RootElement

                        let mutable successProp = Unchecked.defaultof<JsonElement>
                        if root.TryGetProperty("success", &successProp) && successProp.GetBoolean() then
                            let mutable resultProp = Unchecked.defaultof<JsonElement>
                            if root.TryGetProperty("result", &resultProp) then
                                let mutable urlProp = Unchecked.defaultof<JsonElement>
                                if resultProp.TryGetProperty("url", &urlProp) then
                                    return Ok (urlProp.GetString())
                                else
                                    return Ok "Deployment created successfully"
                            else
                                return Ok "Deployment created successfully"
                        else
                            let mutable errorsProp = Unchecked.defaultof<JsonElement>
                            if root.TryGetProperty("errors", &errorsProp) then
                                return Error $"Deployment failed: {responseBody}"
                            else
                                return Error $"Deployment failed: {responseBody}"
                    else
                        return Error $"Deployment failed - HTTP {int response.StatusCode}: {responseBody}"
                with
                | ex -> return Error $"Failed to deploy: {ex.Message}"
            }

        /// Get deployment status
        member this.GetDeployment(projectName: string, deploymentId: string) : Async<Result<JsonElement option, string>> =
            async {
                try
                    let! result = client.PagesDeploymentGetDeploymentInfo(deploymentId, projectName, accountId)
                    match result with
                    | PagesDeploymentGetDeploymentInfo.OK response ->
                        if response.success then
                            return Ok response.result
                        else
                            let errorMsg =
                                response.errors
                                |> List.map (fun e -> e.message)
                                |> String.concat "; "
                            return Error $"Failed to get deployment: {errorMsg}"
                with
                | ex -> return Error $"Failed to get deployment: {ex.Message}"
            }

        /// List deployments for a project
        member this.ListDeployments(projectName: string) : Async<Result<JsonElement option, string>> =
            async {
                try
                    let! result = client.PagesDeploymentGetDeployments(projectName, accountId)
                    match result with
                    | PagesDeploymentGetDeployments.OK response ->
                        return Ok response.result
                with
                | ex -> return Error $"Failed to list deployments: {ex.Message}"
            }
