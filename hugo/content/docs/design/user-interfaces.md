---
title: "User Interfaces: Reactive Areas and Shared Semantics"
linkTitle: "User Interfaces"
description: "Cold component composition and reactive areas across native displays and WREN"
date: 2026-09-18
weight: 85
params:
  status: "Design"
---

We are designing a Clef-native reactive-area update engine for Fidelity.UI, using our native and WREN experiments to test shared component semantics. The same authoring model should serve embedded displays and native applications, with a DOM/WebView realization for browser-based interfaces. The [Fidelity.UI repository](https://forge.spkez.dev/FidelityFramework/Fidelity.UI) contains the framework work and its architectural review.

We prefer ordinary functions and list composition with modifiers for view syntax. Computation expressions could offer another surface over the same operations where scoped resources or dependent computations become clearer. Either surface can describe concurrent sections because execution ownership is a separate choice. Our [UI model]({{< relref "/blog/fidelity-ui-model" >}}) draws on Fabulous and ReactiveElmish.Avalonia alongside Partas.Solid and Fable.Ripple.

Native components should be as approachable as web components. Named functions would accept typed properties and actions, with child descriptions arranged through layout combinators. LVGL's [widget composition](https://lvgl.io/docs/open/9.4/details/common-widget-features/tree.html) is a reference for controls and their parts, including styles and events. [Solid components](https://docs.solidjs.com/concepts/components/basics) offer a complementary model of reusable functions and selective bindings. We would adapt those component ideas to our own cold activation and ownership contract.

## Cold construction and demand

A UI description should remain cold until an owned mount or explicit application demand activates it. The owner then determines which incremental work is demanded and when to retire its resources. Our approach draws on Jimmy Byrd's IcedTasks, acknowledged in the [concurrency lineage]({{< relref "/blog/dotnet-to-fidelity-concurrency" >}}), and on Braid's explicit [continuation and reduction structure]({{< relref "/docs/design/concurrency/dcont-inet-duality" >}}).

Our specification distinguishes deferred computation from deferred graph construction. An ordinary `Incremental<'T>` defers its computation until [demanded](/spec/draft/incremental-computation/#63-demand-registration). A [cold subgraph](/spec/draft/incremental-computation/#incremental--cold), `Cold<Incremental<'T>>`, additionally defers construction and dependency registration until forced. An unobserved stale node may retain its cached state without recomputing. The [lazy-value specification](/spec/draft/lazy-representation/) describes deferred evaluation with one-time caching.

Mounted setup would run on activation. That includes application effects and subscriptions, along with timers and backend work. Mounted state belongs to a stable identity, while leaf bindings or pure area computations update demanded results. Under the specified `Effect.create` semantics, creating an effect registers an always-demanded sink and runs it initially. A cold UI description must therefore defer that call until owner activation. A hidden area may retain its state after withdrawing demand, with final retirement deferred until outstanding resource use permits release.

The application determines demand independently of visibility. On-demand work would be the default. An application could also keep a background projection current through a service-owned observer: a time-series projection could continue updating while its chart is closed, so reopening starts from current data. The service observer has its own lifetime and can remain active after the visual observer closes.

Scoped prewarming would let the application request data preparation before a view becomes visible. Pure layout or paint preparation could also run when the constraints and resources are known. A retained cache with no active demand instead preserves results that may become stale. Keeping data current and preparing its presentation have separate costs: DOM nodes and native layout still require preparation, as do text resources and presentation buffers. The application must admit the desired work and refresh results when their constraints change.

Cold execution puts first-use latency in the resource budget alongside ongoing work and retained memory. Controlled hardware and unikernel deployments can make that budget easier to characterize. Deadline guarantees still require bounds on execution and resource contention under the intended load. An eager policy may trade memory and background computation for lower switching latency, with the benefit measured on the selected target.

## The native core

A reactive area is a mounted visual subtree with a stable identity and disposal scope. Its retained results depend on reactive inputs and layout constraints, including those used for painting. The engine would invalidate work that depends on a changed value and reuse unaffected results. Framebuffer placement and execution ownership are separate choices: several areas could share a render target and one execution owner.

The engine needs a stabilization contract for reactive state, followed by contracts for layout and rendering. Layout includes measurement and arrangement. Rendering includes paint preparation and rasterization, followed by composition and presentation. A text or font change can affect layout, while a color change may affect only paint. A transform may reuse cached content when the backend supports that operation. Parent constraints and child measurements remain dependencies across execution owners, as described in our [layout design]({{< relref "/blog/window-layout-with-fidelity" >}}).

Moving or removing content can damage both its old and new visual bounds. The renderer must account for clipping and overlap, including transparency and effects. A bounded fallback can redraw a larger region when precise damage would cost too much. Embedded renderers could retain tiles or partial display stripes instead of a full-size image per area. We are also studying LVGL's rendering and resource-management techniques for the Clef-native engine.

## Shared components and motion profiles

The common component vocabulary should include fades and eased state transitions alongside layout and styling. A cold transition would start its timer on activation, when its owner admits clock or frame demand. The transition needs defined behavior for retargeting and cancellation, including completion and suspension. State remains authoritative while the presentation interpolates toward it. Retirement stops transition demand, with buffer release subject to outstanding renderer use. LVGL's [animations](https://lvgl.io/docs/open/9.4/details/main-modules/animation.html) and [style transitions](https://lvgl.io/docs/open/9.4/details/common-widget-features/styles/transitions.html) are references for this authoring model.

Our intended STM32H7 instrument profile omits decorative motion as a product choice. A Sweet Potato panel with the intended Waveshare 7-inch ultrawide touchscreen is a candidate for selected fades and eased transitions. GPU desktops and capable WREN hosts could admit richer motion through the same component contract. Disabling decorative motion must preserve the final state and essential feedback. Acceptance of the richer board profile requires graphics measurements and display/input tests for the chosen deployment, beyond its video-decoding and HDMI capabilities.

Motion cost depends on the property. Changing size may require layout, while color may require repainting. Opacity and transforms can reuse retained content where the renderer supports the required group composition. Texture placement must respect layer-memory and queued-frame budgets. The browser could use its animation facilities when lifecycle and interruption semantics match. Focus and hit testing require explicit behavior during a fade, with disposal governed by ownership.

## Ownership and execution

We would begin with an owner-local incremental graph driving several areas, comparing leaf property bindings with pure area recomputation and hybrids. An owned mount would acquire state and resources once per identity. Demanded updates could then recompute pure visual results while preserving mounted setup. Pure preparation or raster work may run on workers when input and output ownership permit it. Larger sections may have independent owners and communicate through bounded, versioned projections, as discussed in our [concurrency design]({{< relref "/blog/scaling-fidelityui" >}}). Visual areas and memory regions have distinct roles, as do execution domains and OS presentation surfaces.

Signals describe current values and dependencies. Messages describe occurrences and delivery. Local stabilization must validate every changed input to a shared dependent, while message handling must preserve required actions. Coalescing duplicate commands, for example, could lose a user's repeated clicks. Cross-process and network projections need explicit snapshot and ordering protocols, including reconnect and failure policies.

Dependency analysis must follow reactive reads and effects, including helper calls and dynamic branches. Lexical capture describes a closure's environment, while actual reads establish its active dependencies. Our native reactive callbacks can therefore be closures with owned state. Function pointers serve the foreign callback boundary. The [Reactive Signals specification](/spec/draft/reactive-signals/) defines this surface over the intrinsic computation model.

Mounted ownership must cover subscriptions and callbacks, including asynchronous work. Retirement invalidates late results. Buffer reuse must wait for workers and external consumers to release their references. A computation may stop before its workers have joined or the compositor has released its buffer. GPU or display-flush completion adds another resource-specific boundary. Closing an inspector would normally retire its visual observer while a separate service owner continued demanding the shared projection.

## WREN and portable behavior

Our WREN experiments use a DOM/WebView frontend with a Composer-native host. In WrenHello, Fable compiles F#/Partas.Solid source for the Solid/Vite frontend build. The host and frontend exchange bounded ASCII commands and events through WebKit script messages. We intend to add BAREWire/WebSocket integration and a Composer Clef-to-JSX producer as described in the [JavaScript/JSX toolchain review]({{< relref "/docs/design/javascript-targeting/javascript-jsx-toolchain" >}}).

The browser would own DOM layout and painting under the shared binding and lifetime contracts. Its layer allocation can differ from native area boundaries. Our portable vocabulary should cover controls and their behavior, from text and activation through focus and accessibility. Layout constraints belong in that shared vocabulary. HTML/CSS-specific features would remain target capabilities rather than acquire an automatic native translation.

## Form behavior and replaceable presentation

`Fable.Ripple.Form.Plain` separates reusable form behavior from replaceable field and list renderers. We intend a similar separation for Fidelity.UI: typed parsing and validation would be reusable, along with editing and submission behavior. Native reactive areas and DOM controls would implement the same form semantics through their respective input and rendering contracts. Focus and error exposure would follow the same owned lifecycle.

Ripple's form item and render-context types return `DomItem`. Its dynamic composition uses `Html.dynamic`, and asynchronous validation uses browser promises and timers. Our portable form contract must define corresponding operations for each target. Subscriptions and validation work would begin at owned activation, including any required timers. A designer schema could later describe forms built from this behavior and presentation interface.

## Acceptance profiles

HelloWayland exercises native presentation and bounded parallel rasterization. WrenHello exercises an embedded frontend/native-host integration. We would use those experiments to develop acceptance cases for the area engine, then extend them to multiwindow behavior and additional targets.

The first native cases should compare partial redraw with full redraw as areas change independently. They need text whose size affects layout, as well as movement and removal under overlapping content.

Activation cases must show that unused descriptions start no mounted work. Application demand must control recomputation independently of visibility, and retirement must prevent late results from reviving an owner. First-use measurements should compare kept-current projections with scoped prewarming and stale retained caches. Closing a visual observer must preserve separately owned service demand.

Form cases should reuse behavior through different presentations while keeping validation and subscription ownership singular. Later cases should cover worker completion during resize or close, then run the same authored components through WREN. Embedded execution needs bounded resource measurements. Text entry and accessibility require platform acceptance, followed by distributed projection tests.

Record allocation and retained memory alongside layout and paint work. Measure event-to-presentation latency under the intended load. Those results would determine where compiler specialization reduces cost within each target profile.
