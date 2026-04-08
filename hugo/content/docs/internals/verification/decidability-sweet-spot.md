---
title: "The Decidability Sweet Spot"
linkTitle: "The Decidability Sweet Spot"
description: "How DTS maps dimensional constraints to Z3's QF_LIA logic fragment for microsecond-scale transparent verification"
weight: 20
date: 2026-02-25
authors: ["Houston Haynes"]
tags: ["Formal Methods", "Z3", "Type Systems"]
params:
  originally_published: 2026-02-25
  migration_date: 2026-02-25
---

> This article is part of the [Transparent Verification](..) series. It builds
> on [From Double Annotation to Discovery](../double-annotation-discovery) to explain the
> algebraic foundation that makes zero-annotation verification decidable.

## DTS: A Distinct Formal Category

The fundamental advantage of the Dimensional Type System (DTS) is its restriction to a specific algebraic niche that general dependent type systems cannot exploit.

A dimensional type system assigns to each numeric value a dimension drawn from a finitely generated free abelian group. The base dimensions (length, time, mass, temperature, electric current, luminous intensity, amount of substance) generate the group under multiplication, with integer exponents. Formally, let \(\mathcal{D} = \mathbb{Z}^n\) be the dimension space. Each dimension \(d \in \mathcal{D}\) is a vector of integer exponents:

\[d_{\text{velocity}} = (1, -1, 0, 0, \ldots) \quad \text{(length}^1 \cdot \text{time}^{-1}\text{)}\]

\[d_{\text{force}} = (1, -2, 1, 0, \ldots) \quad \text{(length}^1 \cdot \text{time}^{-2} \cdot \text{mass}^1\text{)}\]

Dimensional consistency of an arithmetic expression reduces to linear algebra over \(\mathbb{Z}\): addition requires operand dimensions to be equal; multiplication adds exponent vectors; division subtracts them; exponentiation scales them. These operations are closed in \(\mathbb{Z}^n\) and decidable in \(O(n)\) per operation.

This is the critical distinction from dependent types. A dependent type can encode an *arbitrary predicate* over values. Checking whether two dependent types are equal may require proving an arbitrary theorem. Dimensional consistency checking requires comparing two integer vectors, a constant-time operation per base dimension.

General dependent type systems are subject to undecidability and may require timeout heuristics and fuel limits during SMT solving. Because DTS constraints reduce to linear algebra over integers, they map to one of Z3's most well-studied, guaranteed-decidable logic fragments: **`QF_LIA`**. CCS is designed to ask Z3 to solve a bounded system of linear equations. Z3 resolves these `QF_LIA` obligations in microseconds, guaranteeing the polynomial-time inference required for real-time language server responses.

| Property | DTS | Dependent Types |
|---|---|---|
| Type checking | Decidable (linear algebra over \(\mathbb{Z}\)) | Undecidable in general |
| Inference | Complete and principal | Incomplete; requires annotations |
| Runtime representation | No runtime cost; metadata only | May require runtime witnesses |
| Expressiveness | Abelian group constraints on numeric types | Arbitrary predicates over values |
| Proof obligations | Derived automatically from PSG structure | May require interactive proof |
| Compilation model | Attributes that guide code generation | Types that participate in code generation |

The analogy is to regular expressions and context-free grammars. Regular expressions are a distinct formal class with distinct closure properties, distinct recognition algorithms, and distinct practical applications. DTS occupies an analogous position relative to dependent types: a distinct formal class that happens to overlap in expressive power for a specific domain but differs in every computational property that matters for practical tooling.

## Design-Time Verification: The Transparent Z3 Partner

Integrating Z3 directly into CCS to handle decidable SMT proof obligations is what makes transparent verification possible. The verification process is designed to happen continuously at design time. As the developer types, Lattice will traverse the PSG and invoke Z3 in the background. The NTU simultaneously acts as the proof apparatus for Z3, deriving proof obligations from the PSG's structure. Every arithmetic operation in the PSG produces a Z3 assertion, governed by the fixed rules of dimensional algebra. The developer writes zero proof code.

### The Gravitational Force Example

Consider a developer writing a standard gravitational force calculation:

```fsharp
let computeForce mass1 mass2 distance =
    let g = 6.674e-11 // compiler knows this is m^3 * kg^-1 * s^-2
    g * mass1 * mass2 / (distance * distance)
```

The developer provides no proofs and no dependent-type annotations. Here is what CCS is designed to do silently as it builds the PSG nodes:

**Step 1: Variable Assignment.** CCS assigns integer dimension vectors to every variable. In SMT-LIB2 format, this would generate:

```lisp
(declare-const d_m1_kg Int)
(declare-const d_m2_kg Int)
(declare-const d_dist_m Int)
```

**Step 2: Operation Constraints ("Natural Bounds").** When CCS processes `distance * distance`, it knows multiplication means *adding* dimensional exponents. It would automatically generate the Z3 constraint:

