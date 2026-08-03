---
title: "Fidelity on MCU"
linkTitle: "Fidelity on MCU"
description: "Two paths to a microcontroller: binding a vendor HAL through Farscape, and compiling Clef straight to the reset vector"
weight: 10
date: 2024-01-02T16:59:54+06:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
aliases:
  - /docs/internals/hardware/fidelity-on-stm32/
params:
  originally_published: 2024-01-02
  migration_date: 2026-02-15
---

Embedded development typically trades hardware control against high-level abstraction. In the Fidelity Framework's hardware/software co-design, the register layout and access rules a developer would otherwise track by hand become types the compiler checks, and they resolve to native machine code.

From a level of remove, we consider two ways to bring Clef onto a microcontroller. The first binds a vendor's Hardware Abstraction Layer through Farscape, so an application reaches the silicon through the vendor's own C API expressed as typed Clef libraries that statically bind to those established interfaces. The second compiles Clef directly to the reset vector with no intermediate beneath it, a pure-Clef unikernel where the binary on the device is the complete artifact. The first path is the faster route onto a well-supported board. The second is the choice when a security case requires the trusted computing base to be the source itself.

## Pre-Optimized Hardware Mapping

Hardware semantics are captured at the highest level of the compilation pipeline and carried through to native code generation, in place of layouts and register mappings resolved during execution. By parsing hardware definitions (such as CMSIS headers for ARM-based microcontrollers) and transforming them into Clef abstractions the compiler type-checks, then lowering those through MLIR to native code, we would catch a mismatched register access at compile time and still emit machine code identical to hand-written assembly. Memory layouts would be resolved before the first line of MLIR is generated.

## Two Layers

The framework contemplates a design that keeps the memory-mapping infrastructure distinct from the code an application developer writes against it. The lower layer would hold the BAREWire descriptors, the header parsing, and the MLIR transformations that provide the control an embedded target needs. The upper layer would be idiomatic Clef code that reads like normal application development.

Once the memory scaffolding is in place, application code would not reference it directly. Farscape is designed to parse vendor-provided headers into memory mappings and hardware abstractions, packaging them into a focused library. Application developers would import that module and work in business logic, without reaching for register offsets or bit manipulation unless they choose to.

This hardware abstraction work would happen once per microcontroller family. When Farscape generates bindings for a target microcontroller (STM32, or any supported family), a later developer targeting the same hardware would reference the existing library and start writing application code immediately.

```fsharp
// Application layer: code written by any Clef developer
module SensorApplication =
    open STM32F4.Hardware

    // Clean, business-focused Clef code
    let monitorTemperatureSensor() =
        // Initialize hardware with simple calls
        let uart = UART.initialize 115200<baud>
        let statusLED = GPIO.configureOutput(GPIOA, Pin5)

        // Focus on business logic
        let rec monitorLoop() = async {
            let! temperature = readSensorAsync()

            if temperature > 85.0<celsius> then
                statusLED.turnOn()
                uart.send $"ALERT: High temperature {temperature}°C"
            else
                statusLED.turnOff()

            do! Async.Sleep 1000
            return! monitorLoop()
        }

        monitorLoop()
```

Developers would work with idiomatic Clef code while the framework holds to the performance of hand-written C.

## The MCU Compilation Challenge

STM32 microcontrollers and the Renesas RA6M5 sit in the same class: built around ARM Cortex-M cores, they use the Thumb-2 instruction set and have distinct memory regions at fixed addresses. Code lives in Flash memory starting at 0x08000000, variables reside in SRAM at 0x20000000, and hardware peripherals are accessed through memory-mapped registers at addresses like 0x40020000. These parts run with no operating system and no memory management unit, so there is no budget for abstraction overhead.

The traditional compilation path loses information at each stage. By the time code reaches LLVM, the compiler must infer intent from memory access patterns, often missing optimization opportunities or requiring unsafe constructs to reach the desired behavior. The Fidelity Framework would invert this information flow, preserving hardware intent from the source level through to machine code.

## Path One: Binding a Vendor HAL Through Farscape

Every microcontroller family ships a C description of its own hardware, and the fastest route to working code is to reuse that description rather than re-derive it. Farscape reads those vendor headers and produces the typed Clef abstraction the application writes against.

### The MLIR and LLVM Pipeline

