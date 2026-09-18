---
title: "Leveraging Fabulous for Native UI"
linkTitle: "Leveraging Fabulous for Native UI"
description: "Functional components, optional reducers and native reactive areas in Fidelity.UI"
date: 2025-05-20
lastmod: 2026-09-18
authors: ["Houston Haynes"]
tags: ["Design", "Innovation"]
params:
  originally_published: 2025-05-20
  migration_date: 2026-03-12
---

I want building a native interface to feel as approachable as composing a web layout. The F# community has given us several useful ways to do that, and Fabulous remains an important reference for its functional authoring style. In our Fidelity.UI work, we are also drawing on ReactiveElmish.Avalonia, Partas.Solid and Fable.Ripple, particularly where their approaches help a component update independently.

We are designing Fidelity.UI around Clef's intrinsic reactive foundation, with `Incremental<'T>` as the default for derived state and `Observable<'T>` for producer-driven events. The [reactive-area engine]({{< relref "/docs/design/user-interfaces" >}}) would use those dependencies to update native layout and painting. A DOM backend would preserve the component behavior while using browser rendering.

## The functional authoring experience

A component should read as a function of typed properties, actions and children. Functions and lists are the preferred view syntax because they keep small layouts compact and larger compositions familiar. Computation expressions can provide an additional surface where scoped setup or dependent composition becomes clearer. Both forms need the same identity, activation and disposal rules.

A CE does not imply a thread, and a list does not imply a single execution domain. A program can compose several independently owned sections through ordinary functions. The execution contract decides where their work runs.

Fabulous remains useful as an API guide without making source compatibility the acceptance criterion. The criteria are whether applications can express their controls clearly, update the work that depends on a change, and preserve behavior across the admitted targets.

## Domain state

An application can use a pure reducer for domain transitions:

```fsharp
type Model = { Count: int }
type Message = Increment | Reset

let update message model =
    match message with
    | Increment -> { model with Count = model.Count + 1 }
    | Reset -> { model with Count = 0 }
```

This function is independent of any rendering backend. A component can dispatch messages to an owner that applies the reducer, publishes the resulting state and stabilizes the demanded derivations. A small local control can instead use a settable signal directly. These approaches compose: a larger application may use reducers for business rules and local signals for a disclosure panel or an in-progress edit.

We intend a state change to invalidate the relevant bindings or pure visual projections while preserving the component's state and subscriptions. Commands retain their occurrence semantics even when the visible result stays the same. Pressing a button twice can request two operations, whereas deriving the same label twice may allow an equality cutoff.

## Component identity

A mounted component has a logical identity and an owner. Setup establishes its state and resources once for that identity. Incremental updates can recompute derived values, measurement or paint without repeating setup.

Lists make the distinction concrete. A stable item key identifies the component to retain, while an updated item payload supplies its current properties. Reordering a row should preserve its editing state. Replacing a record with the same key should still update its displayed values. Removal retires its owned work according to the lifecycle contract. Keys alone do not solve all three problems.

Our Clef callbacks can capture owned state through ordinary closures. Their environment must remain valid while callbacks can run. We would preserve that environment and its lifetime in the backend's foreign-callback adapter, allowing an application handler to refer directly to its component state.

## Cold construction and readiness

We want an unused view to cost only what is needed to describe it. Activation gives the component an owner and starts its admitted effects. A regular `Incremental<'T>` defers calculation until demand. A cold wrapper can also defer construction of the graph.

`Effect.create` installs an active, always-demanded sink, so a cold component must defer that call until activation.

An application may keep a service-owned projection current while its view is closed. It may also prewarm data or layout under an explicit resource budget. These are readiness policies over the same demand model. A retained cache with no observer can become stale, so retaining a view is not equivalent to keeping it ready.

## Native components, layout and motion

We are studying LVGL's widget composition and its treatment of styles and control states alongside Solid's function components and reactive bindings. For Fidelity.UI, the component needs an owned lifetime that covers both the control's behavior and the resources used to display it.

Our native engine would stabilize the reactive graph before updating layout and paint. Several visual areas could share an execution owner, with lightweight drawing results retained for each. An embedded backend could update bounded tiles or display stripes, while a capable desktop backend could use cached images where they save work.

Transitions fit this model when the target profile admits them. An opacity transition needs owned clock demand, interruption rules and a defined final value. A size transition can trigger layout, so it has a different cost. The intended STM32H7 instrument profile omits decorative motion while retaining essential feedback and DSP smoothing. More capable panel and desktop profiles can admit fades and easing after measuring their rendering path.

## Form behavior and presentation

Forms illustrate the boundary between behavior and presentation. Parsing, validation, dirty state, submission and item identity should survive a change of visual skin. Raw text under edit must remain distinct from the last valid domain value. Native controls and DOM controls then realize that behavior through their own focus, text input and accessibility contracts.

We intend the shared vocabulary to cover controls and their behavior, with explicit extensions for platform-specific facilities. A portable layout would use those controls. A browser-only view could also use markup and CSS directly.

In HelloWayland we can examine native presentation and bounded raster work. WrenHello gives us an embedded frontend with a native host. I would like the next component experiments to preserve an edit while its row moves, replace its presentation without restarting validation, and retire the control while an asynchronous result is pending. Those are useful tests of whether the authoring experience holds together beyond a static layout.
