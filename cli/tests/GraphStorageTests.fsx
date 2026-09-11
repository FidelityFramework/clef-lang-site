// Against the built search worker running locally with an isolated D1 database:
// dotnet fsi cli/tests/GraphStorageTests.fsx http://localhost:8787
open System
open System.Net.Http
open System.Text
open System.Text.Json

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length <> 1 then failwith "Supply a local test worker URL."
let target = Uri(args.[0])
if not target.IsLoopback then failwith "These destructive fixture tests require a loopback worker."
let client = new HttpClient(BaseAddress = target)
client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-local-key")
let check condition message = if not condition then failwith message
let post (payload: obj) =
    use body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
    use response = client.PostAsync("/graph/rebuild", body).GetAwaiter().GetResult()
    int response.StatusCode
let read () =
    let text = client.GetStringAsync("/graph").GetAwaiter().GetResult()
    use json = JsonDocument.Parse(text)
    let root = json.RootElement
    let nodes = root.GetProperty("nodes").EnumerateArray() |> Seq.map (fun n -> n.GetProperty("data").GetProperty("id").GetString()) |> Set.ofSeq
    let edges = root.GetProperty("edges").EnumerateArray() |> Seq.map (fun e -> e.GetProperty("data").GetProperty("id").GetString()) |> Set.ofSeq
    nodes, edges
let node id =
    {| pageUrl = $"/blog/{id}/"; contentType = "blog"; layer = "blog"; title = $"Page {id}"
       summary = ""; tags = ""; publishedAt = ""; extUrl = ""; category = "" |}
let edge source target kind =
    {| source = $"/blog/{source}/"; target = $"/blog/{target}/"; edgeType = kind; weight = 1.0; label = "" |}

// Exceed the former row-at-a-time workload, including every edge category.
let nodes = [| for i in 0 .. 299 -> node i |]
let edges = [| for i in 0 .. 299 do for offset in 1 .. 7 -> edge i ((i + offset) % 300) ([|"href"; "cites"; "tag"|].[offset % 3]) |]
check (post {| nodes = nodes; edges = edges |} = 200) "Full graph rebuild failed"
let fullNodes, fullEdges = read ()
check (fullNodes.Count = 300 && fullEdges.Count = 2100) "Full graph was truncated"
check (post {| nodes = nodes; edges = edges |} = 200) "Idempotent rebuild failed"
check (read () = (fullNodes, fullEdges)) "Repeat rebuild changed the graph"

let replacement = {| nodes = [|node 0; node 1|]; edges = [|edge 0 1 "href"|] |}
check (post replacement = 200) "Replacement failed"
let before = read ()
check ((fst before).Count = 2 && (snd before).Count = 1) "Replacement retained stale rows"

// Fail inside edge insertion, after the batch's deletes and node insertion.
let invalidEdge = {| source = "/blog/0/"; target = "/blog/1/"; edgeType = (null: string); weight = 1.0; label = "" |}
check (post {| nodes = nodes; edges = [|invalidEdge|] |} >= 400) "Invalid insert unexpectedly succeeded"
check (read () = before) "Failed insertion did not roll back the entire graph replacement"
check (post {| nodes = ([||] : obj array); edges = ([||] : obj array) |} = 400) "Empty graph was accepted"
check (read () = before) "Empty rebuild changed the graph"
printfn "Atlas storage checks passed: full graph, repeat, replacement, rollback, empty rejection."
client.Dispose()
