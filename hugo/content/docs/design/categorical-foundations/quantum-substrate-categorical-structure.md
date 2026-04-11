---
title: "The Quantum Substrate: Categorical Structure and the Hardware Maturity Gap"
linkTitle: "Quantum Substrate"
description: "What Categorical Foundations Provide for Quantum, and What They Do Not"
date: 2025-09-07T10:00:00+06:00
weight: 05
authors: ["Houston Haynes"]
tags: ["Architecture", "Innovation"]
params:
  originally_published: 2025-09-07
  original_url: "https://speakez.tech/blog/quantum-substrate-categorical-structure/"
  migration_date: 2026-02-15
---

## The Categorical Structure of Quantum Mechanics

Quantum mechanics has been categorical since before computer scientists adopted the vocabulary. Abramsky and Coecke's work on categorical quantum mechanics [1] formalized what physicists had been using informally: quantum processes compose as morphisms in a dagger compact category, a monoidal category with a contravariant involution that captures the adjoint (conjugate transpose) operation on Hilbert spaces (\(\dagger\)-compact category).

In concrete terms:

- **Objects** are Hilbert spaces (the state spaces of quantum systems)
- **Morphisms** are completely positive maps (quantum channels, including unitary evolution and measurement)
- **The dagger** (\(\dagger\)) assigns to each morphism its adjoint: if \(U\) is a unitary gate, then \(U^\dagger\) is its conjugate transpose, satisfying \(UU^\dagger = U^\dagger U = I\)
- **The monoidal structure** (\(\otimes\)) captures tensor products: the state space of a composite quantum system is the tensor product of its components

