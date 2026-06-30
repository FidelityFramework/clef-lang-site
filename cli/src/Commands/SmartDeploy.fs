namespace ClefLang.CLI.Commands

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open ClefLang.CLI
open ClefLang.CLI.Core

module SmartDeploy =

    /// Compute SHA256 hash of a file's contents (for change detection)
    let private hashFile (path: string) : string option =
        if File.Exists(path) then
            use sha = SHA256.Create()
            use stream = File.OpenRead(path)
            let bytes = sha.ComputeHash(stream)
            Some (BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant())
        else None

    /// Run a process with extra environment variables and capture stdout (Ok) or stderr (Error)
    let private runProcessOut
        (name: string)
        (args: string)
        (workingDir: string)
        (env: (string * string) list)
        : Result<string, string> =
        try
            let psi = ProcessStartInfo(name, args)
            psi.WorkingDirectory <- workingDir
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true
            for key, value in env do
                psi.Environment.[key] <- value
            use proc = Process.Start(psi)
            let stdout = proc.StandardOutput.ReadToEnd()
            let stderr = proc.StandardError.ReadToEnd()
            proc.WaitForExit()
            if proc.ExitCode = 0 then Ok stdout else Error stderr
        with ex -> Error ex.Message

    /// The clef-lang-spec go-module import path, read from hugo/go.mod.
    /// This is the stable module identifier; it does NOT change when the backing
    /// git remote moves hosts (e.g. github.com → forge.spkez.dev), so we never
    /// hardcode a remote URL here.
    let private specModulePath (workingDir: string) : string option =
        let goMod = Path.Combine(workingDir, "hugo", "go.mod")
        if File.Exists(goMod) then
            File.ReadAllLines(goMod)
            |> Array.tryPick (fun line ->
                let parts = line.Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                if parts.Length >= 1 && parts.[0].Contains("clef-lang-spec") then Some parts.[0]
                else None)
        else None

    /// The spec version currently pinned in hugo/go.mod (what is vendored/deployed locally)
    let private pinnedSpecVersion (workingDir: string) : string option =
        let goMod = Path.Combine(workingDir, "hugo", "go.mod")
        if File.Exists(goMod) then
            File.ReadAllLines(goMod)
            |> Array.tryPick (fun line ->
                let parts = line.Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                if parts.Length >= 2 && parts.[0].Contains("clef-lang-spec") then Some parts.[1]
                else None)
        else None

    /// The latest upstream spec version on the fidelity branch, resolved via go's
    /// module machinery. Host-agnostic and read-only: `go list -m` follows whatever
    /// GOPROXY/insteadOf is configured and does not mutate go.mod/go.sum, so it can
    /// run BEFORE the deploy pulls the module — fixing the chicken-and-egg where
    /// go.sum only changes after the deploy's spec refresh.
    let private latestSpecVersion (workingDir: string) : string option =
        match specModulePath workingDir with
        | None -> None
        | Some modulePath ->
            let hugoDir = Path.Combine(workingDir, "hugo")
            // Bypass the Go module proxy so we resolve the fidelity branch directly from
            // Git, matching the deploy's refresh (the proxy can serve stale branch HEADs).
            // Derive the no-proxy pattern from the module's org prefix so it follows the
            // remote if the host moves (e.g. github.com → forge.spkez.dev).
            let orgPrefix =
                let segs = modulePath.Split('/')
                if segs.Length >= 2 then $"{segs.[0]}/{segs.[1]}/*" else $"{modulePath}*"
            let env = [
                "GONOPROXY", orgPrefix
                "GONOSUMDB", orgPrefix
                "GONOSUMCHECK", orgPrefix
            ]
            match runProcessOut "go" $"list -m {modulePath}@fidelity" hugoDir env with
            | Ok out ->
                let parts = out.Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                if parts.Length >= 2 then Some parts.[1] else None
            | Error _ -> None

    /// Determine which workers need redeployment from changed file paths
    let private changedWorkers (workerFiles: string list) : Set<string> =
        workerFiles
        |> List.choose (fun path ->
            let parts = path.Replace("\\", "/").Split('/')
            // workers/smart-search/src/Foo.fs → "smart-search"
            if parts.Length >= 2 && parts.[0] = "workers" then
                Some parts.[1]
            else
                None)
        |> Set.ofList

    /// Deploy only the workers whose source files changed
    let private deployChangedWorkers
        (config: Config.CloudflareConfig)
        (workers: Set<string>)
        (verbose: bool)
        : Async<Result<unit, string>> =
        async {
            for name in workers do
                printfn ""
                match name with
                | "smart-search" ->
                    printfn "=== Deploying smart-search worker (source changed) ==="
                    let! result = Deploy.executeSmartSearch config "./workers/smart-search" false true verbose
                    match result with
                    | Error e -> printfn "  Warning: smart-search deploy failed: %s" e
                    | Ok url -> printfn "  Deployed: %s" url

                | "search" ->
                    printfn "=== Deploying search worker (source changed) ==="
                    let! result = Deploy.executeSearch config "./workers/search" false true verbose
                    match result with
                    | Error e -> printfn "  Warning: search deploy failed: %s" e
                    | Ok url -> printfn "  Deployed: %s" url

                | "content-sync" ->
                    printfn "=== Deploying content-sync worker (source changed) ==="
                    let! result = Deploy.executeContentSync config "./workers/content-sync" false true verbose
                    match result with
                    | Error e -> printfn "  Warning: content-sync deploy failed: %s" e
                    | Ok url -> printfn "  Deployed: %s" url

                | other ->
                    printfn "  Unknown worker directory: %s (skipping)" other

            return Ok ()
        }

    /// Sync content to R2 and reindex search — soft failures (workers may not be deployed)
    let private syncAndIndex (config: Config.CloudflareConfig) (force: bool) (verbose: bool) : Async<int> =
        async {
            printfn ""
            printfn "=== Syncing Content to R2 ==="
            let! syncResult = Sync.execute config "./hugo/content" false 8788 verbose
            let syncCount =
                match syncResult with
                | Ok count ->
                    printfn "  Synced: %d files" count
                    count
                | Error e ->
                    printfn "  Skipped: %s" e
                    0

            printfn ""
            printfn "=== Indexing Content for Search ==="
            // A forced smart-deploy hard-recreates the Vectorize index: a soft purge
            // only deletes vectors D1 still tracks, so orphaned vectors from moved or
            // deleted content survive and keep surfacing in search. --force must mean a
            // genuine clean rebuild, so it tears the index down and re-pushes from empty.
            let! indexResult = Index.execute "./hugo/content" force force false 8787 verbose
            match indexResult with
            | Ok indexed -> printfn "  Indexed: %d sections" indexed
            | Error e -> printfn "  Skipped: %s" e

            // Rebuild the corpus graph (Map modal) from the same content walk. Idempotent
            // full rebuild, so it carries both broad-stroke (--force) and incremental updates;
            // soft-fails like sync/index if the search worker is not deployed.
            printfn ""
            printfn "=== Rebuilding Corpus Graph (Map) ==="
            let! graphResult = Graph.execute "./hugo/content" false 8787 verbose
            match graphResult with
            | Ok _ -> ()
            | Error e -> printfn "  Skipped: %s" e

            return syncCount
        }

    /// Rank deployment scopes by breadth (higher = more work)
    let private scopeRank = function
        | Config.NoDeploy -> 0
        | Config.PagesOnly -> 1
        | Config.PagesAndR2 -> 2
        | Config.FullDeploy -> 3

    /// Return the broader of two deployment scopes
    let private broaderScope a b =
        if scopeRank a >= scopeRank b then a else b

    /// Resolve HEAD to actual SHA for state storage
    let private resolveHeadSha (workingDir: string) : string =
        match GitDiffAnalyzer.resolveRef "HEAD" workingDir with
        | Ok sha -> sha
        | Error _ -> "unknown"

    /// Save state with actual commit SHA and current go.sum hash
    let private saveDeployState (workingDir: string) (extra: Config.DeploymentState -> Config.DeploymentState) =
        let state = Config.loadState () |> Option.defaultValue Config.defaultState
        let sha = resolveHeadSha workingDir
        let goSumHash = hashFile (Path.Combine(workingDir, "hugo", "go.sum"))
        // Record the spec version actually deployed (go.mod is refreshed to the
        // fidelity-branch HEAD during the deploy). Keep the previous value if go.mod
        // can't be read so we never blank out the tracking.
        let specVersion = pinnedSpecVersion workingDir |> Option.orElse state.LastSpecVersion
        let updated =
            extra { state with
                        LastDeployedCommit = Some sha
                        LastGoSumHash = goSumHash
                        LastSpecVersion = specVersion }
        Config.saveState updated

    /// Check for changes beyond committed git diffs:
    /// - Hugo module updates (go.sum changed since last deploy)
    let private detectExtraChanges
        (workingDir: string)
        (state: Config.DeploymentState option)
        (_verbose: bool)
        : Config.DeploymentScope * string list =

        let mutable reasons = []
        let mutable scope = Config.NoDeploy

        // clef-lang-spec upstream changes — resolve the latest fidelity-branch version
        // via `go list -m` and compare to what we last deployed. This runs BEFORE the
        // module is pulled, so an upstream spec commit is detected without first having
        // to refresh go.sum (which only changes during the deploy's spec refresh).
        let lastSpecVersion = state |> Option.bind (fun s -> s.LastSpecVersion)
        match latestSpecVersion workingDir, lastSpecVersion with
        | Some latest, Some last when latest <> last ->
            reasons <- $"clef-lang-spec updated ({last} -> {latest})" :: reasons
            scope <- broaderScope scope Config.PagesAndR2
        | Some latest, None ->
            reasons <- $"clef-lang-spec version not yet tracked (now {latest})" :: reasons
            scope <- broaderScope scope Config.PagesAndR2
        | _ -> ()

        // Hugo module changes (go.sum hash differs from last deploy) — catches other
        // already-vendored module updates (e.g. theme) that the spec probe won't see.
        let currentGoSumHash = hashFile (Path.Combine(workingDir, "hugo", "go.sum"))
        let lastGoSumHash = state |> Option.bind (fun s -> s.LastGoSumHash)
        match currentGoSumHash, lastGoSumHash with
        | Some current, Some last when current <> last ->
            reasons <- "Hugo modules updated (go.sum changed)" :: reasons
            scope <- broaderScope scope Config.PagesAndR2  // Module changes affect content + pages
        | Some _, None ->
            reasons <- "Hugo modules detected (first tracked deploy)" :: reasons
            scope <- broaderScope scope Config.PagesAndR2
        | _ -> ()

        (scope, List.rev reasons)

    let execute
        (config: Config.CloudflareConfig)
        (baseRef: string option)
        (force: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            if force then
                printfn "Force flag set — full rebuild: workers, pages, content sync, search index"
                printfn ""

                // 1. Deploy all workers
                let allWorkers = Set.ofList ["search"; "smart-search"; "content-sync"]
                let! _ = deployChangedWorkers config allWorkers verbose

                // 2. Deploy Pages (refresh spec module). forceUpload=true: --force re-uploads
                // every asset, bypassing the check-missing gate that can leave a deployment
                // serving stale bytes held under a hash from a prior bad deploy.
                printfn ""
                printfn "=== Deploying Hugo Site to Cloudflare Pages ==="
                let! pagesResult = DeployPages.execute config "./hugo" "clef-lang" true true verbose
                match pagesResult with
                | Error e -> return Error $"Pages deployment failed: {e}"
                | Ok url ->

                // 3. Sync content + force re-index (purge + rebuild)
                let! _ = syncAndIndex config true verbose

                let workingDir = Environment.CurrentDirectory
                saveDeployState workingDir (fun s -> { s with LastSyncTimestamp = Some DateTime.UtcNow })

                printfn ""
                printfn "Force deployment complete!"
                return Ok url
            else
                let workingDir = Environment.CurrentDirectory
                let state = Config.loadState ()

                // Determine base ref — use stored SHA, not symbolic "HEAD"
                let actualBaseRef =
                    match baseRef with
                    | Some r -> r
                    | None ->
                        match state with
                        | Some s ->
                            s.LastDeployedCommit |> Option.defaultValue "HEAD~1"
                        | None -> "HEAD~1"

                printfn "Analyzing changes from %s to HEAD..." (actualBaseRef.Substring(0, min 12 actualBaseRef.Length))
                printfn ""

                // 1. Git diff analysis (committed changes)
                let gitScope, gitAnalysis =
                    match GitDiffAnalyzer.analyze actualBaseRef "HEAD" workingDir with
                    | Error e ->
                        printfn "  Git diff failed: %s (continuing with extra checks)" e
                        (Config.NoDeploy, None)
                    | Ok analysis -> (analysis.RecommendedScope, Some analysis)

                // 2. Extra change detection (modules, working tree)
                let extraScope, extraReasons = detectExtraChanges workingDir state verbose

                // Merge scopes — take the broader one
                let mergedScope = broaderScope gitScope extraScope

                // Collect all reasons
                let allReasons =
                    let gitReasons = gitAnalysis |> Option.map (fun a -> a.Reasoning) |> Option.defaultValue []
                    gitReasons @ extraReasons

                let hasContentChanges =
                    let gitContent = gitAnalysis |> Option.map (fun a -> a.ContentFilesChanged.Length > 0) |> Option.defaultValue false
                    let extraContent = extraReasons |> List.exists (fun r -> r.Contains("content") || r.Contains("module") || r.Contains("spec"))
                    gitContent || extraContent

                printfn "Recommended scope: %A" mergedScope
                for reason in allReasons do
                    printfn "  - %s" reason
                printfn ""

                match mergedScope with
                | Config.NoDeploy ->
                    printfn "No deployment needed."
                    return Ok "No changes requiring deployment"

                | Config.PagesOnly ->
                    printfn "Executing Pages-only deployment..."
                    printfn ""

                    printfn "=== Deploying Hugo Site to Cloudflare Pages ==="
                    // Always refresh spec module to pick up upstream changes
                    let! pagesResult = DeployPages.execute config "./hugo" "clef-lang" true false verbose
                    match pagesResult with
                    | Error e -> return Error $"Pages deployment failed: {e}"
                    | Ok _ ->

                    saveDeployState workingDir id

                    printfn ""
                    printfn "Pages deployment complete!"
                    return Ok "Pages deployed"

                | Config.PagesAndR2 ->
                    printfn "Executing Pages + content sync + search index deployment..."
                    printfn ""

                    // 1. Deploy Pages (refresh spec module — this path triggers on go.sum/content changes)
                    printfn "=== Deploying Hugo Site to Cloudflare Pages ==="
                    let! pagesResult = DeployPages.execute config "./hugo" "clef-lang" true false verbose
                    match pagesResult with
                    | Error e -> return Error $"Pages deployment failed: {e}"
                    | Ok _ ->

                    // 2. Sync + index (soft)
                    let! syncCount = syncAndIndex config false verbose

                    saveDeployState workingDir (fun s -> { s with LastSyncTimestamp = Some DateTime.UtcNow })

                    printfn ""
                    printfn "Deployment complete!"
                    if syncCount > 0 then
                        printfn "  Content synced: %d files" syncCount
                    return Ok "Pages + content deployed"

                | Config.FullDeploy ->
                    // Worker and/or CLI changes detected — deploy only what changed
                    let workerFiles =
                        gitAnalysis |> Option.map (fun a -> a.WorkerFilesChanged) |> Option.defaultValue []

                    let workers = changedWorkers workerFiles

                    if workers.IsEmpty then
                        // CLI-only changes — no workers to deploy, just rebuild pages
                        printfn "CLI changes detected — rebuilding pages..."
                        printfn ""
                    else
                        printfn "Deploying %d changed worker(s): %s" workers.Count (String.Join(", ", workers))

                        let! _ = deployChangedWorkers config workers verbose
                        ()

                    // Always deploy pages when infra changes (refresh spec to pick up any module updates)
                    printfn ""
                    printfn "=== Deploying Hugo Site to Cloudflare Pages ==="
                    let! pagesResult = DeployPages.execute config "./hugo" "clef-lang" true false verbose
                    match pagesResult with
                    | Error e -> return Error $"Pages deployment failed: {e}"
                    | Ok _ ->

                    // If content also changed, sync + index
                    if hasContentChanges then
                        let! _ = syncAndIndex config false verbose
                        ()

                    saveDeployState workingDir id

                    printfn ""
                    if workers.IsEmpty then
                        printfn "Pages deployment complete!"
                        return Ok "Pages deployed (CLI changes only)"
                    else
                        let workerList = String.Join(", ", workers)
                        printfn "Deployment complete! Workers redeployed: %s" workerList
                        return Ok $"Workers (%s{workerList}) + Pages deployed"
        }
