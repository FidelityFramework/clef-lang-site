---
title: "Fixing on Falcon"
linkTitle: "Fixing on Falcon"
description: "Fixed-Point Signatures and the Shape of Provable Implementation"
date: 2026-08-17
authors: ["Houston Haynes"]
tags: ["Cryptography", "Post-Quantum", "Verification"]
---

Here is a piece of news that might read as a footnote outside the cryptographic engineering world: Falcon, the post-quantum signature scheme on its way to becoming FIPS 206 under the name FN-DSA, can now sign without floating-point arithmetic. [PQShield announced the result](https://pqshield.com/falcon-without-floating-point/) with a CRYPTO 2026 paper behind it from their researchers and colleagues at the University of Rennes, and the announcement is itself a small act of hospitality: it teaches fixed-point arithmetic from a standing start, interactive widgets included. Thomas Pornin, one of Falcon's designers, maintains [c-fn-dsa](https://github.com/pornin/c-fn-dsa), an integer-only C implementation that tracks the anticipated standard, released to the public domain with hand-written ARM Cortex-M4 assembly.

Falcon's appeal beside ML-DSA, the other lattice signature in NIST's portfolio, is economy: it is the compact, efficient option. The price of that economy is in the signing arithmetic. Falcon signs with floating-point operations, and floating point is a liability in the settings where a compact signature is most attractive. Lower-end devices often have no floating-point unit at all. Where the hardware exists, floating-point operations offer more timing and side-channel leakage avenues than integer code, and real attacks on Falcon implementations have exploited them. The careful fallback, emulated floating point running in constant time, costs about twenty times the speed of native hardware.

We read the announcement with more than spectator interest. The paper's method, *bound everything before it runs, then build to the bounds*, is the pattern our Fidelity Framework is being designed to turn into compiler machinery. This post covers the same ground in the same inviting spirit and the work that caught our eye.

## Real Numbers as Scaled Integers

Anyone who has stored a price in cents has used fixed-point arithmetic. The generalization is nearly as small as the example: pick a base \(b\), pick a number of fractional digits \(f\), and store a real value as the nearest integer to its product with \(b^f\). The integer \(v\) in memory denotes \(v \cdot b^{-f}\). The exponent \(f\) is a compile-time constant, so the running code holds only ordinary integers. In binary the digits are bits, and the notation 32.64 describes a format with 32 bits above the point and 64 below.

The payoff is that real-number arithmetic collapses into operations every processor has. Addition of two values on the same scale is integer addition, nothing more. Multiplication is one step subtler: multiplying two scaled integers doubles the scale, so the recipe is a word-doubling integer multiply followed by dropping the low digits.

Two hazards come with the fixed exponent, one at each end of the word. A value far below the top of its format spends its upper bits on zeros, and precision quietly drains away. A value past the top of its format loses its leading digits, and the arithmetic continues on a number that is wrong. Underflow is a precision loss. Overflow is a correctness loss, and a signing routine publishes its arithmetic, wrong results included.

The paper sorts scale management into two disciplines. Type 1 is the uniform discipline: one \(f\) for the program, sized for the largest value anywhere, paid for in idle bits everywhere else. Type 2 is the per-variable discipline: each variable has its own \(f\), and every multiplication ends with an explicit shift that returns the product to the scale its destination expects. The reward is a format with no idle bits. The price is bookkeeping: each assignment must agree about \(f\) along every path through the program. A per-variable exponent that addition must match and multiplication must sum is a type discipline, maintained by hand.

## Division by Multiplication

Falcon's signing also needs division and square roots, the operations that are the usual argument for a floating-point unit. The classical escape is Newton-Raphson iteration. To divide by \(x\), compute its reciprocal by repeating

\[ y \leftarrow y \cdot (2 - x \cdot y) \]

and look at the ingredients: two multiplications and a subtraction. The recipe for division contains no division.

The convergence is quadratic: the error is squared at every step, so the count of correct digits doubles. Five or six iterations are enough at Falcon's working precision. The implementation first normalizes the divisor into a known interval, so a fixed starting value provably sits inside the basin of convergence, and the loop then runs a fixed number of steps with no early exit.

The fixed count is a security property. Code whose running time varies with its inputs reveals something about those inputs, and when the inputs derive from a private key, a stopwatch becomes an attack instrument. A division that always runs six steps takes the same time on every value, which removes the timing channel from the operation. Falcon's square roots come from the same pattern: a multiply-only iteration run for a fixed count.

## Proof at Key Generation

The mechanics above are the easy half. The hard half is the choice of \(f\): to fix a format you must know how large every value can grow, and the intermediates of Falcon's signing procedure are shaped by randomness and by the private key itself. Margins measured from benchmarks are an engineering concern. Their paper's answer is a theorem: every intermediate variable that appears during signing is bounded above by a function of the private key.

Proving that was the technical labor of the . Symplecticity, a symmetry of the lattices Falcon computes over, converts upper bounds into lower bounds, and the same structure permits storing half of the expanded key, an economy observed by Sun et al. in 2022. The intermediates themselves are projections of a discrete Gaussian distribution onto an affine line, and concentration bounds cap how far such a projection strays from its center.

The elegant part is where the theorem is put to work: key generation. When a candidate key is produced, four quantities are computed from it: the infinity norms of the key's three internal transforms (\(\mathrm{FFT}(f,g)\), \(\mathrm{FFT}(F,G)\), \(\mathrm{FFT}(k)\)) and a hybrid measure \(\alpha\). The FFT is the fast Fourier transform, Falcon's working representation for polynomial arithmetic, and an infinity norm is the largest magnitude any coefficient reaches. Each quantity is compared against a fixed threshold (the paper names them \(\gamma_{f,g}\), \(\gamma_{\text{hybrid}}\), \(\gamma_{F,G}\), \(\gamma_{\text{root}}\)). A candidate that misses any check is discarded, and key generation restarts.

The four checks work as certificates.

> Every accepted key carries, by construction, a bound on every intermediate its signing path will ever produce.

A rejected candidate is an overflow surfaced at design time, before the key exists to sign anything. With the thresholds in place, every intermediate on the signing path stays below \(2^{21}\), and fewer than half of otherwise admissible keys are lost to the checks. The arithmetic punchline is one comparison: \(21 < 32\). A global 32.64 format accommodates every value the signing path can produce, with no overflow risk. The format question is settled at key generation, by proof, before a single signature exists.

The authors' C implementation is Type 1, one uniform 64.64 format across the computation, and it signs about seven times faster than constant-time emulated floating point. Their reported timings, normalized to native floating-point hardware at 1:

| Operation | Native FP | Emulated FP (constant time) | Fixed point (64.64) |
|---|---|---|---|
| Key expansion | 1 | 32.3 | 3.52 |
| Signing | 1 | 13.19 | 1.85 |

Verification never used floating point, and key generation already had a fixed-point treatment from Pornin's earlier work. Signing was the last floating-point dependency in the scheme. Its removal is this entries supposition.

## Design-Time Discharge

Step back from the particulars and the paper reads as a method. The researchers performed design-time discharge by hand: they enumerated every obligation the implementation must meet, proved each one before the program runs, and let the proofs license both the format and the shortcuts. The price, in this instance, was a specialist team and a CRYPTO publication cycle. That cost is why the pattern stays rare, and why we want it in a compiler where proof materials are a correlated by-product of the lowering process.

We see that pattern as compiler machinery, and the Fidelity Framework is being designed toward each piece of it. Bounds come first: in our staged-discharge design, a range on a value is an obligation proved before execution and carried forward as part of the compilation evidence.

Formats come second. A Type 2 scaling factor is a compile-time annotation with an arithmetic of its own, an exponent that multiplication sums and an explicit shift coerces, and an annotation with an arithmetic is a type. Our type substrate is built on that kind of algebra, the abelian-exponent structure underneath units of measure, and for the curious the [Negative and Fractional Types](/docs/design/types/negative-fractional-types/) design note develops how far the same structure extends. A compiler carrying scaling factors as types would derive each variable's format from its proved range, reclaiming by construction the bits a uniform format leaves idle.

Simplifications come third. Fast lattice arithmetic uses lazy reduction, deferring the cleanup step of modular arithmetic while intermediate values provably fit the word. In the original work that inspired us, that permission is a hand proof or a comment in the assembly. Under our discharged bounds it becomes an ordinary transformation, licensed by proof instead of by hand: the first of a class we think of as range-licensed simplifications.

Constant time is the fourth piece, and the one where current best practice is hand-written assembly: an optimizing compiler with no model of the property may rewrite branchless code into a branch, and cannot be trusted to preserve what it does not represent. Pornin's Cortex-M4 assembly is today's low-level answer. By contrast, the pipeline we are designing treats constant time as a property to carry and verify through every lowering. The property the source established would still hold and would be verified in the binary the linker emits.

The final artifact deserves the same scrutiny. A bound proved over source code is a statement about source text, and the program that signs is a binary, shaped by every transformation in between. Our HelloProof exercise sketches a principled direction: proofs at design time, re-checked over the compiler's intermediates, then re-proved against the actual layout using an external proof assistant directly inpsecting the memory map in the binary.

## From Binding to Port

The first step we are considering is a binding to Pornin's c-fn-dsa. It is public domain and tracks the anticipated FIPS 206. Its functions take caller-provided buffers, a convention that suits our region-based memory design as well as the embedded builds it was written for. The Cortex-M4 assembly covers the class of device where an absent FPU is the ordinary case, the setting where this result matters most.

The purpose is to build toward is a native port: Falcon signing directly expressed in Clef, integer arithmetic from key expansion to the emitted signature. An all-integer program sits inside the surface a young native compiler proves first. Integer ranges and word-width invariants are the obligation classes our verification design already covers. The conceit is that a signature scheme whose correctness argument is built from exactly those obligations is a demonstration made to order.

Known-answer tests are the bridge between those milestones. The gate is byte equality: the port reproduces the reference implementation's output on every published vector, bit for bit, or the port is wrong. Fixed point is friendlier to that rubric than floating point, because hardware floating point comes with weaker determinism guarantees, while integer arithmetic reproduces exactly on every target. The scoreboard after the gate is cycle counts, measured against the published art. And if the key-generation checks enter the FN-DSA standard, the scheme arrives pre-shaped for a compiler that proves bounds instead of trusting the programmer's arithmetic.

## Deliberate Fixation

The name of this post, set beside our [Fixed-Point Scaffolding pre-print](https://arxiv.org/abs/2606.02854), is not an accident. Fixing on Falcon is fixing on the class of systems where proof and implementation meet.

We have some lofty goals for crytographic computation in the Fidelity Framework, and this case study "fell into our lap" at the right time in the development of this framework. We look forward to both the binding and porting exercises as a forcing function to bring deeper, more principled execution to the Fidelity Framework.
