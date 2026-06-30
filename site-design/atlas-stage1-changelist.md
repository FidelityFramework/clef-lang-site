# Stage-1 Cross-Link Changelist (for review)

**28 honest edges** (after one-anchor collapse), connecting **12 spec kernel nodes** (8→20 of 54).
Each link wraps a phrase in the source page with `[phrase](/spec/draft/<slug>/)`. No edits applied yet.

Legend: `[layer/kind]` source-layer / reference-classification.


## → /spec/draft/discriminated-union-representation/  (6 inbound)

1. **docs/internals/mlir/hello-world-goes-native.md** `[docs/implementation]`
   - anchor sentence: _Composer represents discriminated unions using MLIR's type system. A Result type compiles to a tagged representation where the tag indicates which case is active:_
   - wrap phrase: `Composer represents discriminated unions using MLIR's type system`

2. **blog/fpga-and-hardware-inference.md** `[blog/implementation]`
   - anchor sentence: _The `Color` discriminated union has 8 cases. Its tag value ranges from 0 to 7: 3 bits. `Mode` has 2 cases: 1 bit._
   - wrap phrase: `The `Color` discriminated union has 8 cases. Its tag value ranges from 0 to 7: 3 bits`

3. **docs/design/memory/memory-management-by-choice.md** `[docs/architectural]`
   - anchor sentence: _Immutable records map to contiguous memory blocks, discriminated unions correspond to tagged memory layouts, and higher-order functions often resolve statically._
   - wrap phrase: `discriminated unions correspond to tagged memory layouts`

4. **blog/runtime-revolution-fidelity.md** `[blog/implementation]`
   - anchor sentence: _field order in a discriminated union_
   - wrap phrase: `If the two compilers disagreed about field order in a discriminated union`

5. **docs/design/javascript-targeting/streaming-inference-through-the-actor-pipeline.md** `[docs/implementation]`
   - anchor sentence: _The Clef compiler verifies this type at design time. The BAREWire schema is derived from it: three cases, three tags, each with a fixed payload layout._
   - wrap phrase: `three cases, three tags, each with a fixed payload layout`

6. **docs/internals/pipeline/intelligent-tree-shaking.md** `[docs/implementation]`
   - anchor sentence: _When analyzing discriminated unions, we track not just type usage but individual case usage:_
   - wrap phrase: `When analyzing discriminated unions, we track not just type usage but individual case usage`


## → /spec/draft/units-of-measure/  (5 inbound)

7. **docs/design/types/dimensional-type-safety.md** `[docs/architectural]`
   - anchor sentence: _This is where dimensional type safety becomes essential. Units of measure express constraints that are orthogonal to execution model._
   - wrap phrase: `Units of measure express constraints that are orthogonal to execution model`

8. **docs/design/types/negative-fractional-types.md** `[docs/architectural]`
   - anchor sentence: _Kennedy's units of measure_
   - wrap phrase: `Kennedy's units of measure established the pattern: dimensional consistency reduces to unification o`

9. **docs/design/memory/spatial-mechanics.md** `[docs/implementation]`
   - anchor sentence: _units of measure_
   - wrap phrase: `units of measure Clef inherits from its F# lineage, our dimensional types carry physical meaning thr`

10. **blog/danger-close-why-types-matter.md** `[blog/justification]`
   - anchor sentence: _Our Clef language carries units of measure with zero runtime cost._
   - wrap phrase: `The system allowed temperature values to exist without their units of measure`

