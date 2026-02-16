namespace ClefLang.CLI

open System
open Argu

module Program =

    [<RequireQualifiedAccess>]
    type ProvisionArgs =
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type SyncArgs =
        | [<AltCommandLine("-d")>] Content_Dir of path: string
        | [<AltCommandLine("-l")>] Local
        | [<AltCommandLine("-p")>] Port of int
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Content_Dir _ -> "Hugo content directory (default: ./hugo/content)"
                | Local -> "Use local content-sync worker (localhost:8788)"
                | Port _ -> "Local worker port (default: 8788, requires --local)"
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type DeployArgs =
        | [<AltCommandLine("-n")>] Worker_Name of name: string
        | [<AltCommandLine("-w")>] Worker_Dir of path: string
        | [<AltCommandLine("-s")>] Skip_Build
        | [<AltCommandLine("-f")>] Force
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Worker_Name _ -> "Worker to deploy: smart-search, search, content-sync, all (default: smart-search)"
                | Worker_Dir _ -> "Worker source directory (overrides default for worker)"
                | Skip_Build -> "Skip worker build step"
                | Force -> "Force deployment even if source unchanged"
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type MigrateArgs =
        | [<AltCommandLine("-v")>] Verbose
        | Skip_Provision
        | Skip_Sync
        | Skip_Index
        | Skip_Deploy

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Verbose -> "Enable verbose output"
                | Skip_Provision -> "Skip resource provisioning"
                | Skip_Sync -> "Skip content sync"
                | Skip_Index -> "Skip search indexing"
                | Skip_Deploy -> "Skip worker deployment"

    [<RequireQualifiedAccess>]
    type StatusArgs =
        | [<AltCommandLine("-j")>] Json

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Json -> "Output status as JSON"

    [<RequireQualifiedAccess>]
    type AnalyzeDiffArgs =
        | [<AltCommandLine("-b")>] Base of ref: string
        | [<AltCommandLine("-h")>] Head of ref: string
        | [<AltCommandLine("-j")>] Json

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Base _ -> "Base git ref (default: from state or HEAD~1)"
                | Head _ -> "Head git ref (default: HEAD)"
                | Json -> "Output as JSON"

    [<RequireQualifiedAccess>]
    type SmartDeployArgs =
        | [<AltCommandLine("-b")>] Base of ref: string
        | [<AltCommandLine("-f")>] Force
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Base _ -> "Base git ref for comparison"
                | Force -> "Force spec refresh + build + deploy pages (no state checks)"
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type DeployPagesArgs =
        | [<AltCommandLine("-d")>] Hugo_Dir of path: string
        | [<AltCommandLine("-n")>] Project_Name of name: string
        | [<AltCommandLine("-r")>] Refresh_Spec
        | [<AltCommandLine("-f")>] Force
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Hugo_Dir _ -> "Hugo site directory (default: ./hugo)"
                | Project_Name _ -> "Pages project name (default: clef-lang)"
                | Refresh_Spec -> "Pull latest spec from clef-lang-spec before building"
                | Force -> "Force spec refresh, build, and deploy (no state checks)"
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type IndexArgs =
        | [<AltCommandLine("-d")>] Content_Dir of path: string
        | [<AltCommandLine("-l")>] Local
        | [<AltCommandLine("-p")>] Port of int
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Content_Dir _ -> "Hugo content directory (default: ./hugo/content)"
                | Local -> "Use local search worker (localhost:8787)"
                | Port _ -> "Local worker port (default: 8787, requires --local)"
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type PurgeArgs =
        | [<AltCommandLine("-l")>] Local
        | [<AltCommandLine("-p")>] Port of int
        | [<AltCommandLine("-v")>] Verbose

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Local -> "Use local content-sync worker (localhost:8788)"
                | Port _ -> "Local worker port (default: 8788, requires --local)"
                | Verbose -> "Enable verbose output"

    [<RequireQualifiedAccess>]
    type CLIArgs =
        | [<CliPrefix(CliPrefix.None)>] Provision of ParseResults<ProvisionArgs>
        | [<CliPrefix(CliPrefix.None)>] Sync of ParseResults<SyncArgs>
        | [<CliPrefix(CliPrefix.None)>] Deploy of ParseResults<DeployArgs>
        | [<CliPrefix(CliPrefix.None); CustomCommandLine("deploy-pages")>] DeployPages of ParseResults<DeployPagesArgs>
        | [<CliPrefix(CliPrefix.None)>] Migrate of ParseResults<MigrateArgs>
        | [<CliPrefix(CliPrefix.None)>] Status of ParseResults<StatusArgs>
        | [<CliPrefix(CliPrefix.None); CustomCommandLine("analyze-diff")>] AnalyzeDiff of ParseResults<AnalyzeDiffArgs>
        | [<CliPrefix(CliPrefix.None); CustomCommandLine("smart-deploy")>] SmartDeploy of ParseResults<SmartDeployArgs>
        | [<CliPrefix(CliPrefix.None)>] Index of ParseResults<IndexArgs>
        | [<CliPrefix(CliPrefix.None)>] Purge of ParseResults<PurgeArgs>
        | [<AltCommandLine("-V")>] Version

        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Provision _ -> "Provision Cloudflare resources (R2, D1, Vectorize)"
                | Sync _ -> "Sync Hugo content to R2"
                | Deploy _ -> "Deploy workers (smart-search, search, content-sync, all)"
                | DeployPages _ -> "Deploy Hugo site to Cloudflare Pages"
                | Migrate _ -> "Full migration: provision -> sync -> deploy"
                | Status _ -> "Show deployment status"
                | AnalyzeDiff _ -> "Analyze git diff to determine deployment scope"
                | SmartDeploy _ -> "Deploy based on git diff analysis"
                | Index _ -> "Index content into D1 FTS5 + Vectorize for search"
                | Purge _ -> "Purge all content from R2 bucket"
                | Version -> "Show version"

    let private loadConfig () : Result<Config.CloudflareConfig, string> =
        let accountId = Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID")
        let apiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN")

        if String.IsNullOrEmpty(accountId) then
            Error "CLOUDFLARE_ACCOUNT_ID environment variable not set"
        elif String.IsNullOrEmpty(apiToken) then
            Error "CLOUDFLARE_API_TOKEN environment variable not set"
        else
            let r2AccessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID")
            let r2SecretKey = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY")
            Ok {
                Config.CloudflareConfig.AccountId = accountId
                ApiToken = apiToken
                R2AccessKeyId = if String.IsNullOrEmpty(r2AccessKey) then None else Some r2AccessKey
                R2SecretAccessKey = if String.IsNullOrEmpty(r2SecretKey) then None else Some r2SecretKey
            }

    let private runAsync (computation: Async<Result<'a, string>>) : int =
        match Async.RunSynchronously computation with
        | Ok _ -> 0
        | Error e ->
            eprintfn "Error: %s" e
            1

    [<EntryPoint>]
    let main argv =
        let parser = ArgumentParser.Create<CLIArgs>(programName = "clef")

        try
            let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)

            if results.Contains <@ CLIArgs.Version @> then
                printfn "ClefLang CLI v0.1.0"
                0
            else
                match results.GetSubCommand() with
                | CLIArgs.Provision args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let verbose = args.Contains <@ ProvisionArgs.Verbose @>
                        Commands.Provision.execute config verbose
                        |> runAsync

                | CLIArgs.Sync args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let contentDir = args.GetResult(<@ SyncArgs.Content_Dir @>, "./hugo/content")
                        let useLocal = args.Contains <@ SyncArgs.Local @>
                        let localPort = args.GetResult(<@ SyncArgs.Port @>, 8788)
                        let verbose = args.Contains <@ SyncArgs.Verbose @>
                        Commands.Sync.execute config contentDir useLocal localPort verbose
                        |> runAsync

                | CLIArgs.Deploy args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let workerName = args.GetResult(<@ DeployArgs.Worker_Name @>, "smart-search")
                        let skipBuild = args.Contains <@ DeployArgs.Skip_Build @>
                        let force = args.Contains <@ DeployArgs.Force @>
                        let verbose = args.Contains <@ DeployArgs.Verbose @>

                        let deployOne name dir =
                            match name with
                            | "search" -> Commands.Deploy.executeSearch config dir skipBuild force verbose
                            | "content-sync" -> Commands.Deploy.executeContentSync config dir skipBuild force verbose
                            | _ -> Commands.Deploy.executeSmartSearch config dir skipBuild force verbose

                        match workerName with
                        | "all" ->
                            let computation = async {
                                let! r1 = Commands.Deploy.executeContentSync config "./workers/content-sync" skipBuild force verbose
                                match r1 with
                                | Error e -> return Error e
                                | Ok _ ->
                                let! r2 = Commands.Deploy.executeSmartSearch config "./workers/smart-search" skipBuild force verbose
                                match r2 with
                                | Error e -> return Error e
                                | Ok _ ->
                                return! Commands.Deploy.executeSearch config "./workers/search" skipBuild force verbose
                            }
                            computation |> runAsync
                        | name ->
                            let defaultDir =
                                match name with
                                | "search" -> "./workers/search"
                                | "content-sync" -> "./workers/content-sync"
                                | _ -> "./workers/smart-search"
                            let workerDir = args.GetResult(<@ DeployArgs.Worker_Dir @>, defaultDir)
                            deployOne name workerDir |> runAsync

                | CLIArgs.DeployPages args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let hugoDir = args.GetResult(<@ DeployPagesArgs.Hugo_Dir @>, "./hugo")
                        let projectName = args.GetResult(<@ DeployPagesArgs.Project_Name @>, "clef-lang")
                        let force = args.Contains <@ DeployPagesArgs.Force @>
                        let refreshSpec = force || args.Contains <@ DeployPagesArgs.Refresh_Spec @>
                        let verbose = args.Contains <@ DeployPagesArgs.Verbose @>
                        Commands.DeployPages.execute config hugoDir projectName refreshSpec verbose
                        |> runAsync

                | CLIArgs.Migrate args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let verbose = args.Contains <@ MigrateArgs.Verbose @>
                        if args.Contains <@ MigrateArgs.Skip_Provision @> ||
                           args.Contains <@ MigrateArgs.Skip_Sync @> ||
                           args.Contains <@ MigrateArgs.Skip_Index @> ||
                           args.Contains <@ MigrateArgs.Skip_Deploy @> then
                            Commands.Migrate.executeSelective
                                config
                                (args.Contains <@ MigrateArgs.Skip_Provision @>)
                                (args.Contains <@ MigrateArgs.Skip_Sync @>)
                                (args.Contains <@ MigrateArgs.Skip_Index @>)
                                (args.Contains <@ MigrateArgs.Skip_Deploy @>)
                                verbose
                            |> runAsync
                        else
                            Commands.Migrate.execute config verbose
                            |> runAsync

                | CLIArgs.Status args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let json = args.Contains <@ StatusArgs.Json @>
                        if json then
                            Commands.Status.executeJson config |> runAsync
                        else
                            Commands.Status.executeText config |> runAsync

                | CLIArgs.AnalyzeDiff args ->
                    let baseRef = args.TryGetResult <@ AnalyzeDiffArgs.Base @>
                    let headRef = args.GetResult(<@ AnalyzeDiffArgs.Head @>, "HEAD")
                    let json = args.Contains <@ AnalyzeDiffArgs.Json @>
                    if json then
                        Commands.AnalyzeDiff.executeJson baseRef headRef |> runAsync
                    else
                        Commands.AnalyzeDiff.executeText baseRef headRef |> runAsync

                | CLIArgs.SmartDeploy args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let baseRef = args.TryGetResult <@ SmartDeployArgs.Base @>
                        let force = args.Contains <@ SmartDeployArgs.Force @>
                        let verbose = args.Contains <@ SmartDeployArgs.Verbose @>
                        Commands.SmartDeploy.execute config baseRef force verbose
                        |> runAsync

                | CLIArgs.Index args ->
                    let contentDir = args.GetResult(<@ IndexArgs.Content_Dir @>, "./hugo/content")
                    let useLocal = args.Contains <@ IndexArgs.Local @>
                    let localPort = args.GetResult(<@ IndexArgs.Port @>, 8787)
                    let verbose = args.Contains <@ IndexArgs.Verbose @>
                    Commands.Index.execute contentDir useLocal localPort verbose
                    |> runAsync

                | CLIArgs.Purge args ->
                    match loadConfig() with
                    | Error e ->
                        eprintfn "Error: %s" e
                        1
                    | Ok config ->
                        let useLocal = args.Contains <@ PurgeArgs.Local @>
                        let localPort = args.GetResult(<@ PurgeArgs.Port @>, 8788)
                        let verbose = args.Contains <@ PurgeArgs.Verbose @>
                        Commands.Purge.execute config useLocal localPort verbose
                        |> runAsync

                | CLIArgs.Version ->
                    printfn "ClefLang CLI v0.1.0"
                    0

        with
        | :? ArguParseException as e ->
            printfn "%s" e.Message
            if e.ErrorCode = ErrorCode.HelpText then 0 else 1
        | ex ->
            eprintfn "Unexpected error: %s" ex.Message
            1
