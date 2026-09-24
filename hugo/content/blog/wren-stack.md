---
title: "The WREN Stack"
linkTitle: "WREN Stack"
description: "A system WebView and native application core within Fidelity's shared UI model"
date: 2026-01-06
lastmod: 2026-09-18
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2026-01-06
  migration_date: 2026-03-12
---

Pressing the increment button in our WrenHello application sends a command from the web frontend to native Clef code. The backend changes its counter and returns an event, then the frontend updates the displayed value. We use this small application to develop the boundary between a web interface and a native application core.

WREN stands for **WebView, Reactive, Embedded, Native**. We embed the application's frontend assets and host them in a system WebView, with native code responsible for the workloads and platform operations assigned to it. Within our [shared UI direction]({{< relref "/docs/design/user-interfaces" >}}), WREN would be the DOM/WebView realization alongside the Clef-native reactive-area engine we are designing.

## Our WrenHello Host

The frontend currently uses F#/Partas.Solid through Fable and Solid/Vite. Build scripts embed the resulting HTML into source consumed by the Composer-native host:

```text
F# / Partas.Solid → Fable → JSX → Solid / Vite → embedded HTML
                                                        ↓
                                              Composer-native host
```

The frontend and backend share a command/event vocabulary. Native code owns the counter, while theme state is local to the frontend. Their current bridge exchanges bounded ASCII messages through WebKit script messaging. We would introduce a binary BAREWire transport as a separate integration step.

Our native UI acceptance runner exercises the real button-to-native-to-DOM sequence and malformed messages. It also checks window commands and retirement. Its scope is the experimental single-window Linux host, with general multiwindow behavior requiring a broader protocol and lifetime model.

Our [repository graph](/blog/retrieving-fidelity/#knowledge-representation-again) also makes WrenHello's native dependencies easier to investigate. We can follow project references into the WebKit and GTK bindings, then inspect a binding's generator declaration and likely generation profile with source citations.

For a Composer-produced frontend, we would compile Clef through the portable middle end into JavaScript or a JSX handoff. The existing Partas transformation is a Fable plugin. Implementing the corresponding Clef pathway requires preserving reactive reads and ownership through our compiler. The [JavaScript/JSX toolchain review]({{< relref "/docs/design/javascript-targeting/javascript-jsx-toolchain" >}}) describes those obligations.

## Embedded Assets

WrenHello's scripts join the frontend bundle and native program into an application build. We intend to bring that orchestration into Composer so that the project describes both products and the embedding step.

Deployment still includes the selected WebView libraries or runtime. WebKitGTK, WKWebView and WebView2 are the host integrations we would investigate for Linux, macOS and Windows respectively. Availability and servicing belong in each deployment profile, alongside the application assets.

The WebView executes JavaScript with browser-managed memory and scheduling. Our native core can use Clef's native lifetime model while the renderer retains its own execution costs. We would measure startup and working memory from the packaged application, including its host dependencies.

## The Application Protocol

A frontend command requests a state transition. An event or snapshot reports what the authoritative owner accepted. At that boundary, we need validation and error handling, with ordering and acknowledgments appropriate to the application.

Our BAREWire declarations are a candidate representation layer. Shared source types help keep both sides aligned, while codec and framing tests establish their actual agreement. Host script messages and local WebSockets have different delivery contracts. Shared memory introduces another set of ownership obligations, so we would select transport per host. [Getting the Signal with BAREWire]({{< relref "/blog/getting-the-signal-with-barewire" >}}) develops that part of the design.

For a larger application, we would attach revisions to state updates and provide snapshots for recovery. A browser-side signal would contain a local projection of received state. Current-value telemetry could coalesce, while user commands would retain their occurrence and acknowledgment semantics.

## Portable Components

We want a settings panel or telemetry view to be straightforward to compose through the same functions on native and web targets. Typed properties and child content would define the component interface. Layout and styling would compose around shared control behavior, with DOM-specific document features available through explicit capabilities.

Fades and easing could share declarative intent as well. A WebView might use browser animation facilities, while the native engine would use its own rendering path. We would preserve lifecycle and interruption behavior across both. The browser would continue to perform DOM layout and painting, determining its own compositing boundaries.

Solid is useful prior art for that implementation. We can compare a Solid adapter with direct DOM realization using the same semantic cases. Each needs to preserve batching and effects, along with keyed identity and disposal. We would keep the authoring API independent of that implementation choice.

## Workload and View Lifetimes

Our UI descriptions begin cold. An owner activates the required state and effects, then demanded computations update the relevant outputs. A route change could retire a view or retain it under an explicit policy. A retained hidden view might release visual demand while preserving local editing state.

A service observer could keep telemetry current during that interval. Reopening its inspector would attach observation to current state. We would compare this kept-current policy with prepared views and retained stale caches when measuring switching latency.

Closing a window also has to reject late results and retire callbacks. Workers and host rendering operations can retain access to resources beyond that logical closure, so reuse must wait for completion. Our next shared-component exercise would combine editing and keyed children with close/reopen during delivery, through both native presentation and the bundled WebView.