Compilation begins with Clef code that uses BAREWire-generated hardware abstractions. These are compile-time constructs that capture the hardware memory model: code that configures a GPIO pin expresses an intent that would compile directly to the register access pattern, with no function call standing between the source and the hardware. The abstraction is resolved once, when the library is generated:

```fsharp
// Hardware abstraction library (generated once, used by many)
// Available from the ClefPak library registry (clefpak.dev) after initial generation
module STM32F4.GPIO =
    // BAREWire schema capturing GPIO hardware layout from CMSIS headers
    let gpioLayoutSchema =
        BAREWire.schema {
            field "MODER" BAREWireType.UInt32    // Mode register at offset 0x00
            field "OTYPER" BAREWireType.UInt32   // Output type at offset 0x04
            field "OSPEEDR" BAREWireType.UInt32  // Speed register at offset 0x08
            field "PUPDR" BAREWireType.UInt32    // Pull-up/down at offset 0x0C
            field "IDR" BAREWireType.UInt32      // Input data at offset 0x10
            field "ODR" BAREWireType.UInt32      // Output data at offset 0x14
            field "BSRR" BAREWireType.UInt32     // Bit set/reset at offset 0x18
            field "LCKR" BAREWireType.UInt32     // Lock register at offset 0x1C
            field "AFR" (BAREWireType.Array(BAREWireType.UInt32, 2)) // Alternate function
            fixedSize 40  // Total size in bytes
            alignment 4   // Word alignment for ARM
        }

    // Type-safe GPIO configuration with zero runtime cost
    let configurePin (port: GPIOPort) (pin: int) (mode: PinMode) =
        // BAREWire creates a zero-copy view over hardware registers
        use registers = BAREWire.createView port.BaseAddress gpioLayoutSchema

        // Type-safe register manipulation with compile-time offset calculation
        let moder = registers.ReadField<uint32> "MODER"
        let shift = pin * 2
        let mask = ~~~(3u <<< shift)
        let newModer = (moder &&& mask) ||| ((uint32 mode) <<< shift)
        registers.WriteField "MODER" newModer
```

Alex (enhanced with XParsec for pattern recognition) first transforms the Clef AST into MLIR operations, recognizing BAREWire patterns and translating them with pre-calculated offsets and alignment requirements.

Memory-Mapped I/O (MMIO) requires volatile semantics. Composer uses LLVM dialect operations within MLIR to preserve these hardware interaction guarantees:

```mlir
func.func @configurePin(%port_base: i32, %pin: i32, %mode: i32) {
    // MODER register access with compile-time known offset (0x00)
    // Using LLVM dialect for volatile MMIO operations
    %moder_ptr = llvm.inttoptr %port_base : i32 to !llvm.ptr<i32>
    %current_moder = llvm.load %moder_ptr {volatile} : !llvm.ptr<i32>

    // Bit manipulation with optimal instruction selection
    %c2 = arith.constant 2 : i32
    %shift = arith.muli %pin, %c2 : i32
    %c3 = arith.constant 3 : i32
    %mask_base = arith.shli %c3, %shift : i32
    %mask = arith.xori %mask_base, %c_neg1 : i32

    %cleared = arith.andi %current_moder, %mask : i32
    %mode_shifted = arith.shli %mode, %shift : i32
    %new_moder = arith.ori %cleared, %mode_shifted : i32

    // Atomic write back to hardware register
    llvm.store %new_moder, %moder_ptr {volatile} : !llvm.ptr<i32>
    return
}
```

The MLIR then lowers through the LLVM dialect to LLVM IR, targeting the specific ARM architecture:

```llvm
; LLVM IR for thumbv7em-none-eabihf target
define void @configurePin(i32 %port_base, i32 %pin, i32 %mode) {
entry:
  %moder_ptr = inttoptr i32 %port_base to i32*
  %current = load volatile i32, i32* %moder_ptr, align 4

  ; Efficient bit manipulation using ARM instructions
  %shift = shl i32 %pin, 1
  %mask_base = shl i32 3, %shift
  %mask = xor i32 %mask_base, -1
  %cleared = and i32 %current, %mask
  %mode_shifted = shl i32 %mode, %shift
  %new_value = or i32 %cleared, %mode_shifted

  store volatile i32 %new_value, i32* %moder_ptr, align 4
  ret void
}
```

Finally, LLVM's ARM backend generates Thumb-2 assembly:

