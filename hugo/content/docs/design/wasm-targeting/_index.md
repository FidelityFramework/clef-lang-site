---
title: "WebAssembly Targeting"
linkTitle: "WebAssembly Targeting"
weight: 81
---

WebAssembly is the framework's second pathway to the web, and it is the one that arrives on home ground. A WebAssembly module is a sealed, statically typed artifact with a linear memory the compiler lays out completely, which is the shape our native targets already hold. [JavaScript Targeting](/docs/design/javascript-targeting/) reaches the web's ecosystem. This section covers the peer that reaches the web's machine.

The target has two faces. In the browser, a module ships beside JavaScript and shares the interop seam the JavaScript pathway already defines. Behind WASI, the system interface, the same module class runs on servers and edge hosts, and reads as another point on [the substrate range our hardware section describes](/docs/internals/hardware/on-metal-extended/): a declaration of what the image assumes and what linkage it carries. In both faces the module's single linear memory is a described address space, the kind of layout BAREWire already treats as a contract rather than a convention.

What moved this section onto the page is the stack-switching proposal. Its core design, typed continuations, adds delimited continuations with effect handlers to the target's own instruction set, and delimited continuations are [the form our concurrency already takes](/docs/design/concurrency/delimited-continuations/). LLVM remains the core substrate our lowering rides. The WAMI dialect work out of CMU, standing art we cite rather than carry, models the stack-switching side at the MLIR level and earns real runway here as the proposal matures. The entries in this section draw a hard boundary between what a Clef program targets today and what the standard is preparing, starting with the decision that shapes everything else: [coroutines versus stack switching](/docs/design/wasm-targeting/coroutine-versus-stack-switching/).
