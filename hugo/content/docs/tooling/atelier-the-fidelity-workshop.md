---
title: "Atelier: The Fidelity Workshop"
linkTitle: "Atelier"
description: "The purpose-built development environment where the framework's design-time surface becomes a place to work"
date: 2026-06-27
authors: ["Houston Haynes"]
tags: ["Tooling", "Design", "Architecture"]
weight: 90
params:
  status: "Preliminary design"
---

{{< callout type="info" >}}
Atelier is in preliminary design. This entry describes the environment we envision and the capabilities it would gather. The compiler-side surface it would present aims to be consistent with the design-time experience the verification and language-server work develops elsewhere in this site.
{{< /callout >}}

Atelier, French for a workshop or *'studio'*, is the development environment we are building for the Fidelity ecosystem. It is a craftsman's workshop, made by the people who build the compiler, holding the specific instruments the work calls for rather than a universal toolkit that covers the general case and misses the framework-specific work.

We support the editors developers already use. [Lattice](/docs/tooling/leveling-up-with-lattice/) brings Clef into VSCode and Neovim through the language-server protocol, taking the lead from the Ionide work that served F# so well, and a great deal of the framework's design-time experience reaches a developer right there. The framework's design-time surface also has parts that a language-server protocol was never shaped to carry: obligations discharged as a program is elaborated and [surfaced at the point of writing](/blog/between-a-rocq-and-a-hard-case/); escape and arena placement shown as navigable annotations; the [Program Semantic Graph](/docs/internals/pipeline/learning-to-walk/) treated as a live object a developer can watch transform through the pipeline. This is the surface the framework describes in [Formal Verification as Compilation Byproduct](/docs/design/categorical-foundations/formal-verification-compilation-byproduct/), and owning the environment lets us present it directly rather than negotiate through a host editor's extension points. Atelier is where that surface becomes a place to work.

## The WREN Stack

Atelier is targeting the [WREN Stack](/blog/wren-stack/), the same desktop pattern the framework offers generally: a reactive frontend in [Partas.Solid](https://github.com/Partas/Partas.Solid) rendered in the system WebView, a native backend compiled by [Composer](https://github.com/FidelityFramework/Composer), and [BAREWire](/blog/getting-the-signal-with-barewire/) as the typed contract between them. The pattern has a published reference in [WRENHello](https://github.com/FidelityFramework/WRENHello), so a developer can read a working example before building on it. The state is in flux, but we're comfortable with the result being lighter than Electron-based clients, taking advantage of web-style layouts we can revise quickly as the design changes.

We chose this on two counts. It holds our tool chain to the computational responsibility we argue for across the Fidelity Framework: an editor whose own resource use answers to the efficiency we ask of the code it compiles. And it makes self-hosting practical for Atelier eventually: the environment used to build Fidelity applications would *itself* be a Fidelity application, built on the architecture it supports. 

## The Framework-Specific Surface

Atelier's reason to exist is the framework-specific surface, the design-time experience the rest of this site develops, gathered into one environment built to hold it:

- **The verification surface at the point of writing.** The local, decidable facts the compiler settles as a program is elaborated: dimensions, lifetimes, escape behavior, and the structural obligations a solver discharges fast enough to show inline. These surface where the code is written, with no separate pass. This is the design-time half of the [decidability sweet spot](/docs/internals/verification/decidability-sweet-spot/).
- **A live Program Semantic Graph.** The graph the compiler [builds and saturates](/docs/internals/pipeline/baker-saturation-engine/), rendered as something a developer can navigate and watch change through the pipeline, rather than an artifact that only the compiler ever sees. Its underlying [hypergraph structure](/docs/internals/pipeline/hyping-hypergraphs/) is a graph in its own right, so direct visualization pays off over a flattened view.
- **Steerable diagnostics for the harder calls.** The pit-of-success surface [arena hoisting](/docs/design/memory/arena-hoisting/) describes, where the compiler does the safe thing on its own and offers the faster one as a visible choice the developer can take.
- **Debugging for the control flow the model is built on.** First-class support for the [delimited continuations](/docs/design/concurrency/delimited-continuations/) the framework's concurrency model rests on, showing captures and resumptions as the non-linear control flow they are, which a single call stack would flatten.

Each of these is the presentation layer for work the compiler already does, and an extension over a host editor can carry a good deal of it, which is why Lattice exists and why we keep investing in it. Atelier makes that analysis legible, in one place, at the instant it matters. What direct control of the environment adds is integration we get to design rather than approximate: the graph viewer, the verification surface, and the debugger sharing one layout and one set of conventions, so the experience feels native to the framework because it was built as one piece. The goal is time to insight, and owning the environment shortens it.

## Toolchain Position

Atelier would be the environment a developer works in. [Lattice](/docs/tooling/leveling-up-with-lattice/) is the language server it would leverage, and Composer is the compiler underneath both. The division is clean: Lattice answers the questions an editor asks of a compiler, Composer does the compilation and the verification, and Atelier is where those instruments come together for the developer. As we extend the toolchain to cover the verification evidence a certification lab re-checks, Atelier is where we intend to present it to the person doing the work.

## The Harder Demands

Two of the framework's harder problems fall to the workshop, and both are why we built a dedicated environment instead of extending a conventional editor.

The first is the [separate proof assistant](/blog/between-a-rocq-and-a-hard-case/). Most verification the framework does stays inside a fast decidable fragment an SMT solver settles at the point of writing, and that is the surface a developer sees every day. A narrower class of guarantee, the relational and probabilistic properties a cryptographer or a control engineer reaches for, sits above that fragment and is discharged against a Rocq-class kernel, brought in only as the domain demands it. The trusted base moves exactly once across the whole scaffold: the solver carries the everyday tiers, and the kernel enters only at the top one. That same kernel is the tool a certification lab would re-run the proof in, so the workshop has two boundaries to present at once: compiler-to-editor for the developer, and compiler-to-auditor for the lab. Atelier would need to make that boundary legible, where the solver's verdict ends and the kernel's warrant begins, without dragging the everyday developer through machinery meant for the specialist who asked for it.

The second is the breadth of what Composer compiles to. Composer carries an MLIR scaffold and the back-ends built on it, and those back-ends target genuinely different silicon. [HelloArty](https://github.com/FidelityFramework/HelloArty) compiles idiomatic Clef through the FPGA pipeline to CIRCT output and synthesizes onto a Digilent Arty A7, with bit widths and machine classification [inferred from the source](/blog/fpga-and-hardware-inference/) rather than hand-declared. [HelloNappy](https://github.com/FidelityFramework/HelloNappy) takes a pure Clef function and lowers it through the AIE back-end onto the AMD XDNA2 NPU on Strix Halo, deriving the tile count from the kernel's own shape. One source language and one inference discipline feed three lowering paths: CIRCT for the fabric, AIE for the NPU, and LLVM for the host that coordinates them, often inside one application joined by a [BAREWire](/blog/getting-the-signal-with-barewire/) contract. A workshop for this framework cannot assume a single target. It has to show a developer how a value is represented on each one, where the [representation a target chooses](/docs/internals/verification/proofs-to-silicon/) differs from another's, and where a [BAREWire boundary](/blog/getting-the-signal-with-barewire/) sits between processors.

The deeper design resides in the [Atelier repository](https://github.com/speakeztechnologies/Atelier), which summarizes how we plan to lay out the initial features and tooling support. 
The argument for why one observation surface can serve the developer, the tooling, and the program alike is made accessibly in [Opining Upon Reflection](/blog/opining-upon-reflection/).
