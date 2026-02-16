namespace ClefLang.CLI.Commands

open ClefLang.CLI

module Migrate =

    /// Full migration: provision -> sync -> deploy
    let execute (config: Config.CloudflareConfig) (verbose: bool) : Async<Result<string, string>> =
        async {
            printfn "Starting full migration..."
            printfn ""

            // Step 1: Provision resources
            printfn "=== Step 1/3: Provisioning Resources ==="
            printfn ""

            let! provisionResult = Provision.execute config verbose
            match provisionResult with
            | Error e -> return Error $"Provisioning failed: {e}"
            | Ok _state ->

            printfn ""

            // Step 2: Sync content to R2
            printfn "=== Step 2/3: Syncing Content ==="
            printfn ""

            let hugoContentDir = "./hugo/content"
            let! syncResult = Sync.execute config hugoContentDir false 8788 verbose
            match syncResult with
            | Error e -> return Error $"Content sync failed: {e}"
            | Ok syncCount ->

            printfn ""

            // Step 3: Deploy worker
            printfn "=== Step 3/3: Deploying Worker ==="
            printfn ""

            let workerDir = "./workers/ask-ai"
            let! deployResult = Deploy.execute config workerDir false false verbose
            match deployResult with
            | Error e -> return Error $"Worker deployment failed: {e}"
            | Ok workerUrl ->

            // Final summary
            printfn ""
            printfn "=========================================="
            printfn "Migration Complete!"
            printfn "=========================================="
            printfn ""
            printfn "Resources:"
            printfn "  R2 Bucket:   %s" Config.defaultResourceNames.R2BucketName
            printfn "  D1 Database: %s" Config.defaultResourceNames.D1DatabaseName
            printfn ""
            printfn "Content synced: %d files" syncCount
            printfn ""
            printfn "Worker URL: %s" workerUrl
            printfn ""
            printfn "Next steps:"
            printfn "  1. Configure Hugo to use worker URL for Ask AI feature"
            printfn "  2. Deploy Hugo site to Cloudflare Pages"
            printfn "  3. Set up custom domain (optional)"

            return Ok workerUrl
        }

    /// Migrate with selective steps
    let executeSelective
        (config: Config.CloudflareConfig)
        (skipProvision: bool)
        (skipSync: bool)
        (skipDeploy: bool)
        (verbose: bool)
        : Async<Result<string, string>> =
        async {
            let mutable lastResult: Result<string, string> = Ok "No operations performed"

            if not skipProvision then
                printfn "=== Provisioning Resources ==="
                printfn ""
                let! result = Provision.execute config verbose
                match result with
                | Error e -> lastResult <- Error e
                | Ok _ -> ()
                printfn ""

            match lastResult with
            | Error _ -> return lastResult
            | Ok _ ->

            if not skipSync then
                printfn "=== Syncing Content ==="
                printfn ""
                let! result = Sync.execute config "./hugo/content" false 8788 verbose
                match result with
                | Error e -> lastResult <- Error e
                | Ok _ -> ()
                printfn ""

            match lastResult with
            | Error _ -> return lastResult
            | Ok _ ->

            if not skipDeploy then
                printfn "=== Deploying Worker ==="
                printfn ""
                let! result = Deploy.execute config "./workers/ask-ai" false false verbose
                match result with
                | Error e -> lastResult <- Error e
                | Ok url -> lastResult <- Ok url
                printfn ""

            return lastResult
        }
