---
title: "Storage on Metal"
linkTitle: "Storage on Metal"
description: "Durable state for freestanding targets: the sealed blob floor, the namespace ledger above it, and the coeffects that carry both to a target"
weight: 55
date: 2026-08-31T09:00:00-04:00
authors: ["Houston Haynes"]
tags: ["Architecture"]
---

A freestanding target owns its own persistence. There is no filesystem underneath a sealed image, and the [substrate spectrum](/docs/internals/hardware/on-metal-extended/) this section describes reaches parts whose storage is a region of flash behind the [MMIO seam](/docs/internals/hardware/fidelity-on-mcu/). This page describes how our design supplies durable state on such targets, and how the same structures continue upward to hosted and clustered deployments. The normative definitions are in the spec: [Modular Blob Storage](/spec/draft/modular-blob-storage/) for the substrate and [Namespace Storage](/spec/draft/namespace-storage/) for the layer above it. This page is the orientation, and those chapters are the contract.

## The Sealed Floor

Modular Blob Storage (MBS) is the durable rung of the lifetime lattice: values that outlive the process, at known locations, under an access policy. Its shape is set by what a constrained target can afford. An MBS instance is a fixed set of slots sized at provision time, placed in static storage with no heap anywhere in the path. Records are written and read whole, which yields crash consistency without a journal. Addressing is by opaque, store-issued handles, with a small secret-free index for selection. Every record is sealed at rest under a 256-bit-class symmetric cipher whose key is device-bound, so the medium itself carries no security attribution.

The spec treats durability as a coeffect on the [Program Semantic Graph](/spec/draft/program-semantic-graph/): survives-power-loss, atomic-write, and sealed travel with the value through the middle end and are committed at target binding, where the pathway selects the non-volatile medium, the atomic-write primitive, and the sealing capability. A target without those capabilities is a capability failure at binding, in the same manner as the other platform declarations this section documents.

## The Ledger Above It

A filesystem adds an open namespace, a path hierarchy, run-time growth, and a mutable metadata tree. The [Namespace Storage](/spec/draft/namespace-storage/) draft supplies the first rungs of that ladder in a form the same constrained targets can afford, and its central decision is that the namespace keeps no mutable tree. Namespace state is the fold of an append-only, hash-linked ledger of change entries. Checkpoints of the fold are compressed, sealed, and written back as ordinary MBS records, and a single root record binds the segment set to its checkpoint position with one atomic write.

Storing the namespace's own cold metadata in the blob substrate keeps the design at one persistence mechanism for the target. It also gives the flash layer the write pattern it tolerates best, since appending is the only mutation, and it makes recovery, audit, and tamper evidence properties of one structure. The durability coeffect gains two components here, chained and replayable, committed at target binding like the rest.

## Continuity Upward

Nothing in the ledger, the segments, or the root record names a scale, and the chapters mark their server-scale readings as informative sections rather than separate designs. At cluster size the ledger reads as a subscribable metadata event stream, the segments read as compressed metadata chunks in bulk storage, and the custody anchor is a keyring in the metadata store in place of the device sequester. The blog entry [An Emergent File System Model]({{< ref "an-emergent-file-system-model" >}}) covers the design study behind that continuity, including the production system whose metadata architecture we read against these chapters, and the S3-shaped sealed image we see at the far end.

## Related Sections

- [Fidelity on MCU](/docs/internals/hardware/fidelity-on-mcu/): the M33 target where the sealed floor gets its first instantiation, and the MMIO seam beneath it
- [Clef on Metal Extended](/docs/internals/hardware/on-metal-extended/): the substrate spectrum these storage layers serve
- [Modular Blob Storage](/spec/draft/modular-blob-storage/) and [Namespace Storage](/spec/draft/namespace-storage/): the normative chapters
