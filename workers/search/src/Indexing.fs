namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.D1

module Indexing =

    /// Check if value is null or undefined
    let inline private isNullOrUndefined (x: 'a) : bool =
        emitJsExpr x "$0 == null"

    /// Index a single section into D1 and Vectorize
    let indexSection
        (env: WorkerEnv)
        (section: IndexSectionRequest)
        : JS.Promise<{| success: bool; id: string; message: string |}> =
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
                    return {| success = true; id = section.id; message = "unchanged" |}
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

                // Generate embedding and upsert to Vectorize
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
                let! _ = env.VECTORIZE.upsert(!![| vector |])

                // Mark as vector-indexed
                let! _ = env.DB.prepare("UPDATE content_sections SET vector_indexed = 1 WHERE id = ?")
                            .bind(section.id).run<obj>()

                return {| success = true; id = section.id; message = "indexed" |}

            with ex ->
                return {| success = false; id = section.id; message = ex.Message |}
        }

    /// Batch index sections
    let indexBatch (env: WorkerEnv) (sections: IndexSectionRequest array) =
        promise {
            let! results =
                sections
                |> Array.map (indexSection env)
                |> Promise.all

            let indexed = results |> Array.filter (fun r -> r.message = "indexed") |> Array.length
            let unchanged = results |> Array.filter (fun r -> r.message = "unchanged") |> Array.length
            let failed = results |> Array.filter (fun r -> not r.success) |> Array.length

            return {|
                success = (failed = 0)
                indexed = indexed
                unchanged = unchanged
                failed = failed
                results = results
            |}
        }