```asm
configurePin:
    ldr     r3, [r0]        @ Load MODER register
    lsls    r1, r1, #1      @ Calculate bit position
    movs    r2, #3          @ Prepare mask
    lsls    r2, r2, r1      @ Shift mask to position
    bics    r3, r2          @ Clear mode bits
    lsls    r2, r2, r1      @ Shift mode value
    orrs    r3, r2          @ Set new mode
    str     r3, [r0]        @ Store back to MODER
    bx      lr              @ Return
 
```

This assembly matches what an expert embedded programmer would write by hand. The application developer who wrote the temperature monitoring code never sees any of this.

### Parsing Hardware Headers with Farscape

Farscape starts from the vendor headers themselves: CMSIS (Cortex Microcontroller Software Interface Standard) for ARM-based microcontrollers, the Flexible Software Package (FSP) for the Renesas RA family. Both describe every register, bit field, and memory map in the microcontroller.

Farscape uses XParsec (the same parser combinator library that powers other parts of the Fidelity toolchain) to read these headers, so a malformed header fails the parse with a precise diagnostic at its source location, where a wrapper-based flow would carry the mistake forward into a silently wrong binding. The output is quotation-based hardware descriptors that integrate with the Clef compilation pipeline rather than raw P/Invoke bindings. [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/) traces this path in depth, from the CMSIS `__I`/`__O`/`__IO` qualifiers through to the `MemoryModel` record the compiler consumes.

When a contributor runs Farscape on a new microcontroller's headers, it generates a complete Clef hardware abstraction library with three components: quotations encoding memory layout, active patterns for PSG recognition, and a MemoryModel record for Clef integration.

```c
// Example CMSIS header content (input to Farscape)
typedef struct
{
  __IO uint32_t MODER;    /*!< GPIO port mode register,               Address offset: 0x00 */
  __IO uint32_t OTYPER;   /*!< GPIO port output type register,        Address offset: 0x04 */
  __IO uint32_t OSPEEDR;  /*!< GPIO port output speed register,       Address offset: 0x08 */
  __IO uint32_t PUPDR;    /*!< GPIO port pull-up/pull-down register,  Address offset: 0x0C */
  __IO uint32_t IDR;      /*!< GPIO port input data register,         Address offset: 0x10 */
  __IO uint32_t ODR;      /*!< GPIO port output data register,        Address offset: 0x14 */
  __IO uint32_t BSRR;     /*!< GPIO port bit set/reset register,      Address offset: 0x18 */
  __IO uint32_t LCKR;     /*!< GPIO port configuration lock register, Address offset: 0x1C */
  __IO uint32_t AFR[2];   /*!< GPIO alternate function registers,     Address offset: 0x20-0x24 */
} GPIO_TypeDef;

#define GPIOA_BASE            (AHB1PERIPH_BASE + 0x0000UL)
#define GPIOA                 ((GPIO_TypeDef *) GPIOA_BASE)
```

Farscape transforms these C definitions into Clef quotations and active patterns that preserve the hardware semantics and carry the access rules as types the compiler can check. Once generated, the abstractions are usable without reference to the underlying layout.

This is the path to take when a vendor HAL already exists and building the application on top of it is acceptable. The generated library would recognize the HAL's own calls (a `HAL_GPIO_WritePin` on STM32, its FSP equivalent on the RA family) and lower them to the register accesses they stand for. The artifact is a Clef application over a vendor runtime.

## Path Two: A Pure-Clef Unikernel to the Reset Vector

The second path removes the vendor runtime entirely. Composer would compile Clef straight to the ELF the device mounts, without a CMSIS HAL, an FSP, an RTOS, or a C runtime beneath it. The security case that motivates this is direct: the binary on the device is the binary the verifier saw, down to the reset vector. This is the path the [Post-Quantum Credential]({{< ref "cryptographic-certainty" >}}) work takes on the Renesas EK-RA6M5 (a Cortex-M33 part), where the trusted computing base has to be the audited source and nothing under it.

The vendor runtime used to bring the clocks up, clear the module-stop bits, and sequence the peripheral registers. Removing the HAL moves that work into the Clef source. The design problem is to express that irreducibly ordered, effectful sequence in a language whose posture is values and inference, without smuggling C's idioms back in under an ML surface.

### The Closure Substrate

The prior a reader brings from C is that low-level work means pointers, null sentinels, casts, and mutation ordered by statement sequence. None of those are the substrate here. The Fidelity substrate down to bare Cortex-M33 silicon is flat closures, thunks, and deferred computation values as the primitive machine representation. The register layer is built one level up, out of that same material.

