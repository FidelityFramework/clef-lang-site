---
title: "Streaming Inference Through the Actor Pipeline"
linkTitle: "Streaming Inference"
description: "How BAREWire's Frame Format and Tell-First Semantics Enable Token-Level Streaming from Container Inference to Edge Delivery"
date: 2026-04-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
---

## The Streaming Problem in Inference

Autoregressive models produce output one token at a time. A BitNet ADM running inside a container generates tokens sequentially, each conditioned on the preceding sequence. The full response may be hundreds of tokens. The client should not wait for the last token before seeing the first. The [unified actor architecture](/blog/unified-actor-architecture/) establishes how Prospero supervisors and Olivier workers communicate over BAREWire. This article examines what happens when that communication is a stream of inference tokens rather than a single request/response pair.

This is not a novel observation. Every LLM deployment solves it. The standard approach is Server-Sent Events with JSON payloads:

```
data: {"token": " the", "index": 42, "finish_reason": null}\n\n
data: {"token": " answer", "index": 43, "finish_reason": null}\n\n
data: {"token": "", "index": 44, "finish_reason": "stop"}\n\n
```

Every frame carries field names as strings, redundantly, because JSON is self-describing. The receiver parses each line, finds the `data:` prefix, parses the JSON, looks up `"token"` by string key, checks `"finish_reason"` by string key. The schema is rediscovered from every message.

The question is whether the Fidelity framework's compilation pipeline offers something structurally better. It does, and the mechanism is BAREWire.

## BAREWire Frames for Token Streaming

The inference response is a discriminated union:

```fsharp
type InferenceResponse =
    | Token of text: string * index: int     // tag 0
    | StreamEnd of totalTokens: int          // tag 1
    | StreamError of message: string         // tag 2
```

The Clef compiler verifies this type at design time. The BAREWire schema is derived from it: three cases, three tags, each with a fixed payload layout. Each token becomes a single frame:

```
┌────────────┬────────┬───────────────┬──────────────────────────┐
│ Header     │ Tag: 0 │ Corr: 7      │ Payload: " the", 42     │
│ (4 bytes)  │ (1 B)  │ (4 bytes)    │ (len-prefix str + varint)│
└────────────┴────────┴───────────────┴──────────────────────────┘
```

A typical token frame is roughly 18 bytes versus 60+ for the SSE/JSON equivalent. No field names. No string key lookup. No JSON parsing. The receiver reads the tag, knows it is a `Token`, reads the string length, reads the string, reads the varint index. The stream ends when a frame with tag 1 (`StreamEnd`) arrives. An error is tag 2.

The correlation ID ties the entire stream to the original request. The client sent a request with correlation ID 7. Every response frame carries correlation ID 7. The client's WebSocket handler matches on correlation ID and routes all frames to the same response handler. Multiple concurrent inference requests multiplex over the same WebSocket, each with a different correlation ID, each accumulating its own token stream independently.

## Three Substrates, One Frame Format

The [spatial mechanics](/docs/design/memory/spatial-mechanics/) article describes three streaming models: materialized, demand-driven, and spatial dataflow. Each applies to a different segment of the inference streaming path.

### Inside the Container: Spatial

The inference pipeline is spatial. The BitNet model processes through layers as a spatial dataflow pipeline. Data flows through processing stages with bounded buffering. On native hardware with SIMD, this is the materialized/spatial hybrid described in the spatial mechanics article: fixed-size tensors flowing through vectorized stages. The container runs native code compiled through LLVM. Dimensional types governed the range analysis. Representation selection chose the numeric format. The coeffect system verified the memory lifetimes. All of this happened at compile time.

Each generated token exits the inference pipeline as a value with a known type. The container's BAREWire serializer encodes it as a frame and sends it over the container binding.

### Container to Worker: Demand-Driven

Each generated token is a BAREWire frame. This is demand-driven in the spatial mechanics terminology: the container produces output incrementally, and each increment becomes a self-contained frame. The frame header gives the length. No accumulation is required. No schema negotiation occurs at the transport boundary.

The container does not wait for acknowledgment between tokens. It generates, serializes, sends. Generates, serializes, sends. This is the tell-first architecture described in the [BAREWire signal article](/blog/getting-the-signal-with-barewire/). The Worker receives each frame as it arrives.

### Worker to Client: Protocol Relay

The Worker receives BAREWire frames from the container and forwards them over WebSocket. The Worker is a protocol adapter. It does not buffer the full response. Each BAREWire frame from the container becomes a WebSocket frame to the client. The streaming is transparent: the Worker relays frames as they arrive.

The Worker's JavaScript is compiled through JSIR as described in the [JSIR article](../jsir-javascript-as-mlir-backend/). The BAREWire deserializer and WebSocket serializer both derived from the same MLIR ops in the shared middle-end. The Worker does not inspect the payload. It reads the frame header, determines the length, and forwards.

