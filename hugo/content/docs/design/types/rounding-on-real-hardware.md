---
title: "Rounding on Real Hardware"
linkTitle: "Rounding on Real Hardware"
description: "Why rounding stops being free once representation leaves the platform's hands, and how each processor realizes it"
weight: 45
---

The [Rounding spec chapter](/spec/draft/rounding/) states what must hold. This page
is the readable version: why rounding becomes a decision the framework has to make
at all, and how that decision lands differently on a CPU, an FPGA, and the formats
in between. The spec binds; this page explains.

## Why rounding was ever free

For most of a working programmer's life, rounding is something they never think
about, and that is a gift from IEEE 754. The standard picked one rounding rule,
round-to-nearest with ties going to the even digit, and made every mainstream
processor implement it the same way in hardware. A program adds two floats and the
result rounds correctly without anyone asking for it. The rounding was decided once,
in 1985, for everyone.

That free ride depends on a single assumption: every value uses the same
representation, so they can all share one rounding rule. The moment a framework
takes representation away from the platform and chooses it per target, the shared
rule is gone. A posit does not round the way an IEEE float does. A fixed-point value
rounds at a scale the developer set. An interval has to round its two ends in
opposite directions. There is no longer one rule the platform carries for you, so
the framework has to carry it instead. That is the whole reason a rounding chapter
exists: representation selection bought a precision win, and the bill it left behind
is that rounding is now a choice someone has to make on purpose.

## Two jobs rounding does

It helps to separate the two places rounding shows up, because they behave nothing
alike.

The first is **converting a value from one representation to another**. A 64-bit
result has to become a 32-bit one to fit a register; a quire's exact sum becomes a
posit at the end of an accumulation; a value computed on the FPGA crosses to the
host. Something has to give, and rounding decides what. Get it wrong and the answer
is a little less accurate than it could have been. That is a real cost, but it is a
cost in precision, and the value is still a perfectly good number.

The second is **an operation committing a direction while it computes**. Usually
this is invisible, because round-to-nearest is what everyone wants. The exception is
the interval, and it is the exception that forces the whole design. An interval is a
pair, a low end and a high end, and it is only honest if it truly contains the
answer. To stay honest, the low end has to round down (toward negative infinity) and
the high end has to round up (toward positive infinity), on every single operation.
Round the low end the wrong way by one bit and the interval now claims to contain a
value that sits just below it. That is not a less-accurate interval. It is a lie. The
enclosure has stopped enclosing.

This difference, a wrong rounding costing accuracy versus a wrong rounding telling a
falsehood, is why the spec carries the two cases differently. The conversion case rides
along as a tracked fact, carried as a coeffect to be surfaced where it was written. The
interval case is specified into the type itself, so a value that cannot round outward is
not a well-formed interval in the first place. One is a note in the margin; the other is
a load-bearing wall.

## How a CPU rounds: a global switch

A CPU has a rounding mode, and the surprising part is that there is exactly one of
them, shared by everything. On an x86 chip it lives in a control register called
MXCSR; on an ARM chip it is the FPCR. Two bits select the direction, and once set,
that direction applies to every floating-point instruction that follows until
something changes it. It is a switch on the wall, not a setting on each lamp.

For ordinary arithmetic this is fine, because the switch sits on round-to-nearest
and stays there. For an interval it is a genuine problem. An interval wants its low
end rounded down and its high end rounded up, which means flipping the switch between
the two halves of every operation. And flipping the switch is slow. In one
published benchmark, changing the rounding mode and changing it back cost on the order
of thirty times a normal operation on an Apple M1, and closer to seventy times on a
high-end x86 part. The chip stalls its pipeline each time the mode moves.

There are two honest ways to live with this on a CPU. Compute all the low ends first
with the switch set one way, then flip once and compute all the high ends, so the
expensive flip happens a handful of times instead of constantly. Or accept the
penalty and pay it. Either way, directed rounding on a CPU is something you can do,
but not something that is free, and the framework records it as such: the capability
is there, but it is emulated in the sense that matters, which is cost.

## How an FPGA rounds: a wire, not a switch

An FPGA inverts the whole situation, and this is the part worth sitting with, because
it is why the demo and the design point at fabric.

An FPGA has no rounding-mode register, because it has no fixed instructions to apply
a mode to. Every operation is laid out as actual logic, gates and wires, built to
order when the design is synthesized. Rounding on an FPGA is a small piece of that
logic: a few gates that decide whether to bump the truncated result up by one, and
the *direction* of that decision is just how the gates are wired. Round-down wires
one way, round-up the other.

The consequence is that choosing a rounding direction on an FPGA costs nothing at
runtime. You do not flip a switch while the circuit runs; the direction is laid into the
gates before the circuit exists. An interval that costs seventy times normal on a CPU,
because of all that switch-flipping, costs nothing extra on an FPGA, because the low-end
datapath is synthesized to round down and the high-end datapath to round up, side by
side, each running full speed. The price is paid once, at synthesis, not on every
operation.

