namespace ClefLang.CLI.Commands

open System
open System.Text.Json
open ClefLang.CLI
open ClefLang.CLI.Core

module AnalyzeDiff =

    let private scopeToString (scope: Config.DeploymentScope) : string =
        match scope with
        | Config.PagesOnly -> "PAGES_ONLY"
        | Config.PagesAndR2 -> "PAGES_AND_R2"
        | Config.FullDeploy -> "FULL_DEPLOY"
        | Config.NoDeploy -> "NO_DEPLOY"

    let private classificationToString (c: Config.ChangeClassification) : string =
        match c with
        | Config.Cosmetic -> "cosmetic"
        | Config.ContentAddition -> "content-addition"
        | Config.ContentRemoval -> "content-removal"
        | Config.ContentModification -> "content-modification"
        | Config.WorkerChange -> "worker-change"
        | Config.CLIChange -> "cli-change"
        | Config.ConfigChange -> "config-change"
        | Config.InfrastructureChange -> "infrastructure-change"

    let executeText (baseRef: string option) (headRef: string) : Async<Result<unit, string>> =
        async {
            let actualBaseRef =
                match baseRef with
                | Some r -> r
                | None ->
                    match Config.loadState() with
                    | Some state -> state.LastDeployedCommit |> Option.defaultValue "HEAD~1"
                    | None -> "HEAD~1"

            let workingDir = Environment.CurrentDirectory
            match GitDiffAnalyzer.analyze actualBaseRef headRef workingDir with
            | Error e -> return Error e
            | Ok result ->
                printfn "Git Diff Analysis"
                printfn "================="
                printfn ""
                printfn "  Base:  %s" result.BaseRef
                printfn "  Head:  %s" result.HeadRef
                printfn "  Files: %d changed" result.Files.Length
                printfn ""

                printfn "Recommended Deployment Scope: %s" (scopeToString result.RecommendedScope)
                printfn ""

                printfn "Reasoning:"
                for reason in result.Reasoning do
                    printfn "  - %s" reason
                printfn ""

                if result.Files.Length > 0 then
                    printfn "File Analysis:"
                    for file in result.Files do
                        printfn "  %s %s" file.ChangeType file.FilePath
                        printfn "       Classification: %s" (classificationToString file.Classification)
                        printfn "       Lines: +%d -%d, Hunks: %d" file.LinesAdded file.LinesRemoved file.HunkCount
                        match file.TokenDelta with
                        | Some delta -> printfn "       Token delta: ~%d" delta
                        | None -> ()
                    printfn ""

                if result.AffectedPosts.Length > 0 then
                    printfn "Affected Content:"
                    for post in result.AffectedPosts do
                        printfn "  - %s" post
                    printfn ""

                return Ok ()
        }

    let executeJson (baseRef: string option) (headRef: string) : Async<Result<unit, string>> =
        async {
            let actualBaseRef =
                match baseRef with
                | Some r -> r
                | None ->
                    match Config.loadState() with
                    | Some state -> state.LastDeployedCommit |> Option.defaultValue "HEAD~1"
                    | None -> "HEAD~1"

            let workingDir = Environment.CurrentDirectory
            match GitDiffAnalyzer.analyze actualBaseRef headRef workingDir with
            | Error e -> return Error e
            | Ok result ->
                let options = JsonSerializerOptions(WriteIndented = true)
                let json = JsonSerializer.Serialize(result, options)
                printfn "%s" json
                return Ok ()
        }
