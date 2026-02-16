namespace ClefLang.CLI

open System
open System.IO
open System.Text.Json

module Config =

    type CloudflareConfig = {
        ApiToken: string
        AccountId: string
        R2AccessKeyId: string option
        R2SecretAccessKey: string option
    }

    type ResourceNames = {
        SmartSearchWorkerName: string
        ContentSyncWorkerName: string
        SearchWorkerName: string
        R2BucketName: string
        SmartSearchD1Name: string
        SearchD1Name: string
        VectorizeIndexName: string
    }

    let defaultResourceNames = {
        SmartSearchWorkerName = "clef-smart-search"
        ContentSyncWorkerName = "clef-content-sync"
        SearchWorkerName = "clef-search"
        R2BucketName = "clef-blog-content"
        SmartSearchD1Name = "clef-smart-search"
        SearchD1Name = "clef-search"
        VectorizeIndexName = "clef-content"
    }

    /// Deployment scope determined by git diff analysis
    type DeploymentScope =
        | PagesOnly           // Only cosmetic changes - just sync Hugo output to Pages
        | PagesAndR2          // Content changes - sync Pages + update R2 + re-index D1 FTS5
        | FullDeploy          // Worker/CLI changes - full deployment including worker
        | NoDeploy            // No relevant changes detected

    /// Classification of a file change
    type ChangeClassification =
        | Cosmetic            // Typo fix, grammar, minor wording
        | ContentAddition     // New content, new sections, significant additions
        | ContentRemoval      // Deleted content
        | ContentModification // Significant rewrites
        | WorkerChange        // Changes to worker code
        | CLIChange           // Changes to CLI tool
        | ConfigChange        // Changes to config files
        | InfrastructureChange // Changes to CI/CD, scripts

    /// Analysis result for a single file
    type FileAnalysis = {
        FilePath: string
        ChangeType: string    // A=Added, M=Modified, D=Deleted, R=Renamed
        Classification: ChangeClassification
        HunkCount: int
        LinesAdded: int
        LinesRemoved: int
        TokenDelta: int option // Estimated token change for content files
    }

    /// Complete diff analysis result
    type DiffAnalysisResult = {
        BaseRef: string
        HeadRef: string
        Files: FileAnalysis list
        RecommendedScope: DeploymentScope
        Reasoning: string list
        ContentFilesChanged: string list
        WorkerFilesChanged: string list
        AffectedPosts: string list  // Slugs of content that changed
    }

    type DeploymentState = {
        R2BucketCreated: bool
        SmartSearchD1Id: string option
        SmartSearchWorkerDeployed: bool
        SmartSearchWorkerUrl: string option
        ContentSyncWorkerDeployed: bool
        ContentSyncWorkerUrl: string option
        ContentSyncApiKey: string option
        SearchWorkerDeployed: bool
        SearchWorkerUrl: string option
        SearchIndexApiKey: string option
        SearchD1Id: string option
        VectorizeIndexCreated: bool
        LastDeployHash: string option
        LastSyncTimestamp: DateTime option
        LastDeployedCommit: string option
        LastGoSumHash: string option
    }

    let loadConfig () : Result<CloudflareConfig, string> =
        let apiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN")
        let accountId = Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID")
        let r2AccessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID")
        let r2SecretKey = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY")

        match apiToken, accountId with
        | null, _ -> Error "CLOUDFLARE_API_TOKEN environment variable not set"
        | _, null -> Error "CLOUDFLARE_ACCOUNT_ID environment variable not set"
        | token, account ->
            Ok {
                ApiToken = token
                AccountId = account
                R2AccessKeyId = if String.IsNullOrEmpty(r2AccessKey) then None else Some r2AccessKey
                R2SecretAccessKey = if String.IsNullOrEmpty(r2SecretKey) then None else Some r2SecretKey
            }

    let stateFilePath = ".clef-deploy-state.json"

    let private jsonOptions = JsonSerializerOptions(WriteIndented = true)

    let loadState () : DeploymentState option =
        if File.Exists(stateFilePath) then
            try
                let json = File.ReadAllText(stateFilePath)
                Some (JsonSerializer.Deserialize<DeploymentState>(json, jsonOptions))
            with _ ->
                None
        else
            None

    let saveState (state: DeploymentState) : unit =
        let json = JsonSerializer.Serialize(state, jsonOptions)
        File.WriteAllText(stateFilePath, json)

    let defaultState = {
        R2BucketCreated = false
        SmartSearchD1Id = None
        SmartSearchWorkerDeployed = false
        SmartSearchWorkerUrl = None
        ContentSyncWorkerDeployed = false
        ContentSyncWorkerUrl = None
        ContentSyncApiKey = None
        SearchWorkerDeployed = false
        SearchWorkerUrl = None
        SearchIndexApiKey = None
        SearchD1Id = None
        VectorizeIndexCreated = false
        LastDeployHash = None
        LastSyncTimestamp = None
        LastDeployedCommit = None
        LastGoSumHash = None
    }
