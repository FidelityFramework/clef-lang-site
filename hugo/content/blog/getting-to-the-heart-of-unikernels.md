---
title: "Getting to the Heart of Unikernels"
linkTitle: "Getting to the Heart of Unikernels"
description: "A toolchain that makes kernels first-class citizens from server workloads to microcontrollers."
date: 2026-07-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
---

## 'Hidden' Hierarchy

```mermaid
graph LR
    ROOT["Sealed artifact<br/>one image, self-contained"]

    ROOT --> SEQ["Braided Parallelism<br/>instruction stream"]
    ROOT --> SIMT["SIMT<br/>lockstep thread groups"]
    ROOT --> DF["Dataflow<br/>compiled graph"]
    ROOT --> FIX["Fixed fabric<br/>synthesized logic"]

    SEQ --> MCU["SoC/MCU<br/>reset vector,<br/>register direct"]
    SEQ --> UVM["CPU MicroVM<br/>hypervisor<br/>virtual devices"]
    SEQ --> CON["CPU Container<br/>host kernel<br/>syscall surface"]

    SIMT --> GPU["GPU<br/>kernels in warps"]

    DF --> NPU["NPU<br/>compiled graph"]
    DF --> CGRA["CGRA<br/>array configuration"]

    FIX --> FPGA["FPGA<br/>bitstream"]
    FIX --> ASIC["ASIC<br/>fixed function"]

    classDef root fill:#2a2a2a,stroke:#888,color:#ddd;
    classDef model fill:#2a2a2a,stroke:#888,color:#ddd;
    classDef highlight fill:#4a2a2a,stroke:#F88,color:#ddd;
    classDef target fill:#1a2a3a,stroke:#48a,color:#cdf;
    classDef hightarget fill:#1a2a5a,stroke:#48F,color:#cdf;
    class ROOT root;
    class SEQ highlight;
    class SIMT,DF,FIX model; 
    class MCU,UVM,CON hightarget;
    class GPU,NPU,CGRA,FPGA,ASIC target;
```

Most standard applications run on top of [a runtime host]({{< ref "seeing-beyond-assemblies" >}}), and always an operating system. The OS has its own layers 'down the stack': a kernel, a variety of drivers, package managers, support tools, and a shell. Most of that stack sits idle for the workload's life on that device. It still ships on the platform, still gets patched on a staggered schedule, and still presents all of the capability and risk that comes with it. That is the ordinary shape of most software today, and what "runs on an operating system" has meant for decades.

By contrast, the unikernel's introduction to computing has been uneven: developed in a research lab, praised through a hype cycle that receded, and now daily production at a scale that most applications developers don't notice. The idea underneath holds steady. Seal the computation graph down to what a workload needs, statically coupled at build time, with no software host stack resident beneath it, and most of that idle surface simply is not there to patch, exploit, or wait on. The image starts faster because it skips the layers that ordinary boot traverses, and it is smaller because nothing unused was ever linked in. That combination, less to attack and less to boot, is why the concept keeps resurfacing across a wide range of deployment shapes, from containers and microVMs down to workloads built for a microcontroller. Our framework treats the artifact class as a category with normative language of its own, and we are designing the deployment story to extend to whatever form that a given business need may require.

## One Sealed Image

The word "unikernel" can be seen as coming with some unfair baggage. When the topic comes up, the mental image tends toward austerity: one core, one thread, no allocator, a workload squeezed onto limited hardware. Of course the palette available is much more expansive. 

