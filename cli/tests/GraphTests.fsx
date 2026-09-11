// Run after dotnet build cli/ClefLang.CLI.fsproj:
// dotnet fsi cli/tests/GraphTests.fsx
#r "../bin/Debug/net10.0/ClefLang.CLI.dll"

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text.Json
open ClefLang.CLI.Commands

let root = Path.Combine(Path.GetTempPath(), "clef-graph-tests-" + Guid.NewGuid().ToString("N"))
let content = Path.Combine(root, "content")
let write (relative: string) (text: string) =
    let path = Path.Combine(root, relative)
    Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
    File.WriteAllText(path, text)

let check condition message = if not condition then failwith message

try
    write "hugo.toml" "baseURL = 'https://example.invalid/'\n"
    write "content/docs/design/source.md" """---
title: Source
draft: false
---
[Relative](../target/#section)
[Absolute duplicate](/docs/design/target/)
[Tooling](../../tooling/inspector/)
[Draft](/blog/unpublished/)
[Missing](../missing/)
[Self](./#section)
"""
    write "content/docs/design/target.md" "---\ntitle: Target\n---\nPublished.\n"
    write "content/docs/tooling/inspector.md" "---\ntitle: Inspector\n---\nPublished.\n"
    write "content/blog/unpublished.md" "---\ntitle: Draft\ndraft: true # not public\n---\n[Target](/docs/design/target/)\n"

    // Capture the real extractor's HTTP payload without touching a deployed database.
    let socket = new TcpListener(IPAddress.Loopback, 0)
    socket.Start()
    let port = (socket.LocalEndpoint :?> IPEndPoint).Port
    socket.Stop()
    use listener = new HttpListener()
    listener.Prefixes.Add($"http://localhost:{port}/")
    listener.Start()
    let payload =
        async {
            let! extraction = Graph.execute content true port false |> Async.StartChild
            let! request = listener.GetContextAsync() |> Async.AwaitTask
            use reader = new StreamReader(request.Request.InputStream)
            let! body = reader.ReadToEndAsync() |> Async.AwaitTask
            request.Response.StatusCode <- 200
            request.Response.Close()
            let! result = extraction
            match result with
            | Error e -> return failwith e
            | Ok _ -> return body
        } |> fun work -> Async.RunSynchronously(work, 30000)
    use json = JsonDocument.Parse(payload)
    let nodes =
        json.RootElement.GetProperty("nodes").EnumerateArray()
        |> Seq.map (fun n -> n.GetProperty("pageUrl").GetString()) |> Set.ofSeq
    let links =
        json.RootElement.GetProperty("edges").EnumerateArray()
        |> Seq.filter (fun e -> e.GetProperty("edgeType").GetString() = "href")
        |> Seq.map (fun e -> e.GetProperty("source").GetString(), e.GetProperty("target").GetString())
        |> Seq.toList
    check (nodes.Contains "/docs/tooling/inspector/") "Tooling page omitted"
    check (not (nodes.Contains "/blog/unpublished/")) "Draft leaked into Atlas"
    let expected =
        Set.ofList [ "/docs/design/source/", "/docs/design/target/"
                     "/docs/design/source/", "/docs/tooling/inspector/" ]
    check (Set.ofList links = expected && links.Length = 2)
        $"Incorrect links (relative paths, duplicates, draft/missing/self filtering): {links}"
    printfn "Atlas extraction regression checks passed."
finally
    Directory.Delete(root, true)
