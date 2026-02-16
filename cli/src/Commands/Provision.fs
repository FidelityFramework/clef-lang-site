namespace ClefLang.CLI.Commands

open ClefLang.CLI
open ClefLang.CLI.Core

module Provision =

    let private d1Schema = """
        CREATE TABLE IF NOT EXISTS query_log (
            id TEXT PRIMARY KEY,
            query_text TEXT NOT NULL,
            embedding_hash TEXT,
            timestamp TEXT NOT NULL,
            response_cached INTEGER NOT NULL DEFAULT 0,
            response_latency_ms INTEGER,
            source_urls TEXT,
            source_count INTEGER
        );

        CREATE INDEX IF NOT EXISTS idx_query_log_timestamp ON query_log(timestamp);
        CREATE INDEX IF NOT EXISTS idx_query_log_embedding ON query_log(embedding_hash);

        CREATE TABLE IF NOT EXISTS query_patterns (
            pattern_hash TEXT PRIMARY KEY,
            canonical_query TEXT NOT NULL,
            frequency INTEGER DEFAULT 1,
            last_seen TEXT NOT NULL,
            avg_latency_ms REAL,
            cache_hit_rate REAL,
            warming_priority REAL DEFAULT 0.0,
            last_warmed TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_patterns_priority ON query_patterns(warming_priority DESC);
    """

    let execute (config: Config.CloudflareConfig) (verbose: bool) : Async<Result<Config.DeploymentState, string>> =
        async {
            use httpClient = HttpHelpers.createAuthenticatedClient config.ApiToken
            let resources = Config.defaultResourceNames

            printfn "Provisioning Cloudflare resources..."
            printfn ""

            // 1. Create R2 bucket
            printfn "  [1/2] Creating R2 bucket: %s" resources.R2BucketName
            let r2 = R2Client.R2Operations(httpClient, config.AccountId)
            let! r2Result = r2.CreateBucket(resources.R2BucketName)
            match r2Result with
            | Error e -> return Error $"R2 bucket creation failed: {e}"
            | Ok () ->

            if verbose then printfn "        Bucket ready"

            // 2. Create D1 database
            printfn "  [2/2] Creating D1 database: %s" resources.D1DatabaseName
            let d1 = D1Client.D1Operations(httpClient, config.AccountId)
            let! d1Result = d1.CreateDatabase(resources.D1DatabaseName)
            match d1Result with
            | Error e -> return Error $"D1 database creation failed: {e}"
            | Ok d1Id ->

            if verbose then printfn "        Database ID: %s" d1Id

            // Initialize schema
            printfn "        Initializing schema..."
            let! schemaResult = d1.ExecuteSQL(d1Id, d1Schema)
            match schemaResult with
            | Error e -> return Error $"Schema initialization failed: {e}"
            | Ok _ ->

            if verbose then printfn "        Schema initialized"

            let state: Config.DeploymentState = {
                R2BucketCreated = true
                D1DatabaseId = Some d1Id
                AskAiWorkerDeployed = false
                AskAiWorkerUrl = None
                ContentSyncWorkerDeployed = false
                ContentSyncWorkerUrl = None
                ContentSyncApiKey = None
                LastDeployHash = None
                LastSyncTimestamp = None
                LastDeployedCommit = None
            }

            Config.saveState state

            printfn ""
            printfn "Provisioning complete!"
            printfn ""
            printfn "  R2 Bucket:   %s" resources.R2BucketName
            printfn "  D1 Database: %s (ID: %s)" resources.D1DatabaseName d1Id

            return Ok state
        }
