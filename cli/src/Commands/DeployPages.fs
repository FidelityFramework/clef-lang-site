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

    let private refreshSpecModule (hugoDir: string) (verbose: bool) : Result<unit, string> =
        printfn "        Clearing spec module cache..."
        let exitCode, _, stderr =
            runProcess "hugo" "mod clean --pattern *clef-lang-spec*" hugoDir verbose

        if exitCode <> 0 then
            Error $"hugo mod clean failed: {stderr}"
        else

        printfn "        Pulling latest spec from fidelity branch..."
        let exitCode, _, stderr =
            runProcess "hugo" "mod get -u github.com/FidelityFramework/clef-lang-spec@fidelity" hugoDir verbose

        if exitCode <> 0 then
            Error $"hugo mod get failed: {stderr}"
        else

        printfn "        Updating vendor directory..."
        let exitCode, _, stderr =
            runProcess "hugo" "mod vendor" hugoDir verbose

        if exitCode <> 0 then
            Error $"hugo mod vendor failed: {stderr}"
        else

        if verbose then
            let _, stdout, _ = runProcess "hugo" "mod graph" hugoDir false
            let specLine =
                stdout.Split('\n')
                |> Array.tryFind (fun l -> l.Contains("clef-lang-spec"))
            match specLine with
            | Some line -> printfn "        Module: %s" (line.TrimStart())
            | None -> ()

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

            let totalSteps = if refreshSpec then 4 else 3
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

            // Ensure project exists
            nextStep (sprintf "Checking Pages project: %s" projectName)

            let! exists = pages.ProjectExists(projectName)
            let! projectReady =
                if not exists then
                    async {
                        printfn "        Creating project..."
                        let! createResult = pages.CreateProject(projectName, "main")
                        match createResult with
                        | Error e -> return Error $"Failed to create Pages project: {e}"
                        | Ok () ->
                            if verbose then printfn "        Created project successfully"
                            return Ok ()
                    }
                else
                    async {
                        if verbose then printfn "        Project exists"
                        return Ok ()
                    }

            match projectReady with
            | Error e -> return Error e
            | Ok () ->

            // Deploy
            nextStep "Deploying site..."

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
