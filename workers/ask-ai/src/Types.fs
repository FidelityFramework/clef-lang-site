namespace ClefLang.AskAI

open System
open Fable.Core
open Fable.Core.JsInterop
open CloudFlare.Worker.Context
open CloudFlare.D1
open CloudFlare.AI.Generated

[<AutoOpen>]
module Types =

    /// A single search result from the BM25 query (sent by the client)
    [<AllowNullLiteral>]
    [<Interface>]
    type SearchResult =
        abstract member title: string with get
        abstract member url: string with get
        abstract member snippet: string with get
        abstract member score: float with get

    /// Incoming synthesis request - pre-ranked BM25 results + the original query
    [<AllowNullLiteral>]
    [<Interface>]
    type SynthesizeRequest =
        abstract member query: string with get
        abstract member results: SearchResult array with get

    /// Response with AI-generated summary grounded in the provided results
    type SynthesizeResponse = {
        summary: string
        sourceCount: int
    }

    /// Query log entry for D1 analytics
    type QueryLogEntry = {
        Id: string
        QueryText: string
        Timestamp: DateTime
        ResponseLatencyMs: int
        SourceUrls: string
        SourceCount: int
    }

    /// Worker environment bindings
    [<AllowNullLiteral>]
    [<Interface>]
    type WorkerEnv =
        inherit Env
        abstract member DB: D1Database with get
        abstract member AI: Ai<obj> with get
        abstract member ALLOWED_ORIGIN: string with get

    /// Error response
    type ErrorResponse = {
        error: string
    }

    /// Health check response
    type HealthResponse = {
        status: string
    }
