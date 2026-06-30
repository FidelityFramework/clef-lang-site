namespace ClefLang.Search

open System
open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.Worker.Context
open Fidelity.CloudEdge.D1
open Fidelity.CloudEdge.Vectorize

[<AutoOpen>]
module Types =

    /// A search result returned to the client
    type SearchResult = {
        id: string
        pageTitle: string
        sectionTitle: string
        pageUrl: string
        contentType: string
        snippet: string
        publishedAt: string
        score: float
    }

    /// BM25-only search response
    type SearchResponse = {
        query: string
        results: SearchResult array
        totalResults: int
        searchTimeMs: int
    }

    /// Hybrid search response (BM25 + vector with RRF)
    type HybridSearchResponse = {
        query: string
        results: SearchResult array
        totalResults: int
        searchTimeMs: int
        fusionMethod: string
    }

    /// A single section to index (received from CLI via /index endpoint)
    [<AllowNullLiteral>]
    [<Interface>]
    type IndexSectionRequest =
        abstract member id: string with get
        abstract member contentType: string with get
        abstract member pageSlug: string with get
        abstract member pageTitle: string with get
        abstract member pageUrl: string with get
        abstract member sectionIndex: int with get
        abstract member sectionTitle: string with get
        abstract member content: string with get
        abstract member tags: string with get
        abstract member summary: string with get
        abstract member publishedAt: string with get
        abstract member contentHash: string with get

    /// Batch index request from CLI
    [<AllowNullLiteral>]
    [<Interface>]
    type BatchIndexRequest =
        abstract member sections: IndexSectionRequest array with get

    /// Reconcile request from CLI: the complete set of section IDs that should exist
    /// after the current index pass. The worker deletes every D1 row and vector whose
    /// id is NOT in this set — the stale remainder left when content moves (its
    /// content-type/slug/section-index changes its id) or is deleted.
    [<AllowNullLiteral>]
    [<Interface>]
    type ReconcileRequest =
        abstract member validIds: string array with get

    /// A graph node received from the CLI graph extractor (page-grained)
    [<AllowNullLiteral>]
    [<Interface>]
    type GraphNodeRequest =
        abstract member pageUrl: string with get
        abstract member contentType: string with get
        abstract member layer: string with get
        abstract member title: string with get
        abstract member summary: string with get
        abstract member tags: string with get
        abstract member publishedAt: string with get
        abstract member extUrl: string with get

    /// A graph edge received from the CLI graph extractor
    [<AllowNullLiteral>]
    [<Interface>]
    type GraphEdgeRequest =
        abstract member source: string with get
        abstract member target: string with get
        abstract member edgeType: string with get
        abstract member weight: float with get
        abstract member label: string with get

    /// Full graph rebuild payload from the CLI (idempotent replace)
    [<AllowNullLiteral>]
    [<Interface>]
    type GraphRebuildRequest =
        abstract member nodes: GraphNodeRequest array with get
        abstract member edges: GraphEdgeRequest array with get

    /// Cloudflare Workers AI binding
    [<AllowNullLiteral>]
    [<Interface>]
    type Ai =
        abstract member run: model: string * input: obj -> JS.Promise<obj>

    /// Worker environment bindings
    [<AllowNullLiteral>]
    [<Interface>]
    type WorkerEnv =
        inherit Env
        abstract member DB: D1Database with get
        abstract member AI: Ai with get
        abstract member VECTORIZE: VectorizeIndex with get
        abstract member ALLOWED_ORIGIN: string with get
        abstract member INDEX_API_KEY: string with get

    /// Error response
    type ErrorResponse = {
        error: string
    }

    /// Health check response
    type HealthResponse = {
        status: string
    }