```lisp
;; d(denom) = 2 * d(distance)
(assert (= d_denom_m (+ d_dist_m d_dist_m)))
```

**Step 3: Division Constraints.** For the final division, CCS knows division means *subtracting* exponents:

```lisp
;; d(return) = d(numerator) - d(denom)
(assert (= d_ret_m (- d_num_m d_denom_m)))
```

**Step 4: Boundary Constraints.** If the function is called with typed arguments such as `mass1 : float<kg>`, `mass2 : float<kg>`, and `distance : float<m>`, CCS treats those as hard assertions:

```lisp
(assert (= d_m1_kg 1))
(assert (= d_m2_kg 1))
(assert (= d_dist_m 1))
```

Z3 then verifies if the *inferred* constraints (naturally derived from the code operations) match the *explicit* boundary constraints provided by the developer. The result:

- `d_g` resolves to `m^3 · kg^-1 · s^-2` (the gravitational constant's natural dimension)
- Return dimension: `m^3 · kg^-1 · s^-2 + kg + kg - 2·m = kg · m · s^-2 = newtons`

Because this is just basic integer addition and subtraction over a bounded system, Z3 solves it instantly and returns `SAT`. CCS then stamps the PSG node with its proof certificate. The proof cert is generated before MLIR lowering, with the syntactic footprint of standard F#.

### Dimensionally Polymorphic Inference

Without any type annotations at all, the function remains dimensionally polymorphic. The DTS inference assigns dimension variables `'d_g`, `'d_m1`, `'d_m2`, `'d_dist` and propagates constraints through the expression tree via extended Hindley-Milner unification. The inferred return type is `float<'d_g + 'd_m1 + 'd_m2 - 2 * 'd_dist>`, a parametric family. Only when the function is called with concrete dimensional types does unification resolve the full system.

A function `let scale factor value = factor * value` infers type `float<'d1> -> float<'d2> -> float<'d1 * 'd2>` without any annotation. The inference is complete (every dimensionally consistent program can be typed without annotation), principal (the inferred type is the most general), and decidable (the constraint system is finite and the solution algorithm terminates). These properties are shared with standard Hindley-Milner inference and are *not* shared with dependent type inference in general.

### Deprecating the "Double Annotation"

If a developer were to attempt a dependent-type-style annotation in Clef, something like `[<Requires(dim_a = dim_b + dim_c)>]`, the DTS would make it redundant. CCS would have already generated that exact constraint from the arithmetic operations in the code.

If a developer explicitly annotates a boundary (the "push model"), like `(m1: float<kg>)`, CCS treats that as a hard assertion in Z3: `(assert (= d_m1_kg 1))`. Z3 then verifies whether the inferred constraints naturally derived from the code operations match the explicit boundary constraints. If they conflict, Z3 returns `UNSAT`, and Lattice will highlight the exact line of code where the physics broke down.

By making the NTU responsible for translating standard arithmetic operators into SMT linear equations, the developer is isolated from theorem proving entirely.

## From UNSAT Cores to Actionable Diagnostics

Because the SMT proofs are resolved at design time within the saturated PSG, Lattice will be able to exploit them for precise diagnostics.

When Z3 returns `UNSAT` for a set of constraints, the plan is for Lattice to produce an **unsat core**, the exact subset of conflicting constraints. CCS translates that mathematical core back into the specific PSG edges that caused the conflict.

If an engineer attempts to accumulate gradients of dimension \(\langle \text{newtons} / \text{meters} \rangle\) with \(\langle \text{joules} / \text{seconds} \rangle\), Lattice will highlight the *exact operation* and explain the physical impossibility, backed by a formal mathematical proof, all without leaving the editor.

### Closure Under Differentiation

This capability extends naturally to auto-differentiation. The dimensional algebra is closed under differentiation: if \(f : \mathbb{R}^{\langle d_1 \rangle} \to \mathbb{R}^{\langle d_2 \rangle}\), then:

\[\frac{\partial f}{\partial x} : \mathbb{R}^{\langle d_2 \cdot d_1^{-1} \rangle}\]

The gradient of a loss function with dimension \(\langle \text{loss} \rangle\) with respect to a parameter with dimension \(\langle d \rangle\) carries dimension \(\langle \text{loss} \cdot d^{-1} \rangle\). This follows from the abelian group structure: differentiation is division in the dimensional algebra, and division is closed in \(\mathbb{Z}^n\). The inference algorithm extends to auto-differentiation graphs without modification. In a physics-informed model where the loss function includes terms with physical units (force residuals in newtons, energy conservation violations in joules), DTS would verify that gradient accumulation respects dimensional consistency, decidably, without annotation, and with no runtime overhead for the verified properties.

The [next article](../memory-coeffect-algebra) extends the transparent verification model from dimensional constraints to memory safety.
