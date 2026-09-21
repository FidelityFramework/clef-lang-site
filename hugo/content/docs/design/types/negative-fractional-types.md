---
title: "Negative and Fractional Types"
linkTitle: "Negative & Fractional Types"
weight: 15
description: "A proposed native discipline for directed evaluation and value-indexed resources, with explicit composition, recovery and lowering obligations."
date: 2026-06-02
lastmod: 2026-09-21
authors: ["Houston Haynes"]
tags: ["NTU", "Reversibility", "Compact Closed Categories", "Formal Methods"]
---

Negative and fractional types would let Clef describe directed evaluation and value-sensitive resource use in its Native Type Universe. Their useful promise is that a compiler could keep those relationships available while choosing control flow, storage and a target realization. An application could use ordinary domain operations while Baker derives the supported obligations and instantiates the laws that justify them.

**Status: proposed extension.** The reference calculi supply operational foundations. Their integration with Clef's sharing, effects, inference and target representations still needs explicit rules and preservation arguments. The [specification glossary](/spec/draft/terms-and-definitions/) keeps these forms non-normative; the working companion `Composer/docs/Negative_Fractional_Types_Architecture.md` develops their proposed compiler contract. [A Path Less Traveled](/blog/a-path-less-traveled/) explains the relationship to native bidirectional composition and a quieter application interface. The working `Composer/docs/Bidirectional_Composition_Plan.md` tracks the coordinated work in one place.

## The two reference constructions

