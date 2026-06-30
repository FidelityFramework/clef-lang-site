namespace ClefLang.CLI.Commands

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open ClefLang.CLI
open ClefLang.CLI.Core

/// Corpus-graph extractor. Walks the same content as Index, but extracts the LINK GRAPH
/// (which Index's stripMarkdown discards) plus arXiv citations and shared-tag membership,
/// then POSTs the page-grained node/edge set to the search worker's /graph/rebuild.
/// This is the build step that makes the Map modal current on every deploy.
module Graph =

    /// The six Fidelity pre-prints (the apex kernel), id -> short label.
    let private preprints =
        [ "2603.16437", "DTS+DMM"
          "2603.17627", "Program Hypergraph"
          "2603.18104", "Adaptive Domain Models"
          "2603.25414", "Decidable by Construction"
          "2606.02854", "Fixed-Point Scaffolding"
          "2606.04352", "Negative & Fractional Types" ]
        |> Map.ofList

    /// External paper labels resolved from how the prose cites them (curated).
    let private externalTitles =
        [ "2511.01754", "Beckmann & Setzer"
          "2603.15569", "Mamba-3"
          "2509.00587", "Mehta & Hsu — Symmetry Hoare Logic"
          "2603.28627", "Cain et al. — Shor's at 10k qubits"
          "2603.20105", "λ-RLM (Roy et al.)"
          "2603.01615", "Jonnalagadda et al. — Bounded Posits"
          "2406.02528", "MatMul-free LM"
          "1811.02209", "Better Late Than Never (HCP)"
          "2103.14466", "Prioritise the Best Variation" ]
        |> Map.ofList

    /// An external paper becomes a node only if cited by at least this many pages
    /// (keeps the yellow ring small and meaningful — "not too many external links").
    [<Literal>]
    let private extThreshold = 2

    type private Node =
        { PageUrl: string; ContentType: string; Layer: string
          Title: string; Summary: string; Tags: string; PublishedAt: string; ExtUrl: string
          // spec taxonomy category (Process|Language|Semantics|Representation|Compiler|Platform);
          // empty for non-spec nodes. Drives the spec ring's 3-tier banding in the Atlas.
          Category: string }

    type private Edge =
        { Source: string; Target: string; EdgeType: string; Weight: float; Label: string }

    let private parseFrontMatter (content: string) =
        let m = Regex.Match(content, @"^---\s*\n([\s\S]*?)\n---\s*\n([\s\S]*)$")
        if m.Success then Some (m.Groups.[1].Value, m.Groups.[2].Value) else None

    let private field (yaml: string) (name: string) =
        let m = Regex.Match(yaml, $@"^{name}:\s*[""']?(.+?)[""']?\s*$", RegexOptions.Multiline)
        if m.Success then m.Groups.[1].Value.Trim() else ""

    /// Tags as comma-joined string, from either inline [a, b] or YAML list form.
    let private tagList (yaml: string) =
        let inlineM = Regex.Match(yaml, @"^tags:\s*\[(.*?)\]", RegexOptions.Multiline)
        if inlineM.Success then
            inlineM.Groups.[1].Value.Split(',')
            |> Array.map (fun s -> s.Trim().Trim('"', '\''))
            |> Array.filter (fun s -> s <> "")
            |> String.concat ","
        else
            let listM = Regex.Match(yaml, @"^tags:\s*\n((?:\s*-\s*.+\n?)+)", RegexOptions.Multiline)
            if listM.Success then
                Regex.Matches(listM.Groups.[1].Value, @"-\s*(.+)")
                |> Seq.cast<Match>
                |> Seq.map (fun x -> x.Groups.[1].Value.Trim().Trim('"', '\''))
                |> String.concat ","
            else ""

    /// docs content types collapse to the 'docs' ring; blog/spec keep their own.
    let private layerOf (contentType: string) =
        match contentType with
        | "blog" -> "blog"
        | "spec" -> "spec"
        | _ -> "docs"

    // Absolute internal links to docs/blog/spec pages. The char class now includes '#'
    // so a section-anchored link (…/foo/#3-2-bar) still captures; the anchor is stripped
    // to the page node below.
    let private internalLink = Regex(@"\]\((/(?:docs|blog|spec)/[a-zA-Z0-9/_#.-]+/?)\)")
    // Relative spec→spec links use the spec's own convention: ](foo.md). These appear only
    // inside spec files and resolve to /spec/draft/<basename>/.
    let private relativeSpecLink = Regex(@"\]\(([a-z][a-z0-9-]*)\.md(?:#[a-zA-Z0-9-]+)?\)")
    let private arxivLink = Regex(@"arxiv\.org/abs/([0-9]+\.[0-9]+)")

    /// Normalize a link target to a canonical page-node URL: strip any #anchor and ensure
    /// a single trailing slash.
    let private normalizeTarget (raw: string) : string =
        let noAnchor = match raw.IndexOf('#') with | -1 -> raw | i -> raw.Substring(0, i)
        if noAnchor.EndsWith("/") then noAnchor else noAnchor + "/"

    let execute
        (hugoContentDir: string)
        (useLocal: bool)
        (localPort: int)
        (verbose: bool)
        : Async<Result<int, string>> =
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

            let hugoDir = Path.GetDirectoryName(Path.GetFullPath(hugoContentDir))
            let specDir = Index.vendorSpecContent hugoDir verbose

            let specBaseAndFiles =
                match specDir with
                | Some dir ->
                    let baseDir = Path.GetDirectoryName(dir)
                    let files =
                        Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories)
                        |> Array.filter (fun f -> not (Path.GetFileName(f).StartsWith("_")))
                        |> List.ofArray
                    Some (baseDir, files)
                | None -> None

            let baseDirs =
                [ hugoContentDir ]
                @ (specBaseAndFiles |> Option.map fst |> Option.toList)

            let localFiles =
                [ Path.Combine(hugoContentDir, "blog"); Path.Combine(hugoContentDir, "docs") ]
                |> List.filter Directory.Exists
                |> List.collect (fun d -> Directory.GetFiles(d, "*.md", SearchOption.AllDirectories) |> List.ofArray)
                |> List.filter (fun f -> not (Path.GetFileName(f).StartsWith("_")))

            let specFiles = specBaseAndFiles |> Option.map snd |> Option.defaultValue []
            let mdFiles = localFiles @ specFiles

            // Pass 1: build the page node set + collect raw bodies keyed by page_url.
            let contentNodes = System.Collections.Generic.Dictionary<string, Node>()
            let bodies = System.Collections.Generic.Dictionary<string, string>()
            let tagsByPage = System.Collections.Generic.Dictionary<string, string list>()

            for file in mdFiles do
                match Index.classifyContent baseDirs file with
                | Some (contentType, pageUrl) ->
                    let raw = File.ReadAllText(file)
                    let yaml, body =
                        match parseFrontMatter raw with
                        | Some (y, b) -> y, b
                        | None -> "", raw
                    let title =
                        let t = field yaml "title"
                        if t <> "" then t
                        else Path.GetFileNameWithoutExtension(file).Replace("-", " ")
                    let tags = tagList yaml
                    let node =
                        { PageUrl = pageUrl; ContentType = contentType; Layer = layerOf contentType
                          Title = title; Summary = (field yaml "description")
                          Tags = tags; PublishedAt = (field yaml "date"); ExtUrl = ""
                          Category = field yaml "category" }
                    contentNodes.[pageUrl] <- node
                    bodies.[pageUrl] <- body
                    if tags <> "" then tagsByPage.[pageUrl] <- (tags.Split(',') |> Array.toList)
                | None -> ()

            let pageSet = Set.ofSeq contentNodes.Keys

            // Pass 2: edges. href (page->page), cites (page->paper), and arxiv citation counts.
            let edges = ResizeArray<Edge>()
            let arxivCiters = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>()

            for KeyValue(pageUrl, body) in bodies do
                let seen = System.Collections.Generic.HashSet<string>()
                let addEdge (target: string) =
                    if pageSet.Contains target && target <> pageUrl && seen.Add(target) then
                        edges.Add { Source = pageUrl; Target = target; EdgeType = "href"; Weight = 1.0; Label = "" }
                // Absolute /docs|blog|spec links (anchor stripped to the page node).
                for m in internalLink.Matches(body) do
                    addEdge (normalizeTarget m.Groups.[1].Value)
                // Relative foo.md spec→spec links resolve to /spec/draft/<basename>/, but only
                // when the SOURCE page is itself a spec page (relative links are spec-internal).
                if pageUrl.StartsWith("/spec/draft/") then
                    for m in relativeSpecLink.Matches(body) do
                        addEdge $"/spec/draft/{m.Groups.[1].Value}/"
                for m in arxivLink.Matches(body) do
                    let aid = m.Groups.[1].Value
                    if not (arxivCiters.ContainsKey aid) then
                        arxivCiters.[aid] <- System.Collections.Generic.HashSet<string>()
                    arxivCiters.[aid].Add(pageUrl) |> ignore

            // paper nodes: 6 pre-prints always; external only at/above the threshold.
            let paperNodes = ResizeArray<Node>()
            for KeyValue(aid, label) in (preprints |> Map.toSeq |> dict) do
                let pid = $"arxiv:{aid}"
                paperNodes.Add
                    { PageUrl = pid; ContentType = "preprint"; Layer = "preprint"
                      Title = label; Summary = $"arXiv:{aid} — Fidelity pre-print"
                      Tags = ""; PublishedAt = ""; ExtUrl = $"https://arxiv.org/abs/{aid}"; Category = "" }
            for KeyValue(aid, citers) in arxivCiters do
                if not (preprints.ContainsKey aid) && citers.Count >= extThreshold then
                    let pid = $"arxiv:{aid}"
                    let label = externalTitles |> Map.tryFind aid |> Option.defaultValue $"arXiv:{aid}"
                    paperNodes.Add
                        { PageUrl = pid; ContentType = "external"; Layer = "external"
                          Title = label; Summary = $"external citation · {citers.Count} pages · arXiv:{aid}"
                          Tags = ""; PublishedAt = ""; ExtUrl = $"https://arxiv.org/abs/{aid}"; Category = "" }

            let paperIds = paperNodes |> Seq.map (fun n -> n.PageUrl) |> Set.ofSeq

            // cites edges (page -> paper), only to paper nodes we kept
            for KeyValue(aid, citers) in arxivCiters do
                let pid = $"arxiv:{aid}"
                if paperIds.Contains pid then
                    for c in citers do
                        edges.Add { Source = c; Target = pid; EdgeType = "cites"; Weight = 1.0; Label = "" }

            // tag membership edges (page<->page sharing a tag); skip ubiquitous tags (hairball) and singletons
            let pagesByTag = System.Collections.Generic.Dictionary<string, ResizeArray<string>>()
            for KeyValue(pageUrl, tags) in tagsByPage do
                for t in tags do
                    if not (pagesByTag.ContainsKey t) then pagesByTag.[t] <- ResizeArray()
                    pagesByTag.[t].Add(pageUrl)
            let tagPairs = System.Collections.Generic.HashSet<string>()
            for KeyValue(_, pages) in pagesByTag do
                let ps = pages |> Seq.distinct |> Seq.sort |> Seq.toArray
                if ps.Length >= 2 && ps.Length <= 12 then
                    for i in 0 .. ps.Length - 2 do
                        for j in i + 1 .. ps.Length - 1 do
                            let key = ps.[i] + ">>" + ps.[j]
                            if tagPairs.Add(key) then
                                edges.Add { Source = ps.[i]; Target = ps.[j]; EdgeType = "tag"; Weight = 1.0; Label = "" }

            let allNodes = (contentNodes.Values |> List.ofSeq) @ (paperNodes |> List.ofSeq)

            // Narrate the extraction the way the search index does — the graph rebuild should
            // be as legible as indexing, not an opaque two-liner.
            let countLayer layer = allNodes |> List.filter (fun n -> n.Layer = layer) |> List.length
            let countEdge t = edges |> Seq.filter (fun e -> e.EdgeType = t) |> Seq.length
            // Connectivity: inbound href degree per node — the "is the spec ring lit" signal.
            let inboundHref = System.Collections.Generic.Dictionary<string, int>()
            for e in edges do
                if e.EdgeType = "href" then
                    inboundHref.[e.Target] <- (if inboundHref.ContainsKey e.Target then inboundHref.[e.Target] else 0) + 1
            let specNodes = allNodes |> List.filter (fun n -> n.Layer = "spec")
            let specLit = specNodes |> List.filter (fun n -> inboundHref.ContainsKey n.PageUrl) |> List.length

            printfn ""
            printfn "Building corpus graph from %d pages..." contentNodes.Count
            printfn "  Nodes: %d total" allNodes.Length
            printfn "    spec %d, docs %d, blog %d, pre-prints %d, external %d"
                (countLayer "spec") (countLayer "docs") (countLayer "blog") (countLayer "preprint") (countLayer "external")
            printfn "  Edges: %d total" edges.Count
            printfn "    href (cross-links) %d, cites (papers) %d, tag (themes) %d"
                (countEdge "href") (countEdge "cites") (countEdge "tag")
            printfn "  Spec connectivity: %d/%d spec entries reached by an inbound link" specLit specNodes.Length

            // POST the rebuild
            let nodePayload =
                allNodes |> List.map (fun n ->
                    {| pageUrl = n.PageUrl; contentType = n.ContentType; layer = n.Layer
                       title = n.Title; summary = n.Summary; tags = n.Tags
                       publishedAt = n.PublishedAt; extUrl = n.ExtUrl; category = n.Category |})
            let edgePayload =
                edges |> Seq.map (fun e ->
                    {| source = e.Source; target = e.Target; edgeType = e.EdgeType
                       weight = e.Weight; label = e.Label |}) |> Seq.toList

            use httpClient = new HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")
            let jsonOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            let payload = {| nodes = nodePayload; edges = edgePayload |}
            let json = JsonSerializer.Serialize(payload, jsonOptions)
            use content = new StringContent(json, Encoding.UTF8, "application/json")

            try
                let! response = httpClient.PostAsync($"{workerUrl}/graph/rebuild", content) |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    printfn "  Posting to %s/graph/rebuild ..." workerUrl
                    printfn "✓ Graph live: %d nodes, %d edges served from the edge (D1)" allNodes.Length edges.Count
                    return Ok 0
                else
                    let! err = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    return Error $"Graph rebuild failed ({int response.StatusCode}): {err}"
            with ex ->
                return Error $"Graph rebuild request failed: {ex.Message}"
        }
