---
title: "A Window Layout System for Fidelity"
linkTitle: "Window Layout System"
description: "Incremental measurement, arrangement and damage in the native reactive-area engine"
date: 2025-02-02
lastmod: 2026-09-18
authors: ["Houston Haynes"]
tags: ["Design"]
params:
  originally_published: 2025-02-02
  migration_date: 2026-03-12
---

A status label gets longer, the row beside it narrows, and the chart below may need to move. That small change is a useful test for our native UI model. We want Fidelity.UI to update the affected layout while retaining the work that is still valid.

We are designing a [reactive-area engine]({{< relref "/docs/design/user-interfaces" >}}) beneath ordinary functional composition. Developers would arrange reusable controls in rows or grids, and the engine would track which measurements and drawing results depend on their inputs. For native targets we would own that rendering work. A DOM backend would preserve the component semantics and use browser layout.

## UI structure

A UI has several related structures:

| Structure | What it describes |
|---|---|
| Component tree | Controls, children, identity and semantic relationships |
| Reactive graph | Values read by derived calculations and bindings |
| Spatial layout and damage | Bounds, overlap, clipping and pixels affected by a change |
| Execution ownership | Where mutation, work admission and resource retirement occur |

A panel can contain several reactive areas under one owner. That owner may send drawing preparation to a worker while retaining control of the component and platform window.

We would make execution placement an ownership decision, available to components written through either functions and lists or a computation expression.

## Measurement and arrangement

Measurement computes a desired size under constraints. Arrangement assigns the actual bounds. Text metrics, font selection, wrapping, child measurements, padding and available space all contribute dependencies.

Consider a dashboard with a title, a chart and a status indicator. A color change to the status indicator normally affects paint. A longer status label may change its measurement and the surrounding row's arrangement. Resizing the window can invalidate the chart's geometry even when its data has not changed. The graph must represent those distinctions so an apparently local input cannot leave parent layout stale.

`Incremental<'T>` provides the demand, caching and cutoff vocabulary. The engine can keep a measurement result while its inputs remain valid and defer unused derivations. A cutoff can stop propagation from an unchanged result, while other changed inputs still require their dependents to be checked.

We would run component setup once for its mounted identity. Repeated measurement would use the resulting state while subscriptions and data feeds retain their established lifetimes.

## Paint damage

After stabilization and layout, the renderer determines which pixels may have changed. Movement damages the old bounds as well as the new bounds. Removal exposes content behind the removed object. Clipping, transparent overlap, shadows and effects can expand the region that needs repainting.

Our renderer would combine these spatial consequences before choosing a redraw region. Where a smaller region cannot be justified, it would redraw a larger one. We can compare both against a forced full redraw to check the pixels.

For a small display, we might retain layout records and prepared drawing commands. A desktop backend might also retain text resources or cached images. We would choose that storage against the target's memory budget and the cost of regenerating the content.

## Cold construction and hidden areas

Our cold descriptions defer application work until activation establishes an owner and demand. A mounted visual observer would request the calculations needed for presentation. Once that demand ends, the graph could retain cached values and leave stale results unevaluated.

Applications can deliberately choose more readiness. A chart service may keep its data projection current while the chart is hidden. A prewarm scope may prepare layout when the destination size and font resources are known. A later size or font change invalidates that preparation. Preparing visual resources requires additional work beyond keeping the data current.

Reattaching a prepared component should reuse its logical instance where the ownership contract permits. Its subscriptions would then continue under the existing owner rather than being installed a second time.

## Motion dependencies

We want fades and eased transitions in the component vocabulary. Application state specifies the target, while presentation state interpolates toward it using a monotonic clock observed for the transition's lifetime.

Animating a width can require measurement and arrangement on each update. Animating a color can require painting. A transform or opacity change may reuse retained content on a backend with suitable composition support. The cost follows the property and backend capability.

A transition needs a defined response when its target changes or its owner suspends. Disabling decorative motion should reach the final state and preserve essential feedback. During an exit transition, the application also needs to specify when the control loses focus and stops accepting input.

The STM32H7 instrument profile omits decorative motion. A richer panel or GPU desktop can admit it within measured frame, memory and power budgets. These are profiles of the same component model.

## Rendering workers and presentation

An owner can send immutable inputs and an exclusively owned output region to a worker for suitable preparation or raster work. Completion returns a result with the input revision and owner generation it belongs to. A resized or retired area can reject an obsolete result.

After rejecting an obsolete result, the backend must still retain its storage while another participant uses it. A worker may be writing a buffer, or a compositor may be scanning one out. Each participant has its own completion or release event.

Platform event-thread requirements also remain part of the backend contract. Native compilation does not permit arbitrary threads to mutate a window or DOM. The UI owner admits changes and coordinates external work according to the platform's rules.

## Redraw checks

We would compare partial updates against forced full redraw, beginning with independent area changes and text reflow. Movement and removal add the exposed-background case. Clipping and transparent overlap add interactions between areas. Resize and close also need tests with work still in flight.

Alongside the pixel comparison, we would record which nodes and measurements were invalidated. Paint time and damaged area would show the redraw cost, with retained memory and event-to-presentation latency measured under the same workload. The corresponding DOM tests would check component identity and lifecycle while the browser performs layout and painting.

See also [Building User Interfaces]({{< ref "fidelity-ui-model" >}}), [Scaling Fidelity.UI]({{< ref "scaling-fidelityui" >}}) and [The WREN Stack]({{< ref "wren-stack" >}}).
