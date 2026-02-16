namespace ClefLang.CLI.Commands

open ClefLang.CLI

module Migrate =

    /// Full migration: provision -> deploy workers -> sync -> index
    let execute (config: Config.CloudflareConfig) (verbose: bool) : Async<Result<string, string>> =
        async {
            printfn "Starting full migration..."
            printfn ""

            // Step 1: Provision resources (R2, D1, Vectorize)
            printfn "=== Step 1/5: Provisioning Resources ==="
            printfn ""

            let! provisionResult = Provision.execute config verbose
            match provisionResult with
            | Error e -> return Error $"Provisioning failed: {e}"
            | Ok _state ->

            printfn ""

            // Step 2: Deploy all workers (they must be up before sync/index can reach them)
            printfn "=== Step 2/5: Deploying Workers ==="
            printfn ""

            let! contentSyncResult = Deploy.executeContentSync config "./workers/content-sync" false false verbose
            match contentSyncResult with
            | Error e -> return Error $"Content-sync worker deployment failed: {e}"
            | Ok contentSyncUrl ->

            let! smartSearchResult = Deploy.executeSmartSearch config "./workers/smart-search" false false verbose
            match smartSearchResult with
            | Error e -> return Error $"Smart-search worker deployment failed: {e}"
            | Ok smartSearchUrl ->

            let! searchResult = Deploy.executeSearch config "./workers/search" false false verbose
            match searchResult with
            | Error e -> return Error $"Search worker deployment failed: {e}"
            | Ok searchUrl ->

            printfn ""

            // Step 3: Sync content to R2 (via content-sync worker)
            printfn "=== Step 3/5: Syncing Content to R2 ==="
            printfn ""

            let hugoContentDir = "./hugo/content"
            let! syncResult = Sync.execute config hugoContentDir false 8788 verbose
            let syncCount =
                match syncResult with
                | Ok n -> n
                | Error e ->
                    printfn "Warning: Content sync failed: %s" e
                    0

            printfn ""

            // Step 4: Index content for search (via search worker)
            printfn "=== Step 4/5: Indexing Content for Search ==="
            printfn ""

            let! indexResult = Index.execute hugoContentDir false 8787 verbose
            let indexCount =
                match indexResult with
                | Ok n -> n
                | Error e ->
                    printfn "Warning: Search indexing failed: %s" e
                    0

            printfn ""

            // Step 5: Deploy Hugo site to Cloudflare Pages
            printfn "=== Step 5/5: Deploying Hugo Site to Cloudflare Pages ==="
            printfn ""

            let! pagesResult = DeployPages.execute config "./hugo" "clef-lang" false verbose
            match pagesResult with
            | Error e -> return Error $"Pages deployment failed: {e}"
            | Ok _ ->

            // Final summary
            printfn ""
            printfn "=========================================="
            printfn "Migration Complete!"
            printfn "=========================================="
            printfn ""
            printfn "Resources:"
            printfn "  R2 Bucket:       %s" Config.defaultResourceNames.R2BucketName
            printfn "  D1 (smart):      %s" Config.defaultResourceNames.SmartSearchD1Name
            printfn "  D1 (search):     %s" Config.defaultResourceNames.SearchD1Name
            printfn "  Vectorize:       %s" Config.defaultResourceNames.VectorizeIndexName
            printfn ""
            printfn "Workers:"
            printfn "  Content-sync:    %s" contentSyncUrl
            printfn "  Smart-search:    %s" smartSearchUrl
            printfn "  Search:          %s" searchUrl
            printfn ""
            printfn "Content synced:    %d files" syncCount
            printfn "Search indexed:    %d sections" indexCount

            return Ok "Migration complete"
        }

    /// Migrate with selective steps
    let executeSelective
        (config: Config.CloudflareConfig)
        (skipProvision: bool)
        (skipSync: bool)
        (skipIndex: bool)
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

            // Deploy workers first so sync/index endpoints are available
            if not skipDeploy then
                printfn "=== Deploying Workers ==="
                printfn ""
                let! result = Deploy.executeContentSync config "./workers/content-sync" false false verbose
                match result with
                | Error e -> printfn "Warning: Content-sync deploy failed: %s" e
                | Ok _ -> ()

                let! result = Deploy.executeSmartSearch config "./workers/smart-search" false false verbose
                match result with
                | Error e -> lastResult <- Error e
                | Ok _ -> ()

                let! result = Deploy.executeSearch config "./workers/search" false false verbose
                match result with
                | Error e -> printfn "Warning: Search worker deploy failed: %s" e
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
                | Error e -> printfn "Warning: Content sync failed: %s" e
                | Ok _ -> ()
                printfn ""

            if not skipIndex then
                printfn "=== Indexing Content for Search ==="
                printfn ""
                let! result = Index.execute "./hugo/content" false 8787 verbose
                match result with
                | Error e -> printfn "Warning: Search indexing failed: %s" e
                | Ok _ -> ()
                printfn ""

            return lastResult
        }
