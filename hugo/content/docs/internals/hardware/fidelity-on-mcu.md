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

Fidelity has two routes onto a microcontroller: bind a vendor HAL, or own the reset-to-application path. The EK-RA6M5 credential work takes the second route. Its first milestone is HelloBlinky: one colored LED blinks, S1 changes color, and S2 changes the blink period. Reset, SysTick, and the two external interrupt routes establish the foundation for later Ariel, Olivier, and Prospero work.

As implemented and tested on September 9, 2026, HelloBlinky runs on the physical EK-RA6M5 through Composer's MCU/LLVM path. The implementation supplies opaque width-typed MMIO handles, volatile accesses, early 32-bit lowering, owned startup/vectors, and BAREWire-generated memory layout. S1 selects blue/green/red; S2 selects cadence. The user confirmed both controls and held-button behavior, then confirmed the requested 10% duty-cycle dimming. Exact artifacts and board traces are retained with HelloBlinky. This establishes the first finite board path; the comprehensive platform and credential runtime still have further work.

## Pre-Optimized Hardware Mapping

The intended flow preserves a register's address, access width, permissions, and effects until the backend can commit them to machine operations. The compiler should reject an unsupported access at the source boundary. The platform package supplies hardware facts, with document revisions and register sections as provenance.

A layout alone is insufficient. Write-one-to-clear bits, read side effects, unlock keys, permitted transfer widths, reserved bits, and required delays need explicit descriptions. A legal address and a correctly sized value do not establish a legal device operation.

## Two Layers

`Fidelity.Platform` describes the MCU family and the board. The EK-RA6M5 package is intended to become as extensive as the ArtyA7 package: clocks, memory, pins, interrupt routes, connectors, and peripheral capabilities belong there. HelloBlinky consumes the first verified subset; it does not define the package's eventual scope.

The application selects behavior through that package. Register addresses and board wiring should have one maintained definition. Generated bindings and manually reconciled declarations both need provenance; generation does not remove the need to check them against the hardware manual and board schematic.

## The MCU Compilation Challenge

The RA6M5 uses a Cortex-M33. Its code flash starts at `0x00000000`; `0x08000000` is the RA6M5 data-flash base, not its application code-flash base. STM32 examples using `0x08000000` for code must not be copied into this board's linker script. Memory regions and security attribution must follow the selected device and configuration. See the [RA6M5 hardware manual](https://www.renesas.com/en/document/man/ra6m5-group-users-manual-hardware), memory map and option-setting memory chapters.

The target contract must reach lowering before pointer, index, aggregate, or call layouts are fixed. Passing a Thumb target triple only to the final LLVM tool cannot repair a host-sized ABI already emitted by MLIR. The September baseline still emitted 64-bit memref descriptor fields and a hosted-shaped `main`; both need explicit treatment before reset can call the payload.

## Path One: Binding a Vendor HAL Through Farscape

A HAL binding calls the vendor's implementation under its ABI. It does not automatically replace a HAL call with an equivalent sequence of register writes. Such replacement would need a separate, validated compiler transformation covering the HAL's behavior.

### The MLIR and LLVM Pipeline

The binding path uses the existing foreign-call machinery. Native target selection, argument layouts, ownership, callbacks, and linked dependencies remain part of its contract. A generated signature is useful evidence about the boundary, but does not establish peripheral initialization or interrupt correctness.

### Parsing Hardware Headers with Farscape

Current Farscape invokes Clang for its C/C++ AST, preprocessor macros, and layout information. XParsec participates in parsing and transformation; it is not a complete replacement for Clang's C/C++ frontend. The tool is currently hosted on .NET. [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/) explains the distinction between build-tool dependencies and firmware dependencies.

CMSIS qualifiers help identify volatile and const declarations. They do not encode the complete register protocol. In particular, a C write-only qualifier commonly expands to `volatile`, which does not prohibit reads in the C type system. Register-specific access rules still require the manual or suitable device metadata.

## Path Two: A Pure-Clef Unikernel to the Reset Vector

A direct image owns startup, vectors, memory initialization, faults, and the peripherals it uses. It can use the Renesas debugger and programming tools without linking FSP, a HAL, or an RTOS into the image.

The board does not presume an RTOS. In the installed FSP reference project, `Reset_Handler` calls `SystemInit` before `main`; the application's scheduler starts later. Replacing that project transfers the startup responsibilities to our image. A small assembly boundary may still be required to establish the machine state needed by compiled Clef; that boundary belongs in the artifact and its review.

Removing a vendor runtime narrows the code shipped on the device. It does not reduce the trusted computing base to source text alone. The compiler, lowering passes, linker, startup, silicon behavior, and any boot/security configuration remain relevant assumptions. Source-level verification is not verification of the final binary unless a separate evidence chain establishes that relationship.

### The Closure Substrate

Clef's [closure representation](/spec/draft/closure-representation/) and [FFI boundary](/spec/draft/ffi-boundary/) distinguish language values from sanctioned external handles. A hardware vector requires an entry with the right calling convention and retention rules. An arbitrary captured closure cannot be placed in the vector table as though it were an exception-handler address.

The register boundary should expose the specified width-typed MMIO handles, with ordinary Clef integer values checked against each operation's range. Raw pointers and source-level machine-width aliases are not the application interface. The compiler owns address conversion and exact-width machine operations.

### The Imperative Seam

The [backend contract](/spec/draft/backend-lowering-architecture/) holds a typed MMIO operation to serialization. On the LLVM path, its access must become an appropriately sized volatile load or store. A discarded read must survive optimization, and a polling read must remain inside its loop. Ordinary memory operations or phantom sequencing tokens alone do not establish these properties.