## Contrast with Conventional Streaming

The difference is not just wire efficiency. It is structural.

In the SSE/JSON pattern, the client must parse each message to determine its type. The string `"finish_reason"` appears in every message. The receiver must check whether its value is `null` or `"stop"` by string comparison. If the API adds a new field, every consumer must be updated to handle JSON objects with an unexpected key. If a field is renamed, the consumer silently gets `undefined` and fails downstream.

In the BAREWire pattern, the discriminated union is the schema. Adding a new case (`| StreamMetrics of latency: float<ms>`, tag 3) produces a new tag. Existing clients that do not know tag 3 reject it at the frame level before any handler code executes. Renaming a field changes nothing on the wire because fields are positional, not named. Removing a case changes the tag mapping, which changes the schema, which breaks the schema identity check. Every structural change is visible at the type level, verified at compile time, and enforced at the wire level.

The schema identity proxy described in the [JSIR article](../jsir-javascript-as-mlir-backend/) holds for every frame in the stream, not just the first one. A stream of 500 token frames is 500 independently typed, independently parseable messages, each carrying the same schema guarantee.

## Multiplexing and Correlation

A single WebSocket connection between a client and a Worker can carry multiple concurrent inference streams. The correlation ID is the multiplexing key. Request 7 produces token frames with correlation ID 7. Request 12 produces token frames with correlation ID 12. Both flow over the same WebSocket. The client demultiplexes on correlation ID and routes each stream to its own handler.

This matters for interactive applications where a user might issue multiple queries in rapid succession, or where a supervisor issues parallel inference requests to different ADM instances. The WebSocket is a single connection. The BAREWire frames are independent messages on that connection. No stream interferes with another.

With MoQ/QUIC in the future, each inference stream could become a separate QUIC stream with independent ordering. No head-of-line blocking from other traffic on the connection. The frame format does not change. The transport changes. The BAREWire frames are transport-agnostic by design.

## Design-Time Spec for Runtime Reliability

The entire path from inference output to client display is specified at design time and enforced at runtime without runtime validation code.

**Dimensional consistency**: the inference model's output types were verified at compile time, as described in the [decidability sweet spot](/docs/internals/verification/decidability-sweet-spot/). The DTS rejected every program that could produce dimensionally inconsistent results. The runtime does not check dimensions.

**BAREWire schema**: derived at design time from the verified `InferenceResponse` type definition. Enforced at runtime by byte layout, not by validation code. The receiver does not inspect field names. It reads positions determined by the compiled schema.

**Frame structure**: fixed at design time by the type definition. Self-enforcing at runtime because the tag-to-layout mapping is compiled into both the serializer and the deserializer.

**Cross-substrate compatibility**: guaranteed at design time by shared MLIR ops, following the pipeline unification described in [the JSIR article](../jsir-javascript-as-mlir-backend/). The container's native serializer and the Worker's JavaScript deserializer both derived from the same BAREWire dialect ops. Byte-identical output is a structural property of the pipeline, not a property verified by testing.

**Representation selection**: decided at design time from dimensional range analysis, as detailed in the [posit arithmetic design](/docs/design/categorical-foundations/posit-arithmetic-dimensional-type-systems/). On the native container, the compiler selected the optimal numeric format. On the JavaScript Worker, the representation is IEEE 754 float64. The [JSIR article](../jsir-javascript-as-mlir-backend/) describes the representation fidelity diagnostic that surfaces this divergence at design time.

The runtime does not check dimensions. It does not validate schemas. It does not negotiate representations. It does not inspect frame structure. It runs the code the compiler emitted, which is correct because the compiler verified the specification that the code implements.

The DTS paper states the principle formally: "Decidability is not a property the framework discovers; it is a property the framework enforces through the structure of its type language." The streaming inference pipeline is the distributed systems application of that principle. The compiler enforces structure. The runtime inherits reliability. The wire format bridges the gap where the compiler's jurisdiction ends and the runtime's begins.

## Where This Architecture Has Limits

The design-time guarantees cover type safety, schema identity, and dimensional consistency of the streaming path. They do not cover the operational concerns that arise from deploying streams across distributed infrastructure.

### Backpressure

Tell-first semantics mean the container does not wait for acknowledgment between tokens. If the container produces tokens faster than the Worker can relay or the client can consume, frames accumulate in the WebSocket send buffer. TCP flow control provides coarse-grained backpressure at the transport level, but it operates on byte volume, not on frame boundaries. A slow client causes the Worker's outbound buffer to grow until the runtime intervenes.

Cloudflare Workers have memory limits per isolate. A sustained backpressure situation can cause the Worker to exceed its memory budget and be evicted. The BAREWire frame format does not address this. Backpressure is a runtime concern that the compiler cannot verify. The practical mitigation is rate limiting at the container: the container tracks how many unacknowledged frames are in flight and pauses generation when the count exceeds a threshold. This requires a feedback channel from the Worker to the container, which inverts the tell-first model for flow control while preserving it for data delivery.