The "uni" portion of the term describes the shape of the artifact. Core count, however, is a property of the deployment target. So are thread count, memory strategy, and whether a library is statically linked. [Our spec draws that same line](/spec/draft/backend-lowering-architecture/#5-entry-point-example) between the freestanding compilation a unikernel requires and everything else a specific target may or may not grant.

The "kernel" half has its own history. The word began as the seed inside the husk, the part kept when everything around it is stripped away, and computing has taken it in two directions since. The operating-system kernel grew outward into a resident, general-purpose platform that serves every workload on the machine. The GPU usage stayed near the seed: one computation, dispatched as one of many duplicate units, run to completion. We take the term unikernel back toward its center, the kernel of one application's full compute scope, carrying exactly what that application needs.

```mermaid
flowchart TB

    subgraph SEAL["Sealed image"]
        direction TB
        SA["Application"] --> SL["C library (musl / glibc), statically coupled, only what is used"]
        SL --> SK["kernel / bare hardware"]
    end
    subgraph GEN["General-purpose OS"]
        direction TB
        GA["Application"] --> GL["C library, dynamically linked"]
        GL --> GU["shell, package manager, userland"]
        GU --> GD["vendor driver stack"]
        GD --> GK["kernel"]
    end

    classDef idle fill:#2a2a2a,stroke:#888,color:#bbb,stroke-dasharray:4 3;
    classDef present fill:#1a2a3a,stroke:#48a,color:#cdf;
    class GU,GD idle;
    class GA,GL,GK,SA,SL,SK present;
```

*Every dashed layer on the left is resident and privileged whether or not the workload touches it. The sealed image has no counterpart to those layers.*

Most toolchains can already produce a freestanding artifact. That half of the problem was solved a long time ago. What has kept unikernels a specialist's tool is everything past that: hand-written service layers standing in for the OS pieces a workload still needs, one hypervisor's guest format at a time, and an extremely narrow passage to garden-variety developer workflow. Fidelity approaches this from the compiler down rather than the binary up. The same Clef source that targets a hosted Linux process today would target a sealed artifact by changing what the [Platform Descriptor](/spec/draft/platform-bindings/) declares, with the program text untouched. Static coupling, the discipline that removes dynamic-linking seams, falls out of that same freestanding compilation. And our actor-and-arena memory model, designed for every target, [deterministic lifetimes tied to actor scope]({{< ref "raii-in-olivier-and-prospero" >}}) rather than a garbage collector, is exactly what a sealed build requires to run without a runtime, or even an operating system, beneath the image.

## Flexing Without 'musl'

The sealed-image diagram above shows a C library statically linked into the image, and musl is the usual choice there: it is small, static-link-friendly, and already the libc behind most scratch-container and unikernel builds. 

Fidelity aims to provide three choices, one for each linkage tier to satisfy a variety of technical use cases. The first of course is the fully hosted model: the C binding path we've written about extensively and built with our Farscape binding library generator, dynamically resolved against the system's shared libraries like any ordinary Linux process. The second is [freestanding with a static libc](/docs/design/interop/library-binding): the same Farscape bindings coupled at build time, musl sealed into the image, the recipe in the diagram above. The third is the tier this entry is about: [a bare target with no C runtime at all](/spec/draft/ffi-boundary/), reached by compiling Clef to native code with no C ABI in the path. On this tier no libc/musl is linked, there is no foreign-function boundary, and interior memory follows the compiler's own lifetime lattice rather than `malloc` and `free`. We intend the arena and static-storage discipline to be the memory model on that tier. There would be no allocator to call because none would be linked. This may seem like a radical idea, which is why we support other, more recognized approaches. However, we expect the third, fully native approach to predominate in our work as the pattern and platform mature.

## Understanding Prior Art

We made the commitments above with an unadorned view of the field's history. The record holds up for a systems concept made real: the founding paper still reads well, the production survivors are traceable by name, and the reasons the first wave stalled were well documented. We read that record for what it settled, and for what it left open to develop further.

[MirageOS](https://mirage.io) is the most prominent prior-art waypoint. The 2013 paper [*Unikernels: Library Operating Systems for the Cloud*](https://anil.recoil.org/papers/2013-asplos-mirage.pdf) made a compelling argument: specialize the build to the application, link only what the application uses, and boot the result directly as a virtual guest. The part of that project which demonstrably survived into the wild is the networking. Docker Desktop ships [VPNKit](https://github.com/moby/vpnkit), a service assembled from Mirage networking libraries, and it has routed container traffic on developer machines for a decade. The ecosystem that followed broadened the substrate choices: [Unikraft](https://unikraft.org) reports millisecond-scale boots for specialized images, and the [unikernels-as-processes](https://dl.acm.org/doi/10.1145/3267809.3267845) line of research showed the artifact running as an ordinary process behind a narrow syscall filter, no hypervisor required.

It is fair to ask why the 2010s wave receded without taking over the cloud. The objections were practical. Debugging norms assumed a shell in the image, observability assumed an agent, and patching assumed a package manager. Linux userland gravity is real, and a decade ago the operational tooling demanded that teams give up more than the smaller footprint provided in return. We're sensitive to this, and we structure the framework so each of those 'trades' resolves in favor of team productivity and developer ergonomics.

It can also be viewed as ironic that mainstream practice spent ten years moving ***toward*** the unikernel's assumptions. Idempotent and immutable infrastructure became the default at serious shops, and patch-by-rebuild stopped being an objection once CI pipelines redeployed on every merge. Scratch-based container images holding a single static binary went from curiosity to recognized supply-chain practice. The industry adopted, piece by piece, the *operational* model unikernels presented all at once.

## From Freestanding Builds to Sealed Images

Our compilation story started with freestanding desktop apps and builds toward the sealed image for microcontrollers. The spec defines [two freestanding entry modes](/spec/draft/program-structure-and-execution/): a hosted-ELF Linux form that carries no libc and terminates through the exit syscall, and a bare-metal form for Cortex-M33 class parts that enters at the reset vector with no loader and no syscall ABI at all. The [work on microcontroller-class hardware](/docs/internals/hardware/fidelity-on-mcu/) is where the small end of that story develops, both the vendor-HAL route and the pure-Clef unikernel. Reactive pipelines for sensor-class deployments are part of [how we think about those targets]({{< ref "fidelityrx-native-reactivity" >}}).

On the M33, we see the [Platform Descriptor](/spec/draft/platform-bindings/) declaring a 4-byte word with no heap region. Nothing about the mechanism is small-end specific: a container definition that grants eight cores and a gigabyte of memory under a narrow syscall policy is also a platform declaration, so the compiler that will build the M33 prototype would equally produce an image that runs [multi-threaded Olivier actors](/docs/design/concurrency/the-three-layer-actor-contract/) with [arena-backed, RAII-disciplined memory](/docs/design/memory/raii-in-olivier-and-prospero/) inside that larger grant. Details matter, but the emphasis in our bench work now is to ensure the generality of our design to serve a variety of targets.

Our first concrete unikernel implementation will be the bare-metal M33 build for [our post-quantum credential prototype](https://speakez.tech/portfolio/post-quantum-credential/). The application *is* the operating system. Control arrives at the reset vector, and the first code to run is credential logic. [The credential authority chapter](/spec/draft/credential-authority/) specifies the design, and the prototype will put the sealed-image properties to work directly on silicon.

## The Cold-Start Collapse

Deployment substrates for a sealed image span three forms today, and the useful comparison is who provides the machine:

| Substrate | Who provides the machine | What the image assumes |
|-----------|--------------------------|------------------------|
| Bare metal | The silicon itself | Nothing; entry at the reset vector |
| MicroVM (Firecracker-class) | A hypervisor's virtual hardware | Virtual devices; the image brings its own service layer |
| Container (LXC-style, scratch image) | The host kernel's syscall interface | Syscalls within the granted policy; empty userland |

Purists sometimes reserve the word for the middle row, where the image brings its own kernel-role code as a guest. We read the class by its properties instead: one sealed artifact with an empty userland and no resident stack inside its boundary. The process form carries those properties intact, which is the argument the unikernels-as-processes research made formally. Deployment platforms are heading toward exactly that form. Our plan is to approach it directly as opposed to the industry pattern of "backing into principal" one furtive step at a time.

Cold start is where the difference is measurable. A conventional container cold start does work a sealed artifact never generates: it pulls layered filesystems, starts an init process, walks the dynamic linker across shared objects, and then warms whatever runtime the application may require. A Clef-compiled unikernel would skip nearly all of it. There is always hardware bring-up, but the cost is bounded by what the image carries: one small static binary, no interpretation or JIT warmup, no linker pass at entry, and arena-based memory with no garbage collector to initialize. Entry is a jump. [AWS built Firecracker](https://firecracker-microvm.github.io/) to boot minimal microVMs in roughly a hundred milliseconds, and it runs underneath their Lambda container service. Boots measured in single-digit milliseconds inside that same harness appear throughout the unikernel literature. The floor keeps dropping as the artifact approaches the application with little additional boundary.

```mermaid
flowchart LR

    subgraph SEAL["Sealed image cold start (single-digit ms in the literature)"]
        direction LR
        S1["hardware<br/>bring-up"] --> S2["jump to entry"] --> S3["run"]
    end
    subgraph CONV["Conventional container cold start (commonly seconds)"]
        direction LR
        C1["pull layered<br/>filesystem"] --> C2["start init<br/>process"] --> C3["walk dynamic<br/>linker"] --> C4["warm runtime<br/>(JIT / GC)"] --> C5["run"]
    end

    subgraph BUILD["paid once, at build time"]
        direction LR
        B1["one small<br/>static binary"]
        B2["links resolved by<br/>static coupling"]
        B3["native code +<br/>arena memory plan"]
    end

    C1 -.-> B1
    C3 -.-> B2
    C4 -.-> B3

    classDef paid fill:#2a2a2a,stroke:#888,color:#bbb,stroke-dasharray:4 3;
    classDef built fill:#2a3a2a,stroke:#8a8,color:#cfc;
    classDef fast fill:#1a2a3a,stroke:#48a,color:#cdf;
    class C1,C2,C3,C4 paid;
    class B1,B2,B3 built;
    class C5,S1,S2,S3 fast;
```

That arithmetic drew us to [Cloudflare's Container infrastructure](https://developers.cloudflare.com/containers/) and shaped the hybrid we sketched in [Unexpected Fusion]({{< ref "unexpected-fusion" >}}): CloudEdge actors for coordination, Fidelity-compiled unikernels for compute-dense work. With near-instant cold starts, scale-to-zero would become the default rather than a compromise, and dense deployment of minimal-overhead images would recast the runtime cost model. Fan-out across instances would ride on BAREWire-framed messages, [the coordination model our JavaScript-targeting design lays out](/docs/design/javascript-targeting/jsir-javascript-as-mlir-backend/) for compute that outgrows one instance. Firecracker and LXC-style substrates would take the same build with different isolation trade-offs. That is the practical payoff of an artifact class defined by self-containment: the substrate becomes a neutral deployment decision instead of an architectural impediment.

## Leveling the Horizon Line

From our design perspective, there is a question we keep circling: *where does the concept stop?* We theorize in this section, but we hold the taxonomy loosely as we expect it to take a more definitive shape as our bench work continues.

FPGA and dedicated-IC practice has always worked the way this entry describes. A bitstream is a sealed artifact that configures the fabric and 'becomes the system' for that device. Nobody asks what operating system the fabric runs, because the question has no referent. [Our hardware-inference work]({{< ref "fpga-and-hardware-inference" >}}) sits in that tradition. The dedicated-silicon world never needed the unikernel concept because it never had the operating-system assumption to begin with.

General-purpose accelerators sit closer to that world than their CPU-adjacent position suggests. The unit of work handed to a GPU is already called a kernel, and the scheduling vocabulary around it, warps and command queues and streams, describes hardware-managed dispatch with no OS norms in the vicinity. [Unified-memory APUs](/docs/internals/memory-fabrics/rdna-unified-memory-desktop/) pull those workloads into the CPU's address space without adding an operating system between the workload and the compute units. NPUs accept compiled graphs. CGRAs accept configurations. Each device takes a sealed artifact, starts it, with granted resources to completion.

The wrinkle is the element that starts the process. These workloads depend on a bootstrapping orchestrator, usually a CPU, and that dependence is the obvious resistance to admitting them to the class. We think the bootloader analogy holds here *as well*. An M33 build receives control from a boot ROM whose role ends at that handoff, and nobody counts the boot ROM as a *host*. The line we find useful is residence rather than role: whether a software host stays underneath the workload during execution, or hands over at entry and disappears. A GPU kernel that runs to completion on its compute units sits on the sealed-image side of that line. A workload that round-trips through a driver mid-flight sits on the other. Reasonable people could draw the line elsewhere, and we expect our own position to sharpen as the compilation work reaches more deeply into those targets.

Whether or not the term stretches in that way, the leveling is what we are after. Spatial and dedicated processors never reflected operating-system norms, and the sealed image brings general-purpose deployment into the same frame. One conceptual horizon line runs flat across the landscape, from microcontroller and microVM to container and fabric, and the question at every point is the same: what did the platform declare, and what does the artifact require?

## A New Normal for Dedicated Systems

A 'sealed artifact' deploys into general-purpose environments with near-instant startup and a security posture that closes the dynamic-linking seam and strips out the userland an intruder could exploit. It widens the menu of substrates a workload can run on. The choice of physical substrate comes late, weighed against a processor's capabilities and the developer's available targets. In our design the reach of any one build is bounded by its platform declaration, among other constraints. So the eight-core container build and the single-core sensor node would be the same programming model at two 'notch points' across a spectrum of options.

Unikernels is only read as a niche when the operating system is treated as a presumed constant center of gravity, and dedicated systems programming never accepted that premise. General-purpose CPUs are late arrivals to a stance the rest of the hardware landscape considers ordinary, one that the economics of Kubernetes-class scheduling now favor on both efficiency and security grounds.

We build freestanding images today, at the small end where they are the only option and on hosted Linux where they are an advantaged choice for certain scenarios. The sealed container is also a direction we envision for our CloudEdge design work. And the horizon-leveling question, how far down the accelerator landscape one compilation discipline can reach, is one we expect to keep developing in the open.

## Related Entries

- [Getting the Signal with BAREWire]({{< ref "getting-the-signal-with-barewire" >}}): reactive programming in Clef without subscription overhead, one signal model across native, web, and managed targets
- [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/): actor-aware memory management through deterministic lifetimes, with each actor owning an arena that manages its own lifetime without adverse overhead
- [Arena Hoisting](/docs/design/memory/arena-hoisting/): the Lattice analyzer that trades a runtime guard for a compile-time arena placement, keeping the safe default and surfacing the faster choice
- [Unexpected Fusion]({{< ref "unexpected-fusion" >}}): how an agentic systems architecture inspired by OCaml and Erlang informs Fidelity's continuation-passing core
