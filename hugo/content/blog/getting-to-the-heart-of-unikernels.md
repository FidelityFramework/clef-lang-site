---
title: "Getting to the Heart of Unikernels"
linkTitle: "Getting to the Heart of Unikernels"
description: "One self-contained image from microcontroller to microVM to the edge, and why that posture is a new normal for systems programming"
date: 2026-07-06
authors: ["Houston Haynes"]
tags: ["Architecture", "Design", "Innovation"]
---

Millions of developers run MirageOS code every working day, and almost none of them would say so if asked. Docker Desktop ships its network layer as [VPNKit](https://github.com/moby/vpnkit), a service assembled from [MirageOS](https://mirage.io) networking libraries. The packets moving between containers and the host on macOS and Windows pass through code that began as a research unikernel at Cambridge, described in the 2013 paper [*Unikernels: Library Operating Systems for the Cloud*](https://anil.recoil.org/papers/2013-asplos-mirage.pdf). The artifact class most often described as exotic has spent more than a decade on production duty inside one of the most mainstream developer tools in the industry.

That gap between reputation and reality is worth closing, because the reputation carries baggage the definition does not support. When unikernels come up in conversation, the mental image tends toward the hair shirt: one core, one thread, no allocator, a workload squeezed onto hardware that leaves no other choice. That image mistakes the first demonstration hardware for the class. A unikernel is a single, self-contained image, sealed at build time, with no software stack resident beneath it inside its own boundary. Core count is a property of the target it lands on. So are thread count, memory strategy, and whether a C library is linked. None of them belong to the term.

## One Sealed Image

The "uni" names the artifact. In the library-OS lineage the unikernel descends from, the services an operating system would provide become libraries the application links against, and the build emits one object that contains the application together with everything it needs to run. One image. The word says nothing about how many cores that image may use, how many threads it may run, or what its memory discipline looks like once control arrives.

We recently tightened our own spec language on exactly this point, and the axes it now keeps separate are a useful map of the territory. [The backend chapter draws hosted and freestanding as modes of a compilation leg](/spec/draft/backend-lowering-architecture/#5-entry-point-example): a hosted leg assumes an OS runtime beneath the program, a freestanding leg assumes none, and the mode governs only that. Word size, allocator, C library, and core count are properties of the specific target. [The FFI chapter carves linkage three ways](/spec/draft/ffi-boundary/): hosted with a dynamically linked libc, freestanding with a statically coupled libc, and bare with no C runtime at all. Each carve answers a different question, and conflating them is how the hair-shirt reputation got started.

Three distinct disciplines nest here without collapsing into one another:

| Discipline | What it commits | What it leaves open |
|-----------|-----------------|---------------------|
| Freestanding compilation | No hosted OS assumed at compile time | Linkage, allocator, threading, deployment substrate |
| Static coupling | Bindings resolved at build; no dynamic linker at run time | Whether a host OS exists at all |
| Unikernel | One sealed image; no resident software stack inside its boundary | Core count, threading, memory strategy, substrate |

Every unikernel is freestanding-compiled and statically coupled. Neither property alone makes a unikernel: a freestanding Linux binary with a statically coupled libc runs happily as an ordinary hosted process, and plenty of static binaries assume a full distro underneath them. The toolchains already treat these as separate choices. C compilers have accepted `-ffreestanding` for decades, Rust builds core-only crates under `no_std`, and our own toolchain produces freestanding images from Clef source today.

Core count deserves one more sentence, because it is the conflation we most recently had to unwind. Delimited continuations run through everything we build; they are the substrate under `async`, actor `receive`, and every suspension point in [our concurrency design](/docs/design/concurrency/delimited-continuations/). On a single-core part, the braid presses that same continuation surface into service as [the cooperative scheduler](/spec/draft/dcont-representation/#7-normative-single-core-cooperative-scheduling), a forcing function of having one thread of control rather than a property of continuations or of unikernels. Core count enters our normative language there, and nowhere else.

## The Library OS Already Went Mainstream

MirageOS made the founding argument: specialize the image to the application, link only what the application uses, and boot it directly as a virtual machine guest. The images came out small, the attack surface shrank with them, and boot times landed in milliseconds. VPNKit carried the same libraries into Docker Desktop, where they have routed developer traffic ever since. The ecosystem that followed broadened the substrate choices: [Unikraft](https://unikraft.org) reports millisecond-scale boots for specialized images, and the [unikernels-as-processes](https://dl.acm.org/doi/10.1145/3267809.3267845) line of research showed the artifact running as an ordinary process behind a narrow syscall filter, no hypervisor required. IBM's Nabla containers took that route to production experiments.

It is fair to ask why the 2010s wave receded without taking over the cloud. The objections were practical. Debugging norms assumed a shell in the image; observability assumed an agent; patching assumed a package manager. Linux userland gravity is real, and a decade ago the operational tooling demanded that teams give up more than the image sizes gave back.

Mainstream practice then spent ten years moving toward the unikernel's assumptions. Immutable infrastructure became the default posture of serious shops, and patch-by-rebuild stopped being an objection once CI pipelines redeployed on every merge as routine. Scratch-based container images holding a single static binary went from curiosity to recognized supply-chain practice. The industry adopted, piece by piece, the operational model unikernels required all at once. We took inspiration from Mirage in the early days of our design work, and part of that inspiration was the conviction that the model was early rather than wrong.

## From Freestanding Builds to Sealed Images

Our compilation story starts from the freestanding discipline and builds toward the sealed image. Today we compile [two freestanding entry modes](/spec/draft/program-structure-and-execution/): a hosted-ELF Linux form that carries no libc and terminates through the exit syscall, and a bare-metal form for Cortex-M33 class parts that enters at the reset vector with no loader and no syscall ABI at all. The [work on STM32-class hardware](/docs/internals/hardware/fidelity-on-stm32/) is where the small end of that story lives, and reactive pipelines for sensor-class deployments are part of [how we think about those targets]({{< ref "fidelityrx-native-reactivity" >}}).

The piece that generalizes is the [Platform Descriptor](/spec/draft/platform-bindings/), a quotation-based structure that declares what a target provides, from word size and memory regions to entry convention and heap presence. The compiler commits to exactly what the descriptor declares. On the M33 that means a 4-byte word with no heap region, entered at the reset vector. Nothing in the mechanism is small-end specific. A container definition that grants eight cores and a gigabyte of memory under a narrow syscall policy is also a platform declaration, and the descriptor is designed to mirror it, so that an image compiled against that declaration uses the granted capability and assumes nothing beyond it.

Capability inside the image scales the same way. A freestanding image on capable hardware can run multi-threaded, schedule [Olivier actors](/docs/design/concurrency/the-three-layer-actor-contract/) with [arena-backed, RAII-disciplined memory](/docs/design/memory/raii-in-olivier-and-prospero/), and lean on the same compile-time machinery we apply everywhere else: [escape analysis that classifies lifetimes as a coeffect](/docs/internals/verification/memory-coeffect-algebra/) and [analyzers that surface arena placement decisions to developers](/docs/design/memory/arena-hoisting/). The actor model and the orchestration layer above it remain design-stage work, and we describe them in those terms. The claim we can make plainly is architectural: the artifact class imposes no capability ceiling. What the container definition grants, the image can be compiled to use.

Static coupling closes the loop on the security half of the story. A dynamically linked service resolves part of its behavior at run time, when the loader walks a search path and binds whatever it finds there. Every step of that walk is a seam where a substituted library changes what the program does without changing the program. Sealing the bindings at build time closes the seam: the image that ships is the image that runs, resolved once under the builder's control against artifacts the build can attest. There is no linker inside the image for an attacker to redirect and no preload path to hijack. Once inside, there is no shell and no userland to live off. This is supply-chain risk reduction by construction, the same family of guarantee we pursue when [protocol invariants are fixed at compile time]({{< ref "cryptographic-certainty" >}}) and travel with the artifact rather than being enforced around it.

Our first concrete unikernel implementation is the bare-metal M33 build for [our post-quantum credential prototype](https://speakez.tech/portfolio/post-quantum-credential/). A credential device concentrates every property this section describes. In essence, the application *is* the operating system: the image carries every element necessary to satisfy its remit and nothing beyond that, so no idle machinery slows the device and no excess surface area accumulates where a vulnerability could emerge. Control arrives at the reset vector and lands directly in credential logic. [The credential authority chapter](/spec/draft/credential-authority/) specifies the design; the prototype puts the sealed-image properties to work on real silicon.

## The Cold-Start Collapse

Deployment substrates for a sealed image span three forms today, and the useful comparison is who provides the machine:

| Substrate | Who provides the machine | What the image assumes |
|-----------|--------------------------|------------------------|
| Bare metal | The silicon itself | Nothing; entry at the reset vector |
| MicroVM (Firecracker-class) | A hypervisor's virtual hardware | Virtual devices; the image brings its own service layer |
| Container (LXC-style, scratch image) | The host kernel's syscall interface | Syscalls within the granted policy; empty userland |

Purists sometimes reserve the word for the middle row, where the image brings its own kernel-role code as a guest. We read the class by its properties instead: one sealed artifact with an empty userland and no resident stack inside its boundary. The process form carries those properties intact, which is the argument the unikernels-as-processes research made formally, and it is the form that matters most for where deployment platforms are heading.

Cold start is where the arithmetic turns visible. A conventional container cold start pays for work a sealed image never generates: it pulls layered filesystems, starts an init process, walks the dynamic linker across shared objects, and then warms whatever runtime the application carries. A Clef-compiled image would pay almost none of that bill. The artifact is one small static binary; nothing in it needs interpretation or JIT warmup, no linker pass runs at entry, and arena-based memory skips garbage-collector initialization. Entry is a jump. [AWS built Firecracker](https://firecracker-microvm.github.io/) to boot minimal microVMs in roughly a hundred milliseconds, and it runs underneath Lambda; boots measured in single-digit milliseconds inside that same harness appear throughout the unikernel literature. The floor keeps dropping as the image approaches the application and nothing else.

That arithmetic drew us to [Cloudflare's Container infrastructure](https://developers.cloudflare.com/containers/) and shaped the hybrid we sketched in [Unexpected Fusion]({{< ref "unexpected-fusion" >}}): CloudEdge actors for coordination, Fidelity-compiled unikernels for compute-dense work. Near-instant cold starts would make scale-to-zero the default posture rather than a compromise, and dense deployment of minimal-overhead images points at a cost model that recasts applications the current economics rule out. Fan-out across instances rides on [BAREWire-framed messages]({{< ref "getting-the-signal-with-barewire" >}}), keeping coordination on the same typed contract the rest of our stack uses. Firecracker and LXC-style substrates would take the same image with different isolation trade-offs, which is the practical benefit of an artifact class defined by self-containment: the substrate becomes a deployment decision instead of a build decision.

## Leveling the Horizon Line

From our design perspective, there is a question we keep circling: where does the concept stop? We theorize in this section, and hold the taxonomy loosely.

FPGA and dedicated-IC practice has always worked the way this entry describes. A bitstream is a sealed artifact that configures the fabric and becomes the system; nobody asks what operating system the fabric runs, because the question has no referent. [Our hardware-inference work]({{< ref "fpga-and-hardware-inference" >}}) sits in that tradition. The dedicated-silicon world never needed the unikernel concept because it never had the operating-system assumption to subtract.

General-purpose accelerators sit closer to that world than their CPU-adjacent position suggests. The unit of work handed to a GPU is already called a kernel, and the scheduling vocabulary around it, warps and command queues and streams, describes hardware-managed dispatch with no OS norms anywhere in it. [Unified-memory APUs](/docs/internals/hardware/rdna-unified-memory-desktop/) pull those workloads into the CPU's address space without adding an operating system between the workload and the compute units. NPUs accept compiled graphs. CGRAs accept configurations. Each device takes a sealed artifact, ignites it, and lets it own its granted resources to completion.

The wrinkle is the igniter. These workloads depend on a bootstrapping orchestrator, usually a CPU, and that dependence is the obvious objection to counting them in the class. We think the bootloader analogy carries the weight here. An M33 image receives control from a boot ROM that then steps out of the way, and nobody counts the boot ROM as a host. The line we find useful is residence rather than ignition: whether a software host stays underneath the workload during execution serving it, or hands over at entry and disappears. A GPU kernel that runs to completion on its compute units sits on the sealed-image side of that line. A workload that round-trips through a driver mid-flight sits on the other. Reasonable people could draw the line elsewhere, and we expect our own position to sharpen as the compilation work reaches those targets.

Whether or not the term stretches that far, the leveling is what we are after. Spatial and dedicated processors never reflected operating-system norms, and the sealed image brings general-purpose deployment into the same frame. One conceptual horizon line runs flat across the landscape, from microcontroller and microVM to container and fabric, and the question at every point on it is the same: what did the platform declare, and what does the artifact assume?

## A New Normal for Dedicated Systems

The practical value lands first. A sealed image deploys into general-purpose environments with near-instant startup and a security posture that closes the dynamic-linking seam and strips out the userland an intruder would live off. It widens the menu of substrates a workload can land on, because self-containment makes the substrate a late decision. And it does this with capability bounded by the platform declaration, so the eight-core container image and the single-core sensor node are the same kind of thing at two points on one spectrum.

The framing we want to leave behind is the corner. Unikernels read as a niche only when the operating system is treated as the natural center of gravity, and dedicated systems programming never granted that premise. FPGA and ASIC work treats the artifact as the system as a matter of course, and compilation for spatial processors is growing in the same soil. General-purpose CPUs are late arrivals to a posture the rest of the hardware landscape considers ordinary, and the tooling shift of the last decade says the arrival is well underway.

We build freestanding images today, at the small end where the discipline is unavoidable and on hosted Linux where it is a choice. The sealed-container story is where our CloudEdge design work is headed, and the horizon-leveling question, how far down the accelerator landscape one compilation discipline can reach, is one we expect to keep working at in the open. There is more to come as that work matures with our customers.

## Related Entries

- [Unexpected Fusion]({{< ref "unexpected-fusion" >}}): the CloudEdge-plus-unikernel hybrid whose deployment economics this entry develops
- [Getting the Signal with BAREWire]({{< ref "getting-the-signal-with-barewire" >}}): the typed message contract that would coordinate fan-out across sealed instances
- [RAII in Olivier and Prospero](/docs/design/memory/raii-in-olivier-and-prospero/): the actor-scoped memory design that rides inside these images
- [Arena Hoisting](/docs/design/memory/arena-hoisting/): how placement decisions surface for developer review instead of disappearing into the compiler