- The flat closure is the code-address mechanism. A code pointer paired with one flat block of captured values, stack- or region-allocated, with no environment chain and no GC. The pointer representation exists only inside the compiler's lowering and never surfaces in the language. The spec states this as foundational in [closure representation](/spec/draft/closure-representation/), with the backend resolving the pointer cast per target under [backend lowering architecture](/spec/draft/backend-lowering-architecture/).
- The thunk is that same closure with a computed flag and a value slot, null-free before and after it is forced ([lazy representation](/spec/draft/lazy-representation/)). The sequence is a thunk that stores iteration state ([seq representation](/spec/draft/seq-representation/)), and the continuation is a thunk whose captured state is the rest of the program. One representation family serves for code addresses, deferred computation, iteration, and suspension across the system, including the unikernel's event loop.
- Typed values replace the remaining C idioms. `Option` replaces null, `Result` replaces error codes, and width-typed register handles (`Reg8`/`Reg16`/`Reg32` in the MMIO seam below) replace pointer arithmetic at device memory. The spec's [Option/null marshalling rules](/spec/draft/ffi-boundary/#4-optionnull-marshalling) govern the boundary where a Clef `Option` meets a C API. The register regime excludes `voidptr`, pointer math, cast-to-object, and null, and the exclusion is structural in the language rather than enforced by a linter.

A `Mmio.write32` is designed to compile to a single volatile 32-bit store at a literal address, and a closure call to an indirect branch through a struct field. Those are the same instructions a C compiler would emit. The source program reaches those instructions only through typed accessors and closure calls, never through raw pointers, casts, or null.

### The Imperative Seam

The silicon demands bring-up as a specific sequence of register writes, in order, with a side effect at each step. Clef kept the imperative tooling from its F# lineage for exactly this kind of work. The design contains that imperative effect at one named layer, the MMIO seam, and keeps everything above it as ordinary values.

Four principles govern the driver modules.

1. **Keep the imperative register-poke core.** Bring-up is irreducibly ordered and effectful (write `PLLCCR`, poll the stable flag, write the clock-source select). Direct register writes are correct there, and the goal is not to dissolve the effect into a pure abstraction.
2. **Contain it at one layer.** All register effects live in the `Mmio` module: width-typed accessors plus a bounded spin (`waitUntil16`) and a discard-read barrier (`readback32`). Nothing above this layer touches a register, so it is the one audit point where volatile, width, and ordering guarantees attach.
3. **Make ordering visible in types.** Above the seam, configuration is immutable `Plan` and `Cfg` values, and the ordering between bring-up steps is carried by phantom witness tokens threaded as values: `ClockReady`, `ModuleStarted`, `PinsAnalog`, `AdcConfigured`, `DrainArmed`. Each token is minted only by the step that establishes its invariant and demanded as an argument by the next step. Because each token is a real value threaded through, it is also a real SSA def-use dependency the optimizer cannot hoist across, even at low optimization levels.
4. **Refuse the OO-shaped control block.** A vendor HAL threads an opaque control structure into every call and mutates it in place, so "what has happened so far" lives in a mutable cell and ordering is implied by the sequence of method calls. Transcribing that into Clef reproduces C's organization in ML syntax. The target is that organization (control-block-as-object, self-mutating methods, ordering-by-call-sequence), and the imperative effects stay.

### The Bring-Up as a Computation Expression

The clock-and-peripheral bring-up reads as a `result` computation expression whose steps are the ordered writes, with the bounded waits as the `do!` bindings that can fail. The function produces the witness token that downstream steps demand, so the ordering is the type-checker's obligation as much as the reader's. Clock generation runs the longest of these sequences: roughly forty ordered writes and spin-waits with unlock bracketing and a flash-wait-state set before the core clock is raised:

