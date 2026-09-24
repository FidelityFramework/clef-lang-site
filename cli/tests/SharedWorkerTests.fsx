// Run after dotnet build cli/ClefLang.CLI.fsproj:
// dotnet fsi cli/tests/SharedWorkerTests.fsx
#r "../bin/Debug/net10.0/ClefLang.CLI.dll"

open ClefLang.CLI
open ClefLang.CLI.Core
open ClefLang.CLI.Commands

let check condition message = if not condition then failwith message

check (GitDiffAnalyzer.classifyFilePath "workers/shared/Synthesis.fs" = Some Config.WorkerChange)
    "Shared synthesis policy must be classified as a worker change"
check (SmartDeploy.changedWorkers [ "workers/shared/Synthesis.fs" ] = Set.ofList [ "search"; "smart-search" ])
    "A shared-only correction must rebuild both synthesis workers"
check (SmartDeploy.changedWorkers [ "workers\\shared\\Synthesis.fs"; "workers/search/src/Search.fs" ] = Set.ofList [ "search"; "smart-search" ])
    "Shared worker dispatch must normalize paths and deduplicate workers"
check (SmartDeploy.changedWorkers [ "workers/content-sync/src/Main.fs"; "hugo/content/blog/topic.md" ] = Set.singleton "content-sync")
    "Unrelated changes must retain their existing worker scope"

printfn "Shared worker deployment classification checks passed."