11. **blog/high-speed-inference.md** `[blog/exemplar]`
   - anchor sentence: _One advantage of [our Clef language](https://clef-lang.com) for inference is its zero-cost units of measure system, which carries dimensional correctness into physics-aware models. Clef inherits this _
   - wrap phrase: `its zero-cost units of measure system, which carries dimensional correctness into physics-aware mode`


## → /spec/draft/platform-bindings/  (4 inbound)

12. **docs/design/interop/library-binding.md** `[docs/architectural]`
   - anchor sentence: _This document covers one part of that work: the hybrid library binding architecture._
   - wrap phrase: `hybrid binding architecture`

13. **docs/internals/farscape/binding-cpp-to-clef-in-farscape.md** `[docs/justification]`
   - anchor sentence: _A sophisticated generator that produces complete Clef modules with `[<FidelityExtern>]` attributed binding declarations, proper lifetime management, error handling, and optimization metadata for LLVM _
   - wrap phrase: `Farscape will include comprehensive test generation to validate ABI compatibility`

14. **docs/design/structure-and-performance/source-level-dependency-resolution.md** `[docs/architectural]`
   - anchor sentence: _The `[<FidelityExtern>]` attribute is a **quotation carrier** — CCS recognizes it during compilation, emits an opaque extern node in the PSG (Program Semantic Graph) with the library and symbol metada_
   - wrap phrase: `Farscape generates up to three layers of output`

15. **blog/the-farscape-bridge.md** `[blog/architectural]`
   - anchor sentence: _Layer 1 binding declarations and Layer 2 idiomatic wrappers_
   - wrap phrase: `generated bindings carry through to the same native output`


## → /spec/draft/atomic-operations/  (3 inbound)

16. **docs/internals/hardware/cache-aware-compilation-cpu.md** `[docs/architectural]`
   - anchor sentence: _The C++ memory model, standardized in C++11 and refined since, represents a necessary response to the realities of modern hardware. Yet its complexity burdens every developer who touches concurrent co_
   - wrap phrase: `Our designs lead us to a layered model where this complexity exists but remains contained. Library i`

17. **docs/internals/hardware/cache-aware-compilation-gpu.md** `[docs/architectural]`
   - anchor sentence: _Memory ordering on GPUs differs fundamentally from CPU models. Where CPUs provide strong ordering guarantees (x86's TSO model, for instance, ensures stores become visible in program order), GPUs emplo_
   - wrap phrase: `Memory ordering on GPUs differs fundamentally from CPU models`

18. **docs/internals/hardware/rdna-unified-memory-desktop.md** `[docs/implementation]`
   - anchor sentence: _Fidelity's semantic graph captures the full data flow from NPU to GPU to CPU. When Alex generates MLIR for this pipeline, it can identify cross-agent handoff points and insert appropriate fences autom_
   - wrap phrase: `it can identify cross-agent handoff points and insert appropriate fences automatically`


## → /spec/draft/access-kinds/  (3 inbound)

19. **blog/doubling-down-dmm-dts.md** `[blog/implementation]`
   - anchor sentence: _The `ReadOnly` access mode is a dimension. The `4<bytes>` alignment is a dimension. The `celsius` unit is a dimension. They all constrain how the compiler generates code for this memory location. They_
   - wrap phrase: `The `ReadOnly` access mode is a dimension`

20. **docs/design/types/bcl-to-ntu.md** `[docs/architectural]`
   - anchor sentence: _### Access Kinds_
   - wrap phrase: `cannot write to ReadOnly pointer`

21. **docs/internals/hardware/on-metal-revisited.md** `[docs/implementation]`
   - anchor sentence: _Semantic preservation. The parser captures `__I`, `__O`, and `__IO` qualifiers as first-class constructs, mapping them directly to `AccessKind` values that flow through the compilation pipeline._
   - wrap phrase: `mapping them directly to `AccessKind` values`


## → /spec/draft/memory-regions/  (1 inbound)

22. **docs/internals/hardware/on-metal-revisited.md** `[docs/implementation]`
   - anchor sentence: _Memory regions inform volatile semantics_
   - wrap phrase: `This quotation encodes everything the compiler needs to generate correct memory-mapped access: base `


## → /spec/draft/width-inference/  (1 inbound)

23. **blog/fpga-and-hardware-inference.md** `[blog/architectural]`
   - anchor sentence: _Machine classification and width inference are two instances of the same principle: the compiler reads your code, derives a physical property from its structure, and acts on it before you reach the sy_
   - wrap phrase: `width inference`


## → /spec/draft/closure-representation/  (1 inbound)

24. **docs/design/memory/gaining-closure.md** `[docs/implementation]`
   - anchor sentence: _flat closures_
   - wrap phrase: `Implementing flat closures in our Composer compiler required careful orchestration`


## → /spec/draft/type-definitions/  (1 inbound)

25. **blog/discriminated-unions-post-transformer-ai.md** `[blog/exemplar]`
   - anchor sentence: _Clef discriminated unions_
   - wrap phrase: `Clef discriminated unions could provide a more natural representation`


## → /spec/draft/lazy-representation/  (1 inbound)

26. **docs/design/structure-and-performance/why-lazy-is-hard.md** `[docs/architectural]`
   - anchor sentence: _Our implementation builds directly on the flat closure architecture described in [Gaining Closure](/docs/design/memory/gaining-closure/), itself an extension of techniques pioneered in Standard ML com_
   - wrap phrase: `A lazy value is a flat closure with additional fields for memoization state`


## → /spec/draft/reactive-signals/  (1 inbound)

27. **blog/getting-the-signal-with-barewire.md** `[blog/implementation]`
   - anchor sentence: _This hybrid provides the ergonomics of implicit tracking (signal reads within a tracked scope are automatically captured) while making scope boundaries explicit._
   - wrap phrase: `signal reads within a tracked scope are automatically captured`


## → /spec/draft/platform-predicates/  (1 inbound)

28. **docs/design/types/bcl-to-ntu.md** `[docs/architectural]`
   - anchor sentence: _platform predicates_
   - wrap phrase: `NTU introduces platform predicates as a more principled alternative`


## Honestly unlinked (no genuine site — correct, not a gap)

- `/spec/draft/lexical-analysis/` — no genuine discussion survived verification
- `/spec/draft/ffi-boundary/` — boundary-by-design at the chapter level; the flat-closure-marshaling sub-thread is a real future link target (owner note)