```fsharp
// L1 — the Plan is an immutable value. Pure, inspectable, hashable.
type Plan = { PllSrc: PllSource; Pllmul: PllMul; IclkDiv: Div; (* ... *) Flwt: Flwt }
let entropyPlan : Plan = { (* the bring-up numbers as literals *) }
let validate (p: Plan) : Result<Plan, RatioError> = // pure ratio check, no effect

// L0-ordered sequence. Irreducibly imperative, and correct here. The seam is the
// Mmio.* calls; the order WITHIN this function is the silicon-mandated statement sequence.
// PRODUCES ClockReady — the reified postcondition, the only way downstream proves clocks up.
let configureForEntropy () : Result<ClockReady, ClockFault> =
    result {
        do   Mmio.write16 Reg.PRCR 0xA503us                       // unlock clock + low-power regs
        do   Mmio.write8 Reg.MOSCCR 0x00uy                        // start 24 MHz crystal
        do!  Mmio.waitUntil16 Reg.OSCSF 0x08us 0x08us moscBudget  // BOUNDED wait -> Result
             |> Result.mapError (fun _ -> MoscTimeout)
        do   Mmio.write16 Reg.PLLCCR (encodePll entropyPlan)      // PLL divider, source, multiplier
        do   Mmio.write8 Reg.PLLCR 0x00uy                         // start PLL
        do!  waitPll pllBudget                                     // stable-flag poll, bounded
        do   Mmio.write8 Reg.FLWT (flwtBits entropyPlan.Flwt)     // flash wait states before raising the clock
        do   Mmio.write32 Reg.SCKDIVCR (dividerBits entropyPlan)
        do   Mmio.write8 Reg.SCKSCR 0x05uy                        // switch system clock to PLL
        do   Mmio.write16 Reg.PRCR 0xA500us                       // re-lock
        return ClockReady                                          // mint the witness
    }

// DEMANDS ClockReady; PRODUCES ModuleStarted. Read-back after every module-stop clear.
let startModules (_: ClockReady) : Result<ModuleStarted, ClockFault> =
    Mmio.write32 Reg.MSTPCRD (clearBits [16; 15] (Mmio.read32 Reg.MSTPCRD))  // enable two ADC units
    Mmio.readback32 Reg.MSTPCRD                                    // write-completion barrier (mandatory)
    Ok ModuleStarted
```

The imperative core is still present. `configureForEntropy` is ordered register pokes, as the silicon demands. What is gone is the mutable control block around it: no forgeable `ctrl.ClockReady <- true`, no unbounded spin that wedges the boot on a dead crystal, no missing read-back barrier, and no runtime `if` standing in for a type. `startModules` cannot be called without a `ClockReady`, and `ClockReady` exists only because `configureForEntropy` returned it.

### Costs and Open Work

The witness-token ordering between steps holds today, because it is expressed as SSA def-use dependencies. The ordering of the writes inside a single step, and the correctness of the bounded spins and read-back barriers, depend on volatile stores surviving lowering: today a discarded read-back can be removed as dead code and a spin-wait's load can be hoisted, so those constructs are the right shape to author now while the lowering support that enforces them is still in progress. The framework's proven non-hosted path today is FPGA synthesis (the [HelloArty](/docs/internals/pipeline/learning-to-walk/) design reaches placed hardware through the CIRCT path), and the bare-metal ELF leg is the second backend path this work stands up.

## Choosing a Path

The choice between the paths follows from what the application needs the trusted computing base to be. For the credential work, three backend strategies were weighed before the pure-Clef unikernel was chosen.

**Use stock LLVM with a bare-metal triple and codegen discipline.** Use the stock `thumbv8m.main-none-eabi` target that embedded Rust, Zig, and C use for the M33. Teach Composer to pass the real triple and CPU, and to emit volatile stores with the small barrier set the Cortex-M33's in-order Device-memory semantics require. Ship the reset vector, the linker script, the bare-metal startup, and a small set of freestanding builtins. This is plumbing plus one real subsystem (startup), not a new compiler, and it is the chosen path.

**Make the seam compiler-enforced.** Add a distinguished MMIO operation to Composer's backend model, a sibling of the existing flat-closure pointer mechanism, and grow toward a peripheral-region coeffect so "this pointer is device memory" becomes a type-checked, witness-validated fact the compiler enforces on its own, with no reliance on the `Mmio` module's name as the marker. It is a real subsystem layered on the first path, and the planned second stage once the first path proves out on hardware.

**Own the ELF backend, bypassing LLVM.** Rejected for this target. Composer already has a non-LLVM backend leg (the CIRCT path that reaches FPGA silicon), so the middle end is deliberately backend-agnostic and LLVM is one leg among several. A hand-rolled ELF codegen would mean reinventing instruction selection, Thumb-2 encoding, and register allocation. That is a large unaudited layer, exactly the surface the no-HAL posture exists to avoid, and the stock LLVM leg does all of it. AMD's Peano is the instructive precedent: a purpose-built LLVM target for AIE, whose lesson is to build a custom LLVM target rather than bypass LLVM. Here the stock `thumbv8m` target already honors volatile and emits the barriers, so no custom target is needed at all.