[James and Sabry](https://legacy.cs.indiana.edu/~sabry/papers/rational.pdf) distinguish additive and multiplicative extensions of reversible computation. [Chen and Sabry](https://homes.luddy.indiana.edu/sabry/files/2021/02/popl.pdf) subsequently give the fractional construction a value-indexed account. We propose the latter as Clef's first reference core, keeping logic-variable search as a separate research direction.

The additive construction gives a negative type `Neg<A>`. Its unit and counit turn evaluation between the positive and negative directions under the calculus's directed semantics. The signature `0 -> A + Neg<A>` does not describe allocating a tuple containing two ordinary values. Here `0` is the additive unit and `+` is a sum; ordinary Clef `unit` has a different meaning. Reading the construction as a bijection between ordinary sets would miss its operational interpretation.

The value-indexed fractional construction pairs a particular `v : A` with a resource indexed by that value. Discharging the resource against `w : A` requires the specified match with `v`; the reference calculus includes failure on a mismatch. A provisional surface name such as `Recip<A>` therefore leaves information that the elaborated construction must retain: the value index, the pairing instance and its usage conditions. The separate logic-variable calculus instead creates a variable whose resolution participates in its search semantics. Choosing one account changes the operational contract.

The categorical conclusion also needs care. Chen and Sabry's [February 7, 2021 erratum](https://homes.luddy.indiana.edu/sabry/files/2021/02/errata.pdf) corrects Theorem 25's inverse-category claim: generalized inverses need not be unique. It does not withdraw Theorem 24's compact-closure result or the separate pointed fractional construction. A selected reversal operation can still have checked laws; its selection and those laws must remain explicit.

Compact closure itself supplies dual objects and unit/counit maps satisfying snake equations. It does not make every program invertible, make every dagger unitary, or make every object a tensor inverse. The extension must state which additional equations its operations establish.

## Bidirectional composition and reversal

A computation can receive requirements from a later use while producing values for that use. The compiler can represent both dependencies without promising to recover an earlier runtime state. For example, a required odd output from an addition constrains its inputs but does not uniquely identify them. Reversibility adds an inverse or reconstruction law over an admitted state domain.

The distinction matters for library composition. Two state constructions can share the arrow shape `S -> (A, S)` while connecting intermediate state in different directions. Baker needs the selected operation's semantics to establish the connection. Removing a source wrapper does not remove that obligation. A Haskell reference can help specify the composition laws without determining Clef's public names or requiring a dedicated application-facing monadic builder.

Nor does every reversible operation need a backup beside its result. Consider a model whose existing state consists of coordinates `q` and momenta `p`. This schematic update has a computed inverse:

```text
forward:                    reverse:
    p := p + F(q)               q := q - G(p)
    q := q + G(p)               p := p - F(q)
```

The equalities hold for total pure `F` and `G` under unchanged parameters, with exact addition/subtraction on the admitted domain or explicitly modular arithmetic. Reverse order restores the inputs needed to recompute each increment. The two state components already belong to the model; no sequence of their earlier values is required. Ordinary rounded floating-point updates need a further argument. [JANUS](https://arxiv.org/abs/1704.07715) is a concrete example of constructing bitwise reversible numerical updates with integer and floating-point arithmetic.

For a many-to-one operation, enough distinguishing information must instead remain available or be reproducible. The recovery policy can use a smaller residual, a checkpoint, or deterministic replay. An inverse recipe and its proof are compiler information; retained execution values consume runtime storage. The [Lyapunov window](/docs/design/types/lyapunov-window/) describes a proposed error-bounded reconstruction policy and its separate numerical and storage obligations.

## Dimensions, resources and ordinary sharing

The proposed notations describe different kinds of information:

```text
Neg<float<N>>       a force value participating in directed reverse evaluation
Recip<float<N>>     provisional notation for a value-sensitive force resource
float<N^-1>         a numerical quantity with reciprocal-force dimension
```

A reciprocal resource does not automatically invert a physical dimension. Rational measure exponents are likewise a separate dimensional extension. Kennedy-style dimensional unification can supply one component of the analysis; it does not establish value matching, inverse laws or resource usage.

Clef's lifetime analysis and flat closure representation help identify where a value lives and which delayed computation can access it. Those properties do not make all values linear or all operations information-preserving. Ordinary immutable sharing must coexist with explicit rules for exclusive resources. Copying a frame or forcing a memoized value twice cannot silently create two independently consumable instances of one resource.

A captured immutable scalar retains its value across a delay. A captured mutable location retains its storage identity while its contents may change. Reversal or resource discharge must establish which value it actually needs. BAREWire schemas and lifetime contracts extend the representation account across boundaries, while delivery, external effects and recovery require their own protocols.

## One graph with joint relationships

The PHG gives an operation and its requirements shared participant identities. Some facts can be attached to one node, such as a known dimension. Other obligations relate several participants: an operation, a value index, a resource instance, a later consumer and their storage. A typed hyperedge makes that relation explicit. It is part of the program's semantic representation, not a second running computation.

For this extension the relation would retain:

- the selected operation and its ordered inputs and outputs;
- the carrier type, value index and scoped pairing instance;
- the allowed resource uses and matching or failure behavior;
- the applicable law, its premises and discharge evidence;
- any retained runtime information required by the selected realization.

Static source identities also need an interpretation for repeated execution. Two calls to the same allocation site do not create the same resource instance. Likewise, attaching a proof-obligation label does not establish the obligation. A lowering must preserve the checked relationship or re-establish the property its target operations require.

The analyses have different mathematical structures. Dimensional equations, lifetime orders, support joins and value equalities need their own transfer rules and sound interfaces. A particular analysis may use a lattice to organize improving knowledge. That does not make the hypergraph itself a lattice or give the combined system one unification algorithm and complexity bound.

## Resolution without application ceremony

The intended application path uses domain operations whose laws are admitted by the framework or library author. Baker identifies the construction, derives its obligations and checks the available premises. Alex witnesses the settled construction into a permitted target realization. [Proof composition and tooling](/docs/internals/verification/proof-composition-and-tooling/) describes how reusable laws can support this ordinary-code workflow without requiring a handwritten proof at each use.

A matching condition known statically can be discharged before execution. A contract that permits runtime matching must preserve both its success and failure behavior. A required static condition that remains unresolved blocks the boundary relying on it. A solver's satisfying assignment cannot replace an actual input, and convergence of compiler analysis does not prove productivity of a source-level recursive value.

The memory objective is similarly concrete. Keep current state and required live work; retain recovery information only where the selected contract requires it. For a fixed number of forward-AD directions, current tangents need not form a history tape. Tangents carry derivatives, however, and do not themselves establish an inverse. Representation, reconstruction tolerance, checkpoint policy and peak live storage should meet in the same region's constraints before a realization is selected.

These mechanisms could support reversible simulation, controlled resource discharge and recovery-aware model updates. Bayesian evidence, quantum unitarity and adiabatic approximation each add their own domain laws. A common graph can preserve their connections while keeping the conclusions distinct. The useful compiler result is a supported construction with an inspectable justification and a realizable resource budget.
