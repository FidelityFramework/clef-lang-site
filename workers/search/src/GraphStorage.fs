namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop

/// Database reconciliation. Both modes compare and write inside a single D1 batch.
module GraphStorage =

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

    // The same column mapping drives comparison and insertion, so metadata changes
    // cannot be missed by a separate hash or a hand-maintained comparison list.
    type private RowSet =
        { Table: string
          Keys: (string * string) list
          Values: (string * string) list }

    let private nodes =
        { Table = "graph_nodes"
          Keys = [ "page_url", "pageUrl" ]
          Values = [ "content_type", "contentType"; "layer", "layer"; "title", "title"
                     "summary", "summary"; "tags", "tags"; "published_at", "publishedAt"
                     "ext_url", "extUrl"; "category", "category" ] }

    let private edges =
        { Table = "graph_edges"
          Keys = [ "source_url", "source"; "target_url", "target"; "edge_type", "edgeType" ]
          Values = [ "weight", "weight"; "label", "label" ] }

    let private incoming rows =
        rows.Keys @ rows.Values
        |> List.map (fun (column, property) -> $"json_extract(value, '$.{property}') AS {column}")
        |> String.concat ", "
        |> fun columns -> $"WITH incoming AS (SELECT {columns} FROM json_each(?))"

    let private equalKeys rows left right =
        rows.Keys
        |> List.map (fun (column, _) -> $"{left}.{column} = {right}.{column}")
        |> String.concat " AND "

    let private missingKeys rows =
        let columns = rows.Keys |> List.map fst |> String.concat ", "
        let nullKeys = rows.Keys |> List.map (fun (column, _) -> $"{column} IS NULL") |> String.concat " OR "
        // Materialize the incoming key set once. A correlated json_each scan here
        // would reparse the entire payload for every stored edge.
        $"({columns}) NOT IN (SELECT {columns} FROM incoming) OR {nullKeys}"

    let private changedValues rows left right =
        rows.Values
        |> List.map (fun (column, _) -> $"{left}.{column} IS NOT {right}.{column}")
        |> String.concat " OR "

    /// Count the delta against the actual pre-write database, within the transaction.
    let private deltaSql rows force =
        let cte = incoming rows
        let table = rows.Table
        if force then
            $"""{cte}
                SELECT (SELECT COUNT(*) FROM incoming) AS added, 0 AS updated,
                       (SELECT COUNT(*) FROM {table}) AS deleted, 0 AS unchanged"""
        else
            let keys = equalKeys rows "stored" "incoming"
            let changed = changedValues rows "stored" "incoming"
            let missing = missingKeys rows
            $"""{cte}
                SELECT
                    (SELECT COUNT(*) FROM incoming WHERE NOT EXISTS
                        (SELECT 1 FROM {table} stored WHERE {keys})) AS added,
                    (SELECT COUNT(*) FROM incoming JOIN {table} stored ON {keys}
                        WHERE {changed}) AS updated,
                    (SELECT COUNT(*) FROM {table} WHERE {missing}) AS deleted,
                    (SELECT COUNT(*) FROM incoming JOIN {table} stored ON {keys}
                        WHERE NOT ({changed})) AS unchanged"""

    let private deleteSql rows force =
        if force then $"DELETE FROM {rows.Table}"
        else
            $"{incoming rows} DELETE FROM {rows.Table} WHERE {missingKeys rows}"

    let private insertSql rows force =
        let columns = rows.Keys @ rows.Values |> List.map fst |> String.concat ", "
        let insert =
            $"""{incoming rows}
                INSERT INTO {rows.Table} ({columns}, updated_at)
                SELECT {columns}, ? FROM incoming WHERE true"""
        if force then insert
        else
            let keys = rows.Keys |> List.map fst |> String.concat ", "
            let assignments =
                rows.Values
                |> List.map (fun (column, _) -> $"{column} = excluded.{column}")
                |> String.concat ", "
            let changed = changedValues rows rows.Table "excluded"
            // WHERE true above disambiguates SQLite's SELECT ... ON CONFLICT syntax.
            $"{insert} ON CONFLICT ({keys}) DO UPDATE SET {assignments}, updated_at = excluded.updated_at WHERE {changed}"

    let private countsSql =
        "SELECT (SELECT COUNT(*) FROM graph_nodes) AS nodes, (SELECT COUNT(*) FROM graph_edges) AS edges"

    /// Complete desired snapshot in, actual database delta out. Unchanged rows retain
    /// their timestamps. Force deletes and reinserts all rows, including identical ones.
    let write (env: WorkerEnv) force (nodeRows: GraphNodeRequest array) (edgeRows: GraphEdgeRequest array) : JS.Promise<obj> =
        promise {
            do! ensureSchema env
            let now = DateTime.UtcNow.ToString("o")
            let nodeJson, edgeJson = JS.JSON.stringify nodeRows, JS.JSON.stringify edgeRows
            let deletion rows (json: string) =
                let statement = env.DB.prepare(deleteSql rows force)
                if force then statement else statement.bind(json)
            let statements = ResizeArray [
                env.DB.prepare(countsSql)
                env.DB.prepare(deltaSql nodes force).bind(nodeJson)
                env.DB.prepare(deltaSql edges force).bind(edgeJson)
                deletion edges edgeJson
                deletion nodes nodeJson
                env.DB.prepare(insertSql nodes force).bind(nodeJson, now)
                env.DB.prepare(insertSql edges force).bind(edgeJson, now)
                env.DB.prepare(countsSql)
            ]
            let! results = env.DB.batch<obj>(statements)
            let row index =
                match results.[index].results with
                | Some rows when rows.Count > 0 -> rows.[0]
                | _ -> failwith "Graph transaction returned no statistics"
            return createObj [
                "mode" ==> (if force then "rebuild" else "sync")
                "before" ==> row 0
                "nodes" ==> row 1
                "edges" ==> row 2
                "after" ==> row 7
            ]
        }
