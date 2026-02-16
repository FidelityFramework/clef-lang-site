namespace ClefLang.CLI.Commands

open System
open System.IO
open System.Security.Cryptography
open System.Text
open ClefLang.CLI
open ClefLang.CLI.Core

module Deploy =

    let private runProcess (name: string) (args: string) (workDir: string) (verbose: bool) =
        let psi = Diagnostics.ProcessStartInfo(name, args)
        psi.WorkingDirectory <- workDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true

        use proc = Diagnostics.Process.Start(psi)
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if verbose && stdout.Length > 0 then
            printfn "        %s" (stdout.TrimEnd())

        (proc.ExitCode, stdout, stderr)

    /// Compute hash of worker source files for change detection
    let private computeWorkerHash (workerDir: string) : string =
        if not (Directory.Exists workerDir) then
            ""
        else
            let files =
                Directory.GetFiles(workerDir, "*.fs", SearchOption.AllDirectories)
                |> Array.sort

            use sha = SHA256.Create()
            let combined =
                files
                |> Array.map File.ReadAllText
                |> String.concat "\n"

            sha.ComputeHash(Encoding.UTF8.GetBytes combined)
            |> Array.map (fun b -> b.ToString("x2"))
            |> String.concat ""
            |> fun s -> if s.Length >= 16 then s.Substring(0, 16) else s

    /// Ensure dotnet tools (Fable) are available
    let private ensureDotnetTools (verbose: bool) : Result<unit, string> =
        if verbose then printfn "        Restoring dotnet tools..."
        let exitCode, _, stderr = runProcess "dotnet" "tool restore" "." verbose
        if exitCode <> 0 then
            Error $"dotnet tool restore failed: {stderr}"
        else Ok ()

    /// Ensure node_modules exist in worker dir
    let private ensureNodeModules (workerDir: string) (verbose: bool) : Result<unit, string> =
        let nodeModulesPath = Path.Combine(workerDir, "node_modules")
        if Directory.Exists nodeModulesPath then Ok ()
        else
            printfn "        Installing npm dependencies..."
            let exitCode, _, stderr = runProcess "npm" "install" workerDir verbose
            if exitCode <> 0 then
                Error $"npm install failed in {workerDir}: {stderr}"
            else Ok ()

    /// Build a Fable worker: F# → JS via Fable, then bundle via esbuild
    let private buildWorker (workerDir: string) (verbose: bool) : Result<string, string> =
        let outputPath = Path.Combine(workerDir, "dist", "worker.js")

        // Ensure prerequisites
        match ensureDotnetTools verbose with
        | Error e -> Error e
        | Ok () ->

        match ensureNodeModules workerDir verbose with
        | Error e -> Error e
        | Ok () ->

        // Fable: F# → JavaScript
        if verbose then printfn "        Compiling F# via Fable..."
        let exitCode, _, stderr =
            runProcess "dotnet" "fable . --outDir dist/fable --noCache" workerDir verbose

        if exitCode <> 0 then
            Error $"Fable compilation failed: {stderr}"
        else

        // esbuild: bundle into single ESM worker.js
        if verbose then printfn "        Bundling with esbuild..."
        let exitCode, _, stderr =
            runProcess "npx" "esbuild dist/fable/src/Main.js --bundle --format=esm --outfile=dist/worker.js --minify" workerDir verbose

        if exitCode <> 0 then
            Error $"esbuild bundle failed: {stderr}"
        else
            if verbose then printfn "        Built: %s" outputPath
            Ok outputPath

    /// Generate a random API key for worker authentication
    let private generateApiKey () =
        let bytes = Array.create 32 0uy
        use rng = RandomNumberGenerator.Create()
        rng.GetBytes(bytes)
        Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 32)

    /// Generic worker deployment: build → upload → enable subdomain
    let private deployWorker
        (config: Config.CloudflareConfig)
        (workerName: string)
        (workerDir: string)
        (bindings: WorkersClient.WorkerBinding list)
        (skipBuild: bool)
        (force: bool)
        (verbose: bool)
        (lastHash: string option)
        : Async<Result<string * string, string>> = // Returns (workerUrl, currentHash)
        async {
            use httpClient = HttpHelpers.createAuthenticatedClient config.ApiToken
            let workers = WorkersClient.WorkersOperations(httpClient, config.AccountId)

            // Hash-based change detection
            let currentHash = computeWorkerHash workerDir
            if not force && not skipBuild && lastHash = Some currentHash then
                printfn "  Worker source unchanged (hash: %s). Use --force to redeploy." currentHash

                let! subdomainResult = workers.GetSubdomain()
                let existingUrl =
                    match subdomainResult with
                    | Ok (Some subdomain) -> $"https://{workerName}.{subdomain}.workers.dev"
                    | _ -> $"https://{workerName}.workers.dev"

                return Ok (existingUrl, currentHash)
            else

            // Build
            let! buildResult =
                if not skipBuild then
                    printfn "  [1/3] Building worker..."
                    match buildWorker workerDir verbose with
                    | Error e -> async { return Error e }
                    | Ok _ -> async { return Ok () }
                else
                    async { return Ok () }

            match buildResult with
            | Error e -> return Error e
            | Ok () ->

            // Read built output
            let workerPath = Path.Combine(workerDir, "dist", "worker.js")
            if not (File.Exists workerPath) then
                return Error $"Worker file not found: {workerPath}"
            else

            let workerCode = File.ReadAllText(workerPath)

            let metadata: WorkersClient.WorkerMetadata = {
                MainModule = "worker.js"
                Bindings = bindings
                CompatibilityDate = "2024-11-01"
                CompatibilityFlags = []
            }

            // Upload
            printfn "  [2/3] Uploading worker: %s" workerName
            let! uploadResult = workers.UploadWorkerWithBindings workerName workerCode metadata
            match uploadResult with
            | Error e -> return Error $"Worker upload failed: {e}"
            | Ok () ->

            if verbose then printfn "        Uploaded successfully"

            // Enable subdomain
            printfn "  [3/3] Configuring subdomain..."
            let! _ = workers.EnableWorkersDevSubdomain workerName

            let! subdomainResult = workers.GetSubdomain()
            let workerUrl =
                match subdomainResult with
                | Ok (Some subdomain) -> $"https://{workerName}.{subdomain}.workers.dev"
                | _ -> $"https://{workerName}.workers.dev"

            if verbose then printfn "        URL: %s" workerUrl

            return Ok (workerUrl, currentHash)
        }

    /// Deploy smart-search worker
    let executeSmartSearch
        (config: Config.CloudflareConfig)
        (workerDir: string)
        (skipBuild: bool)
        (force: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            let state = Config.loadState () |> Option.defaultValue Config.defaultState
            let resources = Config.defaultResourceNames

            match state.SmartSearchD1Id with
            | None ->
                return Error "Smart search D1 database not provisioned. Run 'provision' first."
            | Some d1Id ->

            printfn "Deploying smart-search worker..."
            printfn ""

            let bindings = [
                WorkersClient.D1Database ("DB", d1Id)
                WorkersClient.AIBinding "AI"
                WorkersClient.PlainText ("ALLOWED_ORIGIN", "https://clef-lang.com,http://localhost:1313")
            ]

            let! result = deployWorker config resources.SmartSearchWorkerName workerDir bindings skipBuild force verbose state.LastDeployHash
            match result with
            | Error e -> return Error e
            | Ok (workerUrl, currentHash) ->

            let updatedState = Config.loadState () |> Option.defaultValue Config.defaultState
            Config.saveState {
                updatedState with
                    SmartSearchWorkerDeployed = true
                    SmartSearchWorkerUrl = Some workerUrl
                    LastDeployHash = Some currentHash
            }

            printfn ""
            printfn "  Smart-search worker: %s" workerUrl
            return Ok workerUrl
        }

    /// Deploy search worker
    let executeSearch
        (config: Config.CloudflareConfig)
        (workerDir: string)
        (skipBuild: bool)
        (force: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            let state = Config.loadState () |> Option.defaultValue Config.defaultState
            let resources = Config.defaultResourceNames

            match state.SearchD1Id with
            | None ->
                return Error "Search D1 database not provisioned. Run 'provision' first."
            | Some d1Id ->

            printfn "Deploying search worker..."
            printfn ""

            // Generate or reuse API key
            let apiKey =
                match state.SearchIndexApiKey with
                | Some key -> key
                | None -> generateApiKey ()

            let bindings = [
                WorkersClient.D1Database ("DB", d1Id)
                WorkersClient.AIBinding "AI"
                WorkersClient.VectorizeIndex ("VECTORIZE", resources.VectorizeIndexName)
                WorkersClient.PlainText ("ALLOWED_ORIGIN", "https://clef-lang.com,http://localhost:1313")
                WorkersClient.PlainText ("INDEX_API_KEY", apiKey)
            ]

            let! result = deployWorker config resources.SearchWorkerName workerDir bindings skipBuild force verbose None
            match result with
            | Error e -> return Error e
            | Ok (workerUrl, _) ->

            let updatedState = Config.loadState () |> Option.defaultValue Config.defaultState
            Config.saveState {
                updatedState with
                    SearchWorkerDeployed = true
                    SearchWorkerUrl = Some workerUrl
                    SearchIndexApiKey = Some apiKey
            }

            printfn ""
            printfn "  Search worker: %s" workerUrl
            return Ok workerUrl
        }

    /// Deploy content-sync worker
    let executeContentSync
        (config: Config.CloudflareConfig)
        (workerDir: string)
        (skipBuild: bool)
        (force: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            let state = Config.loadState () |> Option.defaultValue Config.defaultState
            let resources = Config.defaultResourceNames

            printfn "Deploying content-sync worker..."
            printfn ""

            // Generate or reuse API key
            let apiKey =
                match state.ContentSyncApiKey with
                | Some key -> key
                | None -> generateApiKey ()

            let bindings = [
                WorkersClient.R2Bucket ("CONTENT", resources.R2BucketName)
                WorkersClient.PlainText ("SYNC_API_KEY", apiKey)
            ]

            let! result = deployWorker config resources.ContentSyncWorkerName workerDir bindings skipBuild force verbose None
            match result with
            | Error e -> return Error e
            | Ok (workerUrl, _) ->

            let updatedState = Config.loadState () |> Option.defaultValue Config.defaultState
            Config.saveState {
                updatedState with
                    ContentSyncWorkerDeployed = true
                    ContentSyncWorkerUrl = Some workerUrl
                    ContentSyncApiKey = Some apiKey
            }

            printfn ""
            printfn "  Content-sync worker: %s" workerUrl
            return Ok workerUrl
        }
