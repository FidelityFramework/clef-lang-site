---
title: "Where Native Goes, Mobile Follows"
linkTitle: "Where Native Goes, Mobile Follows"
description: "Extending Fidelity's shared component and native compilation model into mobile platform integration"
date: 2026-05-14
lastmod: 2026-09-18
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
---

A technician checking a machine on a tablet needs the same readings and control rules as the operator at its fixed panel. The tablet adds a different set of interruptions: navigation away from the screen, an on-screen keyboard, suspension while another application is active. We want to share the application behavior while giving each of those interactions a proper platform implementation.

Our Fidelity framework is designed for that reach. We are developing a common component model over native compilation, with a Clef-native reactive-area engine and a DOM/WebView realization through WREN. A portable control would retain its identity and behavior across both, while the selected host would provide presentation and input. Our [UI design]({{< relref "/docs/design/user-interfaces" >}}) includes mobile in those semantics from the outset.

## Shared Application Code

We would share state transitions and validation between the tablet and panel. Their communication could use the same command/event types, with local incremental projections for each interface. An ordinary component function would accept typed properties and child content, then arrange controls through the common layout vocabulary.

Our `Signal`, `Memo` and `Effect` surface is defined over Clef's `Observable` and `Incremental` intrinsics. For UI work, we are designing reactive areas with stable visual identity and retained results. Layout and painting dependencies would allow an update to preserve control state while recomputing the affected output. The [reactive account]({{< relref "/blog/native-reactivity-in-clef" >}}) and [component model]({{< relref "/blog/fidelity-ui-model" >}}) describe that foundation.

A form's validation and submission rules can remain common while its text entry depends on the host. We would expose platform capabilities for document-specific features or device integration. A supported portable control would have to preserve its editing and accessibility behavior through the chosen realization.

## Platform Integration

We select the compiler target together with its application host, including presentation and lifecycle integration.

| Target family | Candidate realization | Integration focus |
|---|---|---|
| Linux desktop and mobile | Native areas through Wayland, or an appropriate WebView host | Surface lifecycle, scaling and input |
| Windows desktop | Selected native rendering stack or WebView2 | Window ownership and runtime deployment |
| Apple desktop and mobile | AppKit/UIKit integration or applicable WebView hosting | Lifecycle and platform controls |
| Android | Platform host for native rendering or WebView presentation | Surface recreation and application lifecycle |
| TV and kiosk | Selected vendor host, such as Tizen, or Linux | Remote/touch navigation and recovery |
| MCU and instruments | Bounded native controls with partial display updates | Display completion and device workload budgets |

We use module signatures to describe the common interface and check conformance of a selected implementation. Behavioral parity then needs execution tests. Through platform descriptors and generated bindings, our compiler would retain the target facts needed for linking and code generation.

Android and Apple mobile hosting also require application entry and lifecycle delivery. Native-library linkage, signing and installation must agree with the selected SDK and toolchain. We would develop those integrations alongside the renderer so that the first device application includes navigation and recovery.

## Our Experimental Hosts

With HelloWayland we have a native presentation experiment and bounded parallel CPU raster work. Its separate worker-completion and compositor-release boundaries are useful for the area renderer we are designing. Text editing and general layout would extend that experiment into interactive controls.

Our WrenHello application embeds a frontend and exchanges commands with a Composer-native host on Linux. Its frontend uses F#/Partas.Solid through Fable and Solid/Vite. The current WebKit bridge carries bounded ASCII messages. We would develop Composer-produced frontend code and a BAREWire transport as separate integration steps, as described in [The WREN Stack]({{< relref "/blog/wren-stack" >}}).

At the embedded end, HelloDISCO has an accepted M7 banner with joystick/LED behavior and palette changes. We intend its future synthesizer to exercise continuous audio demand alongside controls and painting. Audio and autonomous M4 operation would each need their own hardware acceptance before we combine them with the UI workload.

## Demand and Mobile Lifecycle

Our view descriptions begin cold. Mounting or explicitly preparing a view would activate an owner and the required demand. During surface loss, an application might retain editing state while releasing presentation resources, then recreate those resources when the host permits it.

A service observer could keep a projection current while its visual observer is absent. On mobile, that request is subject to the platform's execution allowance. We would preserve the distinction between closing a panel and stopping a service, with an application recovery policy for suspension or process termination.

Native resource retirement must account for workers and external consumers. In a WebView, an owner would still unsubscribe and cancel pending work alongside garbage-collected storage. Before publishing a delayed result, the receiver must check its owner and revision. Selection and draft edits also need deliberate preservation across navigation and surface recreation.

## Product Profiles

We intend the common component vocabulary to include fades and easing. The STM32H7 instrument would omit decorative motion, while richer devices could enable it within measured budgets. Live measurements and DSP control smoothing have separate behavioral purposes and remain active according to application demand.

For GPU-assisted composition, we need an implemented rendering and synchronization path on the selected host. Layer memory and uploads contribute to its cost, alongside text resources and frame pacing. We would measure those costs in the device application rather than infer them from the processor specification.

Our first mobile component exercise would include an editable field and a keyed list. We would check text composition and focus with touch and keyboard input. Accessible naming and navigation would follow. Suspending the application during a service request would test recovery and delayed-result handling. We would run the same semantic cases through native and DOM hosts, recording retained memory and event-to-presentation latency on the selected devices.
