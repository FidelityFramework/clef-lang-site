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
        abstract member contentHash: string with get

    /// Batch index request from CLI
    [<AllowNullLiteral>]
    [<Interface>]
    type BatchIndexRequest =
        abstract member sections: IndexSectionRequest array with get

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
