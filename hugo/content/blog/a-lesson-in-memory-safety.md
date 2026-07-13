---
title: "A Lesson in Memory Safety"
linkTitle: "A Lesson in Memory Safety"
description: "A FreeBSD kernel bug hidden in plain sight by numeric representation."
date: 2026-07-01T00:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Memory Safety", "Types", "Architecture"]
params:
  originally_published: 2026-07-01
---

Some bugs survive in production for years because the code reads exactly like careful engineering. FreeBSD recently resolved a deep issue that fits in that category: a bounds check in the kernel, correct on its face, that fails against a single negative number. The video below breaks it down step by step and is worth watching before reading on.

{{< youtube 14q9KLkbRT8 >}}

The function copies a bounded region of kernel memory out to a user-supplied destination. The kernel buffer `kbuf` is 1 kilobyte. A caller passes a destination and a length, and the code clamps that length so it can (at least *in theory*) never exceed the buffer:

```c
void *memcpy(void *dest, void *src, size_t n);

#define KSIZE 1024
char kbuf[KSIZE];

int copy_from_kernel(void *user_dest, int len) {
    int safe_len = KSIZE < len ? KSIZE : len;
    memcpy(user_dest, kbuf, safe_len);
    return safe_len;
}
```

The clamp looks airtight. If the caller asks for 2 kilobytes, `safe_len` becomes 1024, and `memcpy` copies exactly the buffer. The engineer checked the bound. The check is right there on line 7.

## The 'Secret' Revealed

`len` is an `int`, a signed integer. `memcpy` takes its length as `size_t`, an unsigned integer. Nothing in the code converts between them. C does it silently at the call.

A caller passes `len = -2048`. The clamp evaluates `1024 < -2048`, which is false, so `safe_len` stays at `-2048`. That negative value is then handed to `memcpy`, which reads its argument as unsigned. The bit pattern for `-2048` in a signed integer, read as unsigned, is an enormous positive number. `memcpy` copies that many bytes, reading past the end of the buffer and into the kernel memory that follows it, where secrets like passwords and keys are held, and delivers all of it to the caller.

The bound was checked and deemed correct. It was compromised by a conversion that no line of the code performs: the signed-to-unsigned reinterpretation that C inserts, invisibly, at the boundary between the program's logic and `memcpy`'s contract. The representation of the length was decided by the type names at each end, `int` on one side and `size_t` on the other, and the mismatch between those names is the vulnerability.

C changes a value's type at a boundary without a signal in the source, so the one operation that breaks the code is nearly invisible. This was a deliberate choice in C's design, made for convenience, not some arbitrary limitation of its era. 

> The alternative, requiring every type change to be written explicitly, was available at the time. 

Clef does not allow implicit numeric conversions, and like many other languages there's more than one good reason. Once you see *this* reason in *this* block of code, the rationale jumps out at you like a ticking bomb in plain sight.

That design decision leaves the safety of this code human-carried. Because the language will silently convert the type, it offers no place to record the one fact the code depends on: that `len` must not be negative. Nothing in `int` describes that constraint. So the invariant must reside in the author's head, subject to memory and silent distraction and error. 

> Every contributor who touches this code afterward inherits that silent constraint, along with the language decision that put it there. 

The same default posture takes other forms in other bugs, a unit left out of a type, a dimension the compiler was never told, and [Danger Close: Why Types Matter](/blog/danger-close-why-types-matter/) walks through what a Fahrenheit-for-Celsius or milligrams-for-micrograms mix-up costs when the type is silent on it. Here the fact is both figuratively and literally **a *sign***. This is the information loss we describe in [Deferred Inference](/blog/deferred-inference/): committing to `int` early discards the range from which the compiler would have derived that the value is never negative, and once that range is gone, so is the safety it would have signaled in a design-time diagnostic and a compile-time guarantee to only produce a safe instruction sequence.

## Forcing It Into the Open

Simon Peyton Jones, one of the creators of Haskell, names the property at the heart of this in a recent interview. He was talking about shared mutable state, but the point is the same:

> Reading and writing shared variables that are visible outside the scope is a form of coupling between bits of code that is very invisible. Very invisible. So functional programming forces that to become visible. It forces it into the open.

That is exactly what the FreeBSD bug turns on. The dependency between `len` and `memcpy`, that the value must be non-negative for the bound to hold, is coupling between two bits of code, and it is *invisible*. C does not force it into the open. It lets the coupling stay implicit and the conversion stay silent, and the function's safety rests on a relationship that appears nowhere in the source.

The lesson is not that an engineer should have been more careful. They *were **all*** careful. The check was correct. The lesson is that a language which leaves the coupling invisible leaves the safety to vigilance, and vigilance does not survive contact with a codebase that many people touch over many years. SPJ's own summary of the alternative is blunt: you want to *know* a program is safe, ["rather than just say I've done my best."](https://youtu.be/xcB_LF3cdqw?t=546)

