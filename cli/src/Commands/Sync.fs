namespace ClefLang.CLI.Commands

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open ClefLang.CLI

module Sync =

    type BlogPost = {
        Slug: string
        Title: string
        Date: DateTime
        Summary: string option
        Tags: string list
        Categories: string list
        DotnetTerms: string list
        MlirTerms: string list
        Concepts: string list
        Content: string
        SourcePath: string
    }

    /// Request payload for single post sync
    type SyncRequest = {
        slug: string
        title: string
        date: string
        summary: string
        tags: string array
        content: string
        url: string
    }

    /// Request payload for batch sync
    type BatchSyncRequest = {
        posts: SyncRequest array
    }

    /// Response from sync worker
    type SyncResponse = {
        success: bool
        key: string
        message: string
    }

    /// Response from batch sync
    type BatchSyncResponse = {
        success: bool
        synced: int
        failed: int
        results: SyncResponse array
    }

    let private parseFrontMatter (content: string) (filename: string) : BlogPost option =
        let pattern = @"^---\s*\n([\s\S]*?)\n---\s*\n([\s\S]*)$"
        let m = Regex.Match(content, pattern)
        if m.Success then
            let yaml = m.Groups.[1].Value
            let body = m.Groups.[2].Value
            let slug = Path.GetFileNameWithoutExtension(filename).ToLowerInvariant().Replace(" ", "-")

            let extractField field =
                let p = $@"^{field}:\s*[""']?(.+?)[""']?\s*$"
                let m = Regex.Match(yaml, p, RegexOptions.Multiline)
                if m.Success then m.Groups.[1].Value.Trim() else ""

            // Extract YAML list format (- item\n- item)
            let extractYamlList field =
                let p = $@"^{field}:\s*\n((?:\s*-\s*.+\n?)+)"
                let m = Regex.Match(yaml, p, RegexOptions.Multiline)
                if m.Success then
                    Regex.Matches(m.Groups.[1].Value, @"-\s*(.+)")
                    |> Seq.cast<Match>
                    |> Seq.map (fun x -> x.Groups.[1].Value.Trim().Trim('"', '\''))
                    |> List.ofSeq
                else []

            // Extract inline JSON array format ["item1", "item2"]
            let extractInlineArray field =
                let p = $@"^{field}:\s*\[([^\]]*)\]"
                let m = Regex.Match(yaml, p, RegexOptions.Multiline)
                if m.Success then
                    let arrayContent = m.Groups.[1].Value
                    Regex.Matches(arrayContent, @"""([^""]+)""|'([^']+)'")
                    |> Seq.cast<Match>
                    |> Seq.map (fun x ->
                        let g1 = x.Groups.[1].Value
                        let g2 = x.Groups.[2].Value
                        if not (String.IsNullOrEmpty(g1)) then g1 else g2)
                    |> Seq.filter (fun s -> not (String.IsNullOrEmpty(s)))
                    |> List.ofSeq
                else []

            // Try both formats - YAML list first, then inline array
            let extractList field =
                match extractYamlList field with
                | [] -> extractInlineArray field
                | items -> items

            Some {
                Slug = slug
                Title = extractField "title"
                Date = try DateTime.Parse(extractField "date") with _ -> DateTime.Now
                Summary = let s = extractField "description" in if String.IsNullOrEmpty(s) then None else Some s
                Tags = extractList "tags"
                Categories = extractList "category"
                DotnetTerms = extractList "dotnet_terms"
                MlirTerms = extractList "mlir_terms"
                Concepts = extractList "concepts"
                Content = body
                SourcePath = filename
            }
        else
            None

    /// Build semantic metadata header to prepend to content for better AI Search indexing
    let private buildSemanticHeader (post: BlogPost) : string =
        let sections = ResizeArray<string>()

        // Categories get high visibility
        if not post.Categories.IsEmpty then
            let cats = String.Join(", ", post.Categories)
            sections.Add($"Categories: {cats}")

        // Concepts are key semantic terms
        if not post.Concepts.IsEmpty then
            let conceptsFormatted = post.Concepts |> List.map (fun c -> c.Replace("-", " ")) |> String.concat ", "
            sections.Add($"Key concepts: {conceptsFormatted}")

        // Technical domain terms
        if not post.DotnetTerms.IsEmpty then
            let termsFormatted = post.DotnetTerms |> List.map (fun t -> t.Replace("-", " ")) |> String.concat ", "
            sections.Add($".NET topics: {termsFormatted}")

        if not post.MlirTerms.IsEmpty then
            let termsFormatted = post.MlirTerms |> List.map (fun t -> t.Replace("-", " ")) |> String.concat ", "
            sections.Add($"MLIR/compiler topics: {termsFormatted}")

        // Tags (if any)
        if not post.Tags.IsEmpty then
            let tags = String.Join(", ", post.Tags)
            sections.Add($"Tags: {tags}")

        if sections.Count > 0 then
            String.Join("\n", sections) + "\n\n---\n\n"
        else
            ""

    let private toSyncRequest (post: BlogPost) : SyncRequest =
        let semanticHeader = buildSemanticHeader post
        let enrichedContent = semanticHeader + post.Content

        {
            slug = post.Slug
            title = post.Title
            date = post.Date.ToString("o")
            summary = post.Summary |> Option.defaultValue ""
            tags = post.Tags |> Array.ofList
            content = enrichedContent
            url = $"https://clef-lang.com/blog/{post.Slug}/"
        }

    let execute
        (config: Config.CloudflareConfig)
        (hugoContentDir: string)
        (useLocal: bool)
        (localPort: int)
        (verbose: bool)
        : Async<Result<int, string>> =
        async {
            // Determine worker URL and API key
            let workerUrl, apiKey =
                if useLocal then
                    // Use local content-sync worker
                    $"http://localhost:{localPort}", "dev-local-key"
                else
                    // Load deployment state to get content-sync worker URL
                    let state = Config.loadState () |> Option.defaultValue Config.defaultState
                    let url = state.ContentSyncWorkerUrl |> Option.defaultValue ""
                    let key = state.ContentSyncApiKey |> Option.defaultValue ""
                    url, key

            if String.IsNullOrEmpty(workerUrl) then
                return Error "Content-sync worker not deployed. Run 'deploy' first or use --local flag."
            elif String.IsNullOrEmpty(apiKey) then
                return Error "Content-sync API key not configured. Use --local flag for local development."
            else

            let blogDir = Path.Combine(hugoContentDir, "blog")
            if not (Directory.Exists(blogDir)) then
                return Error $"Blog directory not found: {blogDir}"
            else

            let mdFiles =
                Directory.GetFiles(blogDir, "*.md", SearchOption.AllDirectories)
                |> Array.filter (fun f -> not (Path.GetFileName(f).StartsWith("_")))

            printfn "Syncing %d blog posts to R2 via content-sync worker..." mdFiles.Length
            printfn "  Worker URL: %s" workerUrl
            printfn ""

            // Parse all posts
            let posts =
                mdFiles
                |> Array.choose (fun file ->
                    let content = File.ReadAllText(file)
                    match parseFrontMatter content file with
                    | Some post ->
                        if verbose then printfn "  Parsed: %s" post.Slug
                        Some post
                    | None ->
                        printfn "  Skipping (no frontmatter): %s" (Path.GetFileName(file))
                        None)
                |> Array.map toSyncRequest

            if posts.Length = 0 then
                printfn "No posts to sync."
                return Ok 0
            else

            // Create HTTP client
            use httpClient = new HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")

            // Use batch endpoint for efficiency
            let batchUrl = $"{workerUrl}/batch"
            let payload = { posts = posts }
            let jsonOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            let json = JsonSerializer.Serialize(payload, jsonOptions)

            printfn "  Uploading %d posts..." posts.Length

            try
                use content = new StringContent(json, Encoding.UTF8, "application/json")
                let! response = httpClient.PostAsync(batchUrl, content) |> Async.AwaitTask
                let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                if response.IsSuccessStatusCode then
                    let result = JsonSerializer.Deserialize<BatchSyncResponse>(responseBody, jsonOptions)

                    if verbose then
                        for r in result.results do
                            let status = if r.success then "OK" else "FAILED"
                            printfn "    [%s] %s" status r.key

                    // Update state (only for non-local deployments)
                    if not useLocal then
                        let state = Config.loadState() |> Option.defaultValue Config.defaultState
                        Config.saveState { state with LastSyncTimestamp = Some DateTime.UtcNow }

                    printfn ""
                    printfn "Sync complete: %d uploaded, %d failed" result.synced result.failed

                    if result.failed > 0 then
                        return Error $"Sync partially failed: {result.failed} posts failed to upload"
                    else
                        return Ok result.synced
                else
                    return Error $"Sync request failed: {response.StatusCode} - {responseBody}"

            with ex ->
                return Error $"Sync error: {ex.Message}"
        }
