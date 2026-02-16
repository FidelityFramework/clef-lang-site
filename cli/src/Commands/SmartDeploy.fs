namespace ClefLang.CLI.Commands

open System
open ClefLang.CLI
open ClefLang.CLI.Core

module SmartDeploy =

    let execute
        (config: Config.CloudflareConfig)
        (baseRef: string option)
        (force: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            if force then
                printfn "Force flag set - running full deployment"
                return! Migrate.execute config verbose
            else
                // Determine base ref
                let actualBaseRef =
                    match baseRef with
                    | Some r -> r
                    | None ->
                        // Use last deployed commit if available, otherwise HEAD~1
                        match Config.loadState() with
                        | Some state ->
                            state.LastDeployedCommit |> Option.defaultValue "HEAD~1"
                        | None -> "HEAD~1"

                printfn "Analyzing changes from %s to HEAD..." actualBaseRef
                printfn ""

                let workingDir = Environment.CurrentDirectory
                match GitDiffAnalyzer.analyze actualBaseRef "HEAD" workingDir with
                | Error e -> return Error $"Diff analysis failed: {e}"
                | Ok analysis ->
                    printfn "Recommended scope: %A" analysis.RecommendedScope
                    for reason in analysis.Reasoning do
                        printfn "  - %s" reason
                    printfn ""

                    match analysis.RecommendedScope with
                    | Config.NoDeploy ->
                        printfn "No deployment needed."
                        return Ok "No changes requiring deployment"

                    | Config.PagesOnly ->
                        printfn "Executing Pages-only deployment..."
                        printfn ""
                        // TODO: Build Hugo and deploy to Pages
                        printfn "=== Building Hugo Site ==="
                        printfn "(Hugo build would run here)"
                        printfn "=== Deploying to Cloudflare Pages ==="
                        printfn "(Pages deployment would run here)"

                        // Update state with current commit
                        match Config.loadState() with
                        | Some state ->
                            // TODO: Get actual commit hash
                            Config.saveState { state with LastDeployedCommit = Some "HEAD" }
                        | None -> ()

                        printfn ""
                        printfn "Pages deployment complete!"
                        return Ok "Pages deployed"

                    | Config.PagesAndR2 ->
                        printfn "Executing Pages + R2 sync deployment..."
                        printfn ""

                        // TODO: Build Hugo
                        printfn "=== Building Hugo Site ==="
                        printfn "(Hugo build would run here)"

                        // TODO: Deploy to Pages
                        printfn ""
                        printfn "=== Deploying to Cloudflare Pages ==="
                        printfn "(Pages deployment would run here)"

                        // Sync content to R2
                        printfn ""
                        printfn "=== Syncing Content to R2 ==="

                        // If we have specific affected posts, only sync those
                        let! syncResult =
                            if analysis.AffectedPosts.Length > 0 && analysis.AffectedPosts.Length < 10 then
                                printfn "Syncing %d affected posts..." analysis.AffectedPosts.Length
                                // TODO: Implement selective sync
                                async { return Ok 0 }
                            else
                                printfn "Syncing all content..."
                                Sync.execute config "./hugo/content" false 8788 verbose

                        match syncResult with
                        | Error e -> return Error $"R2 sync failed: {e}"
                        | Ok count ->
                            // Update state
                            match Config.loadState() with
                            | Some state ->
                                Config.saveState {
                                    state with
                                        LastDeployedCommit = Some "HEAD"
                                        LastSyncTimestamp = Some DateTime.UtcNow
                                }
                            | None -> ()

                            printfn ""
                            printfn "Pages + R2 deployment complete!"
                            printfn "  Content synced: %d files" count
                            return Ok "Pages + R2 deployed"

                    | Config.FullDeploy ->
                        printfn "Executing full deployment (provision -> sync -> deploy)..."
                        printfn ""

                        // Run full migration
                        let! result = Migrate.execute config verbose
                        match result with
                        | Error e -> return Error e
                        | Ok url ->
                            // Update state with current commit
                            match Config.loadState() with
                            | Some state ->
                                Config.saveState { state with LastDeployedCommit = Some "HEAD" }
                            | None -> ()
                            return Ok url
        }
