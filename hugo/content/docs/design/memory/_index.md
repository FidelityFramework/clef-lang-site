---
title: Memory Model
weight: 1
---

Clef manages memory without a garbage collector. Instead, the compiler infers ownership, lifetimes, and region boundaries from the program's structure, producing deterministic allocation and cleanup that is both safe and predictable.

These articles trace the evolution of that approach — from the initial decision to support multiple memory strategies under programmer control, through the mechanics of region-based allocation and closure capture, to the spatial reasoning model that lets the compiler verify memory safety at compile time. If you're coming from a GC'd language and wondering how Clef avoids runtime pauses without Rust-style borrow annotations everywhere, start here.
