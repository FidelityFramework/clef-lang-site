namespace ClefLang.CLI.Commands

open System
open System.IO
open ClefLang.CLI
open ClefLang.CLI.Core

module DeployPages =

    let execute
        (config: Config.CloudflareConfig)
        (hugoDir: string)
        (projectName: string)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            use httpClient = HttpHelpers.createAuthenticatedClient config.ApiToken
            let pages = PagesClient.PagesOperations(httpClient, config.AccountId)

            printfn "Deploying Hugo site to Cloudflare Pages..."
            printfn ""

            // Step 1: Build Hugo site
            let publicDir = Path.Combine(hugoDir, "public")
            printfn "  [1/3] Building Hugo site..."

            // Clean public directory to avoid stale files from dev server
            if Directory.Exists(publicDir) then
                if verbose then printfn "        Cleaning public directory..."
                Directory.Delete(publicDir, true)

            // Build Hugo site
            if verbose then printfn "        Running hugo --minify..."
            let psi = System.Diagnostics.ProcessStartInfo("hugo", "--minify")
            psi.WorkingDirectory <- hugoDir
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true

            use proc = System.Diagnostics.Process.Start(psi)
            let stdout = proc.StandardOutput.ReadToEnd()
            let stderr = proc.StandardError.ReadToEnd()
            proc.WaitForExit()

            if proc.ExitCode <> 0 then
                return Error $"Hugo build failed: {stderr}"
            elif not (Directory.Exists(publicDir)) then
                return Error $"Hugo build did not create public directory: {publicDir}"
            else

            let fileCount = Directory.GetFiles(publicDir, "*", SearchOption.AllDirectories).Length
            if verbose then printfn "        Found %d files in public directory" fileCount

            // Step 2: Ensure project exists
            printfn "  [2/3] Checking Pages project: %s" projectName

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

            // Step 3: Deploy
            printfn "  [3/3] Deploying site..."

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