## Correct By Construction

Clef forces that coupling into the open by making it part of the type. Where C decided the representation from two type names, Clef derives it from the value's range, analyzed through the program's dataflow, and carries that decision through compilation. This is the discipline of [width inference](/spec/draft/width-inference/): the bit width of an integer, and whether it is signed at all, follow from the interval a value actually occupies, not from a keyword written at a declaration. The relationship between `len` and `memcpy`, invisible in C, becomes a property the compiler holds and checks. A simple design-time check can save years of *invisible* runtime exposure.

The counterpart function makes the discipline visible. Nothing in it declares a width or a sign:

```fsharp
let KSIZE = 1024
let kbuf : byte[] = Array.zeroCreate KSIZE

let copyFromKernel (userDest: byte[]) len =
    let safeLen = min KSIZE len
    Array.blit kbuf 0 userDest 0 safeLen
    safeLen
```

Trace the bug through our discipline and it has nowhere to form. The count handed to `Array.blit` occupies the range `[0, KSIZE]`. That range is non-negative, so the value is unsigned by construction. Signedness is derived from the range, not declared, and there is no signed form of this length for a caller to smuggle a negative value into. There is no `-2048` to pass, because `-2048` is not a value the length's type can hold. And because the representation is selected once, from the range, and carried to the boundary as a fact the compiler still holds, there is no second, different type name at the far end for the value to be reinterpreted against. The boundary that reinterpreted `int` as `size_t` is not a boundary Clef crosses. Both ends carry the same representation, chosen from the same range.

The bug is not caught by a check. A check is what failed in FreeBSD. The bug is absent because the state it depends on, a length that can be negative and a boundary that silently reinterprets it, is not representable. The range-derived representation is the [Native Type Universe's](/docs/design/types/bcl-to-ntu/) answer to exactly the class of failure that invisible coupling produces: it makes the coupling part of the type, so there is no longer a relationship for a contributor to know about and preserve.

The lowering keeps the decision visible. In MLIR terms, the clamp takes this shape:

```mlir
%ksize = arith.constant 1024 : i11
%safe  = arith.minui %len, %ksize : i11
```

MLIR integers are signless; `i11` says eleven bits and nothing more, the exact width the range `[0, KSIZE]` requires. The signedness the range decided appears as the choice of operation, `arith.minui` rather than `arith.minsi`, so the fact C hid in a type name sits in the instruction stream itself, where every later pass reads it. Both operands carry the same `i11`, and there is no boundary in sight for a second name to reinterpret.

That representation choice, once made, is not discarded on the way to the machine. It is carried through lowering as a fact the later stages hold, the same discipline that keeps [a closure's lifetime](/docs/design/memory/gaining-closure/) and a [value's dimensions](/docs/design/types/dimensional-type-safety/) intact from source to native code. A safety property established at design time is only worth having if it survives to the target, and [information the compiler establishes is not discarded in lowering](/docs/design/structure-and-performance/information-is-not-discarded/).

## The Curse of Systems Archaeology

The video that opens this post takes a bug hidden behind a correct-looking check and makes every step of it legible. Bugs of this shape sit in production code everywhere, in the quiet gap between a procedure's intent and the code's assumptions, and they reside in plain sight for decades precisely because the language makes them so hard to see. That is starting to change. Large language models are increasingly good at parsing details from code the way this video does, tracing a value across a boundary and flagging the assumption, and they will keep turning up anomalies wherever they lurk.

We would rather these bugs have nowhere to live than be found one at a time. That is much of why we are building full memory safety into the Fidelity Framework: the semantic this failure depends on becomes unrepresentable, which closes the category instead of chasing its instances. Our goal is that a bug like this someday reads as a relic from an earlier era of language design.

## Reading Further

- [Width Inference](/spec/draft/width-inference/) - the normative account of selecting representation, and signedness, from a value's analyzed range
- [Numeric Selection](/spec/draft/numeric-selection/) - the real-valued half of the same discipline, for posit, IEEE-754, and fixed-point
- [Clef: From BCL to NTU](/docs/design/types/bcl-to-ntu/) - the Native Type Universe that resolves types to native representations at compile time
- [Wrapping C and C++](/blog/wrapping-c-and-cpp/) - building type and memory safety around legacy code that has these boundaries
- [Fewer Tests; Greater Safety](/blog/fewer-tests-greater-safety/) - why a structural property beats a test that would have to anticipate the exact input
- [Information Is Not Discarded](/docs/design/structure-and-performance/information-is-not-discarded/) - how a design-time safety fact survives lowering to native code
