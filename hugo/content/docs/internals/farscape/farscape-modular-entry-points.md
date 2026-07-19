---
title: "Farscape's Modular Entry Points"
linkTitle: "Farscape Entry Points"
description: "How Fidelity Can Create Zero-Cost Drop-In Replacements for C/C++ Tools"
date: 2025-06-15
authors: ["Houston Haynes"]
tags: ["Design", "Architecture", "Innovation"]
params:
  originally_published: 2025-06-15
  migration_date: 2026-03-12
---

The Fidelity framework's Farscape CLI addresses a pressing challenge in modern software development: how to enhance the safety of battle-tested C/C++ tools without disrupting the countless systems that depend on them. Every day, organizations rely on command-line tools like OpenSSL, libzip, and many others that represent decades of engineering expertise but carry the inherent memory safety risks of their C/C++ heritage. Building on Farscape's current capability -- generating `[<FidelityExtern>]` attributed [Clef](https://clef-lang.com) binding declarations from C headers against the [FFI boundary semantics](/spec/draft/ffi-boundary/) the specification defines -- the "shadow-api" design vision extends the tool toward a more ambitious goal: generating drop-in replacements for widely deployed tools that preserve the original interface exactly while adding type and memory safety. The companion [C++ Binding via Farscape](/docs/internals/farscape/binding-cpp-to-clef-in-farscape/) article covers the same tooling reaching from C into C++ libraries.

The shadow-api design produces transparent recompilations to native binaries rather than bindings or wrappers. Given the same inputs, a replacement passes the original's integration tests and produces outputs identical to its C/C++ predecessor. The safety abstractions employ Clef's zero-cost abstraction principles, where all type safety and bounds checking compile away to efficient native code, unlike many C++ safety extensions or custom compiler approaches that can impose significant runtime overhead. [The Farscape Bridge]({{< ref "the-farscape-bridge" >}}) introduces the broader vision these entry points serve.

## Farscape's Dual Approach

Farscape offers two complementary modes for enhancing C/C++ code safety:

**standard-lib mode** *(implemented)*: Creates `[<FidelityExtern>]` attributed Clef binding declarations and Layer 2 idiomatic wrappers that integrate C/C++ libraries directly into Fidelity applications. Farscape uses clang's two-pass parsing with XParsec post-processing, and the `[<FidelityExtern>]` attribute carries library name and symbol metadata through the entire pipeline so Alex can emit MLIR with appropriate binding strategy attributes. Current focus is libc dynamic binding.

**shadow-api mode** *(design vision)*: Extends the standard-lib foundation to generate complete Fidelity projects that compile to drop-in replacements for existing command-line tools, keeping the external interface unchanged while enforcing memory and type safety internally.

```mermaid
graph TD
    subgraph Original["Original C/C++ Tool"]
        CTOOL[C/C++ Binary]
        UNSAFECODE[Memory Unsafe Code]
        CAPI[Command-Line Interface]
    end

    subgraph FarscapeProcess["Farscape shadow-api Generation"]
        ANALYZE[Header & Binary Analysis]
        CODEGEN[Type-Safe Clef Generation]
        SAFETY[Memory Safety Integration]
        COMPILE[Composer Compilation]
    end

    subgraph SafeReplacement["Drop-In Replacement"]
        SAFETOOL[Type-Safe Binary]
        SAMEAPI[Identical CLI Interface]
        COMPATIBLE[100% Compatible Behavior]
    end

    Original --> FarscapeProcess
    ANALYZE --> CODEGEN
    CODEGEN --> SAFETY
    SAFETY --> COMPILE
    FarscapeProcess --> SafeReplacement
```

## OpenSSL

OpenSSL provides cryptographic functionality through a command-line interface that systems from web servers to embedded devices depend on. Its memory safety record is what makes it a target for a safe replacement:

```bash
# Standard OpenSSL usage - interface must remain identical
openssl genrsa -out private.key 2048
openssl req -new -key private.key -out request.csr -subj "/CN=example.com"
openssl x509 -req -in request.csr -signkey private.key -out certificate.crt -days 365
openssl enc -aes-256-cbc -in secret.txt -out secret.enc -k mypassword
openssl dgst -sha256 document.pdf
openssl rand -hex 32
```

Any organization using OpenSSL has built critical processes around this exact interface. Certificates are generated using these precise commands, scripts expect specific output formats, and integration tests verify exact behavior. A drop-in replacement must match this interface exactly while eliminating the memory safety vulnerabilities that have plagued OpenSSL throughout its history.

## Generating the Drop-In Replacement

Farscape transforms OpenSSL into a memory-safe equivalent through header and binary analysis and code generation:

```powershell
# Generate shadow-api replacement for OpenSSL
farscape generate --type shadow-api --headers openssl/ssl.h openssl/evp.h `
                             --libraries "ssl,crypto" `
                             --output openssl
```

This command analyzes both the OpenSSL binary interface and its underlying libraries, producing a complete Fidelity project structure:

```
openssl_safe/
|-- OpenSSLSafe.fidproj       # Fidelity project configuration
|-- src/
|   |-- main.clef               # Main entry point with command routing
|   |-- keygen.clef            # RSA/DSA key generation
|   |-- certificates.clef      # X.509 certificate operations
|   |-- encryption.clef        # Symmetric encryption operations
|   |-- hashing.clef           # Cryptographic hashing
|   `-- random.clef            # Secure random number generation
|-- tests/
|   `-- compatibility.clef     # Tests ensuring OpenSSL compatibility
`-- README.md                # Generated documentation
```

The resulting project compiles to a binary that can literally replace the original OpenSSL executable:

```bash
# Build the safe replacement
cd openssl
clefpak build

# Deploy as drop-in replacement
sudo cp ./bin/openssl /usr/bin/openssl

# All existing scripts and processes work unchanged
./existing_script.sh  # Uses memory-safe OpenSSL transparently
 
```

## Type-Safe Implementation

The generated Clef implementation keeps OpenSSL's exact interface, with the safety work carried in the types:

```fsharp
// OpenSSLSafe/src/main.clef - Drop-in compatible entry point
module OpenSSLSafe.Main

open Fidelity.Platform
open OpenSSLSafe.KeyGen
open OpenSSLSafe.Certificates
open OpenSSLSafe.Encryption

// Main entry point maintaining exact OpenSSL compatibility
[<EntryPoint>]
let main args =
    if Array.isEmpty args then
        printOpenSSLUsage()
        1
    else
        let subcommand = args.[0]
        let subArgs = Array.sub args 1 (args.Length - 1)

        match subcommand with
        | "genrsa" ->
            // Type-safe RSA key generation with identical output format
            handleGenRSA subArgs
        | "req" ->
            // Type-safe certificate request generation
            handleCertificateRequest subArgs
        | "x509" ->
            // Type-safe X.509 operations
            handleX509Operations subArgs
        | "enc" ->
            // Type-safe symmetric encryption
            handleEncryption subArgs
        | "dgst" ->
            // Type-safe hashing operations
            handleDigest subArgs
        | "rand" ->
            // Type-safe random number generation
            handleRandom subArgs
        | "version" ->
            // Maintain version compatibility while indicating safety
            printfn "OpenSSL 3.0.0 (Fidelity-Safe)"
            0
        | cmd ->
            eprintfn "openssl:Error: '%s' is an invalid command." cmd
            eprintfn ""
            printOpenSSLUsage()
            1

// Type-safe RSA key generation maintaining exact OpenSSL behavior
let handleGenRSA (args: string[]) : int =
    match parseGenRSAArgs args with
    | Ok config ->
        // Open output file descriptor (stdout if "-")
        let fd = if config.OutputFile = "-" then 1l
                 else Fidelity.Libc.IO.openFile config.OutputFile
        match generateRSAKeySafe config.KeySize config.PublicExponent with
        | Ok rsaKey ->
            match writeRSAKeyPEM rsaKey fd with
            | Ok () ->
                if config.Verbose then
                    printfn "Generating RSA private key, %d bit long modulus" config.KeySize
                0
            | Error msg ->
                eprintfn "unable to write 'random state'"
                eprintfn "%s" msg
                1
        | Error msg ->
            eprintfn "%s" msg
            1
    | Error usage ->
        eprintfn "%s" usage
        1
```

## Safety Infrastructure

The shadow-api replacement enforces at compile time what the original C implementation leaves to programmer discipline:

```fsharp
// OpenSSL/src/keygen.clef - Memory-safe key generation
module OpenSSLSafe.KeyGen

open Fidelity.Platform
open FSharp.NativeInterop

// Type-safe key representation with scope-based cleanup
type SafeRSAKey = {
    KeySize: int
    Modulus: nativeptr<byte>
    ModulusLen: int
    PublicExponent: int64
    PrivateData: nativeptr<byte>
    PrivateLen: int
}

// Cryptographically secure memory clearing that won't be optimized away
let secureZero (ptr: nativeptr<byte>) (size: int) =
    for i = 0 to size - 1 do
        NativePtr.set ptr i 0uy
    // Platform-specific volatile barrier prevents optimizer from eliding the writes
    Platform.Bindings.Libc.Memory.memoryBarrier ()

// RSA key generation with compile-time validated constraints
let generateRSAKeySafe (keySize: int) (publicExp: int64) : Result<SafeRSAKey, string> =
    // These safety checks compile to zero-cost native code
    // Clef's type erasure eliminates runtime overhead for type-level guarantees
    if keySize < 1024 then
        Error "Key size too small. Minimum is 1024 bits"
    elif keySize > 16384 then
        Error "Key size too large. Maximum is 16384 bits"
    elif publicExp <> 3L && publicExp <> 65537L then
        Error "Invalid public exponent. Must be 3 or 65537"
    else
        // Use platform CSPRNG for prime generation
        let p = generateSafePrime (keySize / 2)
        let q = generateSafePrime (keySize / 2)

        // Ensure p and q are sufficiently different (OpenSSL compatibility)
        if primesTooClose p q keySize then
            Error "Generated primes too close together"
        else
            let n = bigMul p q
            let phi = bigMul (bigSub p 1L) (bigSub q 1L)

            // Calculate private exponent with bounds checking
            match modularInverse publicExp phi with
            | Some d ->
                let privateData = NativePtr.stackalloc<byte> (keySize / 8)
                encodePrivateKey d privateData

                Ok {
                    KeySize = keySize
                    Modulus = n
                    ModulusLen = keySize / 8
                    PublicExponent = publicExp
                    PrivateData = privateData
                    PrivateLen = keySize / 8
                }
            | None ->
                Error "Unable to compute private exponent"

// PEM output maintaining exact OpenSSL format
let writeRSAKeyPEM (key: SafeRSAKey) (fd: int32) : Result<unit, string> =
    let pemHeader = "-----BEGIN RSA PRIVATE KEY-----\n"
    let pemFooter = "\n-----END RSA PRIVATE KEY-----\n"

    // Encode key data in exact OpenSSL DER format
    match encodeToDER key with
    | Error e -> Error e
    | Ok (derData, derLen) ->
        let base64Data = base64Encode derData derLen
        // Write PEM to file descriptor
        match Fidelity.Libc.IO.write fd pemHeader with
        | Error e -> Error $"Write failed: {e}"
        | Ok _ ->
        match Fidelity.Libc.IO.write fd base64Data with
        | Error e -> Error $"Write failed: {e}"
        | Ok _ ->
        match Fidelity.Libc.IO.write fd pemFooter with
        | Error e -> Error $"Write failed: {e}"
        | Ok _ ->
            // Secure-zero the private key material before returning
            secureZero key.PrivateData key.PrivateLen
            Ok ()
```

## The Many Doors Capability

Beyond drop-in replacements, Farscape offers architectural flexibility for libraries with multiple top-level operations. Consider libzip, which handles ZIP archive operations through several distinct functions that each have their own entry point.

For libraries like libzip, Farscape can generate what we call a "many doors" architecture, multiple entry points that can either be compiled into separate tools or combined into a single compatible binary:

```bash
# Farscape can analyze libzip and identify distinct operations
farscape generate --type shadow-api --headers zip.h \
                    --style many-doors --output libzip --outputtype single
```

This generates a project structure with multiple focused entry points:

```fsharp
// libzip/src/main.clef - Multiple entry points for distinct operations
module LibZipSafe

// Individual entry points for specific operations
[<EntryPoint("zip-create")>]
let createMain args =
    // Focused on ZIP creation with type-safe file handling
    match parseCreateArgs args with
    | Ok config -> createZipArchive config
    | Error msg -> eprintfn "%s" msg; 1

[<EntryPoint("zip-extract")>]
let extractMain args =
    // Focused on extraction with bounds checking
    match parseExtractArgs args with
    | Ok config -> extractZipArchive config
    | Error msg -> eprintfn "%s" msg; 1

[<EntryPoint("zip-list")>]
let listMain args =
    // Read-only operations for listing contents
    match parseListArgs args with
    | Ok config -> listZipContents config
    | Error msg -> eprintfn "%s" msg; 1

[<EntryPoint("zip-test")>]
let testMain args =
    // Integrity testing without extraction
    match parseTestArgs args with
    | Ok config -> testZipIntegrity config
    | Error msg -> eprintfn "%s" msg; 1
```

The many doors architecture provides several benefits for appropriate libraries:

- **Focused Implementation**: Each entry point can be optimized for its specific operation
- **Selective Verification**: SMT proofs can be tailored to the specific safety requirements of each operation
- **Deployment Flexibility**: Organizations can deploy individual tools or the complete compatible binary
- **Testing Granularity**: Each operation can be tested and verified independently

However, the choice of single versus multiple entry points depends on the library's natural structure. Most tools, OpenSSL among them, suit a single entry point that matches the original interface.

## Real-World Impact

The shadow-api approach has practical consequences in four areas:

**Security Enhancement**: Memory safety vulnerabilities in tools like OpenSSL have triggered numerous security incidents. A drop-in replacement could eliminate entire classes of vulnerabilities while maintaining operational continuity.

**Gradual Adoption**: Organizations can harden foundational tools without the coordination an interface change would require. Scripts, automation, and integration tests continue working unchanged.

**Verification Capability**: SMT verification through Z3 enables machine-checked proofs over key operations, providing guarantees that would be difficult to establish for a C implementation.

**Performance Preservation**: Composer's compilation ensures that safety improvements don't compromise the performance that organizations depend on. The zero-cost abstraction principle means the type safety measures, bounds checking, and verification annotations are erased during compilation. The resulting native code performs identically to hand-optimized C.

Consider a real deployment scenario:

```bash
# Current production process (risky but works)
./deploy_certificates.sh  # Uses OpenSSL with memory safety risks

# After Farscape replacement deployment
sudo cp openssl_safe/bin/openssl /usr/bin/openssl

# Same production process (now memory-safe)
./deploy_certificates.sh  # Now uses verified-safe implementation
 
```

## Expanding the Safe Ecosystem

The shadow-api approach scales across the ecosystem of foundational C/C++ tools:

**Cryptographic Tools**: OpenSSL, GnuPG, and similar tools gain memory safety and verification
**Database Tools**: SQLite command-line tools become memory-safe while maintaining compatibility
**Compression Tools**: gzip, tar, and archive utilities gain bounds checking and safe memory management
**System Tools**: Network utilities, file system tools, and system administration commands become safer

Each follows the same pattern. Farscape analyzes the original tool and generates a type-safe Clef implementation. Composer compiles it to a native binary. Integration tests run, proofs are reviewed and validated, and the binary deploys as a drop-in replacement.

## Operational Continuity

Farscape's shadow-api mode targets a standing problem in software engineering: making established C/C++ infrastructure safer without disrupting the systems built around it. By generating drop-in replacements that preserve the original interface while their safety checks compile away to native code, this approach lets organizations strengthen their security posture through gradual adoption.

The many doors capability adds architectural flexibility for libraries where multiple entry points earn their keep, while the drop-in replacement stays the primary aim: safer versions of the tools we depend on, with no change to how we use them.

This shadow-api capability complements Farscape's standard-lib mode, detailed in ["The Farscape Bridge"](/blog/the-farscape-bridge/), by providing a complete spectrum of integration options. Whether embedding C libraries into Fidelity applications or creating safe replacements for system-wide deployment, Farscape delivers native-code performance and compile-time safety in the same binary.
