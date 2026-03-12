---
title: "Verifying F#"
type: blog
date: 2025-05-11T16:59:54+06:00
description: "From Functional Code to Verified Binaries: The Fidelity Framework Approach"
caption: Product Design
image: images/blog/Hedgehog_Proof_Banner.png
ogfeatured: images/blog/Hedgehog_Proof_Banner.png
category: ["Architecture"]
liveLink: https://users.cs.utah.edu/~regehr/papers/pldi25.pdf
author: Houston Haynes
submitDate: May 11, 2025
dotnet_terms: []
mlir_terms: ["MLIR", "optimization"]
concepts: ["formal-verification", "type-safety", "embedded-systems"]
---

Creating software with strong correctness guarantees has traditionally forced developers to choose between practical languages and formal verification. The Fidelity Framework aims to address this challenge through an integration of [Clef](https://clef-lang.com) code, F* proofs, and MLIR's semantic dialects.

This essay explores how the Fidelity Framework builds upon the semantic verification foundations introduced in "First-Class Verification Dialects for MLIR" (Fehr et al., 2025) to create a unique pipeline designed to preserve formal verification from source code to optimized binary. The result would be a system that delivers both safety and performance, without requiring developers to become formal methods experts.

## Making F* Verification Intentions Clear in Clef

The journey begins with standard Clef code, enhanced with verification annotations that express formal properties:

```fsharp
// Clef code with verification annotations
[<F* Requires("input >= 0 && input <= 100")>]
[<F* Ensures("result >= 0 && result <= 10")>]
let normalizeScore (input: int) : int =
    if input < 0 then 0
    elif input > 100 then 10
    else input / 10
```

These annotations express the developer's intent in a familiar Clef syntax, while providing the information needed for formal verification. Developers continue working in Clef rather than switching to specialized verification languages.

For more complex verification scenarios, annotations can express sophisticated properties around memory safety:

```fsharp
// Memory safety verification through annotations
[<F* Requires("Array.length buffer > index")>]
[<F* Ensures("result = buffer.[index]")>]
[<F* EnsuresOnException("exn is IndexOutOfRangeException")>]
let safeGet (buffer: 'T array) (index: int) : 'T =
    buffer.[index]
```

## Memory Layout as a Verification Foundation

One of the most distinctive aspects of the Fidelity Framework is how it elevates memory layout to a first-class concept. Instead of treating memory layout as an artifact of compilation, the framework makes it explicitly definable and verifiable at the source level.

For structures that need explicit memory layouts, we can use concise layout annotations while still enabling verification:

```fsharp
// Memory layout with verification
[<Layout>]
[<F* Ensures("memory_safe_layout")>]
type ImageData = {
    Width: UInt32
    Height: UInt32
    [<Aligned(4)>] Pixels: Span<Byte>
}
```

What makes this approach compelling is its "pre-optimization" approach—memory layout decisions are made at the Clef level, where more semantic information is available, rather than being deferred to later compilation stages. This aims to transform what would normally be an analysis problem into a mapping exercise:

```fsharp
// Layout definition with explicit memory configuration
[<Layout(Explicit)>]
type ImageBuffer = {
    Width: Uint32
    Height: Uint32
    Channels: Uint8
    // Explicit padding handled automatically by the Layout attribute
    [<Aligned(16)>] PixelData: Array<Uint8>
}
```

This explicit memory layout would become a critical enabler for verification by making memory access patterns explicit and verifiable, eliminating abstraction gaps between high-level Clef structures and low-level memory layouts, and supporting platform-specific verification tailored to hardware constraints.

## F* Proof Generation: Automated Verification

The Fidelity Framework is designed to automatically translate F* annotations into separately executed F* verification conditions:

```fsharp
// Generated F* verification (not written by hand)
module Generated
  
  // F* verification condition for normalizeScore
  let normalize_score_verification (input: int) 
    : Tot (result:int{result >= 0 && result <= 10})
    = requires (input >= 0 && input <= 100)
      if input < 0 then 0
      elif input > 100 then 10
      else input / 10
```

This translation layer is key to the Fidelity approach—developers would never need to write F* code directly. Instead, the framework performs a source-to-source transformation that preserves the original Clef logic while adding the dependent types and verification conditions that F* can process.

For memory safety verification with defined layouts, the translation becomes even more powerful:

```fsharp
// Generated F* verification with memory layout awareness
let get_pixel_verification (buffer:image_buffer{has_layout buffer ImageBufferLayout})
                         (x:int{0 <= x && x < buffer.width})
                         (y:int{0 <= y && y < buffer.height})
  : Tot byte (ensures (fun result -> 
      result == buffer.pixel_data.[y * buffer.width + x]))
  = 
  // The verification can reason about memory access because
  // it knows the exact layout of the ImageBuffer structure
  buffer.pixel_data.[y * buffer.width + x]
```

The F* verifier then processes these generated conditions to prove or disprove the specified properties. The results of this verification become certified evidence that travels with the code through the rest of the compilation pipeline.

## XParsec: The Verification Bridge

A critical component in this verification pipeline is XParsec, which serves as the "glue" connecting memory layouts to F* verification:

```fsharp
// XParsec parser for memory layout
let parseLayout =
    parser {
        // Parse the layout header
        let! magic = pUInt32LE
        let! version = pUInt16LE
        let! fieldCount = pUInt16LE
        
        // Parse field definitions
        let! fields = parray (int fieldCount) parseField
        
        return {
            Magic = magic
            Version = version
            Fields = fields |> Array.ofSeq
        }
    }
```

XParsec is designed to enable the Fidelity Framework to:
1. Parse Clef code with layout and verification annotations
2. Extract memory layout information
3. Generate corresponding F* verification conditions
4. Ensure verification properties are preserved in the MLIR translation

## MLIR SMT Dialect as Verification Carrier

After F* verification, the code is translated to MLIR, with verification properties expressed through the SMT dialect:

```mlir
func.func @normalizeScore(%input: i32) -> i32 {
  // Input precondition using SMT dialect
  %zero = arith.constant 0 : i32
  %hundred = arith.constant 100 : i32
  %input_gte_zero = smt.bv.sge %input, %zero : !smt.bv<32>
  %input_lte_hundred = smt.bv.sle %input, %hundred : !smt.bv<32>
  %precondition = smt.and %input_gte_zero, %input_lte_hundred : !smt.bool
  smt.assert %precondition

  // Function implementation
  %c0 = arith.constant 0 : i32
  %c10 = arith.constant 10 : i32
  %c100 = arith.constant 100 : i32
  
  %lt_zero = arith.cmpi slt, %input, %c0 : i32
  %gt_hundred = arith.cmpi sgt, %input, %c100 : i32
  
  %div10 = arith.divsi %input, %c10 : i32
  
  %result = scf.if %lt_zero -> i32 {
    scf.yield %c0 : i32
  } else {
    %temp = scf.if %gt_hundred -> i32 {
      scf.yield %c10 : i32
    } else {
      scf.yield %div10 : i32
    }
    scf.yield %temp : i32
  }
  
  // Postcondition using SMT dialect
  %result_gte_zero = smt.bv.sge %result, %zero : !smt.bv<32>
  %result_lte_ten = smt.bv.sle %result, %c10 : !smt.bv<32>
  %postcondition = smt.and %result_gte_zero, %result_lte_ten : !smt.bool
  smt.assert %postcondition
  
  return %result : i32
}
```

This MLIR representation captures both the operational semantics of the function and its verification properties. The SMT dialect would provide a mechanism to express and verify constraints throughout the compilation process.

From the PLDI paper, the Fidelity Framework leverages the concept of "verification as first-class dialect" - the ability to express verification constraints directly within the IR, rather than as external artifacts that can become disconnected from the code.

## Verification-Preserving Optimization

When optimizations are applied to the MLIR representation, the SMT dialect ensures that verification properties are preserved:

```mlir
// Optimization transformation with verification check
%original_code = ...
%optimized_code = applyOptimization(%original_code)

// Verification that optimization preserves properties
%original_properties = extractProperties(%original_code)
%optimized_properties = extractProperties(%optimized_code)
%preserved = smt.implies %original_properties, %optimized_properties : !smt.bool
smt.assert %preserved
```

If an optimization would violate a verified property, the SMT solver rejects the transformation, ensuring that all optimizations respect the verified constraints. (This is precisely the approach demonstrated in the PLDI paper, where the authors found five miscompilation bugs in upstream MLIR through this verification approach.) 

The translation validation method becomes a key component of the Fidelity Framework's verification process:

```mlir
func.func @validate_transformation(%src: !mlir.operation, %tgt: !mlir.operation) -> i1 {
  // Extract SMT formulas representing the semantics of both operations
  %src_formula = smt.extract_semantics %src : !mlir.operation -> !smt.formula
  %tgt_formula = smt.extract_semantics %tgt : !mlir.operation -> !smt.formula
  
  // Check that target refines source (preserves properties)
  %refines = smt.implies %src_formula, %tgt_formula : !smt.bool
  smt.check_sat %refines : !smt.bool
  
  // Return result of verification
  %result = smt.is_sat %refines : !smt.bool
  return %result : i1
}
```

This validation provides strong guarantees that the optimizer cannot introduce behaviors that violate the verified properties, even when applying aggressive optimizations.

## Platform-Aware Memory Verification

What makes this approach particularly powerful for verification is its platform-aware nature. Memory layouts can be tailored to specific hardware characteristics through functional composition:

```fsharp
let createOptimizedImageBuffer (config: PlatformConfiguration) =
    match config.Platform, config.MemoryModel with
    | PlatformType.Embedded, MemoryModelType.Constrained ->
        // Packed layout for embedded systems
        {
            Layout = Layout.Packed
            Width = FieldType.UInt16
            Height = FieldType.UInt16
            Channels = FieldType.UInt8
            PixelData = FieldType.Array(FieldType.UInt8)
        }
    | _, _ when config.VectorCapabilities = VectorCapabilities.Advanced ->
        // SIMD-optimized layout with alignment
        {
            Layout = Layout.Aligned(16)
            Width = FieldType.UInt32
            Height = FieldType.UInt32
            Channels = FieldType.UInt8
            PixelData = FieldType.Array(FieldType.UInt8)
        }
    | _ -> // Default layout...
```

This enables verification that is sensitive to platform-specific memory constraints:

1. **Embedded systems**: Verification ensures that memory layout is compact and fits within tight constraints
2. **SIMD platforms**: Verification ensures that memory is properly aligned for vector operations
3. **GPUs**: Verification ensures memory layouts compatible with GPU access patterns

## Memory Layout Verification with MLIR

The integration of memory layout definitions with verification creates a powerful system for ensuring memory safety. When combined with the SMT dialect, this enables verification of memory access patterns:

```fsharp
// Clef code with memory layout and verification
[<Layout>]
type ImageBuffer = {
    Width: UInt32
    Height: UInt32
    Pixels: Span<Byte>
}

[<F* Requires("x >= 0 && x < image.Width")>]
[<F* Requires("y >= 0 && y < image.Height")>]
[<F* Ensures("result = image.Pixels.[y * image.Width + x]")>]
let getPixel (image: ImageBuffer) (x: int) (y: int) : byte =
    image.Pixels.[y * int image.Width + x]
```

This translates to MLIR with memory access verification:

```mlir
func.func @getPixel(%image: !memref<{layout="ImageBuffer"}>, %x: i32, %y: i32) -> i8 {
  // Preconditions for x and y
  %width = memref.load %image[0] : !memref<{layout="ImageBuffer"}>
  %height = memref.load %image[4] : !memref<{layout="ImageBuffer"}>
  
  // Verify x is in bounds
  %zero = arith.constant 0 : i32
  %x_gte_zero = smt.bv.sge %x, %zero : !smt.bv<32>
  %x_lt_width = smt.bv.slt %x, %width : !smt.bv<32>
  %x_in_bounds = smt.and %x_gte_zero, %x_lt_width : !smt.bool
  smt.assert %x_in_bounds
  
  // Verify y is in bounds
  %y_gte_zero = smt.bv.sge %y, %zero : !smt.bv<32>
  %y_lt_height = smt.bv.slt %y, %height : !smt.bv<32>
  %y_in_bounds = smt.and %y_gte_zero, %y_lt_height : !smt.bool
  smt.assert %y_in_bounds
  
  // Calculate pixel offset
  %offset_y = arith.muli %y, %width : i32
  %offset = arith.addi %offset_y, %x : i32
  
  // Get pixel value
  %pixels_base = memref.subview %image[8] : !memref<{layout="ImageBuffer"}> to memref<?xi8>
  %pixel = memref.load %pixels_base[%offset] : memref<?xi8>
  
  // Return pixel value
  return %pixel : i8
}
```

The layout information translates directly to memory access patterns that can be verified using the SMT dialect, ensuring memory safety without runtime overhead.

## Zero-Runtime-Cost Memory Safety

The combination of memory mapping and F* verification could enable a capability that has long been considered the "holy grail" of systems programming: zero-runtime-cost memory safety. By moving all verification to compile time, the Fidelity Framework aims to eliminate the need for runtime bounds checks while still guaranteeing memory safety.

Consider this typical pattern:

```fsharp
// Memory-safe array access without runtime checks
[<F* Requires("0 <= index && index < Array.length array")>]
[<F* Ensures("result = array.[index]")>]
let inline getElement (array: 'T[]) (index: int) : 'T =
    // No runtime bounds check needed - verified at compile time
    array.[index]
```

Through verification, the compiler could prove that all calls to `getElement` are safe, eliminating the need for runtime bounds checking without sacrificing safety. This transforms what would normally be a trade-off between safety and performance into a compile-time concern only.

## Example: Verified Image Processing

Let's examine a complete example showing how the Fidelity Framework integrates Clef annotations, memory layouts, F* verification, and MLIR SMT dialect for a verified image processing function:

```fsharp
// Clef implementation with verification annotations
[<F* Requires("src.Width = dst.Width && src.Height = dst.Height")>]
[<F* Ensures("forall (x, y) in (0..src.Width-1, 0..src.Height-1). 
           dst.GetPixel(x, y) = byte(min 255 (2 * int(src.GetPixel(x, y)))))")>]
let brighten (src: ImageBuffer) (dst: ImageBuffer) : unit =
    for y in 0 .. int src.Height - 1 do
        for x in 0 .. int src.Width - 1 do
            let pixel = src.GetPixel(x, y)
            let brightened = byte(min 255 (2 * int pixel))
            dst.SetPixel(x, y, brightened)
```

This is automatically translated to F* verification conditions:

```fsharp
// Generated F* verification
module Generated

  // Verification for brighten function
  let brighten_verification (src: image_buffer) (dst: image_buffer)
    : Tot (ensures (fun _ -> 
             (forall (x y: int). 
                (0 <= x && x < src.width && 0 <= y && y < src.height) ==>
                (dst.get_pixel x y = min 255 (2 * src.get_pixel x y)))))
    = requires (src.width = dst.width && src.height = dst.height)
      
      // Implementation is translated from Clef
      for y in 0 .. (src.height - 1) do
        for x in 0 .. (src.width - 1) do
          let pixel = src.get_pixel x y in
          let brightened = min 255 (2 * pixel) in
          dst.set_pixel x y brightened
```

After F* verification, this is lowered to MLIR with verification properties preserved through the SMT dialect:

```mlir
func.func @brighten(%src: !memref<{layout="ImageBuffer"}>, %dst: !memref<{layout="ImageBuffer"}>) {
  // Verification of dimension compatibility
  %src_width = memref.load %src[0] : !memref<{layout="ImageBuffer"}>
  %src_height = memref.load %src[4] : !memref<{layout="ImageBuffer"}>
  %dst_width = memref.load %dst[0] : !memref<{layout="ImageBuffer"}>
  %dst_height = memref.load %dst[4] : !memref<{layout="ImageBuffer"}>
  
  %width_eq = smt.bv.eq %src_width, %dst_width : !smt.bv<32>
  %height_eq = smt.bv.eq %src_height, %dst_height : !smt.bv<32>
  %dims_match = smt.and %width_eq, %height_eq : !smt.bool
  smt.assert %dims_match
  
  // Loop implementation
  %c0 = arith.constant 0 : i32
  %c1 = arith.constant 1 : i32
  %c2 = arith.constant 2 : i32
  %c255 = arith.constant 255 : i32
  
  %height_minus_1 = arith.subi %src_height, %c1 : i32
  scf.for %y = %c0 to %height_minus_1 step %c1 {
    %width_minus_1 = arith.subi %src_width, %c1 : i32
    scf.for %x = %c0 to %width_minus_1 step %c1 {
      // Get source pixel
      %pixel = call @getPixel(%src, %x, %y) : (!memref<{layout="ImageBuffer"}>, i32, i32) -> i8
      
      // Brighten the pixel
      %pixel_int = arith.extui %pixel : i8 to i32
      %doubled = arith.muli %pixel_int, %c2 : i32
      %clamped = arith.minsi %doubled, %c255 : i32
      %bright_pixel = arith.trunci %clamped : i32 to i8
      
      // Set destination pixel
      call @setPixel(%dst, %x, %y, %bright_pixel) : (!memref<{layout="ImageBuffer"}>, i32, i32, i8) -> ()
    }
  }
  
  // Postcondition verification embedded as SMT operations
 
  return
}
```

Through optimization and lowering, the SMT dialect ensures that the verification properties are preserved, ultimately producing a verified binary without runtime verification overhead.

## The Fidelity Framework Difference

The Fidelity Framework's integrated approach to verification offers several key advantages:

1. **Developer Experience**: Developers work in Clef with simple annotations, not specialized verification languages
2. **Compilation-Time Verification**: All verification happens during compilation, not at runtime
3. **Verification-Preserving Optimization**: Optimizations are verified to preserve proven properties
4. **Zero-Cost Verification**: No runtime overhead for verification in the final binary
5. **Integration with Memory Management**: Memory layouts become part of the verification model
6. **Platform-Specific Optimization**: Memory layouts and verification can be tailored to specific hardware constraints

This approach is enabled by the combination of F*'s dependent type system, pre-optimized memory layouts, XParsec's translation capabilities, and MLIR's SMT dialect for representing and verifying constraints throughout compilation.

## Toward Auto-Generated Verification

While the annotation-based approach described in this article makes verification more accessible, our longer-term vision is even more ambitious: a truly idiomatic Clef development experience where verification annotations are automatically generated rather than manually written by developers.

The explicit annotations shown throughout this article serve to elucidate the verification concepts, but they aren't necessarily the end-state developer experience we envision. The Fidelity Framework is designed with this evolution in mind, leveraging several key capabilities:

1. **Pattern Recognition**: XParsec's advanced parsing capabilities can identify common Clef code patterns that imply verification properties
2. **Memory Layout Inference**: Layout definitions contain rich type information that can be analyzed to automatically generate safety properties
3. **Contextual Verification**: Many verification conditions can be derived from the context in which functions are used

For example, the memory layout definition:

```fsharp
[<Layout>]
type ImageBuffer = {
    Width: UInt32
    Height: UInt32
    Pixels: Span<Byte>
}
```

Already contains the necessary information to infer memory safety properties for common access patterns. Rather than requiring developers to write explicit bounds-checking annotations, tooling could analyze the structure definition and access patterns to automatically generate appropriate verification conditions.

This capability bring us to our track for building professional development tooling. We have plans for Visual Studio Code and JetBrains Rider extensions that:

1. Automatically generates F* verification conditions from idiomatic Clef code
2. Provides real-time visualization of verification results as developers write code
3. Offers intelligent suggestions to make code verifiable when issues are detected
4. Maintains traceability between Clef code, F* proofs, and MLIR representations

Such tooling would preserve the clean, idiomatic Clef development experience while adding the power of formal verification without requiring developers to learn F* or write verbose annotations. This represents our vision for the future of verified programming: one where verification becomes an invisible yet powerful part of everyday development, rather than a specialized activity requiring formal methods expertise. It would be a true force multiplier for development teams and enable degrees of freedom in targeting an ever-wider array of hardware and systems.

## Patent-Pending Innovation

SpeakEZ has a patent pending for this innovation: "System and Method for Verification-Preserving Compilation Using Formal Certificate Guided Optimization" (US 63/786,264). This patent application covers the unique approach to verification-preserving compilation that the Fidelity Framework represents.

As computing evolves toward greater specialization of hardware, the challenge of correctly interfacing with these diverse architectures becomes increasingly critical. SpeakEZ's innovation addresses this challenge by providing a formal verification framework that adapts to the specific characteristics of different hardware platforms while maintaining strong correctness guarantees.

The patent-pending technology enables a new approach to hardware/software co-design, where verification properties can be maintained across the entire compilation pipeline despite aggressive optimizations targeting specialized hardware. This becomes especially important as heterogeneous computing environments with CPUs, GPUs, FPGAs, and domain-specific accelerators become the norm rather than the exception.

## The Future of Verified Programming

The integration of Clef, memory layouts, F* verification, and MLIR in the Fidelity Framework represents a significant advance in making formal verification practical for everyday development. By elevating memory layout to a first-class concept that can be explicitly defined, optimized, and verified at the source level, the framework eliminates a major source of complexity in the verification process.

This approach not only simplifies verification but makes it more powerful, enabling strong guarantees about memory safety, alignment, and access patterns that would be difficult or impossible to achieve in traditional verification systems. The result would be a system that delivers both safety and performance without requiring developers to become formal methods experts.

As development continues, the Fidelity Framework will extend this approach to cover an even wider range of verification properties, including concurrency safety, resource usage constraints, and domain-specific requirements. The foundation laid by the "First-Class Verification Dialects for MLIR" paper provides the technical basis for this ambitious vision - a world where verified programming becomes the norm rather than the exception.

---

## Reference

1. Fehr, M., Fan, Y., Pompougnac, H., Regehr, J., & Grosser, T. (2025). First-Class Verification Dialects for MLIR. Proceedings of the ACM on Programming Languages, 9(PLDI), Article 206. https://users.cs.utah.edu/~regehr/papers/pldi25.pdf
