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

Atelier, French for a workshop or *'studio'*, is the development environment we are building for the Fidelity ecosystem. The name is the thesis: a craftsman's workshop, made by the people who build the compiler, holding the specific instruments the work calls for rather than a universal toolkit that falls short of it's ambitions.

We support the editors developers already use. [Lattice](/docs/tooling/leveling-up-with-lattice/) brings Clef into VSCode and Neovim through the language-server protocol, taking the lead from the Ionide work that served F# so well, and a great deal of the framework's design-time experience reaches a developer right there. The Fidelity Framework's story also has parts that a language-server protocol was never shaped to carry. Obligations discharged as a program is elaborated, [surfaced at the point of writing](/blog/between-a-rocq-and-a-hard-case/). Escape and arena placement shown as navigable annotations. The [Program Semantic Graph](/docs/internals/pipeline/learning-to-walk/) treated as a live object a developer can watch transform through the pipeline. These are the surface the framework already describes in [Formal Verification as Compilation Byproduct](/docs/design/categorical-foundations/formal-verification-compilation-byproduct/), and they are the kind of thing that owning the whole environment lets you present directly rather than negotiate through a host editor's extension points. Atelier is where that surface becomes a place to work.

## The WREN Stack

Atelier is targeting the [WREN Stack](/blog/wren-stack/), the same desktop pattern the framework offers generally: a reactive frontend in [Partas.Solid](https://github.com/Partas/Partas.Solid) rendered in the system WebView, a native backend compiled by [Composer](https://github.com/FidelityFramework/Composer), and [BAREWire](/blog/getting-the-signal-with-barewire/) as the typed contract between them. The pattern has a published reference in [WRENHello](https://github.com/FidelityFramework/WRENHello), so the foundation Atelier stands on is one a developer can already read. The state is in flux, but we're comfortable with this being lighter than Electron-based clients while being able to take advantage of web-style environment layouts that can advance and adapt to changes quickly.

That choice is deliberate on two counts. It keeps our tool chain honest about the computational responsibility our Fidelity Framework argues for, an editor that justifies the resources. And it paves a path for Atelier to eventually be self-hosting: the environment used to build Fidelity applications would *itself* be a Fidelity application, built on the architecture it supports. 

## Beyond the standard editor experience

Atelier's reason to exist is the framework-specific surface, the design-time experience the rest of this site develops, gathered into one environment built to hold it rather than fitted into the spaces a general editor leaves open:

- **The verification surface at the point of writing.** The local, decidable facts the compiler settles as a program is elaborated, dimensions, lifetimes, escape behavior, the structural obligations a solver discharges fast enough to show inline, surfaced where the code is written rather than after a separate pass. This is the design-time half of the [decidability sweet spot](/docs/internals/verification/decidability-sweet-spot/).
- **A live Program Semantic Graph.** The graph the compiler [builds and saturates](/docs/internals/pipeline/baker-saturation-engine/), rendered as something a developer can navigate and watch change through the pipeline, rather than an artifact that only the compiler ever sees. The [hypergraph structure](/docs/internals/pipeline/hyping-hypergraphs/) underneath it is what makes the graph worth visualizing directly.
- **Steerable diagnostics for the harder calls.** The pit-of-success surface [arena hoisting](/docs/design/memory/arena-hoisting/) describes, where the compiler does the safe thing on its own and offers the faster one as a choice the developer can see and choose at their option.
- **Debugging for the control flow the model is built on.** First-class support for the [delimited continuations](/docs/design/concurrency/delimited-continuations/) the framework's concurrency model rests on, showing captures and resumptions as the non-linear control flow they are rather than flattening them onto a single call stack.

Each of these is the presentation layer for work the compiler already does, and an extension over a host editor can carry a good deal of it, which is why Lattice exists and why we keep investing in it. Atelier does not invent the analysis; it makes the analysis legible, in one place, at the instant it matters. What direct control of the environment adds is integration we get to design rather than approximate: the graph viewer, the verification surface, and the debugger sharing one layout and one set of conventions, so the experience feels native to the framework because it was built as one piece. The goal is time to insight, and owning the whole surface is how we shorten it.

## Where it sits in the toolchain

Atelier would be the environment a developer works in; [Lattice](/docs/tooling/leveling-up-with-lattice/) is the language server would leverage, and Composer is the compiler underneath both. The division is clean: Lattice answers the questions an editor asks of a compiler, Composer does the compilation and the verification, and Atelier is the workshop those instruments are laid out in. As the toolchain widens, toward the verification evidence a certification lab re-checks, the workshop is where that surface is meant to come together for the person doing the work.

## Future challenges

Two of the framework's harder problems land squarely on the workshop, and both are reasons the design reaches past a conventional editor rather than settling into one.

The first is the [separate proof assistant](/blog/between-a-rocq-and-a-hard-case/). Most verification the framework does stays inside a fast decidable fragment an SMT solver settles at the point of writing, and that is the surface a developer sees every day. A narrower class of guarantee, the relational and probabilistic properties a cryptographer or a control engineer reaches for, sits above that fragment and is discharged against a Rocq-class kernel, brought in only as the domain demands it. The trusted base moves exactly once across the whole scaffold: the solver carries the everyday tiers, and the kernel enters only at the top one. That same kernel is the tool a certification lab would re-run the proof in, so the boundary the workshop has to present is not just compiler-to-editor but compiler-to-auditor. Atelier would need to make that boundary legible, where the solver's verdict ends and the kernel's warrant begins, without dragging the everyday developer through machinery meant for the specialist who asked for it.

The second is the breadth of what Composer compiles to. Composer carries an MLIR scaffold and the back-ends that ride on it, and those back-ends reach genuinely different silicon. [HelloArty](https://github.com/FidelityFramework/HelloArty) compiles idiomatic Clef through the FPGA pipeline to CIRCT output and synthesizes onto a Digilent Arty A7, with bit widths and machine classification [inferred from the source](/blog/fpga-and-hardware-inference/) rather than hand-declared. [HelloNappy](https://github.com/FidelityFramework/HelloNappy) takes a pure Clef function and lowers it through the AIE back-end onto the AMD XDNA2 NPU on Strix Halo, deriving the tile count from the kernel's own shape. Same source language, same inference discipline, three different lowering paths, CIRCT for the fabric, AIE for the NPU, LLVM for the host that coordinates them, often inside one application joined by a [BAREWire](/blog/getting-the-signal-with-barewire/) contract. A workshop for this framework cannot assume a single target. It has to show a developer where a value lands on each one, where the [representation a target chooses](/docs/internals/verification/proofs-to-silicon/) differs from another's, and where a [BAREWire boundary](/blog/getting-the-signal-with-barewire/) crosses between processors. That is the heterogeneous-toolchain reality the design is meant to make navigable, and it is the part of the picture a curious reader is most likely to underestimate.

The deeper design lives in the [Atelier repository](https://github.com/speakeztechnologies/Atelier), which carries the summary of how we plan to lay out the initial features and tooling support. 