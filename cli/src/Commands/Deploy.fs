namespace ClefLang.CLI.Commands

open System
open System.IO
open System.Security.Cryptography
open System.Text
open ClefLang.CLI
open ClefLang.CLI.Core

module Deploy =

    /// Compute hash of worker source files for change detection
    let private computeWorkerHash (workerDir: string) : string =
        if not (Directory.Exists(workerDir)) then
            ""
        else
            let files =
                Directory.GetFiles(workerDir, "*.fs", SearchOption.AllDirectories)
                |> Array.sort

            use sha = SHA256.Create()
            let combined =
                files
                |> Array.map (fun f -> File.ReadAllText(f))
                |> String.concat "\n"

            sha.ComputeHash(Encoding.UTF8.GetBytes(combined))
            |> Array.map (fun b -> b.ToString("x2"))
            |> String.concat ""
            |> fun s -> if s.Length >= 16 then s.Substring(0, 16) else s

    /// Build the worker (compile F# to JS via Fable)
    let private buildWorker (workerDir: string) (verbose: bool) : Result<string, string> =
        // TODO: Implement actual Fable build
        // For now, assume worker.js exists or needs to be built
        let outputPath = Path.Combine(workerDir, "dist", "worker.js")

        if File.Exists(outputPath) then
            Ok outputPath
        else
            // Placeholder for Fable build process
            printfn "  (Worker build not yet implemented - expecting pre-built worker.js)"
            Error $"Worker output not found: {outputPath}"

    let execute
        (config: Config.CloudflareConfig)
        (workerDir: string)
        (skipBuild: bool)
        (force: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            use httpClient = HttpHelpers.createAuthenticatedClient config.ApiToken
            let resources = Config.defaultResourceNames

            printfn "Deploying worker..."
            printfn ""

            // Load current state
            let state = Config.loadState () |> Option.defaultValue Config.defaultState

            // Check if resources are provisioned
            match state.D1DatabaseId with
            | None ->
                return Error "D1 database not provisioned. Run 'provision' first."
            | Some d1Id ->

            // Compute hash for change detection
            let currentHash = computeWorkerHash workerDir
            if not force && not skipBuild && state.LastDeployHash = Some currentHash then
                printfn "Worker source unchanged (hash: %s). Use --force to redeploy." currentHash
                return Ok (state.AskAiWorkerUrl |> Option.defaultValue "unchanged")
            else

            // Build worker if needed
            let! buildResult =
                if not skipBuild then
                    printfn "  [1/3] Building worker..."
                    match buildWorker workerDir verbose with
                    | Error e -> async { return Error e }
                    | Ok outputPath ->
                        if verbose then printfn "        Built: %s" outputPath
                        async { return Ok () }
                else
                    async { return Ok () }

            match buildResult with
            | Error e -> return Error e
            | Ok () ->

            // Read worker content
            let workerPath = Path.Combine(workerDir, "dist", "worker.js")
            if not (File.Exists(workerPath)) then
                return Error $"Worker file not found: {workerPath}"
            else

            let workerCode = File.ReadAllText(workerPath)

            // Prepare bindings for Ask AI worker
            let bindings = [
                WorkersClient.D1Database ("DB", d1Id)
                WorkersClient.AIBinding "AI"
                WorkersClient.PlainText ("ALLOWED_ORIGIN", "https://clef-lang.com,https://clef-lang.engineering-0c5.workers.dev,http://localhost:1313")
            ]

            let metadata: WorkersClient.WorkerMetadata = {
                MainModule = "worker.js"
                Bindings = bindings
                CompatibilityDate = "2024-11-01"
                CompatibilityFlags = []
            }

            // Upload worker with bindings
            printfn "  [2/3] Uploading worker: %s" resources.AskAiWorkerName
            let workers = WorkersClient.WorkersOperations(httpClient, config.AccountId)

            let! uploadResult = workers.UploadWorkerWithBindings resources.AskAiWorkerName workerCode metadata
            match uploadResult with
            | Error e -> return Error $"Worker upload failed: {e}"
            | Ok () ->

            if verbose then printfn "        Uploaded successfully"

            // Enable workers.dev subdomain and get URL
            printfn "  [3/3] Configuring subdomain..."

            // Enable workers.dev subdomain for the script
            let! enableResult = workers.EnableWorkersDevSubdomain resources.AskAiWorkerName
            match enableResult with
            | Error e ->
                if verbose then printfn "        Warning: %s" e
            | Ok () ->
                if verbose then printfn "        Enabled workers.dev subdomain"

            let! subdomainResult = workers.GetSubdomain()
            let workerUrl =
                match subdomainResult with
                | Ok (Some subdomain) ->
                    $"https://{resources.AskAiWorkerName}.{subdomain}.workers.dev"
                | Ok None ->
                    $"https://{resources.AskAiWorkerName}.workers.dev"
                | Error _ ->
                    $"https://{resources.AskAiWorkerName}.workers.dev"

            if verbose then printfn "        URL: %s" workerUrl

            // Update state
            let newState = {
                state with
                    AskAiWorkerDeployed = true
                    AskAiWorkerUrl = Some workerUrl
                    LastDeployHash = Some currentHash
            }
            Config.saveState newState

            printfn ""
            printfn "Deployment complete!"
            printfn ""
            printfn "  Worker URL: %s" workerUrl

            return Ok workerUrl
        }
