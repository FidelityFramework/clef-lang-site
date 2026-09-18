---
title: "Clef Editor Integration"
linkTitle: "Editor Integration"
description: "Compiler-owned project checking and editor queries through Lattice"
date: 2025-12-06
lastmod: 2026-09-18
aliases:
  - /docs/tooling/clef-autocomplete-integration/
authors: ["Houston Haynes"]
tags: ["Design", "Architecture"]
weight: 10
params:
  originally_published: 2025-12-06
  migration_date: 2026-03-12
---

Lattice connects editors to our Clef Compiler Service (CCS) through the Language Server Protocol. CCS owns project loading and semantic analysis. The editor presents compiler results against the source version that produced them, while Composer consumes the compiler's facts for lowering.

The [Lattice design](/docs/tooling/leveling-up-with-lattice/) describes the editor experience and its Ionide lineage. The [shared integration document](https://github.com/FidelityFramework/Composer/blob/main/docs/Lattice_Integration.md) assigns responsibilities across CCS and the client repositories. The local server resides in Composer beside its CCS integration.

## Compiler-owned project context

A Clef workspace uses `.clef` source files and a TOML `.fidproj` manifest. CCS loads source order and dependencies from that manifest, including the platform declarations required for the selected target. Lattice sends unsaved source contents to the same project-checking service. The client can retain editing buffers without reconstructing a separate semantic model.

The [HelloDimensionsProof manifest](https://github.com/FidelityFramework/Composer/blob/main/samples/lattice/HelloDimensionsProof/HelloDimensionsProof.fidproj) provides an ordered two-file example:

```toml
[package]
name = "HelloDimensionsProof"
version = "0.1.0"
description = "Measured hover and compiler-generated string/buffer obligations for Lattice"

[compilation]
target = "cpu"

[dependencies]
platform = { path = "../../../../Fidelity.Platform/Profiles/Linux_x86_64_Default/Fidelity.Platform.fidproj" }

[build]
sources = ["Units.clef", "Main.clef"]
output = "hello-dimensions-proof"
output_kind = "console"
```

That relative path is resolved from the sample's directory in the Composer checkout. Its platform dependency supplies the target context and brings its own BAREWire dependency. The sample needs compatible sibling checkouts, as described in its [walkthrough](https://github.com/FidelityFramework/Composer/blob/main/samples/lattice/HelloDimensionsProof/README.md).

Our Clef specification defines core types and reactive evaluation primitives such as `Observable` and `Incremental` as intrinsics. Projects use them directly. Separately distributed libraries enter through project dependencies, with [ClefPak](/docs/tooling/clefpak-source-based-package-management/) responsible for source-package distribution. The current editor service checks local dependency paths. Registry acquisition would require additional workspace-service integration.

## Versioned editor results

The [CCS editor service](https://github.com/FidelityFramework/Composer/blob/main/src/CCS.Editor/README.md) serializes checks and freezes their display values before publishing a snapshot. Hover uses compiler source intervals and formatted inferred types. Definition lookup follows resolved references, including shadowed bindings.

An edit invalidates the previous check and its proof results. The server accepts full-document updates and checks after a short pause in notifications. Results from an older check are discarded when newer input supersedes them. Project and dependency files are also watched, including inputs outside the workspace directory.

The local server supports hover and definition lookup alongside located diagnostics. Parser messages that lack structured ranges appear in Lattice output. Completion and semantic tokens would extend these compiler-backed queries. The [server capability description](https://github.com/FidelityFramework/Composer/blob/main/src/Lattice.Server/README.md#implemented-boundary) records the implemented request surface.

## Source-proof queries

Lattice's negotiated proof query returns compiler-authored obligations with their premises and source references. The server dispatches the generated queries to cvc5 and associates each result with the current check generation. Its protocol distinguishes a proved obligation from a counterexample, and preserves unknown or error results as separate states.

A successful query establishes the source obligation under its encoded premises. Preservation through native or JavaScript lowering requires corresponding compiler evidence. Editing an input invalidates the displayed verdict, while hiding the proof view leaves checking active. The [proof-view contract](https://github.com/FidelityFramework/Composer/blob/main/src/Lattice.Server/README.md#proof-view-contract-version-1) defines these messages and result states.

## Local development

Build the server from the Composer checkout with its .NET SDK. The [server README](https://github.com/FidelityFramework/Composer/blob/main/src/Lattice.Server/README.md) provides the build command and stdio launch arguments, including project selection and the cvc5 executable. An LSP client supplies framed requests on standard input.

The [VSCode development walkthrough](https://github.com/FidelityFramework/lattice-vscode/blob/fidelity/client/README.md) opens the dimensional sample in an Extension Development Host. It exercises measured-type hover and definition routing, then shows a diagnostic clearing after an unsaved correction. The proof panel presents the source query results from that check.

Neovim uses the same server boundary through its [Lattice client](https://github.com/FidelityFramework/lattice-vim). Its semantic acceptance work should exercise the same dimensional fixture. Each client must preserve document versions and discard superseded results before adding richer navigation or completion.

## Shared analysis

Required language checks belong in CCS and the applicable Composer lowering passes. Optional editor analysis can inspect those facts or suggest another property to check. Presentation settings determine how results appear, while the compilation requirements remain active.

Native binding tools such as Farscape contribute generated source and declarations to the project inputs. Their C headers can be reviewed with clangd alongside Clef source checked by Lattice. Richer [Atelier](/docs/tooling/atelier-the-fidelity-workshop/) views would query the same compiler-owned information through an interface suited to graph and proof inspection.