Underneath the three options is one boundary: in Composer the middle end (Alex) stays in portable dialects and commits to no target, and the backend is whatever commits to a target, with more than one leg (the LLVM serializer for CPU and MCU, the CIRCT path for FPGA). An `llvm.*` operation in the middle end is a category error rather than a style violation: everything in the program's semantics that the chosen target cannot express is gone the moment the commitment is made, and no other backend leg can recover it. The middle end holds the full semantic content in a form every leg can still read, so each backend commits from complete information.

For the Farscape path the vendor HAL is inside the trusted computing base, and the application ships over a vendor runtime, which is the right trade when the board is well-supported and the security surface tolerates the vendor code. The credential case tolerates neither, so the unikernel path is the one that fits. The published bare-metal record on this site historically routed the Renesas RA family through FSP header bindings, and the credential's direction supersedes that for the pure-Clef unikernel case: the RA6M5 driver layer there is Clef to the reset vector, its register definitions are derived from the FSP headers, and none of the FSP code would run on the device.

## Interrupts and the Vector Table

Interrupt handling shows the same division. On the Farscape path, the generated library would register handlers against the vendor's vector-table machinery, and application code writes an ordinary handler function. On the unikernel path, the compiler would emit the vector table as a flash-resident array: slot zero is the initial stack pointer, slot one is the reset handler, and the peripheral handlers follow, all resolved at compile time with no runtime mutation of the table. Either way the application developer writes a handler that reads like a normal Clef function, and the calling convention and register-safety obligations are the compiler's to discharge. The register regime a handler touches is the same width-typed `Reg*` handles and bounded stack arrays as the rest of the driver layer, so an interrupt path allocates nothing.

## Graduated Memory Management

Memory management follows the same per-target pattern as the hardware abstractions: the strategy would be fixed when the library is generated, transparent to application developers:

```fsharp
// Memory configuration in the generated library
// Automatically selected based on the target microcontroller
module internal MemoryConfiguration =
    let configureMemory() =
        match targetMicrocontroller with
        | STM32F030 ->
            // 4KB RAM: Pure static allocation
            StaticMemory.configure {
                heapSize = 0<bytes>
                stackSize = 1024<bytes>
                staticBuffers = 3072<bytes>
            }
        | STM32F407 ->
            // 192KB RAM: Region-based allocation
            RegionMemory.configure {
                regions = 4
                regionSize = 32<kb>
                stackSize = 16<kb>
            }
        | _ ->
            // Default configuration
            StandardMemory.configure()
```

Application developers would work with standard Clef collections and data structures, while the framework handles the mapping to the appropriate low-level memory operations. On the constrained end, the bit-banging driver layer allocates nothing at all: a register write is value-in, store-out, and buffers are static and linker-provided, an invariant the build can check. [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/) traces the graduated-memory story from stack-only allocation through actor-aware arenas.

## An Accessible Discipline

Across both paths, the aim is to move embedded development from a specialized craft toward a generalized, accessible engineering discipline. On the Farscape path, hardware abstraction becomes a community effort, and each new microcontroller library benefits every Clef developer who targets that family. On the unikernel path, the security-critical applications that could never accept an opaque vendor runtime would get a binary they can audit from the reset vector up. The same Clef source, the same compiler, and the same verification obligations carry across both.

## See also

- [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/): the substrate spectrum past the microcontroller, with the Farscape toolchain and graduated memory in depth
- [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/): the dispatch contract the freestanding profile discharges in full, down to Thread and Handler mode
- [Getting to the Heart of Unikernels]({{< ref "getting-to-the-heart-of-unikernels" >}}): freestanding versus bare-metal entry, the Cortex-M33 reset vector, and no-libc execution
- [Where Native Goes, Mobile Follows]({{< ref "where-native-goes-mobile-follows" >}}): the cross-platform native-compilation thesis this MCU target sits inside
- [Cryptographic Certainty]({{< ref "cryptographic-certainty" >}}): the Post-Quantum Credential work that drives the pure-Clef unikernel on the RA6M5
- [Cache-Conscious Memory Management](/docs/internals/hardware/cache-aware-compilation-cpu/): memory placement decided at compile time across hardware
- [Building Bulletproof eBPF Programs]({{< ref "building-bulletproof-ebpf-programs" >}}): the same platform-descriptor voice aimed at the Linux kernel, where the operating system is the device
