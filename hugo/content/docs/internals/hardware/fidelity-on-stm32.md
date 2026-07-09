---
title: "Fidelity Lowered to STM32"
linkTitle: "Fidelity Lowered to STM32"
description: "Hardware type safety at zero runtime cost through pre-optimized memory mapping"
date: 2024-01-02T16:59:54+06:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
params:
  originally_published: 2024-01-02
  original_url: "https://speakez.tech/blog/fsharp-on-metal-fidelity-lowered-to-stm32/"
  migration_date: 2026-02-15
---

Embedded development has long treated hardware control and high-level type safety as competing goals, keeping it at a distance from the abstractions common in modern software engineering. The Fidelity Framework targets hardware type safety at zero runtime cost through hardware/software co-design, so that expressiveness and efficiency do not have to be traded against each other.

> **December 2025 Update**: As part of our ongoing effort to keep the SpeakEZ blog current with laboratory progress, this article has been revised to reflect architectural evolution in the Fidelity Framework. The core vision remains unchanged: Clef on bare metal with zero runtime cost. This article provides the foundation, and the companion article [Clef on Metal Revisited](https://speakez.tech/blog/fsharp-on-metal-revisited/) presents the next progression in this evolution.

## Pre-Optimized Hardware Mapping

Traditional embedded development approaches hardware access as a runtime concern, with memory layouts and register mappings resolved during execution. Even modern "zero-cost abstractions" often carry hidden overhead through function calls, runtime checks, or suboptimal memory access patterns. The Fidelity Framework reimagines this approach through pre-optimized hardware mapping: capturing hardware semantics at the highest level of the compilation pipeline and carrying them through to native code generation.

By parsing hardware definitions (such as CMSIS headers for ARM-based microcontrollers) and transforming them into type-safe Clef abstractions that flow through MLIR to native code, we would achieve compile-time hardware type safety that generates identical machine code to hand-written assembly. Memory layouts would be optimized before the first line of MLIR is generated.

## Two Layers: Memory Mapping Below, Application Code Above

The framework separates the memory-mapping infrastructure from the code an application developer writes against it. The lower layer holds the BAREWire descriptors, the header parsing, and the MLIR transformations that provide the control an embedded target needs. The upper layer is idiomatic Clef code that reads like normal application development.

Once the memory scaffolding is in place, application code no longer references it directly. Farscape is designed to parse vendor-provided headers into memory mappings and hardware abstractions, packaging them into a focused library. Application developers import that module and work in business logic, without reaching for register offsets or bit manipulation unless they choose to.

This hardware abstraction work happens once per microcontroller family. When Farscape generates bindings for a target microcontroller (STM32, or any supported family), a later developer targeting the same hardware references the existing library and starts writing application code immediately.

```fsharp
// Above the waterline: Application code (written by any Clef developer)
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

Developers work with idiomatic Clef code while the framework holds to the performance of hand-written C. The hardware abstraction is generated once and reused by later developers targeting the same family.

## Understanding the STM32 Compilation Challenge

STM32 microcontrollers exemplify the challenges of embedded development. Built around ARM Cortex-M cores, they use the Thumb-2 instruction set and have distinct memory regions at fixed addresses. Code lives in Flash memory starting at 0x08000000, variables reside in SRAM at 0x20000000, and hardware peripherals are accessed through memory-mapped registers at addresses like 0x40020000. There is no operating system, no memory management unit, and no room for abstraction overhead.

The traditional compilation path loses information at each stage. By the time code reaches LLVM, the compiler must infer intent from memory access patterns, often missing optimization opportunities or requiring unsafe constructs to reach the desired behavior. The Fidelity Framework would invert this information flow, preserving hardware intent from the source level through to machine code.

## The MLIR and LLVM Pipeline: From Clef to Silicon

The compilation journey begins with Clef code that uses BAREWire-generated hardware abstractions. These abstractions are compile-time constructs that capture the hardware memory model and leave no runtime wrapper behind. Clef code that configures a GPIO pin expresses an intent that compiles directly to the register access pattern, with no function call standing between the source and the hardware.

The following trace shows how application code transforms through the compilation pipeline. The hardware abstraction is resolved once when the library is generated:

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

This Clef code transforms through several stages, each preserving the hardware semantics. First, Alex (enhanced with XParsec for pattern recognition) transforms the Clef AST into MLIR operations. Critically, it recognizes BAREWire patterns and translates them with pre-calculated offsets and alignment requirements.

For Memory-Mapped I/O (MMIO), volatile semantics are essential. Composer uses LLVM dialect operations within MLIR to preserve these hardware interaction guarantees:

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

Finally, LLVM's ARM backend generates optimal Thumb-2 assembly:

```armasm
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

This assembly matches what an expert embedded programmer would write by hand, generated from type-safe Clef code with register offsets and layouts resolved at compile time. The application developer who wrote the temperature monitoring code never sees any of this.

## BAREWire Pre-Optimization

The zero-cost abstraction rests on BAREWire's pre-optimization approach. Traditional compilers analyze code to infer memory layouts and optimization opportunities. BAREWire inverts this by generating optimized memory layouts at the Clef level, where the full semantic information is available.

### Parsing Hardware Headers with Farscape

The process begins with Farscape, which parses vendor-provided hardware headers. For ARM-based microcontrollers, these are typically CMSIS (Cortex Microcontroller Software Interface Standard) headers containing comprehensive descriptions of every register, bit field, and memory map in the microcontroller.

Farscape uses XParsec (the same parser combinator library that powers other parts of the Fidelity toolchain) to parse these headers directly in pure Clef. This approach eliminates dependencies on external C/C++ tooling and enables type-safe parsing with excellent error messages. Rather than raw P/Invoke bindings, the output is quotation-based hardware descriptors that integrate with the Clef compilation pipeline.

When a contributor runs Farscape on a new microcontroller's headers, it generates a complete Clef hardware abstraction library with three components: quotations encoding memory layout, active patterns for PSG recognition, and a MemoryModel record for Clef integration.

The generated library captures all hardware semantics while adding type safety:

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

Farscape transforms these C definitions into Clef quotations and active patterns that preserve the hardware semantics while adding type safety. The transformation is semantic rather than syntactic: it captures hardware intent and makes it available to the compiler at the source level. Once generated, the abstractions are usable without reference to the underlying layout.

### Platform-Specific Memory Optimization

BAREWire's true power emerges when combined with platform-specific configuration. Different microcontroller variants have different memory constraints, peripheral layouts, and optimization requirements. The generated libraries adapt to these through functional composition:

```fsharp
// Platform configuration in the generated library
// Application developers rarely need to see this
module internal PlatformOptimization =
    let stm32Config =
        PlatformConfig.base'
        |> withPlatform PlatformType.CortexM4
        |> withMemoryModel MemoryModelType.Constrained
        |> withVectorCapabilities VectorCapabilities.DSPExtensions
        |> withFlashSize (256<kb>)
        |> withSRAMSize (64<kb>)

    // BAREWire generates platform-optimized layouts
    let generateOptimizedLayout config structDef =
        match config.MemoryModel with
        | MemoryModelType.Constrained ->
            // Pack structures tightly for memory-constrained devices
            structDef |> packStructure |> optimizeAlignment 4
        | MemoryModelType.Performance ->
            // Align for optimal access patterns
            structDef |> alignFields 8 |> padForCache
        | _ ->
            structDef // Use default layout
 
```

This platform-aware optimization happens when the library is generated, producing memory layouts tailored to the specific target device. Application developers benefit from these optimizations automatically when they reference the library.

## Hardware Type Safety Without Runtime Cost

The culmination of this approach is a system designed to provide complete hardware type safety with absolutely zero runtime overhead. This isn't intended as a compromise or approximation; it represents a genuine breakthrough in hardware/software co-design.

Consider how interrupt handlers, traditionally one of the most error-prone aspects of embedded development, could become both safe and ergonomic. The complexity of interrupt vector tables and handler registration is handled by the generated library:

```fsharp
// In the generated hardware library (hidden complexity)
module internal InterruptInfrastructure =
    [<VectorTable(BaseAddress = 0x08000000u)>]
    let private vectorTable = {|
        StackPointer = __stack_end
        ResetHandler = Reset.handler
        NMIHandler = NMI.handler
        HardFaultHandler = HardFault.handler
        USART1Handler = ref Unchecked.defaultof<unit -> unit>
        // ... additional handlers
    |}

    // Public API for application developers
    module UART =
        let onDataReceived
            with set (handler: unit -> unit) =
                vectorTable.USART1Handler := handler

// Application code using the simple API
module SerialCommunication =
    open STM32F4.Hardware

    let handleIncomingData() =
        match UART.tryReceive() with
        | Some data ->
            MessageQueue.enqueue data
        | None ->
            () // No data available

    // Register the handler with one line
    do UART.onDataReceived <- handleIncomingData
```

The application developer writes a simple function to handle incoming data. Behind the scenes, the generated library ensures this handler is properly registered in the interrupt vector table, using the correct calling convention, accessing hardware registers safely. The generated machine code is identical to hand-written assembly, yet the developer experience feels like writing a normal Clef event handler.

## Real-World Impact: A New Embedded Development Paradigm

This approach aims to transform embedded development from a specialized craft to an accessible engineering discipline. The creation of hardware abstraction libraries becomes a community effort, with each new microcontroller support benefiting all Clef developers. Once a library exists in the registry, any developer can target that hardware without embedded expertise.

### Development Velocity

Traditional embedded development requires extensive testing to catch errors that manifest only at runtime. With Fidelity's approach, many of these errors are caught at compile time. More importantly, the existence of pre-generated hardware libraries means developers can start productive work immediately:

```fsharp
let implementDataLogger() = async {
    // Initialize components using high-level APIs
    let storage = SDCard.mount()
    let sensor = TemperatureSensor.connect(I2C.Bus1)

    // Business logic with no hardware details
    while true do
        let! reading = sensor.readAsync()
        let timestamp = Clock.now()

        do! storage.appendAsync($"{timestamp},{reading}")
        do! Async.Sleep 1000
}
```

### Community-Driven Hardware Support

The Farscape model creates a virtuous cycle. When a new microcontroller is released, the vendor or an interested community member can generate and publish the hardware abstraction library once. This investment benefits every Clef developer who might target that hardware in the future. Popular microcontrollers quickly gain high-quality support, while even niche hardware becomes accessible to the broader Clef community.

## Memory Management: Graduated Strategies Without Compromise

The Fidelity Framework's approach to memory management adapts to platform constraints while maintaining zero-cost abstractions. Like hardware abstractions, memory management strategies are encapsulated in the generated libraries, transparent to application developers:

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

// Application developers just use normal Clef code
module Application =
    // Memory management is handled transparently
    let processData (input: byte[]) =
        let processed = Array.map transform input
        let filtered = Array.filter isValid processed
        Array.fold accumulate State.initial filtered
```

The memory management strategy is determined when the library is generated based on the target platform. Application developers could work with standard Clef collections and data structures, while the framework handles the complexity of mapping these high-level constructs to the appropriate low-level memory operations.

## See also

- [Where Native Goes, Mobile Follows]({{< ref "where-native-goes-mobile-follows" >}}): the cross-platform native-compilation thesis this STM32 target sits inside
- [Clef on Metal Revisited](/docs/internals/hardware/on-metal-revisited/): a year of graduated-memory evolution on bare metal
- [Cache-Conscious Memory Management](/docs/internals/hardware/cache-aware-compilation-cpu/): memory placement decided at compile time across hardware
