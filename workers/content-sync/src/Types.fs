namespace ClefLang.ContentSync

open Fable.Core
open Fable.Core.JsInterop
open Fidelity.CloudEdge.Worker.Context
open Fidelity.CloudEdge.R2

[<AutoOpen>]
module Types =

    /// Content upload request from CLI (interface for clean JS interop)
    [<AllowNullLiteral>]
    [<Interface>]
    type SyncRequest =
        abstract member slug: string with get
        abstract member title: string with get
        abstract member date: string with get
        abstract member summary: string with get
        abstract member tags: string array with get
        abstract member content: string with get
        abstract member url: string with get

    /// Batch sync request (interface for clean JS interop)
    [<AllowNullLiteral>]
    [<Interface>]
    type BatchSyncRequest =
        abstract member posts: SyncRequest array with get

    /// Worker environment bindings
    [<AllowNullLiteral>]
    [<Interface>]
    type WorkerEnv =
        inherit Env
        abstract member CONTENT: R2Bucket with get
        abstract member SYNC_API_KEY: string with get