This is the load-bearing asymmetry, and it is why the design points at fabric. On a CPU,
rounding direction is a runtime mode, and directed rounding fights the hardware. On an
FPGA, rounding direction is a design property, and directed rounding is the natural state
of the fabric. The same `Interval<Posit32>` is an awkward, expensive thing on a host and
is designed to be a comfortable native construct on a board, and the capability gate is
the honest record of which target you landed on.

## The posit's missing direction

There is a wrinkle that catches people, and it is better to meet it head-on. The
Posit Standard defines exactly one rounding mode, round-to-nearest, and no directed
modes at all. There is no "round a posit toward negative infinity" in the standard,
because posits were designed around a different idea, the quire, which makes the
common need for directed rounding go away by making accumulation exact.

This means a sound interval *over posits* cannot lean on the posit's own arithmetic to
round its ends outward, because that arithmetic does not know how. The fix is old and
reliable: compute each end with the rounding you have, round-to-nearest, then nudge
the low end down by one unit in the last place and the high end up by one. That nudge
is Moore's outward-widening trick, and it guarantees the interval still contains the
truth, at the cost of being a hair looser than a natively directed one would be. The
design specifies exactly this construction, with a diagnostic that distinguishes a
synthesized posit interval from a natively directed one, so a posit interval is never
meant to be quietly passed off as something it is not.

## The quire: rounding once, on purpose

The quire deserves its own moment here, because it is the cleanest example of rounding
as a deliberate design choice rather than a default.

An ordinary running sum in floating point rounds after every addition, and those tiny
roundings pile up across a long accumulation until they matter. The quire refuses to
do that. It is a wide accumulator, 512 bits for a 32-bit posit, large enough to hold
every partial product of a long sum exactly, with no rounding at all along the way. The
rounding happens once, at the very end, when the exact accumulated value is converted
back to a posit. One rounding for the whole sum, not one per step.

That single-rounding discipline is the entire reason the quire helps. It keeps the
structural zeros of a geometric-algebra computation exactly zero through training,
because zero plus zero exactly is still zero, and a phantom rounding never creeps in to
populate a component the algebra says should be empty. And it defeats catastrophic
cancellation, the precision collapse that happens when you subtract two nearly-equal
large numbers, by holding everything exactly until past the point where the
cancellation would have done its damage. None of this is about precision near zero,
which posits do not have. It is about doing the rounding once, in the right place, on
purpose.

## Fixed-point and the overflow question

Fixed-point rounds at whatever bit the developer chose as the least significant, and
it offers the usual menu of directions, toward nearest, toward zero, toward an end.
What makes fixed-point its own conversation is overflow, because a fixed-point value
has a hard ceiling and a hard floor, and a result can run past them.

There are two honest things to do when it does. Saturate, which clamps the result to
the ceiling or floor, so a value that overshot lands at the maximum the format can
hold. Or wrap, which lets the value roll over modularly, the way an odometer rolls
past its last digit back to zero. For a physical quantity, saturation is almost always
the right answer, because a clamped force or voltage is a bounded, recognizable error,
while a wrapped one is a wild value that looks plausible and is completely wrong. The
framework leans toward saturation for dimensioned values for exactly this reason, and
in every case it insists the choice be made rather than assumed, because a silent wrap
is the kind of bug that survives every test and ruins a long run.

## How the design carries rounding

The design treats rounding the way it treats representation and width: as something
inferred and carried, not assumed. A conversion that loses precision is specified to
carry its rounding choice as a coeffect, surfaced at the point it was written. An
interval is specified to carry its directed-rounding requirement in its type, so the
question is settled by construction when the value is formed rather than discovered when
it runs. And every rounding mode a value needs is gated against the target that would
run it, with three outcomes by design: the target does it in hardware, the target does
it at a cost worth surfacing, or the target cannot do it soundly and the build is
specified to say so plainly rather than quietly round the wrong way.

A word on what stands today. The integer half of this discipline lowers to fabric now:
width inference is the shipping sibling, and the coeffect carriage these rounding rules
ride on is the same machinery. The real-valued half, the interval type, per-operation
rounding control, and the conversion syntax that names a rounding mode, is design-stage
work, not a shipping pass; the spec marks that conversion and seal syntax
[Not yet specified], and the sibling [deferred-inference](/blog/deferred-inference/) and
[posit-arithmetic](/docs/design/types/posit-arithmetic/) pages flag the same gaps. This
page describes the shape the design fixes ahead of the pass that will implement it.

The short version is the one the spec opens with. IEEE 754 used to carry rounding for
everyone, invisibly, because everyone shared one representation. The framework gives that
up to win precision per bit, and the rounding chapter is where it picks the bill back up:
rounding becomes a decision, the decision lands differently on a switch-driven CPU than
on a wire-driven FPGA, and the design's job is to make the right one for the target and
never make it silently.
