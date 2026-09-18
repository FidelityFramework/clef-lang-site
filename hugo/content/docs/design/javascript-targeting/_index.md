---
title: JavaScript Targeting
weight: 80
---

JavaScript connects native applications, browser interfaces and Cloudflare's edge. FSharp.CloudEdge supplies F# bindings and management tooling; Conclave is the platform for intelligent distributed systems on Cloudflare. BAREWire is the glue for memory layout, IPC and network contracts, with type and representation metadata available during reasoning and final payloads untagged with respect to that metadata.

The F#/Fable path works today. Composer's proposed JSHIR/JSIR backend would carry more of the Clef compiler's reasoning toward JavaScript output. These pages explore that opportunity, distinguish implemented checks from intended guarantees, and give an [incremental acceptance sequence](jsir-javascript-as-mlir-backend/#from-contract-to-emitted-artifact) for connecting a contract to the operations and artifacts that implement it.

[JavaScript, JSX and the WREN Toolchain](javascript-jsx-toolchain/) separates Clef semantic lowering, JSIR’s proposed JSX bridge, Babel’s printing, Solid’s framework transform and Vite’s bundling. WrenHello supplies the existing downstream path; a Clef JSX producer remains proposed work. JSX is a structured UI handoff alongside ordinary JavaScript targeting.

[Proof Preservation Across Actors and Workflows](proof-preservation-across-actors-and-workflows/) connects boundary checks to continuation validity, numerical joins, retry handling, and durable reconstruction. [Carrying Proofs into JavaScript](/blog/carrying-proofs-into-javascript/) presents the accompanying narrative, extending [Pondering Fearless Parallelism](/blog/pondering-fearless-parallelism/) across event loops and machines.
