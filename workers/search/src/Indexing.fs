namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.D1

module Indexing =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Outcome of preparing one section before the batched Vectorize upsert
    type private Prepared =
        /// Content and vector already current — nothing to do
        | Unchanged of id: string
        /// D1 row written and embedding ready; vector awaits the batched upsert
        | Pending of id: string * vector: obj
        /// D1 write or embedding generation failed for this section
        | Failed of id: string * message: string

    /// Prepare one section: refresh its D1 row if changed and generate its embedding.
    /// Does NOT upsert to Vectorize — upserts are batched in indexBatch so the whole
    /// batch is a single Vectorize request, avoiding the per-request rate limit (40041).
    let private prepareSection
        (env: WorkerEnv)
        (section: IndexSectionRequest)
        : JS.Promise<Prepared> =
        promise {
            try
                // Check if content has changed or vector indexing is incomplete
                let checkSql = "SELECT content_hash, vector_indexed FROM content_sections WHERE id = ?"
                let! existing = env.DB.prepare(checkSql).bind(section.id).first<obj>()

                let needsUpdate =
                    match existing with
                    | None -> true
                    | Some row ->
                        if isNullOrUndefined row then true
                        else string row?content_hash <> section.contentHash

                let needsVectorize =
                    match existing with
                    | None -> true
                    | Some row ->
                        if isNullOrUndefined row then true
                        else int row?vector_indexed <> 1

                if not needsUpdate && not needsVectorize then
                    return Unchanged section.id
                else

                // Update D1 if content changed
                if needsUpdate then
                    // Delete existing row first (triggers will clean FTS5)
                    let! _ = env.DB.prepare("DELETE FROM content_sections WHERE id = ?")
                                .bind(section.id).run<obj>()

                    // Insert new row (trigger will update FTS5)
                    let insertSql = """
                        INSERT INTO content_sections (
                            id, content_type, page_slug, page_title, page_url,
                            section_index, section_title, content, tags, summary,
                            content_hash, indexed_at, vector_indexed
                        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
                    """
                    let stmt = env.DB.prepare(insertSql)
                    let bound = stmt.bind(
                        section.id, section.contentType, section.pageSlug,
                        section.pageTitle, section.pageUrl, section.sectionIndex,
                        section.sectionTitle, section.content, section.tags,
                        section.summary, section.contentHash,
                        DateTime.UtcNow.ToString("o")
                    )
                    let! _ = bound.run<obj>()
                    ()

                // Generate embedding for the batched upsert
                let embeddingText =
                    $"{section.pageTitle} — {section.sectionTitle}\n{section.content}"

                let! embedding = Search.generateEmbedding env.AI embeddingText

                // Create vector as plain JS object — F# DU types (VectorizeVectorMetadata,
                // VectorizeVectorMetadataValue) compile to tagged objects via Fable, which
                // Cloudflare Vectorize can't parse. Plain createObj produces clean JSON.
                let vector =
                    createObj [
                        "id" ==> section.id
                        "values" ==> embedding
                        "metadata" ==> createObj [
                            "page_url" ==> section.pageUrl
                            "page_title" ==> section.pageTitle
                            "section_title" ==> section.sectionTitle
                            "content_type" ==> section.contentType
                        ]
                    ]
                return Pending(section.id, vector)

            with ex ->
                return Failed(section.id, ex.Message)
        }

    /// Batch index sections.
    /// D1 writes and embedding generation run concurrently per section, then ALL
    /// vectors are upserted to Vectorize in one call. Single-vector upserts per
    /// section trip the Vectorize request rate limit (40041) on large corpora.
    let indexBatch (env: WorkerEnv) (sections: IndexSectionRequest array) =
        promise {
            let! prepared =
                sections
                |> Array.map (prepareSection env)
                |> Promise.all

            let pending =
                prepared |> Array.choose (function Pending (id, v) -> Some (id, v) | _ -> None)
            let unchanged =
                prepared |> Array.filter (function Unchanged _ -> true | _ -> false) |> Array.length
            let prepFailures =
                prepared |> Array.choose (function Failed (id, m) -> Some (id, m) | _ -> None)

            // Single batched upsert for the whole batch
            let mutable upsertError = None
            if pending.Length > 0 then
                try
                    let vectors = pending |> Array.map snd
                    let! _ = env.VECTORIZE.upsert(!!vectors)

                    // Mark all upserted sections as vector-indexed (D1, not rate-limited)
                    let! _ =
                        pending
                        |> Array.map (fun (id, _) ->
                            env.DB.prepare("UPDATE content_sections SET vector_indexed = 1 WHERE id = ?")
                                .bind(id).run<obj>())
                        |> Promise.all
                    ()
                with ex ->
                    upsertError <- Some ex.Message

            let results =
                [|
                    for id, _ in pending ->
                        match upsertError with
                        | None -> {| success = true; id = id; message = "indexed" |}
                        | Some msg -> {| success = false; id = id; message = $"VECTOR_UPSERT_ERROR: {msg}" |}
                    for id, msg in prepFailures ->
                        {| success = false; id = id; message = msg |}
                |]

            let indexed = match upsertError with Some _ -> 0 | None -> pending.Length
            let failed = prepFailures.Length + (match upsertError with Some _ -> pending.Length | None -> 0)

            return {|
                success = (failed = 0)
                indexed = indexed
                unchanged = unchanged
                failed = failed
                results = results
            |}
        }