This is the same adjoint structure that appears in [the CDL paper's treatment of neural networks](/blog/categorical-deep-learning-adjoint-correspondence/) and in the HPC adjoint method. The forward/backward duality that unifies backpropagation with sensitivity analysis has a third instance in quantum mechanics: unitary evolution paired with its conjugate.

The mathematics is not an analogy. The composition laws, naturality conditions, and coherence constraints are identical across all three domains. The substrate differs; the algebraic structure does not.

## The Q# Lineage

Microsoft Research's Q# language provides concrete evidence of the alignment between ML-family languages and quantum computation. John Azariah [documented the design process](https://johnazariah.github.io/2018/12/04/tale-of-two-languages.html) of building Q# from F#, and the result is instructive: F#'s computation expressions, algebraic data types, and type inference translated naturally to quantum circuit construction because the categorical structures are compatible.

This is not unique to F#. Any ML-family language with higher-order functions and algebraic data types can express quantum circuits. The relevant property is that the language supports composition of typed morphisms, which is the categorical structure that quantum circuits exhibit.

For the Fidelity framework, the implication is specific: the PSG's mechanisms for tracking forward/backward relationships, propagating coeffects, and verifying composition at target boundaries are architecturally compatible with quantum circuit compilation. A quantum target would slot into the existing multi-target framework as another compilation backend, with its own representation profile (qubit states, gate fidelities, error rates) and its own transfer boundary analysis (classical/quantum interface).

## The Hardware Maturity Gap

The categorical compatibility between our software infrastructure and quantum computation does not mean that quantum compilation is imminent. The gap between mathematical structure and practical hardware is substantial, and honest accounting requires stating it plainly.

**Gate fidelities.** Current superconducting qubit systems (IBM Eagle, Google Sycamore) achieve two-qubit gate fidelities in the 99.7-99.9% range for specific gate types, and trapped-ion platforms (IonQ, Quantinuum) have reported fidelities above 99.9%. For algorithms requiring thousands of sequential gate operations, the cumulative error still renders the output unreliable without error correction, though the per-gate baseline has improved materially since this post was first written in September 2025.

**Error correction overhead.** Fault-tolerant quantum computing requires quantum error correction codes that encode each *logical* qubit in many *physical* qubits. As of this post's original writing in September 2025, the commonly cited overhead for near-term fault tolerance was approximately 1,000 physical qubits per logical qubit, derived from surface-code analyses at the physical error rates of that era. A useful computation requiring 100 logical qubits would have needed approximately 100,000 physical qubits under that baseline, while the largest systems then available held on the order of 1,000 to 1,100 physical qubits total.

The overhead baseline compressed substantially over the months that followed. Recent work on neural decoders for bivariate bicycle codes in the qLDPC family, specifically the [[144, 12, 12]] Gross code, has demonstrated utility-scale logical error rates (around \(10^{-10}\)) at physical error rates of 0.1% when the decoder is a geometry-aware convolutional model rather than a classical belief-propagation pipeline. That configuration encodes 12 logical qubits in 144 physical qubits, a ratio of 12:1 rather than 1,000:1, for approximately two orders of magnitude of compression over the surface-code baseline. The compression is confined to specific parameter regimes and is conditional on the decoder being a learned component rather than an algebraic one; with classical BP-OSD decoding, the ratio collapse does not hold. The practical consequence, and the one that matters for the CRQC timeline framing, is that the threshold where fault-tolerant quantum computing becomes resource-feasible has moved closer in both the physical and logical qubit counts required for specific problem classes. Taken together with [Google's March 2026 ECDLP resource estimates](/blog/cryptographic-certainty/#update-april-2026) and the [Cain et al. neutral-atom result](https://arxiv.org/abs/2603.28627) referenced in that same update, the trajectory is the one the Mosca Moment analysis predicted: a CRQC horizon that narrows faster in practice than the early estimates assumed.

**Decoherence timescales.** Superconducting qubits maintain coherence for approximately 100 microseconds. Gate operations take approximately 20-100 nanoseconds. This limits circuit depth to roughly 1,000-5,000 gates before decoherence dominates, even without accounting for gate errors.

**Connectivity constraints.** Physical qubit architectures have limited connectivity (typically nearest-neighbor on a 2D grid). Algorithms that require arbitrary qubit interactions must route through SWAP gates, increasing circuit depth and error accumulation.

These constraints are hardware limitations, not software limitations. They are being actively addressed by the quantum computing community through improved qubit designs, better error correction codes, and alternative physical substrates (trapped ions, photonic systems, topological qubits). Progress is real but incremental.

The cellular sheaf framework developed in [the compilation sheaf design document](/docs/design/categorical-foundations/the-compilation-sheaf/) gives a precise vocabulary for what quantum error correction is doing, and it identifies a research direction that the framework's structure makes tractable to pursue. Gate errors in a quantum circuit produce local inconsistencies in the circuit sheaf: the stalk at a noisy gate carries an error component that is not consistent with what the structure maps from upstream and downstream gates expect. This is a non-trivial \(H^1\) obstruction to extending the local stalks into a global section over the entire circuit. Quantum error correction codes are algorithms for resolving these obstruction classes by encoding logical qubits in many physical qubits and computing syndromes that identify which obstruction class is present. The surface code and other topological codes are, in this reading, algorithms for finding cocycle witnesses that kill specific \(H^1\) classes of the noisy circuit sheaf.

This framing positions QEC as a target the framework's existing machinery is structurally aligned with. The compilation sheaf already supports stalks of arbitrary categories, structure maps that may have non-trivial kernels, and a dual-pass discharge mechanism that witnesses global sections at every edge of the compilation poset. A quantum target would slot into this machinery as another stalk category (qubit states with completely positive maps as morphisms), and the QEC layer would sit at the boundary where the framework's formally verified fragments hand off to an empirically validated decoder. Each code (surface code, color code, bivariate bicycle code) has an algebraic part the compiler can reason about directly and a decoder part that depends on the full error distribution rather than on the code's algebraic structure alone. The algebraic part fits the framework's parameterized-lemma pattern: specify the code, the noise model, and the connectivity graph, and the compiler instantiates a cocycle witness against them. The decoder part is a learned function whose coefficients come from training against the noise distribution, and the framework's role there is to carry the input specifications and the output bounds across the interface rather than to derive the decoder's behavior from the algebraic structure. Writing the algebraic layer would require collaboration between formal methods researchers, quantum information theorists, and hardware specialists. The decoder layer is an empirical research program in its own right, and the compilation sheaf's contribution there is the structural discipline that keeps the interface between the algebraic and empirical layers honest.

The practical consequence is that improving the physical-to-logical ratio is split between hardware work (lowering physical error rates), algebraic work (designing better codes with favorable distance and rate properties), and empirical work (training decoders to exploit the full error distribution rather than the minimum-weight representatives). The framework has natural positions for the first two, and the compilation sheaf's contribution is the interface discipline that keeps the third connected to them without needing to formally verify its internal structure. The contribution of the cohomological framing is that it gives the framework a place to *put* the QEC layer when the hardware is ready, with the algebraic discipline on one side and the empirical decoder discipline on the other, both connected by the compilation sheaf's structure-map vocabulary. The 1,000-to-12 compression in the transition from surface-code baselines to BB-code results with learned decoders is evidence that the decoder side carries as much of the burden as the code side, and that practical fault tolerance depends on the framework accommodating both kinds of work rather than treating the decoder as a black box handed to it by the hardware.

## What "Quantum-Ready" Means

Given the hardware maturity gap, what does it mean for a software framework to be "quantum-ready"? The answer is modest but specific.

**Architectural compatibility.** The PSG's multi-target compilation architecture does not need structural modification to accommodate a quantum backend. The mechanisms for target-specific representation selection, coeffect tracking, and cross-target transfer analysis generalize to quantum targets. When the compiler selects representations for a quantum target, it evaluates gate decompositions and qubit connectivity constraints using the same optimization framework that selects posit widths for FPGA targets.

**Transfer boundary analysis.** The classical/quantum interface is a transfer boundary with specific characteristics: classical data must be encoded into quantum states (preparation), quantum computation produces classical outcomes (measurement), and the encoding/decoding has a fidelity profile that DTS can analyze. This is structurally identical to the FPGA/CPU transfer boundary, where posit32 values encode to float64 with quantifiable precision loss.

**Verification infrastructure.** The coeffect system's capability tracking could express quantum hardware constraints: qubit count, connectivity topology, gate set, coherence time, and error rates. A computation that requires more qubits than available, or deeper circuits than coherence permits, would produce a capability coeffect failure, just as a computation requiring exact accumulation produces a failure on neuromorphic targets.

What "quantum-ready" does *not* mean is that Fidelity implements quantum circuit compilation today, or that quantum backends will produce useful results on current hardware. The framework accommodates quantum as an eventual target without requiring architectural rework. This is a property of the design, not a shipping feature.

## The Hybrid Computing Model

The more immediate value of categorical compatibility is in hybrid classical/quantum workflows, where a classical optimizer drives a quantum subroutine (the variational quantum eigensolver pattern). In this model:

```mermaid
graph LR
    C1[Classical Optimizer<br>x86, float64] -->|"Parameters θ"| Q[Quantum Circuit<br>QPU, qubit states]
    Q -->|"Measurements"| C2[Classical Post-Processing<br>x86, float64]
    C2 -->|"Gradient estimate"| C1
```

The classical components run on conventional targets with conventional numeric representations. The quantum component executes a parameterized circuit and returns measurement outcomes. The PSG tracks the full loop: parameter preparation (classical), circuit execution (quantum), measurement (classical/quantum boundary), and gradient estimation (classical).

The dimensional type system verifies consistency across the loop. Parameters with physical dimensions (energy in Hartrees, distance in Bohr radii) must maintain dimensional consistency through the quantum circuit and back. The coeffect system tracks the transfer boundary: which values cross the classical/quantum interface, what encoding is used, and what measurement fidelity is expected.

This hybrid model is tractable on near-term quantum hardware because the quantum component is a subroutine with bounded depth, not an entire computation. The error correction overhead is reduced because the circuit depth is short. The classical components handle the optimization loop, gradient estimation, and error mitigation, all of which benefit from the DTS/DMM infrastructure regardless of whether the quantum component is simulated or runs on physical hardware.

## Timeline Expectations

For the Fidelity framework specifically:

**Present.** The PSG architecture supports multi-target compilation. CPU and FPGA targets compile through the MLIR pipeline. The categorical structure of the framework is compatible with quantum backends. Quantum circuit simulation can be targeted as a classical computation with standard compilation.

**Near-term (1-2 years).** Hybrid classical/quantum workflows are the practical frontier, and the resource estimates that have landed since the original writing of this post have compressed the near-term envelope by roughly a year. The framework's transfer boundary analysis applies to the classical/quantum interface, and variational algorithms with shallow quantum circuits are the target use case. The qLDPC and learned-decoder compression discussed above means that some fault-tolerant subroutines now fit inside the near-term window for specific problem sizes, which would not have been the case under the surface-code baseline.

**Medium-term (3-5 years).** Fault-tolerant quantum computing with sufficient logical qubits for genuine algorithmic advantage over classical computation, for the specific problem classes the BB-code and learned-decoder compression favors (quantum chemistry, optimization, sampling). The boundary between near-term and medium-term depends on which decoder architecture the deployed hardware supports: classical BP-OSD pipelines keep the boundary at the earlier surface-code estimate, and geometry-aware learned decoders move it substantially closer. The framework's role is the same in both cases, which is what makes the categorical compatibility claim robust to the specific hardware timeline.

**Long-term.** The categorical foundation provides the theoretical guarantee that quantum targets compose with classical targets through the same algebraic structure. The infrastructure built for classical multi-target compilation transfers to quantum/classical hybrid systems without architectural rework.

This timeline marks when the framework's quantum capabilities become practically useful, conditional on hardware and decoder developments outside our control. The collapsing horizon the Mosca Moment analysis predicted has begun to materialize through parallel advances in both algebraic code design and learned decoder architectures, and the earlier-than-expected arrival of fault-tolerant subroutines for specific problem classes is the practical evidence that the compression is real.

## References

[1] S. Abramsky and B. Coecke, "A categorical semantics of quantum protocols," in *Proc. 19th Annual IEEE Symposium on Logic in Computer Science*, pp. 415-425, 2004.
