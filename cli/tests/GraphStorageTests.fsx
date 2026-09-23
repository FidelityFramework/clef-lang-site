// Against a built worker with an isolated local D1 database:
// dotnet fsi cli/tests/GraphStorageTests.fsx http://localhost:8787 [path/to/local.sqlite]
// The optional SQLite path enables write auditing and timestamp checks (requires python3).
open System
open System.Diagnostics
open System.Net.Http
open System.Text
open System.Text.Json

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 1 || args.Length > 2 then failwith "Supply a local test worker URL and optional SQLite path."
let target = Uri(args.[0])
if not target.IsLoopback then failwith "These destructive fixture tests require a loopback worker."
let client = new HttpClient(BaseAddress = target)
client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-local-key")
let check condition message = if not condition then failwith message
let post route (payload: obj) =
    use body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
    use response = client.PostAsync("/graph/" + route, body).GetAwaiter().GetResult()
    int response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
let success route payload =
    let status, body = post route payload
    check (status = 200) $"{route} failed ({status}): {body}"
    use json = JsonDocument.Parse(body)
    json.RootElement.GetProperty("result").Clone()
let read () = client.GetStringAsync("/graph").GetAwaiter().GetResult()
let count (result: JsonElement) (group: string) (key: string) = result.GetProperty(group).GetProperty(key).GetInt32()
let delta result group added updated deleted unchanged =
    for key, expected in ["added", added; "updated", updated; "deleted", deleted; "unchanged", unchanged] do
        check (count result group key = expected) $"Wrong {group}.{key}: {result}"
let totals result group nodes edges =
    check (count result group "nodes" = nodes && count result group "edges" = edges) $"Wrong {group} totals: {result}"
let node id =
    {| pageUrl = $"/blog/{id}/"; contentType = "blog"; layer = "blog"; title = $"Page {id}"
       summary = ""; tags = ""; publishedAt = ""; extUrl = ""; category = "" |}
let edge source target kind =
    {| source = $"/blog/{source}/"; target = $"/blog/{target}/"; edgeType = kind; weight = 1.0; label = "" |}
let sql query =
    let start = ProcessStartInfo("python3", RedirectStandardOutput = true, RedirectStandardError = true)
    for argument in ["-c"; "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); r=c.execute(sys.argv[2]).fetchall(); c.commit(); print(r)"; args.[1]; query] do
        start.ArgumentList.Add(argument)
    use proc = Process.Start(start)
    let output = proc.StandardOutput.ReadToEnd().Trim()
    let error = proc.StandardError.ReadToEnd()
    proc.WaitForExit()
    check (proc.ExitCode = 0) error
    output
let audit = args.Length = 2
let auditCount expected =
    if audit then check (sql "SELECT COUNT(*) FROM graph_test_writes" = $"[({expected},)]") $"Expected {expected} row writes"
let resetAudit () = if audit then sql "DELETE FROM graph_test_writes" |> ignore