LLVM volatile preserves the number and relative order of volatile accesses; it does not provide general synchronization with ordinary memory. CPU barriers, interrupt masking, and publication of shared RAM state require their own supported semantics. See [LLVM's volatile rules](https://llvm.org/docs/LangRef.html#volatile-memory-accesses).

A volatile read followed by a volatile write is still a read/modify/write sequence, not an atomic update. Use the peripheral's documented set/reset operation where appropriate, and follow the individual register's write semantics. Do not apply a generic read/modify/write helper to write-one-to-clear or read-sensitive registers.

### The Bring-Up as a Computation Expression

An ordered bring-up expression is a useful library design, but its syntax does not prove that effects survive lowering. A future state-transition API can prevent callers from using a peripheral before initialization; the backend must independently preserve the actual accesses and barriers. Do not advertise a bring-up computation expression as supported until it has compiler and target acceptance evidence.

For HelloBlinky, keep the sequence explicit and small: establish startup state, configure the selected GPIO and interrupt routes, arm the periodic tick, then enter the foreground loop. Use the reset clock if it meets the requirements; a high-speed PLL and the credential entropy pipeline are separate work.

### Costs and Open Work

The first gates are typed MMIO, target layout, a defined entry ABI, retained vectors, and a linkable image with no unintended runtime dependencies. Interrupt/mainline communication needs an explicit synchronization discipline. Allocation freedom and bounded handler work must be checked in reachable code and final artifacts, not inferred from compact source syntax.

## Choosing a Path

A vendor HAL is appropriate when its implementation and dependencies fit the project. The direct path is appropriate when the image needs to own and review the startup and device-control surface. Neither choice changes the need for correct register semantics or target validation.

The EK-RA6M5 work has chosen the direct path. eBPF and the actor/supervisor integration are deferred so that the first board milestone remains finite.

## Interrupts and the Vector Table

The board manual identifies the following user controls. These are three separate colored LEDs, not one RGB LED. See [EK-RA6M5 v1 User's Manual](https://www.renesas.com/document/man/ek-ra6m5-v1-users-manual?language=en), User LEDs and User and Reset Switches tables; local revision 1.01 numbers them Tables 18 and 19.

| Control | Connection | HelloBlinky behavior |
| --- | --- | --- |
| LED1, blue | P006 | Selected blink output |
| LED2, green | P007 | Selected blink output |
| LED3, red | P008 | Selected blink output |
| S1 | P005, IRQ10-DS | Cycle selected LED |
| S2 | P004, IRQ9-DS | Cycle a fixed set of blink periods |

The pin IRQ number is not by itself an NVIC vector assignment. Configure the Renesas interrupt routing, clear pending sources using their documented semantics, establish priorities, and retain both the core and external vector entries. Verify the active security state and corresponding vector/register bank.

Architectural reset on Cortex-M33 with Security Extension enters Secure state. A Non-secure application handoff is a separate startup step; memory attribution does not select the architectural reset state. See [Arm's vector-table contract](https://documentation-service.arm.com/static/5e7cd7b67158f500bd5c4f0c?token=), §2.3.4. On RA6M5, ICU event-slot attribution and NVIC interrupt targeting must agree even though their reset defaults differ (hardware manual §13.2.7–9).

Keep default fault and unassigned-interrupt handlers visible. Test reset-to-entry without debugger initialization, then SysTick, then each external route. Debounce must be bounded; holding a button must not starve the periodic tick.

The option bytes also need precise interpretation. In RA6M5 OFS0, `IWDTSTRT` bit 1 and `WDTSTRT` bit 17 use zero for automatic start. All ones does not auto-start both watchdogs. The actual provisioned value determines behavior; HelloBlinky preserves option memory, IDAU boundaries and lifecycle settings while configuring volatile pin/interrupt attribution for this execution. See the hardware manual, §6.2.1.

## Graduated Memory Management

The first image uses fixed storage and bounded work. BAREWire checks the flash/SRAM spaces and vector storage used to generate linker inputs. Its LED pins have no GPT alternate function, so 10 kHz SysTick slots provide 1 kHz software PWM at 10% duty, with application work once per PWM cycle. MOCO supplies nominal 8 MHz ICLK after a protected divider change; cadence inherits oscillator tolerance. Actor arenas, continuation dispatch, supervisor recovery, and durable credential state are subsequent consumers of a verified platform boundary. Hosted Ariel now has native carrier evidence; [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/) explains why that does not yet establish freestanding scheduler conformance.

Future DMA users also need the Renesas cache contract. RA6M5 has an S-cache that can cover internal SRAM; the hardware manual §14.8.4.2 requires software coherency when the CPU shares cached memory with another bus master. Absence of a CMSIS core D-cache macro is insufficient to dismiss that requirement. HelloBlinky does not need a DMA ring.

## An Accessible Discipline

Keep the evidence with the artifact: source and dependency revisions, target/ABI settings, compiler commands, linker map, IR and disassembly checks, and observed reset/interrupt behavior. Update the board declarations and their source manifest when a hardware fact changes. A successful build, a valid ELF, and observed silicon behavior are distinct milestones.

## See also

- [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/): substrates and actual tooling boundaries
- [Scheduling on Metal](/docs/internals/hardware/scheduling-on-metal/): the later freestanding scheduler contract
- [Bring-Up Beyond the CPU](/docs/internals/hardware/bring-up-beyond-the-cpu/): artifact and device validation on other targets
- [Backend Lowering Architecture](/spec/draft/backend-lowering-architecture/) and [FFI Boundary](/spec/draft/ffi-boundary/): normative compiler boundaries
