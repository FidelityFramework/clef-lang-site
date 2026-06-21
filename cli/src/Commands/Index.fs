namespace ClefLang.CLI.Commands

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open ClefLang.CLI

module Index =

    type ContentSection = {
        Id: string
        ContentType: string
        PageSlug: string
        PageTitle: string
        PageUrl: string
        SectionIndex: int
        SectionTitle: string
        Content: string
        Tags: string
        Summary: string
        PublishedAt: string
        ContentHash: string
    }

    /// Split markdown content into sections by H2 headings
    let private splitIntoSections (body: string) : (string * string) list =
        let pattern = @"^##\s+(.+)$"
        let parts = Regex.Split(body, pattern, RegexOptions.Multiline)

        let mutable sections = []
        let mutable i = 0

        // First section is preamble (before any ##)
        if parts.Length > 0 && parts.[0].Trim().Length > 0 then
            sections <- ("Introduction", parts.[0].Trim()) :: sections
            i <- 1
        else
            i <- 1

        while i < parts.Length - 1 do
            let heading = parts.[i].Trim()
            let content = parts.[i + 1].Trim()
            if content.Length > 0 then
                sections <- (heading, content) :: sections
            i <- i + 2

        sections |> List.rev

    /// Strip markdown formatting for cleaner indexing
    let private stripMarkdown (text: string) : string =
        text
        |> fun t -> Regex.Replace(t, @"```[\s\S]*?```", "")       // code blocks
        |> fun t -> Regex.Replace(t, @"`[^`]+`", "")              // inline code
        |> fun t -> Regex.Replace(t, @"!\[.*?\]\(.*?\)", "")      // images
        |> fun t -> Regex.Replace(t, @"\[([^\]]+)\]\([^\)]+\)", "$1") // links → text
        |> fun t -> Regex.Replace(t, @"^#+\s+", "", RegexOptions.Multiline) // headings
        |> fun t -> Regex.Replace(t, @"^\s*[-*]\s+", "", RegexOptions.Multiline) // bullets
        |> fun t -> Regex.Replace(t, @"^\s*>\s+", "", RegexOptions.Multiline) // blockquotes
        |> fun t -> Regex.Replace(t, @"\*{1,2}([^*]+)\*{1,2}", "$1") // bold/italic
        |> fun t -> Regex.Replace(t, @"%%\{.*?\}%%", "")          // mermaid config
        |> fun t -> t.Trim()

    /// Compute content hash for change detection
    let private computeHash (text: string) : string =
        use hasher = SHA256.Create()
        let bytes = Encoding.UTF8.GetBytes(text)
        let hash = hasher.ComputeHash(bytes)
        BitConverter.ToString(hash).Replace("-", "").Substring(0, 16).ToLowerInvariant()

    /// Determine content type and page URL from file path
    /// Handles both local content dirs and vendored spec paths
    let private classifyContent (baseDirs: string list) (filePath: string) : (string * string) option =
        // Normalize to forward slashes and find the relative path against any known base
        let normalized = filePath.Replace("\\", "/")
        let relativePath =
            baseDirs
            |> List.tryPick (fun baseDir ->
                let norm = baseDir.Replace("\\", "/").TrimEnd('/')
                if normalized.StartsWith(norm) then
                    Some (normalized.Substring(norm.Length).TrimStart('/'))
                else None)
            |> Option.defaultValue (Path.GetFileName(filePath))

        // Strip .md extension to derive the URL path from the full directory structure
        let urlPath =
            if relativePath.EndsWith(".md") then relativePath.Substring(0, relativePath.Length - 3)
            else relativePath

        if relativePath.StartsWith("blog/") then
            Some ("blog", $"/{urlPath}/")
        elif relativePath.StartsWith("docs/design/") then
            Some ("design", $"/{urlPath}/")
        elif relativePath.StartsWith("docs/internals/") then
            Some ("internals", $"/{urlPath}/")
        elif relativePath.StartsWith("docs/reference/") then
            Some ("reference", $"/{urlPath}/")
        elif relativePath.StartsWith("docs/guides/") then
            Some ("guides", $"/{urlPath}/")
        elif relativePath.StartsWith("spec/") then
            // Spec URLs use /spec/draft/ prefix instead of /spec/
            let specPath = urlPath.Substring("spec/".Length)
            Some ("spec", $"/spec/draft/{specPath}/")
        else
            None

    /// Parse frontmatter and extract key fields
    let private parseFrontMatter (content: string) =
        let pattern = @"^---\s*\n([\s\S]*?)\n---\s*\n([\s\S]*)$"
        let m = Regex.Match(content, pattern)
        if not m.Success then None
        else Some (m.Groups.[1].Value, m.Groups.[2].Value)

    let private extractField (yaml: string) (field: string) =
        let p = $@"^{field}:\s*[""']?(.+?)[""']?\s*$"
        let m = Regex.Match(yaml, p, RegexOptions.Multiline)
        if m.Success then m.Groups.[1].Value.Trim() else ""

    let private extractYamlList (yaml: string) (field: string) =
        let p = $@"^{field}:\s*\n((?:\s*-\s*.+\n?)+)"
        let m = Regex.Match(yaml, p, RegexOptions.Multiline)
        if m.Success then
            Regex.Matches(m.Groups.[1].Value, @"-\s*(.+)")
            |> Seq.cast<Match>
            |> Seq.map (fun x -> x.Groups.[1].Value.Trim().Trim('"', '\''))
            |> String.concat ","
        else ""

    /// Parse a single content file into indexable sections
    let parseAndSplit (baseDirs: string list) (filePath: string) : ContentSection list =
        let content = File.ReadAllText(filePath)
        match parseFrontMatter content with
        | None -> []
        | Some (yaml, body) ->

        let slug = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant().Replace(" ", "-")
        let title = extractField yaml "title"
        let summary = extractField yaml "description"
        let tags = extractYamlList yaml "tags"
        // Publish date for recency ranking. The migration tooling sets `date` to the
        // original authorship date (it equals params.originally_published), so `date`
        // is the right signal. Normalize to YYYY-MM-DD so it sorts lexically; empty
        // string for spec/undated content, which the worker treats as oldest.
        let publishedAt =
            let raw = extractField yaml "date"
            if raw.Length >= 10 then raw.Substring(0, 10) else raw

        match classifyContent baseDirs filePath with
        | None -> []
        | Some (contentType, pageUrl) ->
            let sections = splitIntoSections body

            sections |> List.mapi (fun i (sectionTitle, sectionContent) ->
                let stripped = stripMarkdown sectionContent
                let id = $"{contentType}/{slug}#{i}"
                // Hash every field the index actually stores, not just the body.
                // The worker skips a section when this hash is unchanged, so a
                // title/tag/summary/url edit must move the hash or it would never
                // re-index (the body alone is identical). Newline-delimited so the
                // field boundaries can't collide across concatenation.
                let hashInput =
                    String.concat "\n" [
                        contentType; pageUrl; title; sectionTitle; tags; summary; publishedAt; stripped
                    ]
                {
                    Id = id
                    ContentType = contentType
                    PageSlug = slug
                    PageTitle = title
                    PageUrl = pageUrl
                    SectionIndex = i
                    SectionTitle = sectionTitle
                    Content = stripped
                    Tags = tags
                    Summary = summary
                    PublishedAt = publishedAt
                    ContentHash = computeHash hashInput
                })

    /// Run a process and return (exitCode, stdout, stderr)
    let private runProcess (name: string) (args: string) (workDir: string) =
        let psi = ProcessStartInfo(name, args)
        psi.WorkingDirectory <- workDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        use proc = Process.Start(psi)
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        (proc.ExitCode, stdout, stderr)

    /// Vendor Hugo modules and return the spec content directory if found
    let private vendorSpecContent (hugoDir: string) (verbose: bool) : string option =
        if verbose then printfn "  Vendoring Hugo modules for spec content..."
        let exitCode, _, stderr = runProcess "hugo" "mod vendor" hugoDir
        if exitCode <> 0 then
            if verbose then printfn "  Warning: hugo mod vendor failed: %s" stderr
            None
        else
            // Spec files land at _vendor/github.com/FidelityFramework/clef-lang-spec/spec/
            let specDir = Path.Combine(hugoDir, "_vendor", "github.com", "FidelityFramework", "clef-lang-spec", "spec")
            if Directory.Exists(specDir) then
                if verbose then printfn "  Found spec content at: %s" specDir
                Some specDir
            else
                if verbose then printfn "  No spec content found in vendor directory"
                None

    /// Execute the index command
    /// Purge the search index via the worker's /purge-index endpoint
    let private purgeIndex (httpClient: HttpClient) (workerUrl: string) (verbose: bool) =
        async {
            if verbose then printfn "  Purging existing index..."
            use content = new StringContent("{}", Encoding.UTF8, "application/json")
            let! response = httpClient.PostAsync($"{workerUrl}/purge-index", content) |> Async.AwaitTask
            let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            if response.IsSuccessStatusCode then
                printfn "  Index purged."
            else
                printfn "  Warning: purge-index failed: %s" body
        }

    let execute
        (hugoContentDir: string)
        (force: bool)
        (useLocal: bool)
        (localPort: int)
        (verbose: bool)
        : Async<Result<int, string>> =
        async {
            let verbose = verbose || force
            let workerUrl, apiKey =
                if useLocal then
                    $"http://localhost:{localPort}", "dev-local-key"
                else
                    let state = Config.loadState () |> Option.defaultValue Config.defaultState
                    let url = state.SearchWorkerUrl |> Option.defaultValue ""
                    let key = state.SearchIndexApiKey |> Option.defaultValue ""
                    url, key

            if String.IsNullOrEmpty(workerUrl) then
                return Error "Search worker not deployed. Run 'deploy' first or use --local flag."
            else

            // Resolve Hugo directory (parent of content dir)
            let hugoDir = Path.GetDirectoryName(Path.GetFullPath(hugoContentDir))

            // Vendor spec content from Hugo modules
            let specDir = vendorSpecContent hugoDir verbose

            // Collect all content directories and their base paths for classification
            let localContentDirs = [
                Path.Combine(hugoContentDir, "blog")
                Path.Combine(hugoContentDir, "docs")
            ]

            // For spec files, the base is the parent of "spec/" so classifyContent sees "spec/..."
            // e.g. if specDir = .../clef-lang-spec/spec, base = .../clef-lang-spec
            let specBaseDirAndFiles =
                match specDir with
                | Some dir ->
                    let baseDir = Path.GetDirectoryName(dir)
                    let files =
                        Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories)
                        |> Array.filter (fun f -> not (Path.GetFileName(f).StartsWith("_")))
                        |> List.ofArray
                    Some (baseDir, files)
                | None -> None

            let baseDirs =
                [ hugoContentDir ]
                @ (specBaseDirAndFiles |> Option.map (fun (b, _) -> b) |> Option.toList)

            let localMdFiles =
                localContentDirs
                |> List.filter Directory.Exists
                |> List.collect (fun dir ->
                    Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories) |> List.ofArray)
                |> List.filter (fun f -> not (Path.GetFileName(f).StartsWith("_")))

            let specMdFiles =
                specBaseDirAndFiles |> Option.map snd |> Option.defaultValue []

            let mdFiles = localMdFiles @ specMdFiles

            printfn "Indexing %d content files (%d local, %d spec)..." mdFiles.Length localMdFiles.Length specMdFiles.Length

            // Parse and split all files
            let allSections = mdFiles |> List.collect (parseAndSplit baseDirs)

            printfn "  Found %d sections across %d pages" allSections.Length mdFiles.Length

            if allSections.IsEmpty then
                printfn "No sections to index."
                return Ok 0
            else

            // Batch upload (chunks of 20 to stay within request size limits)
            use httpClient = new HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")
            httpClient.Timeout <- TimeSpan.FromMinutes(5.0) // Embedding generation takes time

            if force then
                do! purgeIndex httpClient workerUrl verbose

            let jsonOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            let batches = allSections |> List.chunkBySize 20
            let mutable totalIndexed = 0
            let mutable totalUnchanged = 0
            let mutable totalFailed = 0
            // Distinct per-section failure messages → count. The worker reports the real
            // cause (e.g. "VECTOR_UPSERT_ERROR (code = 40041): Too Many Requests") per
            // section; surface it here so a future quota/API change is visible immediately
            // instead of hiding behind a bare failure count.
            let failureReasons = System.Collections.Generic.Dictionary<string, int>()
            let recordReason (msg: string) =
                let key = msg.Trim()
                match failureReasons.TryGetValue(key) with
                | true, n -> failureReasons.[key] <- n + 1
                | false, _ -> failureReasons.[key] <- 1

            for i, batch in batches |> List.indexed do
                printf "  Batch %d/%d (%d sections)..." (i + 1) batches.Length batch.Length
                Console.Out.Flush()
                let payload = {| sections = batch |> Array.ofList |}
                let json = JsonSerializer.Serialize(payload, jsonOptions)
                use content = new StringContent(json, Encoding.UTF8, "application/json")

                try
                    let! response = httpClient.PostAsync($"{workerUrl}/index", content) |> Async.AwaitTask
                    let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    if response.IsSuccessStatusCode then
                        use doc = JsonDocument.Parse(responseBody)
                        let root = doc.RootElement
                        let mutable elem = Unchecked.defaultof<JsonElement>
                        let indexed = if root.TryGetProperty("indexed", &elem) then elem.GetInt32() else 0
                        let unchanged = if root.TryGetProperty("unchanged", &elem) then elem.GetInt32() else 0
                        let failed = if root.TryGetProperty("failed", &elem) then elem.GetInt32() else 0
                        totalIndexed <- totalIndexed + indexed
                        totalUnchanged <- totalUnchanged + unchanged
                        totalFailed <- totalFailed + failed
                        // Capture the actual failure reason from each unsuccessful section
                        if failed > 0 && root.TryGetProperty("results", &elem) && elem.ValueKind = JsonValueKind.Array then
                            for res in elem.EnumerateArray() do
                                let mutable ok = Unchecked.defaultof<JsonElement>
                                let mutable m = Unchecked.defaultof<JsonElement>
                                let isFailure = res.TryGetProperty("success", &ok) && not (ok.GetBoolean())
                                if isFailure && res.TryGetProperty("message", &m) then
                                    recordReason (m.GetString())
                        printfn " %d indexed, %d unchanged, %d failed" indexed unchanged failed
                    else
                        printfn " failed: %s" responseBody
                        recordReason $"HTTP {int response.StatusCode}: {responseBody}"
                        totalFailed <- totalFailed + batch.Length
                with ex ->
                    printfn " error: %s" ex.Message
                    recordReason ex.Message
                    totalFailed <- totalFailed + batch.Length

            printfn ""
            printfn "Indexing complete:"
            printfn "  Indexed:   %d sections" totalIndexed
            printfn "  Unchanged: %d sections" totalUnchanged
            printfn "  Failed:    %d sections" totalFailed

            if failureReasons.Count > 0 then
                printfn ""
                printfn "Failure reasons:"
                for kv in failureReasons |> Seq.sortByDescending (fun kv -> kv.Value) do
                    printfn "  [%dx] %s" kv.Value kv.Key

            if totalFailed > 0 then
                return Error $"Indexing partially failed: {totalFailed} sections"
            else
                return Ok totalIndexed
        }
