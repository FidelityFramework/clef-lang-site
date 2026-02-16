namespace ClefLang.CLI.Core

open System
open System.IO
open System.Diagnostics
open System.Text.RegularExpressions
open ClefLang.CLI

module GitDiffAnalyzer =

    /// Run git command and capture output
    let private runGit (args: string) (workingDir: string) : Result<string, string> =
        let psi = ProcessStartInfo(
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        )
        use proc = Process.Start(psi)
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        if proc.ExitCode = 0 then Ok stdout
        else Error $"Git error: {stderr}"

    /// Resolve a ref (e.g. "HEAD") to its actual commit SHA
    let resolveRef (ref: string) (workingDir: string) : Result<string, string> =
        match runGit $"rev-parse {ref}" workingDir with
        | Ok sha -> Ok (sha.Trim())
        | Error e -> Error e

    /// Get list of uncommitted/untracked files (working tree state)
    let getWorkingTreeChanges (workingDir: string) : Result<(string * string) list, string> =
        match runGit "status --porcelain" workingDir with
        | Error e -> Error e
        | Ok output ->
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun line ->
                let status = line.Substring(0, 2).Trim()
                let path = line.Substring(3)
                (status, path))
            |> List.ofArray
            |> Ok

    /// Get list of changed files with status
    let getChangedFiles (baseRef: string) (headRef: string) (workingDir: string)
        : Result<(string * string) list, string> =
        match runGit $"diff --name-status {baseRef}..{headRef}" workingDir with
        | Error e -> Error e
        | Ok output ->
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun line ->
                let parts = line.Split('\t')
                if parts.[0].StartsWith("R") && parts.Length >= 3 then
                    // Renames: R100\told/path\tnew/path — use destination
                    ("R", parts.[2])
                elif parts.Length >= 2 then
                    (parts.[0], parts.[1])
                else ("?", line))
            |> List.ofArray
            |> Ok

    /// Get diff stats for a specific file
    let getFileStats (baseRef: string) (headRef: string) (filePath: string) (workingDir: string)
        : Result<int * int * int, string> =  // (added, removed, hunks)
        match runGit $"diff --numstat {baseRef}..{headRef} -- \"{filePath}\"" workingDir with
        | Error e -> Error e
        | Ok numstat ->
            let parts = numstat.Trim().Split('\t')
            let added = try int parts.[0] with _ -> 0
            let removed = try int parts.[1] with _ -> 0

            // Count hunks
            match runGit $"diff {baseRef}..{headRef} -- \"{filePath}\"" workingDir with
            | Error _ -> Ok (added, removed, 1)
            | Ok diffOutput ->
                let hunkCount = Regex.Matches(diffOutput, @"^@@", RegexOptions.Multiline).Count
                Ok (added, removed, max 1 hunkCount)

    /// Estimate token delta (rough approximation: ~4 chars per token)
    let estimateTokenDelta (linesAdded: int) (linesRemoved: int) (avgLineLength: int) : int =
        let charDelta = (linesAdded - linesRemoved) * avgLineLength
        charDelta / 4

    /// Classify a content file change based on diff characteristics
    let classifyContentChange
        (changeType: string)
        (linesAdded: int)
        (linesRemoved: int)
        (hunkCount: int)
        : Config.ChangeClassification =
        match changeType with
        | "A" -> Config.ContentAddition
        | "D" -> Config.ContentRemoval
        | "M" ->
            let totalChange = linesAdded + linesRemoved
            let netChange = abs (linesAdded - linesRemoved)

            // Heuristics for cosmetic vs substantial changes
            if totalChange <= 5 && hunkCount = 1 then
                Config.Cosmetic
            elif totalChange <= 15 && netChange <= 3 then
                Config.Cosmetic  // Likely rewording, not new content
            elif linesAdded > 30 && linesAdded > linesRemoved * 2 then
                Config.ContentAddition  // Significant new content
            elif linesRemoved > 30 && linesRemoved > linesAdded * 2 then
                Config.ContentRemoval  // Significant content removal
            else
                Config.ContentModification
        | "R" -> Config.ContentModification  // Renamed file
        | _ -> Config.ContentModification

    /// Classify a file based on its path
    let classifyFilePath (filePath: string) : Config.ChangeClassification option =
        let normalized = filePath.Replace("\\", "/").ToLowerInvariant()
        if normalized.StartsWith("workers/") then
            Some Config.WorkerChange
        elif normalized.StartsWith("cli/") && normalized.EndsWith(".fs") then
            Some Config.CLIChange
        elif normalized.StartsWith("hugo/content/") && normalized.EndsWith(".md") then
            None  // Will be classified by diff analysis
        elif normalized.StartsWith("hugo/") then
            None  // Hugo files need diff analysis
        elif normalized.StartsWith(".github/") then
            Some Config.InfrastructureChange
        elif normalized.EndsWith("config.toml") || normalized.EndsWith(".json") then
            Some Config.ConfigChange
        else
            None

    /// Analyze a single file
    let analyzeFile
        (baseRef: string)
        (headRef: string)
        (changeType: string)
        (filePath: string)
        (workingDir: string)
        : Config.FileAnalysis =

        // Get stats
        let (added, removed, hunks) =
            match getFileStats baseRef headRef filePath workingDir with
            | Ok stats -> stats
            | Error _ -> (0, 0, 1)

        // Classify
        let classification =
            match classifyFilePath filePath with
            | Some c -> c
            | None ->
                // Content file - classify based on diff
                classifyContentChange changeType added removed hunks

        // Estimate tokens for content files
        let tokenDelta =
            if filePath.EndsWith(".md") then
                Some (estimateTokenDelta added removed 80)
            else
                None

        {
            FilePath = filePath
            ChangeType = changeType
            Classification = classification
            HunkCount = hunks
            LinesAdded = added
            LinesRemoved = removed
            TokenDelta = tokenDelta
        }

    /// Determine deployment scope from file analyses
    let determineScope (analyses: Config.FileAnalysis list) : Config.DeploymentScope * string list =
        let reasons = ResizeArray<string>()

        // Check for worker/CLI changes first (highest priority)
        let hasWorkerChanges =
            analyses |> List.exists (fun a ->
                a.Classification = Config.WorkerChange)
        let hasCLIChanges =
            analyses |> List.exists (fun a ->
                a.Classification = Config.CLIChange)

        if hasWorkerChanges then
            reasons.Add("Worker code modified - full deployment required")
        if hasCLIChanges then
            reasons.Add("CLI code modified - full deployment required")

        if hasWorkerChanges || hasCLIChanges then
            (Config.FullDeploy, List.ofSeq reasons)
        else
            // Check content changes
            let contentChanges =
                analyses |> List.filter (fun a ->
                    match a.Classification with
                    | Config.ContentAddition
                    | Config.ContentRemoval
                    | Config.ContentModification -> true
                    | _ -> false)

            let cosmeticOnly =
                analyses |> List.forall (fun a ->
                    match a.Classification with
                    | Config.Cosmetic -> true
                    | Config.InfrastructureChange -> true  // CI handles itself
                    | Config.ConfigChange -> false  // Config changes might need attention
                    | _ -> false)

            if contentChanges.Length > 0 then
                for c in contentChanges do
                    reasons.Add($"Content change in {c.FilePath}: {c.Classification}")
                (Config.PagesAndR2, List.ofSeq reasons)
            elif cosmeticOnly then
                reasons.Add("Only cosmetic changes detected - Pages sync sufficient")
                (Config.PagesOnly, List.ofSeq reasons)
            else
                // Check if there are any Hugo changes at all
                let hasHugoChanges =
                    analyses |> List.exists (fun a ->
                        a.FilePath.StartsWith("hugo/"))
                if hasHugoChanges then
                    reasons.Add("Hugo changes detected")
                    (Config.PagesOnly, List.ofSeq reasons)
                else
                    reasons.Add("No deployment-relevant changes detected")
                    (Config.NoDeploy, List.ofSeq reasons)

    /// Extract affected content slugs (blog, docs, spec)
    let getAffectedPosts (analyses: Config.FileAnalysis list) : string list =
        analyses
        |> List.filter (fun a -> a.FilePath.Contains("/content/") && a.FilePath.EndsWith(".md"))
        |> List.map (fun a ->
            Path.GetFileNameWithoutExtension(a.FilePath).ToLowerInvariant())
        |> List.distinct

    /// Main analysis entry point
    let analyze (baseRef: string) (headRef: string) (workingDir: string)
        : Result<Config.DiffAnalysisResult, string> =
        match getChangedFiles baseRef headRef workingDir with
        | Error e -> Error e
        | Ok changedFiles ->
            if changedFiles.IsEmpty then
                Ok {
                    BaseRef = baseRef
                    HeadRef = headRef
                    Files = []
                    RecommendedScope = Config.NoDeploy
                    Reasoning = ["No files changed between refs"]
                    ContentFilesChanged = []
                    WorkerFilesChanged = []
                    AffectedPosts = []
                }
            else
                let analyses =
                    changedFiles
                    |> List.map (fun (changeType, path) ->
                        analyzeFile baseRef headRef changeType path workingDir)

                let (scope, reasons) = determineScope analyses

                let contentFiles =
                    analyses
                    |> List.filter (fun a -> a.FilePath.Contains("/content/"))
                    |> List.map (fun a -> a.FilePath)

                let workerFiles =
                    analyses
                    |> List.filter (fun a -> a.Classification = Config.WorkerChange)
                    |> List.map (fun a -> a.FilePath)

                let affectedPosts = getAffectedPosts analyses

                Ok {
                    BaseRef = baseRef
                    HeadRef = headRef
                    Files = analyses
                    RecommendedScope = scope
                    Reasoning = reasons
                    ContentFilesChanged = contentFiles
                    WorkerFilesChanged = workerFiles
                    AffectedPosts = affectedPosts
                }
