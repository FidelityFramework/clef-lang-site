namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.Worker.Context

/// Corpus-graph index: a page-grained node/edge graph stored in the same D1 DB as
/// search, rebuilt idempotently by the CLI graph extractor, and served Cytoscape-shaped
/// to the "Map" modal. Payloads crossing to D1/HTTP are plain JS (createObj/==>), never
/// F# DUs (which Fable compiles to tagged objects D1/JSON cannot read).
module Graph =

    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Ensure the graph tables exist. The search D1 may have been provisioned before the
    /// graph schema existed; rather than require a provision re-run, the worker self-heals
    /// on first use (the same ensure-on-use discipline Indexing uses for added columns).
    /// CREATE TABLE IF NOT EXISTS is idempotent and cheap.
    let private ensureSchema (env: WorkerEnv) : JS.Promise<unit> =
        promise {
            let! _ =
                env.DB.prepare("""
                    CREATE TABLE IF NOT EXISTS graph_nodes (
                        page_url TEXT PRIMARY KEY, content_type TEXT NOT NULL, layer TEXT NOT NULL,
                        title TEXT NOT NULL DEFAULT '', summary TEXT DEFAULT '', tags TEXT DEFAULT '',
                        published_at TEXT DEFAULT '', ext_url TEXT DEFAULT '', category TEXT DEFAULT '',
                        updated_at TEXT NOT NULL
                    )""").run<obj>()
            let! _ =
                env.DB.prepare("""
                    CREATE TABLE IF NOT EXISTS graph_edges (
                        source_url TEXT NOT NULL, target_url TEXT NOT NULL, edge_type TEXT NOT NULL,
                        weight REAL NOT NULL DEFAULT 1.0, label TEXT DEFAULT '', updated_at TEXT NOT NULL,
                        PRIMARY KEY (source_url, target_url, edge_type)
                    )""").run<obj>()
            let! _ = env.DB.prepare("CREATE INDEX IF NOT EXISTS idx_graph_nodes_layer ON graph_nodes(layer)").run<obj>()
            let! _ = env.DB.prepare("CREATE INDEX IF NOT EXISTS idx_graph_edges_source ON graph_edges(source_url)").run<obj>()
            let! _ = env.DB.prepare("CREATE INDEX IF NOT EXISTS idx_graph_edges_target ON graph_edges(target_url)").run<obj>()
            let! _ = env.DB.prepare("CREATE INDEX IF NOT EXISTS idx_graph_edges_type ON graph_edges(edge_type)").run<obj>()
            // Self-heal: a graph_nodes table provisioned before the category column existed
            // needs the column added. ALTER fails if it already exists, so guard on PRAGMA.
            let! info = env.DB.prepare("PRAGMA table_info(graph_nodes)").all<obj>()
            let hasCategory =
                match info.results with
                | Some r -> r |> Seq.exists (fun row -> string row?name = "category")
                | None -> false
            if not hasCategory then
                let! _ = env.DB.prepare("ALTER TABLE graph_nodes ADD COLUMN category TEXT DEFAULT ''").run<obj>()
                ()
            return ()
        }

    /// Idempotent full rebuild. Replaces all nodes and edges with the supplied set.
    /// Refuses an empty node set (a CLI bug must not silently wipe the graph).
    let rebuild (env: WorkerEnv) (nodes: GraphNodeRequest array) (edges: GraphEdgeRequest array) : JS.Promise<obj> =
        promise {
            do! ensureSchema env
            let now = DateTime.UtcNow.ToString("o")

            // One D1 batch is one transaction: an insertion failure must leave the
            // previous graph intact. JSON rowsets keep this at four statements instead
            // of thousands of sequential, individually committed requests.
            let nodeSql =
                """
                INSERT INTO graph_nodes
                    (page_url, content_type, layer, title, summary, tags, published_at, ext_url, category, updated_at)
                SELECT json_extract(value, '$.pageUrl'), json_extract(value, '$.contentType'),
                       json_extract(value, '$.layer'), json_extract(value, '$.title'),
                       json_extract(value, '$.summary'), json_extract(value, '$.tags'),
                       json_extract(value, '$.publishedAt'), json_extract(value, '$.extUrl'),
                       json_extract(value, '$.category'), ?
                FROM json_each(?)
                """

            let edgeSql =
                """
                INSERT INTO graph_edges (source_url, target_url, edge_type, weight, label, updated_at)
                SELECT json_extract(value, '$.source'), json_extract(value, '$.target'),
                       json_extract(value, '$.edgeType'), json_extract(value, '$.weight'),
                       json_extract(value, '$.label'), ?
                FROM json_each(?)
                """

            let statements = ResizeArray [
                env.DB.prepare("DELETE FROM graph_edges")
                env.DB.prepare("DELETE FROM graph_nodes")
                env.DB.prepare(nodeSql).bind(now, JS.JSON.stringify nodes)
                env.DB.prepare(edgeSql).bind(now, JS.JSON.stringify edges)
            ]
            let! _ = env.DB.batch<obj>(statements)

            return box {| nodes = nodes.Length; edges = edges.Length |}
        }

    /// Read the whole graph in Cytoscape elements shape: { nodes:[{data}], edges:[{data}], stats }.
    /// Inbound href degree is computed per node (href edges only — the honesty signal must
    /// reflect real prose links, not synthetic tag/semantic edges).
    let read (env: WorkerEnv) : JS.Promise<obj> =
        promise {
            let! nodeRes = env.DB.prepare("SELECT page_url, content_type, layer, title, summary, tags, published_at, ext_url, category FROM graph_nodes").all<obj>()
            let! edgeRes = env.DB.prepare("SELECT source_url, target_url, edge_type, weight, label FROM graph_edges").all<obj>()

            let edgeRows =
                match edgeRes.results with
                | Some r -> r |> Seq.toArray
                | None -> [||]

            // inbound href degree per target
            let indeg = System.Collections.Generic.Dictionary<string, int>()
            for row in edgeRows do
                if string row?edge_type = "href" then
                    let t = string row?target_url
                    indeg.[t] <- (if indeg.ContainsKey t then indeg.[t] else 0) + 1

            let nodeRows =
                match nodeRes.results with
                | Some r -> r |> Seq.toArray
                | None -> [||]

            let nodeEls =
                nodeRows
                |> Array.map (fun row ->
                    let url = string row?page_url
                    let deg = if indeg.ContainsKey url then indeg.[url] else 0
                    let ext = string row?ext_url
                    createObj [
                        "data" ==> createObj [
                            "id" ==> url
                            "layer" ==> string row?layer
                            "contentType" ==> string row?content_type
                            "category" ==> string row?category
                            "title" ==> string row?title
                            "summary" ==> string row?summary
                            "tags" ==> string row?tags
                            "publishedAt" ==> string row?published_at
                            "deg" ==> deg
                            "url" ==> (if ext = "" then url else ext)
                        ]
                    ])

            let edgeEls =
                edgeRows
                |> Array.map (fun row ->
                    let s = string row?source_url
                    let t = string row?target_url
                    let ty = string row?edge_type
                    createObj [
                        "data" ==> createObj [
                            "id" ==> (s + ">>" + t + ">>" + ty)
                            "source" ==> s
                            "target" ==> t
                            "type" ==> ty
                            "weight" ==> (row?weight)
                            "label" ==> string row?label
                        ]
                    ])

            // per-layer counts for the stat line
            let counts = System.Collections.Generic.Dictionary<string, int>()
            for row in nodeRows do
                let l = string row?layer
                counts.[l] <- (if counts.ContainsKey l then counts.[l] else 0) + 1
            let cats = createObj [ for kv in counts -> kv.Key ==> kv.Value ]

            return box (createObj [
                "nodes" ==> nodeEls
                "edges" ==> edgeEls
                "stats" ==> createObj [
                    "nodes" ==> nodeRows.Length
                    "edges" ==> edgeRows.Length
                    "categories" ==> cats
                ]
            ])
        }
