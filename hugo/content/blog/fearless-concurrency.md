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

A recent [Pragmatic Engineer episode](https://www.youtube.com/watch?v=q9xD36NCtZ8) with Alice Ryhl talks through how the Rust team at Google and the Tokio side of their work. She's about as deep in production Rust as anyone. And the practical advice she keeps circling back to, when someone is stuck fighting the borrow checker, is to change the data structure. She says it more than once. When that doesn't get you out, the escape hatch she names is `Rc`, the reference-counted pointer you clone when the analyzer can't see far enough to keep a plain reference alive.

---

{{< youtube q9xD36NCtZ8 >}}

---

I've written about Rust before (see [the prior post](/blog/rust-revisited/)), and I want to be careful here, because none of this is Rust doing something silly or wrong. It's Rust being honest about a real limit. Ryhl [puts the limit plainly](https://www.youtube.com/watch?v=q9xD36NCtZ8&t=1566s) (26:06): "Rust kind of assumes that it can check the scope of that reference by just looking at a single function. But if you have your struct and you're passing it over functions, it might not be possible to make that analysis and so you just get a compiler error." The check is function-local. Once a value's lifetime crosses a function boundary in a way the local view can't follow, you either restructure your data so the analyzer can see it, or you reach for a counted pointer and pay per clone.

A language that sells you zero-cost memory safety hands you reference counting by hand at exactly the points where its static analysis is needed most, and asks you to reshape your program until the analyzer is happy. The burden sits on the designer. Which raises the question: 

> Does a concurrent language have to make you contort your data to fit the checker, or can the checker be built to fit the data? 

Rust borrowed the phrase "fearless concurrency," and it earned it for the memory story. I want to take the phrase the rest of the way, into liveness, and show how an ML-family lineage gets you there from a different starting point.

## Why ML heritage is the right substrate

Start with immutability as the default. When a value can't change after it's bound, two regions of a program that don't share a mutable cell can't interfere, and you get that independence by referential transparency rather than by proving it case by case. Wadler's parametricity does the rest: a polymorphic function can't inspect the values it's generic over, so the parallel evaluation path is licensed by the types instead of asserted by the author. Our compilation spine leans on this directly. Computation expressions decompose into delimited continuations for sequential effects and interaction nets for the pure parallel part, and it's the immutable, parametric core that lets the interaction-net side run symmetric and independent (the [DCont/INet duality doc](/docs/design/dcont-inet-duality/) walks the decomposition).

The second piece is one surface for all of it. Async, actors, queries: in our language they're computation expressions that desugar through the same `Bind`, which is continuation capture under a different name (see [delimited continuations](/docs/design/delimited-continuations/) for how the desugaring lands). You don't get a separate concurrency model bolted on beside the language. You get the language's own sequencing construct, reused.

And the actors are not an invention we reached for to make concurrency tractable after the fact. They descend straight from F#'s `MailboxProcessor`, which carried the Erlang-influenced, message-passing actor model into the ML family years ago. Our Olivier actors keep that shape: each actor owns an arena, messages move between actors, and nothing is shared behind a lock. Immutability gives us memory regions that don't interfere. Parametricity licenses the parallel path. The actor model gives us a concurrency design where the dangerous coupling, shared mutable state under contention, was never on the table to begin with. Both halves of fearless concurrency, the memory safety and the liveness, are reachable from that substrate.

## No `unsafe`

Rust's safety guarantees come with a documented off-ramp. Ryhl's own [description of the `unsafe` keyword](https://www.youtube.com/watch?v=q9xD36NCtZ8&t=1629s) (27:09) is honest: "unsafe is the escape hatch essentially," a block where the compiler stops vouching for you. Every serious Rust codebase reaches one. FFI calls into C, raw pointer arithmetic, a write to a memory-mapped hardware register, none of it fits inside Rust's borrow checker, so the language gives you a labeled room where the rules relax and asks you to be careful. The memory-safety bugs that survive Rust's static checking cluster in and around `unsafe`, because that is the only place they can.

We took a different position on the same boundary. The places Rust marks `unsafe` are real, the hardware is real, the FFI is real, and a peripheral register genuinely sits outside any analysis a borrow checker can run. Our type system describes that boundary as a construct rather than going quiet at it. A hardware register carries an access kind. A read-only peripheral register has a type the compiler will not let you write to, and the runtime boundary to foreign code goes through BAREWire, a structured contract both sides read by construction.

Touching a status register in Rust:

```rust
let status: u8 = unsafe {
    core::ptr::read_volatile(0x4000_4400 as *const u8)
};
// the compiler is no longer checking anything in here
```

The same access in Clef:

```fsharp
// 0x4000_4400, read-only peripheral, 8-bit
let uart = Register.readOnly<u8> 0x4000_4400<addr>

let status = Register.read uart        // fine
let oops   = Register.write uart 0x1   // compiler error - ReadOnly has no write
 
```

The address still names real silicon. `Register.write uart` does not compile, and there is no block you can wrap it in to make it compile. The check rides in the type, so it travels everywhere the value travels.

Ryhl's tour of where the borrow checker runs out lands on the same shape with `Rc`. When ownership stops fitting the analyzer, the recommended path is `Rc`, a reference-counted pointer that does memory management at runtime: a count incremented on every clone, decremented on every drop, and a leak waiting on any reference cycle. `Rc` is reference-counting GC, the counted kind, not a tracing collector running behind your back. Rust hides no garbage collector here. `unsafe` and `Rc` point at two different walls with the same hand: where the static model runs out, the language gives you a runtime escape and trusts you to hold it correctly. We are building toward a boundary you declare and the compiler keeps checking, so there is no escape to hold.

## No skirting deadlock

The borrow checker proves things about memory. No two live references alias a value mutably, no reference outlives what it points at, no data race compiles. None of that constrains whether the program makes progress. A Rust program can be entirely memory-safe and frozen solid: two threads, each holding one lock and blocking on the other, every reference valid, every invariant intact, and the process sits there reporting green while doing nothing. Rust teams handle deadlock the way the rest of the industry does, with a lock-ordering convention written down in a wiki, code review, and the occasional production incident that teaches everyone to respect the convention.

Rust didn't miss this. Deadlock freedom in the general case is undecidable, and the borrow checker is scoped to a fragment it can actually decide. Rust drew its boundary at memory and declined the liveness problem on purpose. We took a different slice of the same undecidable space: the part where the structure is visible enough to prove, with an honest fallback for the part that is not.

A `PostAndReply` suspends the caller's continuation until the callee answers, and that suspension is a wait-for edge from caller to callee. A fire-and-forget `Tell` posts to a mailbox and returns, adding no edge, so the asynchronous fraction of a program cannot deadlock through this at all. We collect the synchronous edges into a wait-for relation on our Program Hypergraph, on the same joint-constraint axis that already carries region and lifetime hyperedges. For the fragment where every callee is a statically resolvable actor reference, deadlock freedom is acyclicity of that relation, and acyclicity is a rank function: an integer per actor behavior such that every edge `u -> v` has `r(u) < r(v)`, which exists exactly when the relation has no cycle. That constraint is QF_LIA, an ordinary Tier-2 obligation discharged by the same solver that handles our interval and bound checks.

Where the callee is chosen by live data (content-based routing, an actor handle passed in a message), the relation is not statically resolvable, and acyclicity over it is undecidable. That call downgrades visibly to supervised execution with a timeout, and the compiler names the call that crossed the boundary. The visible fragment carries a proof, the dynamic one carries a guard and a label, and neither path goes quiet.

A handler doing a synchronous request looks like ordinary code:

```fsharp
let inventory = spawn InventoryActor

let handleOrder (order: Order) = actor {
    // suspends until inventory replies: one wait-for edge, order -> inventory
    let! stock = inventory.PostAndReply (CheckStock order.Sku)
    return reserve stock order
}
```

A cycle is two of those pointing back at each other. Order asks Inventory whether it can reserve; Inventory, mid-reply, asks Order to confirm a pending hold; both park on a reply only the other can send:

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

The compiler reports the path the way our escape analysis reports an escape, the actual chain rather than a generic warning:

```
CCS8031: synchronous wait cycle
  Order.handleFoo waits on Inventory.query,
  which waits on Order.handleBar.
  break the cycle: supply a priority, convert one leg to Tell, or opt into supervised timeout.
```

The lowering underneath makes the diagnostic and the proof the same object. This is illustrative dialect, the real attribute names are still settling as Composer is built. Each `PostAndReply` lowers to a blocking op carrying only its own wait edge as local fact, because no single op can see the whole set:

```mlir
// illustrative
%r = dcont.suspend_on_reply %callee : !actor.ref<"inventory">
       { rpc.wait_edge = #wait<from = "order", to = "inventory"> }
```

The acyclicity obligation rides on the enclosing scope, the smallest region closed under "can send a synchronous reply to," which instructs the seam to gather every edge in the region and prove a rank exists:

```mlir
// illustrative
module @order_system attributes { verif.obligation = #tier2.acyclic_wait } {
  // actor behaviors and their suspend_on_reply ops
}
```

Lowering emits the verification condition into the SMT dialect, and the solver discharges it like any interval check:

```mlir
// illustrative
%edges = collect rpc.wait_edge in @order_system
smt.assert (forall (u v) (=> (wait %u %v) (lt (rank %u) (rank %v))))
smt.check   // sat: a rank exists, acyclic. unsat: the core is the cycle.
```

When the relation cannot be ranked, the unsat core the solver returns is the minimal set of edges that cannot be jointly ordered, which is the cycle. The `CCS8031` chain the developer reads is that core, formatted. One object serves as both the proof failure and the diagnostic, rather than a heuristic warning sitting next to a separate check. The full treatment, including the orderable-cyclic case where the compiler infers the priority a developer would otherwise hand-write, is in [deadlock freedom as an obligation](/docs/design/concurrency/deadlock-freedom-as-an-obligation/). Liveness deserves the same visible, steerable machinery we gave memory, and that is the direction the actor runtime keeps growing toward as the work continues.

## Change the analyzer, not the data structure

Come back to the function-local limit Ryhl named at the top. A struct passed across functions can defeat the borrow checker's single-function view, and her remedy is to change the data structure: reach for an `Rc`, or redesign so the lifetime fits inside one function's view again. The analyzer has a shape it can reason about, and the developer's job is to deform the program until it matches.

Our escape analysis works the other way. The lifetime question lives in our program graph, which spans functions and actors, so a value passed across a call boundary is the same value the analyzer was already tracking. You write the data structure the problem wants. The analyzer does the cross-boundary reasoning. `let mutable x = 0` is ordinary syntax, and the compiler classifies the escape (`StackScoped`, `ClosureCapture`, `ReturnEscape`, `ByRefEscape`) and places `x` on the stack or in an arena from that classification. There are no `'a`-tick lifetime annotations to thread, because the lifetime is a fact the compiler derives rather than a constraint you declare. See [managed mutability](/docs/design/managed-mutability/) for how the classification lowers.

The case worth walking through is a value that escapes from one actor up to an ancestor. The default keeps the value in its owning actor's arena and guards the escaped reference with a sentinel: one O(1) validity check at the boundary, with deterministic release when a counted obligation set empties, and no leak when references form a cycle. You add no annotation for any of that. From there our analyzer suggests hoisting the value into the ancestor's arena, which sheds the guard entirely. Hoisting is accept-to-optimize: the safe-but-guarded form is the pit-of-success default, the faster form is a visible suggestion you can take or override. The mechanics are in the [arena hoisting tooling doc](/docs/internals/tooling/arena-hoisting/) and the [coeffect algebra](/docs/internals/verification/memory-coeffect-algebra/#arena-hoisting-across-the-actor-hierarchy).

Both languages keep a runtime backstop for the case static analysis can't crack. Rust's is the `Rc` from earlier, reached for by hand and unbounded in how much of the graph it ends up covering. Ours is one sentinel, bounded to O(1) at a single boundary, placed by the compiler instead of by you, releasing deterministically with no cycle leak. Clef is not free of runtime checks, and claiming otherwise would be its own escape hatch. What our analysis buys is a cheaper backstop reached for less often, on the structure you actually wanted to write.

## The work ahead

The thread running through all of this is older than Clef. Immutability by default makes pure regions independent by referential transparency, so the parallel path through interaction nets is licensed rather than asserted. Computation expressions give one surface where async, actors, and queries all desugar through the same continuation capture. The actor model arrives by way of F#'s `MailboxProcessor` and the Erlang lineage behind it. Concurrency was the first concern of the languages Clef descends from, and we are building the compiler so that the safety properties follow from that structure instead of being bolted on beside it.

Memory safety carries no `unsafe` to switch it off, and deadlock freedom is proven where the wait-for graph is visible and supervised where live data picks the callee. For readers who want the formal treatment, the obligation framing and the rank-function discharge are worked out in the [fixed-point scaffolding pre-print](https://arxiv.org/abs/2606.02854), where the same structural integrity reaches down into the reals through our negative and fractional types. The fragment we can prove keeps growing as the program graph carries more of the lifetime and wait-for structure into the open, and that is the direction we keep building toward as the work continues.
