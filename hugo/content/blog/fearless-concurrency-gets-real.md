---
title: "Fearless Concurrency Gets Real"
linkTitle: "Fearless Concurrency Gets Real"
description: "How Clef delivers memory safety and liveness integrity for multi-threaded and multi-process applications"
date: 2026-06-18T12:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Concurrency", "Memory Management", "Analysis"]
params:
  originally_published: 2026-06-18
---

A recent Pragmatic Engineer episode with Alice Ryhl talks through how the Rust team at Google and the Tokio side of their work. She's about as deep in production Rust as anyone. And the practical advice she keeps circling back to, when someone is stuck fighting the borrow checker, is to change the data structure. She says it more than once. When that doesn't get you out, the escape hatch she names is `Rc`, the reference-counted pointer you clone when the analyzer can't see far enough to keep a plain reference alive.

---

{{< youtube q9xD36NCtZ8 >}}

---

I've written about Rust before (see [the prior post](/blog/rust-revisited/)), and I want to be careful here, because none of this is Rust doing something silly or wrong. It's Rust being honest about a real limit. Ryhl [puts the limit plainly](https://www.youtube.com/watch?v=q9xD36NCtZ8&t=1566s) (26:06): "Rust kind of assumes that it can check the scope of that reference by just looking at a single function. But if you have your struct and you're passing it over functions, it might not be possible to make that analysis and so you just get a compiler error." The check is function-local. Once a value's lifetime crosses a function boundary in a way the local view can't follow, you either restructure your data so the analyzer can see it, or you reach for a counted pointer and pay per clone.

A language that sells you zero-cost memory safety hands you reference counting by hand at exactly the points where its static analysis is needed most, and asks you to reshape your program until the analyzer is happy. The burden sits on the designer. Which raises the question: 

> Does a concurrent language have to make you contort your code to fit the checker, or can the toolchain be structured to fit the shape of the problem space? 

Rust coined the catch-phrase "fearless concurrency," and it is partially earned for their memory story. The goal here with the Clef language is to take the phrase the rest of the way, into ***liveness***, and show how the Fidelity Framework builds on its ML-family inheritance to actually make good on fearless concurrency, from a more principled starting point than Rust. Our goal is to provide stronger guarantees carried with lighter developer burden.

## A challenging landscape

The usual hazard in concurrent code is two pieces of work touching the same data, one writing while the other reads. If a value can't change after it's bound, two parts of a program that don't share a mutable cell have nothing to fight over, so running them at the same time is safe. The terms of art for those two facts are referential transparency and parametricity, and they are what let our compiler, Composer, treat the pure parts of a program as safe to run in parallel for free.

Our compilation pipeline leans on this directly. Computation expressions decompose into delimited continuations for sequential effects and interaction nets for the pure parallel part, and it's the immutable, parametric core that lets the interaction-net side run symmetric and independent (the [DCont/INet duality doc](/docs/design/dcont-inet-duality/) walks the decomposition).

The second piece is one surface for all of it. Async, actors, queries: in our language they're computation expressions that desugar through the same `Bind`, which is continuation capture under a different name (see [delimited continuations](/docs/design/delimited-continuations/) for how the desugaring lands). You don't get a separate concurrency model bolted on beside the language. You get the language's own sequencing construct, reused.

And the actors are not an invention we reached for to make concurrency tractable after the fact. They descend straight from F#'s `MailboxProcessor`, the message-passing actor model Don Syme added to F# as a nod to Erlang. Our Olivier actors keep that shape: each actor owns an arena, messages move between actors, and nothing is shared behind a lock. Immutability gives us memory regions that don't interfere. Parametricity is what lets the compiler run the pure parts in parallel without a safety annotation. The actor model gives us a concurrency design where the dangerous coupling, shared mutable state under contention, was never on the table to begin with. Both halves of fearless concurrency, the memory safety and the liveness, are reachable from that substrate.

## Why "the fight" happens

"The fight" Ryhl describes is worth understanding. Her words point at it: the borrow checker checks a reference's scope "by just looking at a single function." It reasons locally by default, function by function, and where a value's lifetime crosses a boundary you bridge the gap by hand, with lifetime annotations on the signatures the value passes through. Rust gives you the tools to thread that lifetime across functions. Threading it is the work.

For a value that flows through three functions, you annotate three signatures so each local check has what it needs. Most of the time the annotating is mechanical and you stop noticing. The fight starts where the annotations stop reaching: a struct that holds a reference and gets passed around, a sharing pattern the borrow checker cannot express in the lifetime vocabulary available, a relationship decided at runtime rather than written in a type. At that point the static analysis has gone as far as it can, and you reach for something that takes over from there.

Every one of those reaches takes the same shape. Pick up `Rc` and the lifetime question moves from the compiler to a runtime counter. Pick up `unsafe` and it moves to your own audit. Pick up a lock-ordering convention in a wiki and a liveness question moves to human discipline. Each is what you do when the static analysis runs out. The burden does not disappear. It shifts to the developer and the team that carries it collectively for the maintenance life of the application.

The direction we are taking is to push where the analysis can see, rather than to ask the developer to annotate the gap. A value's lifetime, its sharing, and its wait-for relationships are facts about how the whole program fits together, so we represent them where that fit is visible: as edges in our [Program Hypergraph](https://speakez.tech/blog/coupling-and-cohesion/) that span functions and actors rather than living inside one frame. An analysis that reads from that graph can follow a value across the boundaries a local check can't follow, and derive the relationship a developer might otherwise have to spell out.

## No `unsafe` in Clef

Rust's safety guarantees come with a documented off-ramp. Ryhl's own description of the `unsafe` keyword (27:09) is honest: "unsafe is the escape hatch essentially," a block where the compiler stops vouching for you. Every serious Rust codebase reaches one. FFI calls into C, raw pointer arithmetic, a write to a memory-mapped hardware register, none of it fits inside Rust's borrow checker, so the language gives you a labeled room where the rules relax and asks you to be careful. The memory-safety bugs that survive Rust's static checking cluster in and around `unsafe`, because that is the only place they can.

We took a different position on the same boundary. Take a plain case: a counter that a closure increments, the kind of shared mutable state that shows up everywhere. In Rust, sharing one mutable value across closures runs into ownership, so the idiomatic answer is to wrap it:

```rust
use std::rc::Rc;
use std::cell::RefCell;

let count = Rc::new(RefCell::new(0));
let bump = {
    let count = Rc::clone(&count);
    move || *count.borrow_mut() += 1
};
```

The `Rc<RefCell<_>>` is the safety machinery worn on the surface: a reference count kept at runtime, and a borrow check that moved from compile time into a `borrow_mut()` that panics if the aliasing is ever wrong.

The same thing in Clef is the code with none of that on it:

```fsharp
let mutable count = 0
let bump () = count <- count + 1
```

You write the obvious thing. The design is that escape analysis sees `count` captured by `bump` and places it where its lifetime holds, with no wrapper type, no runtime count, and no `unsafe` in the source. This is work in progress in Clef: mutable bindings that stay in scope compile and run today, and the closure-capture case in this example is the escape integration we are building now, which [managed mutability](/docs/design/managed-mutability/) lays out in detail. The hardware and FFI cases are meant to follow the same rule: a peripheral register would carry its read or write direction in its type, and the boundary to foreign code goes through BAREWire as a structured contract both sides read by construction. The escape hatch Rust labels is, in our design, an ordinary construct the compiler checks rather than a room where it stops checking.

This is where Ryhl's "change the data structure" advice ends up when restructuring isn't enough: you reach for `Rc`. It manages memory at runtime, a count incremented on every clone and decremented on every drop, with the object freed when the count hits zero. That is reference counting, the same automatic memory management a garbage-collected language does, minus the part that reclaims cycles. Two references that point at each other never reach zero, so they leak. `unsafe` and `Rc` are two versions of the same move: where the static analysis runs out, the language hands you a runtime mechanism and trusts you to use it correctly.

## No skirting deadlock

Rust's borrow checker proves things about memory. No two live references alias a value mutably, no reference outlives what it points at, no data race compiles. None of that constrains whether the program makes progress. A Rust program can pass every memory-safety check and still freeze solid at runtime: two threads, each holding one lock and waiting on the other, neither able to move, the process reporting green while doing nothing. Rust teams handle deadlock the way the rest of the industry does, with a lock-ordering convention written down in a wiki, code review, and the occasional production incident that teaches everyone to respect the convention.

This isn't a Rust-specific problem. Deadlock freedom in the general case is undecidable from a proof theoretic point of view, and Rust's borrow checker is scoped to a fragment it can actually decide. Rust drew its boundary at memory and declined the liveness problem on purpose. We took a different slice of the same space: the part where the structure is visible enough to prove, with design-time support for situations that require direct developer intervention.

A `PostAndReply` suspends the caller's continuation until the callee answers, and that suspension is a wait-for edge from caller to callee. A fire-and-forget `Tell` posts to a mailbox and returns, adding no edge, so the asynchronous fraction of a program cannot deadlock through this at all. The plan is to collect the synchronous edges into a wait-for relation on our Program Hypergraph, on the joint-constraint axis that carries region and lifetime hyperedges. For the fragment where every callee is a statically resolvable actor reference, deadlock freedom is acyclicity of that relation, and acyclicity is a rank function: an integer per actor behavior such that every edge `u -> v` has `r(u) < r(v)`, which exists exactly when the relation has no cycle. That constraint is QF_LIA, the same kind of Tier-2 obligation our interval and bound checks already use, which is what makes it a tractable thing to build rather than a research problem.

Where the callee is chosen by live data (content-based routing, an actor handle passed in a message), the relation is not statically resolvable, and acyclicity over it is undecidable. That call downgrades visibly to supervised execution with a timeout, and our compiler design identifies the call that crossed the boundary. The visible fragment carries a proof, the dynamic one carries a guard and a label. Neither path goes quiet and there are no runtime surprises.

A handler doing a synchronous request looks like ordinary code:

```fsharp
let inventory = spawn InventoryActor

let handleOrder (order: Order) = actor {
    // suspends until inventory replies: one wait-for edge, order -> inventory
    let! stock = inventory.PostAndReply (CheckStock order.Sku)
    return reserve stock order
}
```

A cycle is two of those pointing back at each other. Order asks Inventory whether it can reserve; Inventory, mid-reply, asks Order to confirm a pending hold. Each parks on a reply only the other can send:

```fsharp
let handleFoo order = actor {
    let! ok = inventory.PostAndReply (Query order)   // order -> inventory
    return commit ok
}
// inside InventoryActor, handling Query:
let handleBar q = actor {
    let! held = order.PostAndReply (ConfirmHold q)    // inventory -> order
    return held
}
```

Our compiler would report the path the way escape analysis reports an escape, the actual chain rather than a generic warning:

```
CCS8031: synchronous wait cycle
  Order.handleFoo waits on Inventory.query,
  which waits on Order.handleBar.
  break the cycle: supply a priority, convert one leg to Tell, or opt into supervised timeout.
```

The lowering underneath makes the diagnostic and the proof the same object. This is illustrative dialect, the real attribute names are still settling as Composer is built. Each `PostAndReply` lowers to a blocking op carrying only its own wait edge as local fact, because no single op can see the whole set:

```mlir
%r = dcont.suspend_on_reply %callee : !actor.ref<"inventory">
       { rpc.wait_edge = #wait<from = "order", to = "inventory"> }
```

The acyclicity obligation rides on the enclosing scope, the smallest region closed under "can send a synchronous reply to," which instructs the seam to gather every edge in the region and prove a rank exists:

```mlir
module @order_system attributes { verif.obligation = #tier2.acyclic_wait } {
  // actor behaviors and their suspend_on_reply ops
}
```

Lowering would emit the verification condition into the SMT dialect, and the solver would discharge it like any interval check:

```mlir
%edges = collect rpc.wait_edge in @order_system
smt.assert (forall (u v) (=> (wait %u %v) (lt (rank %u) (rank %v))))
smt.check   // sat: a rank exists, acyclic. unsat: the core is the cycle.
 
```

When no such ranking exists, the relation has a cycle, and the developer would see a `CCS8031` error that spells it out: the chain of calls that wait on each other, `A.handleFoo waits on B.query waits on A.handleBar`. Because this analysis rides the program graph, it would surface while you write, in the editor, the same place type errors already show up, not only at the end in a batch compile. And the error is the proof itself failing, not a separate heuristic warning bolted on beside the real check, so there is nothing to second-guess. The full treatment, including the orderable-cyclic case where Composer infers the priority a developer would otherwise hand-write, is in [deadlock freedom as an obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/), and the design-time surfacing is what we are building out in our LSP and Lattice toolchain. Liveness deserves the same visible, steerable machinery we gave memory, and that is the direction our actor runtime keeps growing toward as the work continues.

## An analyzer and the structure it rides

Come back to the function-local limit Ryhl named at the top. A struct passed across functions can defeat Rust's borrow checker, whose single-function view loses the reference, and her remedy is to change the data structure: reach for an `Rc`, or redesign so the lifetime fits inside one function's view again. The analyzer has a shape it can reason about, and the developer's job is to deform the program until it matches.

Our escape analysis works the other way. The lifetime question lives in our program graph, which spans functions and actors, so a value passed across a call boundary is the same value the analyzer was already tracking. You write the data structure the problem wants. The analyzer does the cross-boundary reasoning. `let mutable x = 0` is ordinary syntax, and Composer classifies the escape (`StackScoped`, `ClosureCapture`, `ReturnEscape`, `ByRefEscape`) and places `x` on the stack or in an arena from that classification. There are no `'a`-tick lifetime annotations to thread, because the lifetime is a fact the compiler derives rather than a constraint you declare. We spent some time defining [managed mutability](/docs/design/managed-mutability/) for how the classification lowers.

Consider a value that escapes from one actor up to an predecessor. The default keeps the value in its owning actor's arena and guards the escaped reference with a sentinel: one O(1) validity check at the boundary, with deterministic release when a counted obligation set empties, and no leak when references form a cycle. You add no annotation for any of that. From there our analyzer suggests hoisting the value into the ancestor's arena, which sheds the guard entirely. Hoisting is accept-to-optimize: the safe-but-guarded form is the pit-of-success default, the faster form is a visible suggestion you can take or override. The mechanics are in the [arena hoisting tooling doc](/docs/internals/tooling/arena-hoisting/) and further articulated in our [coeffect algebra](/docs/internals/verification/memory-coeffect-algebra/#arena-hoisting-across-the-actor-hierarchy) documentation.

Both languages keep a runtime backstop for the cases static analysis can't settle. Rust's is the `Rc` from earlier, reached for by hand and unbounded in how much of the graph it covers. Ours is one sentinel, an O(1) check at a single boundary, placed by Composer and released deterministically with no cycle leak. Clef is not free of runtime checks either. The difference is that ours are bounded, automatic, and would be reached for less often.

## The work ahead

The pieces we put together hold because they were chosen together. Computation expressions give one surface where async, actors, and queries all desugar through the same continuation capture, and the immutable, parametric core makes the parallel parts safe to run without anyone marking them. Don Syme brought `MailboxProcessor` into F# himself as a hat-tip to Erlang, a library at the periphery of the language rather than at its core. We are taking that add-on and making it central to the semantics, the move laid out in [our unified actor architecture](/blog/unified-actor-architecture/), so concurrency is Clef's first concern, and the safety properties follow from that structure in a way that supports a cohesive and concise design-time experience.

Our language design doese not carry an `unsafe` keyword, because the boundary it would mark is a checked construct in our system. Deadlock freedom is designed to be proven where the wait-for graph is visible and would be supervised where live data picks the callee. For readers who want the formal treatment, the obligation framing and the rank-function discharge are worked out in the [fixed-point scaffolding pre-print](https://arxiv.org/abs/2606.02854), where the same structural integrity reaches down into the reals through our negative and fractional types. The fragment we will be able to prove grows as the program graph carries more of the lifetime and wait-for structure into the open, and that is the direction we keep building toward as the work continues.

## Small choices loom large

Rust started inside this same family. Its first compiler was written in OCaml, and early Rust carried more of the ML character along with it, a garbage collector, green threads, a typestate system, the runtime-assisted shape of an ML-family language. Mozilla's push to make it a viable C and C++ replacement is what traded those away for ownership and the borrow checker, and that turn is what made Rust the language it is, defined in reflection of C rather than descent from ML. It is a good language because of that choice. We chose a distinct path: keep the ML substrate and build on it something no language in this family has by default, concurrency that is safe and live by construction.

Ryhl's advice is good advice for the language she works in: when the borrow checker fights a developer, the fix is to change the data structure. The compiler doesn't follow the design across the function boundary, so the design is coerced toward the tool. That coercion is a cost the compiler error never shows. It is the hours of reshaping a correct program until a local analysis will sign off on it, and a long arc of support that follows. The fight is a tax on the people who write and maintain the code, paid in the currency of the day, attention and time, not only in the safety of the result.

The design we are building takes a unique direction, the compiler tracking to the required safety features of the code. A program describes the structure the solution calls for, the analysis follows each element as it flows, and Composer carries the burden of proving the result safe and live. Our aim is to deliver a language where correctness and ergonomics are not traded against each other. That is what fearless concurrency means **to *us***, and it is the direction Clef is being built to reach.
