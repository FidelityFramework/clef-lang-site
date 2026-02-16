namespace ClefLang.CLI.Commands

open System.Text.Json
open ClefLang.CLI

module Status =

    let executeText (config: Config.CloudflareConfig) : Async<Result<unit, string>> =
        async {
            match Config.loadState() with
            | None ->
                printfn "No deployment state found. Run 'provision' first."
                return Error "No deployment state"
            | Some state ->
                printfn "ClefLang Deployment Status"
                printfn "========================="
                printfn ""
                printfn "Infrastructure:"
                printfn "  R2 Bucket:       %s" (if state.R2BucketCreated then "Created" else "Not created")
                printfn "  Smart Search D1: %s" (state.SmartSearchD1Id |> Option.map (fun id -> $"ID: {id}") |> Option.defaultValue "Not created")
                printfn "  Search D1:       %s" (state.SearchD1Id |> Option.map (fun id -> $"ID: {id}") |> Option.defaultValue "Not created")
                printfn "  Vectorize:       %s" (if state.VectorizeIndexCreated then "Created" else "Not created")
                printfn ""
                printfn "Workers:"
                printfn "  Smart Search:  %s" (if state.SmartSearchWorkerDeployed then "Deployed" else "Not deployed")
                printfn "  Smart URL:     %s" (state.SmartSearchWorkerUrl |> Option.defaultValue "N/A")
                printfn "  Search:        %s" (if state.SearchWorkerDeployed then "Deployed" else "Not deployed")
                printfn "  Search URL:    %s" (state.SearchWorkerUrl |> Option.defaultValue "N/A")
                printfn "  Content Sync:  %s" (if state.ContentSyncWorkerDeployed then "Deployed" else "Not deployed")
                printfn "  Content URL:   %s" (state.ContentSyncWorkerUrl |> Option.defaultValue "N/A")
                printfn ""
                printfn "Deployment:"
                printfn "  Last Sync:     %s" (state.LastSyncTimestamp |> Option.map (fun t -> t.ToString("o")) |> Option.defaultValue "Never")
                printfn "  Last Commit:   %s" (state.LastDeployedCommit |> Option.defaultValue "N/A")
                printfn ""
                printfn "Resource Names:"
                let resources = Config.defaultResourceNames
                printfn "  Smart Search Worker: %s" resources.SmartSearchWorkerName
                printfn "  Search Worker:       %s" resources.SearchWorkerName
                printfn "  Content Sync Worker: %s" resources.ContentSyncWorkerName
                printfn "  R2 Bucket:           %s" resources.R2BucketName
                printfn "  Smart Search D1:     %s" resources.SmartSearchD1Name
                printfn "  Search D1:           %s" resources.SearchD1Name
                printfn "  Vectorize Index:     %s" resources.VectorizeIndexName

                return Ok ()
        }

    let executeJson (config: Config.CloudflareConfig) : Async<Result<unit, string>> =
        async {
            match Config.loadState() with
            | None ->
                return Error "No deployment state"
            | Some state ->
                let options = JsonSerializerOptions(WriteIndented = true)
                let json = JsonSerializer.Serialize(state, options)
                printfn "%s" json
                return Ok ()
        }
