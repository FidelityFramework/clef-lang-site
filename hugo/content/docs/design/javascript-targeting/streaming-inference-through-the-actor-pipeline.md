---
title: "Streaming Inference Through the Actor Pipeline"
linkTitle: "Streaming Inference"
description: "How BAREWire's Frame Format and Tell-First Semantics Enable Token-Level Streaming from Container Inference to Edge Delivery"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
weight: 50
---

## The Streaming Problem in Inference

Autoregressive models produce output one token at a time. A BitNet ADM inside a container can deliver each token as it is generated, through a Worker to the client. The [unified actor architecture](/blog/unified-actor-architecture/) establishes the proposed Prospero/Olivier path over BAREWire. In this streaming design, a declared response envelope carries correlation and a union case identifies the payload shape. The wire does not carry compiler type, schema, dimension, or proof tags. This page follows that design from the inference loop to the client's screen; Composer's JavaScript lowering remains proposed work.

Every LLM deployment solves this, and the standard approach is Server-Sent Events with JSON payloads:

```
data: {"token": " the", "index": 42, "finish_reason": null}\n\n
data: {"token": " answer", "index": 43, "finish_reason": null}\n\n
data: {"token": "", "index": 44, "finish_reason": "stop"}\n\n
```

This SSE/JSON example repeats field names and requires JSON parsing. A declared binary layout can avoid those names and lookups; either protocol can use generated validators and a schema agreed outside each message.

BAREWire gives the proposed compilation pipeline a compact, shared representation for this path.

## BAREWire Frames for Token Streaming

The inference response is a discriminated union:

```fsharp
type InferenceResponse =
    | Token of text: string * index: int     // case 0
    | StreamEnd of totalTokens: int          // case 1
    | StreamError of message: string         // case 2
 
```

The declared union supplies [three cases and their payload layouts](/spec/draft/discriminated-union-representation/). A case index selects the application variant; it is not a type or schema identifier. Strings make the payload length variable. A conceptual frame separates the stream framing, protocol envelope, and encoded union:

```
┌────────────────┬────────────────────┬─────────────────────────────┐
│ Stream framing │ Protocol envelope  │ BARE application payload    │
│ message length │ kind, correlation  │ Token case, text, index     │
└────────────────┴────────────────────┴─────────────────────────────┘
```

Exact byte counts depend on the pinned framing format, envelope, integer encoding, and token text. The receiver uses the agreed layout to decode the `Token` case, string, and index. `StreamEnd` and `StreamError` are other cases of that layout; their indices do not certify that both endpoints share the same schema.

The correlation ID ties the entire stream to the original request. The client sent a request with correlation ID 7. Every response frame carries correlation ID 7. The client's WebSocket handler matches on correlation ID and routes all frames to the same response handler. Multiple concurrent inference requests multiplex over the same WebSocket, each with a different correlation ID, each accumulating its own token stream independently.

## Three Substrates, One Frame Format

The [spatial mechanics](/docs/design/memory/spatial-mechanics/) article describes three streaming models: materialized, demand-driven, and spatial dataflow. Each applies to a different segment of the inference streaming path.

### Inside the Container: Spatial

The intended native inference pipeline combines spatial dataflow with bounded buffers and vectorized stages. Dimensional analysis, representation selection, and lifetime checking supply obligations for native lowering. Their metadata stays in the PSG/codata until each obligation has fulfilled its role; successful source analysis alone is not a proof that every target lowering preserves it.

Each generated token exits the inference pipeline as a value with a known type. The container's BAREWire serializer encodes it as a frame and sends it over the container binding.

### Container to Worker: Demand-Driven

Each generated token becomes a self-contained frame, permitting incremental delivery. The stream framing gives a declared message length; receiving code must still enforce available bytes and admitted bounds. Endpoint/schema agreement is established separately from the payload, rather than negotiated by a case index.

Tell-first delivery avoids a request/reply acknowledgment for each token, as described in the [BAREWire signal article](/blog/getting-the-signal-with-barewire/). It does not waive transport completion, backpressure, or buffer lifetimes: a send may need awaiting, and its storage must remain valid until the transport has finished with it.

### Worker to Client: Protocol Relay

The Worker receives BAREWire frames from the container and forwards them over WebSocket. The Worker is a protocol adapter. It does not buffer the full response. Each BAREWire frame from the container becomes a WebSocket frame to the client. The streaming is transparent: the Worker relays frames as they arrive.