### Ordering and Reconnection

TCP guarantees in-order delivery on a single connection. A stream of token frames arrives in the order the container sent them as long as the WebSocket connection remains open. If the connection drops mid-stream, the ordering guarantee disappears. The client reconnects and receives a new WebSocket connection. Frames sent before the disconnect are lost unless the Worker buffered them.

Durable Objects have persistent state. A Worker could maintain a stream ledger: correlation ID, last acknowledged frame index, buffered frames pending delivery. On reconnection, the client sends its last received frame index, and the Worker replays from the ledger. This adds complexity. The BAREWire frame format supports it (the correlation ID and index field in each `Token` frame provide the necessary bookkeeping), but the replay logic is application code, not a property the compiler can verify.

For applications where dropped tokens are acceptable (interactive chat where the user can re-query), the simpler approach is to abandon the stream on disconnect and let the client issue a new request. For applications where every token matters (clinical decision support, financial computation), the stream ledger pattern is necessary. The choice is an architectural decision that belongs to the developer.

### Cold Start Latency

The streaming architecture assumes the container is already running when the first token request arrives. In practice, the first request to a cold container incurs startup latency: container image pull, runtime initialization, model loading. For a BitNet model, the model loading step may take seconds. The client experiences a long delay before the first token, followed by rapid streaming once the model is warm.

This is not specific to BAREWire or the Fidelity framework. Every containerized inference deployment faces the same cold start problem. The mitigations are standard: pre-warming containers, keeping a minimum replica count, using smaller model formats that load quickly. The BAREWire frame format does not affect cold start latency. It affects what happens after the first token is generated.

### Payload Boundaries

A typical token frame is 18 bytes. Cloudflare Workers support WebSocket messages up to 1 MB. For token streaming, the frame size is never a concern. For other inference outputs, the frame size may matter.

An embedding vector (1024 float64 values) is 8 KB per frame. A batch of embeddings could approach the WebSocket message limit. A `StreamError` frame with a detailed diagnostic message is unlikely to exceed the limit but is unbounded in principle. The BAREWire frame header encodes the length, so the receiver always knows the frame size before reading the payload. But if a single frame exceeds the WebSocket message limit, the Worker must fragment it, which breaks the one-frame-per-WebSocket-message assumption.

The practical response is to keep inference outputs within the frame size budget. For autoregressive token streaming, this is naturally satisfied. For bulk outputs (embeddings, logits, attention maps), the discriminated union should define a chunked variant that splits the output across multiple frames, each within the size limit. The compiler cannot enforce this because the payload size depends on the model's output, which is a runtime value. The developer must design the discriminated union with the transport constraints in mind.

### Non-Autoregressive Models

The streaming architecture is motivated by autoregressive generation: each token is produced sequentially and can be delivered as it is generated. Not all inference models produce output this way.

A classification model produces a single output. An embedding model produces a single vector. A regression model produces a single value. For these models, the BAREWire frame format still works. The response is a single frame rather than a stream. The correlation ID still ties request to response. The schema identity still holds. But the streaming infrastructure (the token-by-token relay through the Worker, the client-side accumulation, the `StreamEnd` sentinel) is unnecessary overhead. A single request/response frame pair is simpler and more appropriate.

The type definition handles both patterns naturally:

```fsharp
type InferenceResponse =
    | Token of text: string * index: int         // autoregressive streaming
    | StreamEnd of totalTokens: int              // stream termination
    | StreamError of message: string             // error in either mode
    | Classification of label: string * confidence: float   // single response
    | Embedding of values: float array           // single response
```

The compiler verifies all cases. The BAREWire schema covers all tags. The Worker relays whatever frames arrive. The distinction between streaming and single-response is a property of which tags the container actually sends, not a property of the frame format or the transport layer.

### What Testing Must Cover

The design-time guarantees establish that every frame is typed, every schema is derived from verified types, and every cross-substrate serialization is structurally consistent. Testing must cover everything else:

**Latency under load.** How many concurrent inference streams can a single Worker relay before response times degrade? This depends on the Worker's compute budget per request, V8's scheduling behavior, and the container's throughput.

**Reconnection behavior.** If the stream ledger pattern is used, do replayed frames arrive correctly? Does the client handle duplicate frames if the ledger's last-acknowledged index is stale?

**Memory pressure.** Under sustained backpressure, does the Worker stay within its memory budget? Does the container's rate limiting engage before the Worker is evicted?

**Model-specific output.** Does the model produce tokens within the expected size range? Does the response type cover all output cases the model can produce?

These are the concerns that the compiler's type system cannot express. They require integration testing, load testing, and operational monitoring. The compiler removed the structural failure modes. Testing validates the operational characteristics that remain.
