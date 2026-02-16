namespace ClefLang.CLI.Commands

open ClefLang.CLI
open ClefLang.CLI.Core

module Provision =

    let private smartSearchD1Schema = """
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

    let private searchD1Schema = """
        CREATE TABLE IF NOT EXISTS content_sections (
            id TEXT PRIMARY KEY,
            content_type TEXT NOT NULL,
            page_slug TEXT NOT NULL,
            page_title TEXT NOT NULL,
            page_url TEXT NOT NULL,
            section_index INTEGER NOT NULL,
            section_title TEXT NOT NULL DEFAULT '',
            content TEXT NOT NULL,
            tags TEXT DEFAULT '',
            summary TEXT DEFAULT '',
            content_hash TEXT NOT NULL,
            indexed_at TEXT NOT NULL,
            vector_indexed INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS idx_content_sections_page ON content_sections(page_slug);
        CREATE INDEX IF NOT EXISTS idx_content_sections_type ON content_sections(content_type);
    """

    // FTS5 and triggers must be executed as separate statements
    let private searchFts5Schema = """
        CREATE VIRTUAL TABLE IF NOT EXISTS content_fts USING fts5(
            page_title,
            section_title,
            content,
            content=content_sections,
            content_rowid=rowid,
            tokenize='unicode61'
        );
    """

    let private searchTriggerInsert = """
        CREATE TRIGGER IF NOT EXISTS content_fts_insert AFTER INSERT ON content_sections BEGIN
            INSERT INTO content_fts(rowid, page_title, section_title, content)
            VALUES (new.rowid, new.page_title, new.section_title, new.content);
        END;
    """

    let private searchTriggerDelete = """
        CREATE TRIGGER IF NOT EXISTS content_fts_delete AFTER DELETE ON content_sections BEGIN
            INSERT INTO content_fts(content_fts, rowid, page_title, section_title, content)
            VALUES('delete', old.rowid, old.page_title, old.section_title, old.content);
        END;
    """

    let private searchTriggerUpdate = """
        CREATE TRIGGER IF NOT EXISTS content_fts_update AFTER UPDATE ON content_sections BEGIN
            INSERT INTO content_fts(content_fts, rowid, page_title, section_title, content)
            VALUES('delete', old.rowid, old.page_title, old.section_title, old.content);
            INSERT INTO content_fts(rowid, page_title, section_title, content)
            VALUES (new.rowid, new.page_title, new.section_title, new.content);
        END;
    """

    let execute (config: Config.CloudflareConfig) (verbose: bool) : Async<Result<Config.DeploymentState, string>> =
        async {
            use httpClient = HttpHelpers.createAuthenticatedClient config.ApiToken
            let resources = Config.defaultResourceNames
            let d1 = D1Client.D1Operations(httpClient, config.AccountId)

            printfn "Provisioning Cloudflare resources..."
            printfn ""

            // 1. Create R2 bucket
            printfn "  [1/4] Creating R2 bucket: %s" resources.R2BucketName
            let r2 = R2Client.R2Operations(httpClient, config.AccountId)
            let! r2Result = r2.CreateBucket(resources.R2BucketName)
            match r2Result with
            | Error e -> return Error $"R2 bucket creation failed: {e}"
            | Ok () ->

            if verbose then printfn "        Bucket ready"

            // 2. Create smart-search D1 database (query logging, pattern tracking)
            printfn "  [2/4] Creating D1 database: %s" resources.SmartSearchD1Name
            let! d1Result = d1.CreateDatabase(resources.SmartSearchD1Name)
            match d1Result with
            | Error e -> return Error $"D1 database creation failed: {e}"
            | Ok smartSearchD1Id ->

            if verbose then printfn "        Database ID: %s" smartSearchD1Id

            printfn "        Initializing schema..."
            let! schemaResult = d1.ExecuteSQL(smartSearchD1Id, smartSearchD1Schema)
            match schemaResult with
            | Error e -> return Error $"Schema initialization failed: {e}"
            | Ok _ ->

            if verbose then printfn "        Schema initialized"

            // 3. Create search D1 database (content sections, FTS5)
            printfn "  [3/4] Creating search D1 database: %s" resources.SearchD1Name
            let! searchD1Result = d1.CreateDatabase(resources.SearchD1Name)
            match searchD1Result with
            | Error e -> return Error $"Search D1 database creation failed: {e}"
            | Ok searchD1Id ->

            if verbose then printfn "        Database ID: %s" searchD1Id

            printfn "        Initializing search schema..."
            // Execute each schema statement separately (D1 requires this for DDL)
            let searchStatements = [
                searchD1Schema
                searchFts5Schema
                searchTriggerInsert
                searchTriggerDelete
                searchTriggerUpdate
            ]
            let mutable schemaError = None
            for stmt in searchStatements do
                if schemaError.IsNone then
                    let! result = d1.ExecuteSQL(searchD1Id, stmt)
                    match result with
                    | Error e -> schemaError <- Some e
                    | Ok _ -> ()

            match schemaError with
            | Some e -> return Error $"Search schema initialization failed: {e}"
            | None ->

            if verbose then printfn "        Search schema initialized (content_sections + FTS5 + triggers)"

            // 4. Create Vectorize index
            printfn "  [4/4] Creating Vectorize index: %s" resources.VectorizeIndexName
            let vectorize = VectorizeClient.VectorizeOperations(httpClient, config.AccountId)
            let! vectorizeResult = vectorize.CreateIndex(resources.VectorizeIndexName, "@cf/baai/bge-base-en-v1.5")
            match vectorizeResult with
            | Error e -> return Error $"Vectorize index creation failed: {e}"
            | Ok msg ->

            if verbose then printfn "        %s" msg

            // Create metadata index for content_type filtering
            let! metaResult = vectorize.CreateMetadataIndex(resources.VectorizeIndexName, "content_type")
            match metaResult with
            | Error e ->
                if verbose then printfn "        Warning: metadata index creation failed: %s" e
            | Ok () ->
                if verbose then printfn "        Metadata index created (content_type)"

            let state: Config.DeploymentState = {
                R2BucketCreated = true
                SmartSearchD1Id = Some smartSearchD1Id
                SmartSearchWorkerDeployed = false
                SmartSearchWorkerUrl = None
                ContentSyncWorkerDeployed = false
                ContentSyncWorkerUrl = None
                ContentSyncApiKey = None
                SearchWorkerDeployed = false
                SearchWorkerUrl = None
                SearchIndexApiKey = None
                SearchD1Id = Some searchD1Id
                VectorizeIndexCreated = true
                LastDeployHash = None
                LastSyncTimestamp = None
                LastDeployedCommit = None
                LastGoSumHash = None
            }

            Config.saveState state

            printfn ""
            printfn "Provisioning complete!"
            printfn ""
            printfn "  R2 Bucket:        %s" resources.R2BucketName
            printfn "  Smart Search D1:  %s (ID: %s)" resources.SmartSearchD1Name smartSearchD1Id
            printfn "  Search D1:        %s (ID: %s)" resources.SearchD1Name searchD1Id
            printfn "  Vectorize Index:  %s" resources.VectorizeIndexName

            return Ok state
        }