try
    // Large snapshots include every edge category; unchanged sync must write zero rows.
    let nodes = [| for i in 0 .. 299 -> node i |]
    let edges = [| for i in 0 .. 299 do for offset in 1 .. 7 -> edge i ((i + offset) % 300) ([|"href"; "cites"; "tag"|].[offset % 3]) |]
    let full = {| nodes = nodes; edges = edges |}
    let rebuilt = success "rebuild" full
    totals rebuilt "after" 300 2100
    if audit then
        sql "CREATE TABLE IF NOT EXISTS graph_test_writes (kind TEXT)" |> ignore
        for table in ["graph_nodes"; "graph_edges"] do
            for operation in ["INSERT"; "UPDATE"; "DELETE"] do
                sql $"CREATE TRIGGER IF NOT EXISTS audit_{table}_{operation} AFTER {operation} ON {table} BEGIN INSERT INTO graph_test_writes VALUES ('{operation}'); END" |> ignore
        sql "UPDATE graph_nodes SET updated_at = 'before-test'" |> ignore
        sql "UPDATE graph_edges SET updated_at = 'before-test'" |> ignore
        resetAudit ()
    let unchanged = success "sync" full
    totals unchanged "before" 300 2100
    totals unchanged "after" 300 2100
    delta unchanged "nodes" 0 0 0 300
    delta unchanged "edges" 0 0 0 2100
    auditCount 0
    if audit then
        check (sql "SELECT COUNT(*) FROM graph_nodes WHERE updated_at = 'before-test'" = "[(300,)]") "No-op changed timestamps"
    let repeat = success "rebuild" full
    delta repeat "nodes" 300 0 300 0
    delta repeat "edges" 2100 0 2100 0
    auditCount 4800
    if audit then
        check (sql "SELECT COUNT(*) FROM graph_nodes WHERE updated_at = 'before-test'" = "[(0,)]") "Force did not replace unchanged nodes"
        check (sql "SELECT COUNT(*) FROM graph_edges WHERE updated_at = 'before-test'" = "[(0,)]") "Force did not replace unchanged edges"

    // Same cardinality, different data: add/delete nodes, change metadata and edge values.
    let baseline = {| nodes = [|node 0; node 1; node 2|]; edges = [|edge 0 1 "href"; edge 1 2 "tag"|] |}
    success "rebuild" baseline |> ignore
    resetAudit ()
    let changedNode = {| node 1 with title = "Revised"; summary = "New summary"; tags = "x,y"; category = "Language"; publishedAt = "2026-09-22"; extUrl = "https://example.invalid/"; contentType = "spec"; layer = "spec" |}
    let changedEdge = {| edge 0 1 "href" with weight = 2.5; label = "Revised link" |}
    let replacement = {| nodes = [|node 0; changedNode; node 3|]; edges = [|changedEdge; edge 1 3 "cites"|] |}
    let changed = success "sync" replacement
    totals changed "before" 3 2
    totals changed "after" 3 2
    delta changed "nodes" 1 1 1 1
    delta changed "edges" 1 1 1 0
    auditCount 6
    let graph = read ()
    check (graph.Contains("Revised") && graph.Contains("2.5") && not (graph.Contains("/blog/2/"))) "Database differs from desired snapshot"
    resetAudit ()
    let repeatDelta = success "sync" replacement
    delta repeatDelta "nodes" 0 0 0 3
    delta repeatDelta "edges" 0 0 0 2
    auditCount 0

    if audit then
        // Reconcile database drift even when the submitted snapshot has not changed.
        sql "UPDATE graph_nodes SET title = 'database drift', summary = NULL WHERE page_url = '/blog/0/'" |> ignore
        resetAudit ()
        let repaired = success "sync" replacement
        delta repaired "nodes" 0 1 0 2
        delta repaired "edges" 0 0 0 2
        auditCount 1
        resetAudit ()

    // Fail during SQL insertion, after deletion and node writes, to prove rollback.
    // Null weight passes structural validation but violates the D1 NOT NULL constraint.
    let invalidEdge = {| source = "/blog/0/"; target = "/blog/1/"; edgeType = "href"; weight = (null: obj); label = "" |}
    for route in ["sync"; "rebuild"] do
        let before = read ()
        check (fst (post route {| nodes = nodes; edges = [|invalidEdge|] |}) >= 400) "Invalid insertion succeeded"
        check (read () = before) $"{route} failed to roll back"
        auditCount 0
        for invalid in [ box {| nodes = ([||] : obj array); edges = ([||] : obj array) |}
                         box {| nodes = [|node 0|] |}
                         box {| nodes = [|node 0; node 0|]; edges = [||] |}
                         box {| nodes = [|node 0|]; edges = [|edge 0 99 "href"|] |}
                         box {| nodes = [|node 0; node 1|]; edges = [|edge 0 1 "href"; edge 0 1 "href"|] |} ] do
            check (fst (post route invalid) = 400) "Invalid snapshot was accepted"
            check (read () = before) "Rejected snapshot changed graph"

    let noEdges = success "sync" {| nodes = replacement.nodes; edges = ([||] : obj array) |}
    delta noEdges "edges" 0 0 2 0
    totals noEdges "after" 3 0
    if audit then
        // Initial migration into empty tables takes the differential path as well.
        sql "DELETE FROM graph_nodes" |> ignore
        let initial = success "sync" baseline
        totals initial "before" 0 0
        delta initial "nodes" 3 0 0 0
        delta initial "edges" 2 0 0 0

    client.DefaultRequestHeaders.Remove("Authorization") |> ignore
    check (fst (post "sync" full) = 401) "Sync allowed unauthenticated writes"
    check (fst (post "rebuild" full) = 401) "Rebuild allowed unauthenticated writes"
    printfn "Atlas storage checks passed: rebuild, database delta, no-op, deletion, rollback, validation, authentication."
finally
    if audit then
        for table in ["graph_nodes"; "graph_edges"] do
            for operation in ["INSERT"; "UPDATE"; "DELETE"] do
                sql $"DROP TRIGGER IF EXISTS audit_{table}_{operation}" |> ignore
        sql "DROP TABLE IF EXISTS graph_test_writes" |> ignore
    client.Dispose()