The proposed [JSIR backend](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) would derive JavaScript boundary code from the same declared protocol as native code. A relay can forward payload bytes without decoding their values after checking the framing needed for its role. Shared derivation supports a correspondence argument; each target lowering must still preserve the contract.

## Contrast with Conventional Streaming

The wire savings are the smaller difference. What separates the two patterns is structural, and it shows in how each handles change.

In the SSE/JSON example, the client parses each object and interprets `finish_reason`. Generated validation can make that interpretation explicit. Compatibility under added or renamed fields depends on the chosen schema and consumer policy.

In the BAREWire design, adding a union case changes the declared layout. A decoder can reject an unknown case, but two incompatible schemas may reuse the same case index. A positional field rename need not change bytes; reordered fields or changed payload types can. Versioned endpoint/schema agreement must account for those differences separately from frame decoding.

The [schema agreement model](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#schema-identity-as-a-proxy-for-dimensional-agreement) applies across the stream under its endpoint and lowering premises. Each frame remains independently parseable under that agreed layout; no per-frame type or schema tag supplies the agreement.

## Multiplexing and Correlation

A single WebSocket connection between a client and a Worker can carry multiple concurrent inference streams. The correlation ID is the multiplexing key. Request 7 produces token frames with correlation ID 7. Request 12 produces token frames with correlation ID 12. Both flow over the same WebSocket. The client demultiplexes on correlation ID and routes each stream to its own handler.

This matters for interactive applications where a user might issue multiple queries in rapid succession, or where a supervisor issues parallel inference requests to different ADM instances. The WebSocket is a single connection. The BAREWire frames are independent messages on that connection. Correlation separates routing; streams still share transport and resource budgets.

With MoQ/QUIC in the future, each inference stream could become a separate QUIC stream with independent ordering. No head-of-line blocking from other traffic on the connection. The frame format does not change. The transport changes. The BAREWire frames are transport-agnostic by design.

## Design-Time Spec for Runtime Reliability

The aim is to carry one declared contract from inference output to client display, discharging static obligations and generating the checks that remain at open boundaries. The [contract-to-artifact example](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) separates current evidence from proposed lowering.

**Dimensional consistency**: source verification establishes admitted dimensional relations, and lowering must preserve their meaning. Dimensions need not become wire tags; their compiler obligations remain available until fulfilled.

**BAREWire schema**: a shared declaration determines positional encoding. The final payload is untagged with respect to type, schema, dimensions, and proof metadata. Receivers still check open input bounds and any narrowing premises not otherwise discharged.

**Frame structure**: the framing prefix, protocol envelope kind, and application union case serve different roles. A compiled case-to-layout mapping guides decoding; it does not make malformed or truncated bytes impossible.

**Cross-substrate compatibility**: shared derivation gives native and JavaScript codecs one contract to preserve. Current JavaScript-generated SMT evidence proves declaration models, not JavaScript execution. Establishing correspondence to emitted artifacts requires the separate lowering and implementation evidence described in the shared example.

**Representation selection**: native and JavaScript representations must satisfy the declared transfer contract. JavaScript may use `Number`, `BigInt`, or byte storage according to that contract; relaying raw posit bytes does not require converting them to float64. Representation obligations include exactness or admitted error, not just range coverage.

Checks can be removed when their premises have been discharged for the admitted path. Open network input still needs the relevant framing, bounds, and narrowing checks. Untagged payloads and retained compiler metadata make that division possible without carrying a proof certificate in each token.

The intended result is a small streaming runtime whose operations follow an explicit contract. The compiler retains the information needed to justify those operations through lowering, and the wire carries the application data and protocol structure needed for delivery.

## Where This Architecture Has Limits

The contract addresses modeled type, schema, and dimensional obligations together with preservation through lowering. Streaming also needs explicit operational policies for resource limits, reconnection, and delivery.

### Backpressure

Tell-first semantics mean the container does not wait for acknowledgment between tokens. If the container produces tokens faster than the Worker can relay or the client can consume, frames accumulate in the WebSocket send buffer. TCP flow control provides coarse-grained backpressure at the transport level, but it operates on byte volume, not on frame boundaries. A slow client causes the Worker's outbound buffer to grow until the runtime intervenes.

A sustained backlog can exhaust an isolate's memory budget. Bounded queues, rate limits, and credit-based flow control can make the resource policy explicit. Static bounds can justify part of it, while runtime checks enforce variable production and consumption rates. A feedback channel for credits can coexist with tell-first data delivery.

### Ordering and Reconnection

TCP guarantees in-order delivery on a single connection. A stream of token frames arrives in the order the container sent them as long as the WebSocket connection remains open. If the connection drops mid-stream, the ordering guarantee disappears. The client reconnects and receives a new WebSocket connection. Frames sent before the disconnect are lost unless the Worker buffered them.

Durable Objects can persist a stream ledger: correlation ID, last acknowledged index, and frames pending delivery. On reconnection, the client supplies its received position and the Worker replays according to an explicit duplicate-handling policy. Correlation and token indices support that protocol; the byte layout alone does not prove replay correctness.

For applications where dropped tokens are acceptable (interactive chat where the user can re-query), the simpler approach is to abandon the stream on disconnect and let the client issue a new request. For applications where every token matters (clinical decision support, financial computation), the stream ledger pattern is necessary. The choice is an architectural decision that belongs to the developer.

### Cold Start Latency

The streaming architecture assumes the container is already running when the first token request arrives. In practice, the first request to a cold container incurs startup latency: container image pull, runtime initialization, model loading. For a BitNet model, the model loading step may take seconds. The client experiences a long delay before the first token, followed by rapid streaming once the model is warm.

This is not specific to BAREWire or the Fidelity framework. Every containerized inference deployment faces the same cold start problem. The mitigations are standard: pre-warming containers, keeping a minimum replica count, using smaller model formats that load quickly. The BAREWire frame format does not affect cold start latency. It affects what happens after the first token is generated.

### Payload Boundaries

Choose a frame budget against the pinned transport's message limit, including envelope and framing overhead. Token text and diagnostics vary in length, so even token streaming needs an admitted bound or a checked chunking policy.

An embedding vector of 1024 float64 values has 8192 payload bytes before framing and other fields. Larger batches or diagnostic strings can exceed the chosen budget. A length prefix reports size; it does not establish that the size fits the transport or available input. Chunking requires a declared reassembly protocol when one output spans messages.

Declare a bounded or chunked output variant for bulk outputs such as embeddings, logits, and attention maps. The compiler can use static capacity information where available and generate runtime bound checks where size depends on model output. Both paths must account for framing overhead and the receiver's reassembly budget.

### Non-Autoregressive Models

The streaming architecture is motivated by autoregressive generation: each token is produced sequentially and can be delivered as it is generated. Not all inference models produce output this way.

A classification model produces a single output. An embedding model produces a single vector. A regression model produces a single value. For these models, the BAREWire frame format still works. The response is a single frame rather than a stream. The correlation ID still ties request to response. The same endpoint/schema agreement applies. But the streaming infrastructure (the token-by-token relay through the Worker, the client-side accumulation, the `StreamEnd` sentinel) is unnecessary overhead. A single request/response frame pair is simpler and more appropriate.

The type definition handles both patterns naturally:

```fsharp
type InferenceResponse =
    | Token of text: string * index: int         // autoregressive streaming
    | StreamEnd of totalTokens: int              // stream termination
    | StreamError of message: string             // error in either mode
    | Classification of label: string * confidence: float   // single response
    | Embedding of values: float array           // single response
 
```

The declared schema covers both response patterns. The Worker can relay admitted frames using the same framing rules. Application union cases distinguish streaming and single-response output without adding compiler type tags to the wire.

### What Testing Must Cover

Verification must connect the declared layout to actual emitted codecs and malformed-input behavior, as well as exercise the operational path. Useful integration checks include:

**Latency under load.** How many concurrent inference streams can a single Worker relay before response times degrade? This depends on the Worker's compute budget per request, V8's scheduling behavior, and the container's throughput.

**Reconnection behavior.** If the stream ledger pattern is used, do replayed frames arrive correctly? Does the client handle duplicate frames if the ledger's last-acknowledged index is stale?

**Memory pressure.** Under sustained backpressure, does the Worker stay within its memory budget? Does the container's rate limiting engage before the Worker is evicted?

**Model-specific output.** Does the model produce tokens within the expected size range? Does the response type account for all output cases the model can produce?

Integration tests, load tests, and monitoring provide evidence for these operational policies. They complement declaration-model proofs and the separate evidence that generated code preserves the boundary contract.
