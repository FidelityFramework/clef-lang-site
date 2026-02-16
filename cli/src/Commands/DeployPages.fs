namespace ClefLang.CLI.Commands

open System
open System.IO
open ClefLang.CLI
open ClefLang.CLI.Core

module DeployPages =

    let private runProcess (name: string) (args: string) (workDir: string) (verbose: bool) =
        let psi = System.Diagnostics.ProcessStartInfo(name, args)
        psi.WorkingDirectory <- workDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true

        use proc = System.Diagnostics.Process.Start(psi)
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if verbose && stdout.Length > 0 then
            printfn "        %s" (stdout.TrimEnd())

        (proc.ExitCode, stdout, stderr)

    /// Run a process with extra environment variables (e.g. to bypass Go proxy)
    let private runProcessWithEnv (name: string) (args: string) (workDir: string) (env: (string * string) list) (verbose: bool) =
        let psi = System.Diagnostics.ProcessStartInfo(name, args)
        psi.WorkingDirectory <- workDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true

        for (key, value) in env do
            psi.Environment.[key] <- value

        use proc = System.Diagnostics.Process.Start(psi)
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if verbose && stdout.Length > 0 then
            printfn "        %s" (stdout.TrimEnd())

        (proc.ExitCode, stdout, stderr)

    let private refreshSpecModule (hugoDir: string) (verbose: bool) : Result<unit, string> =
        // Bypass Go module proxy — fetch directly from Git to avoid stale cached versions
        let goDirectEnv = [
            "GONOSUMCHECK", "github.com/FidelityFramework/*"
            "GONOPROXY", "github.com/FidelityFramework/*"
            "GONOSUMDB", "github.com/FidelityFramework/*"
        ]

        // 1. Nuke Go's module cache for this module — hugo mod clean only clears Hugo's cache,
        //    Go keeps its own cache that can serve stale branch resolutions
        printfn "        Clearing Go module cache for spec..."
        let exitCode, goModCache, _ = runProcess "go" "env GOMODCACHE" hugoDir false
        if exitCode = 0 then
            let cacheRoot = goModCache.Trim()
            // Go stores with case-folded paths: FidelityFramework → !fidelity!framework
            let downloadCache = Path.Combine(cacheRoot, "cache", "download", "github.com", "!fidelity!framework", "clef-lang-spec")
            let moduleCache = Path.Combine(cacheRoot, "github.com", "!fidelity!framework")
            if Directory.Exists(downloadCache) then
                Directory.Delete(downloadCache, true)
                if verbose then printfn "        Deleted Go download cache"
            if Directory.Exists(moduleCache) then
                // Delete all clef-lang-spec version directories
                for dir in Directory.GetDirectories(moduleCache, "clef-lang-spec*") do
                    Directory.Delete(dir, true)
                    if verbose then printfn "        Deleted cached module: %s" (Path.GetFileName(dir))

        // 2. Clear Hugo's module cache too
        printfn "        Clearing Hugo module cache..."
        let exitCode, _, stderr =
            runProcess "hugo" "mod clean --pattern *clef-lang-spec*" hugoDir verbose

        if exitCode <> 0 then
            Error $"hugo mod clean failed: {stderr}"
        else

        // 3. Fetch latest from Git directly (no proxy, no cache)
        printfn "        Pulling latest spec from fidelity branch (direct, no proxy)..."
        let exitCode, _, stderr =
            runProcessWithEnv "hugo" "mod get -u github.com/FidelityFramework/clef-lang-spec@fidelity" hugoDir goDirectEnv verbose

        if exitCode <> 0 then
            Error $"hugo mod get failed: {stderr}"
        else

        // 4. Re-vendor so the build uses the freshly fetched version
        printfn "        Re-vendoring modules..."
        let exitCode, _, stderr =
            runProcess "hugo" "mod vendor" hugoDir verbose

        if exitCode <> 0 then
            Error $"hugo mod vendor failed: {stderr}"
        else

        // Always show resolved module version
        let _, stdout, _ = runProcess "hugo" "mod graph" hugoDir false
        let specLine =
            stdout.Split('\n')
            |> Array.tryFind (fun l -> l.Contains("clef-lang-spec"))
        match specLine with
        | Some line -> printfn "        Module: %s" (line.TrimStart())
        | None -> printfn "        Warning: clef-lang-spec not found in module graph"

        Ok ()

    let execute
        (config: Config.CloudflareConfig)
        (hugoDir: string)
        (projectName: string)
        (refreshSpec: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            use httpClient = HttpHelpers.createAuthenticatedClient config.ApiToken
            let pages = PagesClient.PagesOperations(httpClient, config.AccountId)

            let totalSteps = if refreshSpec then 3 else 2
            let mutable step = 0
            let nextStep label =
                step <- step + 1
                printfn "  [%d/%d] %s" step totalSteps label

            printfn "Deploying Hugo site to Cloudflare Pages..."
            printfn ""

            // Refresh spec module (conditional)
            let specRefreshResult =
                if refreshSpec then
                    nextStep "Refreshing spec module..."
                    refreshSpecModule hugoDir verbose
                else
                    Ok ()

            match specRefreshResult with
            | Error e -> return Error e
            | Ok () ->

            // Build Hugo site
            let publicDir = Path.Combine(hugoDir, "public")
            nextStep "Building Hugo site..."

            // Clean public directory to avoid stale files from dev server
            if Directory.Exists(publicDir) then
                if verbose then printfn "        Cleaning public directory..."
                Directory.Delete(publicDir, true)

            if verbose then printfn "        Running hugo --minify..."
            let exitCode, _, stderr = runProcess "hugo" "--minify" hugoDir verbose

            if exitCode <> 0 then
                return Error $"Hugo build failed: {stderr}"
            elif not (Directory.Exists(publicDir)) then
                return Error $"Hugo build did not create public directory: {publicDir}"
            else

            let fileCount = Directory.GetFiles(publicDir, "*", SearchOption.AllDirectories).Length
            if verbose then printfn "        Found %d files in public directory" fileCount

            // Deploy (project is created by provision step; skip existence check to avoid API auth flakes)
            nextStep (sprintf "Deploying to Pages project: %s" projectName)

            let progressCallback msg = if verbose then printfn "        %s" msg else printfn "        %s" msg
            let! deployResult = pages.DeployDirectory projectName publicDir (Some "main") (Some "Deploy from CLI") None verbose progressCallback
            match deployResult with
            | Error e -> return Error $"Deployment failed: {e}"
            | Ok url ->

            printfn ""
            printfn "Deployment complete!"
            printfn ""
            printfn "  Site URL: https://%s.pages.dev" projectName
            if url <> "Deployment created successfully" then
                printfn "  Preview URL: %s" url

            return Ok url
        }
