namespace ClefLang.CLI.Commands

open System
open System.Net.Http
open System.Text
open System.Text.Json
open ClefLang.CLI

/// Reconcile the extracted graph with D1, or explicitly replace it on --force.
module Graph =

    let private serialize (snapshot: GraphExtraction.Snapshot) =
        let nodePayload =
            snapshot.Nodes |> List.map (fun n ->
                {| pageUrl = n.PageUrl; contentType = n.ContentType; layer = n.Layer
                   title = n.Title; summary = n.Summary; tags = n.Tags
                   publishedAt = n.PublishedAt; extUrl = n.ExtUrl; category = n.Category |})
        let edgePayload =
            snapshot.Edges |> Seq.map (fun e ->
                {| source = e.Source; target = e.Target; edgeType = e.EdgeType
                   weight = e.Weight; label = e.Label |}) |> Seq.toList

        JsonSerializer.Serialize({| nodes = nodePayload; edges = edgePayload |})

    let private reportChanges (body: string) =
        use json = JsonDocument.Parse(body)
        let result = json.RootElement.GetProperty("result")
        let before = result.GetProperty("before")
        let after = result.GetProperty("after")
        let count (row: JsonElement) (name: string) = row.GetProperty(name).GetInt32()
        printfn "  Database: %d -> %d nodes, %d -> %d edges"
            (count before "nodes") (count after "nodes") (count before "edges") (count after "edges")
        for name in [ "nodes"; "edges" ] do
            let delta = result.GetProperty(name)
            printfn "  %s: %d added, %d updated, %d deleted, %d unchanged" name
                (count delta "added") (count delta "updated") (count delta "deleted") (count delta "unchanged")

    let execute hugoContentDir force useLocal localPort verbose : Async<Result<int, string>> =
        async {
            let workerUrl, apiKey =
                if useLocal then $"http://localhost:{localPort}", "dev-local-key"
                else
                    let state = Config.loadState () |> Option.defaultValue Config.defaultState
                    (state.SearchWorkerUrl |> Option.defaultValue ""),
                    (state.SearchIndexApiKey |> Option.defaultValue "")

            if String.IsNullOrEmpty workerUrl then
                return Error "Search worker not deployed. Run 'deploy' first or use --local."
            else
                try
                    let snapshot = GraphExtraction.extract hugoContentDir verbose
                    let endpoint = if force then "rebuild" else "sync"
                    use httpClient = new HttpClient()
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")
                    use content = new StringContent(serialize snapshot, Encoding.UTF8, "application/json")
                    printfn "  %s graph at %s/graph/%s ..."
                        (if force then "Replacing" else "Reconciling") workerUrl endpoint
                    use! response = httpClient.PostAsync($"{workerUrl.TrimEnd('/')}/graph/{endpoint}", content) |> Async.AwaitTask
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    if response.IsSuccessStatusCode then
                        reportChanges body
                        printfn "✓ Graph live"
                        return Ok 0
                    else
                        return Error $"Graph {endpoint} failed ({int response.StatusCode}): {body}"
                with ex ->
                    return Error $"Graph update failed: {ex.Message}"
        }
