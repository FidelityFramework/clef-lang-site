# Hardware documentation grounding — September 9, 2026

Reviewed and corrected all eight Markdown files under
`hugo/content/docs/internals/hardware/`. This record captures the source snapshot
behind the corrections. It is not a board acceptance report.

## Evidence ownership

| Claim | Maintained source / evidence |
| --- | --- |
| Language and lowering requirements | `clef-lang-spec/spec/backend-lowering-architecture.md`, `platform-bindings.md`, `ffi-boundary.md`, `scheduler-contract.md` |
| Compiler support | `clef/src/Compiler/PSGSaturation/SemanticGraph/Types.fs`; Composer `src/BackEnd/LLVM/{Lowering,Codegen}.fs`, `src/BackEnd/GPU/Pipeline.fs`, and supported middle-end witnesses |
| Farscape's actual parser dependencies | `Farscape/src/Farscape.Core/CppParser.fs` and `Farscape.Core.fsproj` |
| Hosted Ariel acceptance | `Composer/docs/Ariel_Integration_Changes.md`; `HelloWayland/docs/multi-core-cpu.md` |
| FPGA widths and build results | `HelloArty/README.md` and its artifact-specific records |
| NPU limitations | `HelloNappy/README.md` and core-code/device acceptance records |
| MCU hardware facts | Versioned Renesas manuals and `Fidelity.Platform/Hardware/Products/Renesas/EK_RA6M5/` source manifest |
| MCU implementation and board acceptance | `MCU/Renesas/EK-RA6M5/HelloBlinky/README.md` and `docs/ACCEPTANCE.md` with retained artifacts |
| Credential bring-up guidance | `post-quantum-credential/hardware/ek-ra6m5/docs/MMIO-BUILD-GUIDE.md` and `MMIO-GROUNDING-2026-09.md` |

Paths above are relative to the shared `repos` directory, except explicitly
Composer-relative paths. They identify local sources, not public deployment URLs.

## Reviewed revisions

| Repository | Commit |
| --- | --- |
| Composer | `cf48225aef31c8b4c7365273104067a310aa6b83` |
| clef | `5344297980ea47dad4f091a51a9e169b90dfa129` |
| Farscape | `d29ba7c65d07dc928cbcaca2245d6ba3c13856e2` |
| Fidelity.Platform | `9366692eeab22f6d698379d0980315fe8c699f3a` |
| HelloArty | `dc8c5bc727ca0168de9288d894b9539cb7d9c790` |
| HelloNappy | `0d5c8c672d8f194ea7c4cf8afe9f39fb17c9821c` |
| HelloWayland | `81fa3c0954a525702d4f380c022edf77be1790cb` |
| clef-lang-spec | `b21fe8936b113609c77e8373d421328c76a697f2` |
| clef-lang-site before edits | `c2e11ee560f8402d162ae0108ffd70da6c3b9d23` |

Renesas facts were checked against local hardware manual
`r01uh0891ej0150-ra6m5.pdf` (revision 1.50) and board manual
`r20ut4829eg0101-ek-ra6m5-v1-um_mp.pdf` (revision 1.01). Online manual links can
resolve to newer revisions; the board's table numbers are therefore qualified.

## Corrections

- MCU: removed the STM32 flash-map generalization, invalid source/IR/assembly
  examples, automatic HAL-to-register replacement claims, phantom-token ordering
  guarantees, and source-only trusted-computing-base claims. Explained target
  layout before ABI lowering, actual MMIO implementation gaps, reset ownership,
  vector retention, and the distinction between SysTick and external IRQ routing.
- Watchdogs: OFS0 bits 1 and 17 use zero for auto-start; erased all-ones does not
  auto-start both watchdogs. Actual provisioning still governs behavior.
- Farscape: direct Clang invocation and a .NET host are current dependencies;
  XParsec is not a complete C/C++ frontend replacement. CMSIS qualifiers are not
  complete device protocols.
- CPU: exclusive ownership does not isolate physical cache lines. Known layouts
  do not prove cache residency or exact miss counts. Corrected the strided
  12,000-useful-byte example to a 24,000-byte / 375-line footprint under its stated
  assumptions. Removed invented APIs presented as available features.
- GPU: SIMT does not prevent races or imply universal lockstep. Fences and
  barriers have different roles. Address sharing, managed migration, and coherence
  are different capabilities. Reconciled the existing AMD backend with the
  still-proposed general optimization and multi-vendor work.
- Scheduling: acknowledged current hosted carrier evidence; removed the implied
  freestanding conformance claim and the unsafe ISR stack-reset/arena-release
  recipe. Priority and sentinels alone do not establish control-plane immunity
  or safe cancellation of outstanding device work.
- Storage: whole-record APIs do not provide flash atomicity. Sealing and hash
  chains need integrity, freshness, recovery, and trusted-anchor contracts.
- Accelerators: reconciled HelloArty widths with its current README; removed
  unversioned resource/timing counts and stale display binding examples. Kept
  packaging, host integration, and checked device compute as separate gates.

## Keeping subsequent changes grounded

When behavior changes, update the implementation's acceptance record first and
the relevant explanation in the same change. A current-feature claim needs a
source path plus an acceptance case; a device claim additionally needs the
target/configuration and artifact it was observed on. Keep proposed interfaces
explicitly proposed until they satisfy that threshold.

Keep hardware addresses and protocols in the platform package with manual
revision, section/table, device variant, and access semantics. Avoid copying
register tables or transient benchmark counts into multiple explanatory pages.
Link the maintained record instead. A new manual revision requires reconciliation,
not an automatic replacement of old provenance.

Preserve page URLs and used section anchors when tightening prose. Build the
site and check internal links after changes. This review rule is a maintenance
practice, not a claim that automated tooling can prove the prose correct.

## Validation scope

The fresh minimal HelloBlinky compile reaches LLVM IR, but retains a hosted-shaped
entry and 64-bit descriptor fields. The LLVM-only MMIO probe bypasses Composer.
Neither is source-to-board acceptance. No firmware was flashed during this
documentation review.

- `hugo --source hugo --destination /tmp/clef-hardware-review-site --cacheDir
  /tmp/clef-hardware-review-cache --noBuildLock` completed successfully with
  Hugo 0.165.0: 449 pages. Existing theme/config deprecation warnings remain.
- Parsed generated HTML for the hardware pages and aliases: 12 files, 3,287
  internal links checked, with no missing target pages or fragments.
- Compared removed section anchors against references in the site content and
  the local language specification: no referenced anchor was removed.
- `git diff --check` passes for the site and credential documentation changes.

The site was built into `/tmp`; nothing was deployed. The site's pre-existing
`hugo/go.mod` and `hugo/go.sum` edits were retained.

## Follow-through: platform facts and catalog corrections

The platform source manifest is now populated with eight document revisions,
paths, and SHA-256 hashes. `Fidelity.Platform/Hardware/Products/Renesas/EK_RA6M5/docs/HELLOBLINKY_HARDWARE.md`
owns the initial register/interrupt contract and open device gates. The generic
platform endpoint maps remain unimplemented; source availability is no longer
described as the reason for that state.

Further manual review corrected these catalog claims in place:

- Architectural reset enters Secure state; IDAU attribution and a later
  Non-secure handoff are separate facts (Arm Generic User Guide §2.3.4).
- DTC table selection follows the triggering interrupt's security attribution
  (RA6M5 §17.3.1), not CPU boot state. DTCSAR only attributes DTCST.
- Software-reset retention is per-register (Table 5.3); it does not universally
  preserve the clock setup. Debugger qualifications and application re-entry are
  separate contracts.
- Renesas S-cache can cover internal SRAM (§14.8). Missing CMSIS core-cache macros
  do not remove DMA coherency or all cache-related timing concerns.
- A DTC table's location outside BSS is a linker choice. Initializing an allocated
  table remains necessary; allocating one is not a blinky prerequisite.
- The 448-byte core/ICU vector table requires 512-byte alignment. The peripheral
  PFS halfword/byte views use their documented +2/+3 addresses. IELSR.IR clears
  with zero, and IRQCR configuration requires the target event route disabled.

An independent temporary Arm GNU compile validated 30 address/width assertions
against the installed Renesas header. All eight source hashes and 14 local links
in the active cross-repository records passed checks. The site was rebuilt after
the Secure-reset/S-cache additions and the 3,287-link check passed again.

At this initial review, the board schematic was missing and the official
download returned HTTP 403. Later same-day work below resolved the source and
silicon/control questions; complete ADC/DMAC acceptance remains open. The grounding record contains a disposition for every catalog section;
it does not claim those deferred subsystems were implemented or board-tested.

## Same-day implementation follow-through

HelloBlinky subsequently reached the physical RA6M5. CCS/Composer now provide the typed MMIO subset and early 32-bit lowering. The platform migrated its MCU leaf to BAREWire `Description.fs` plus `Registers.clef`; BAREWire validates and generates memory/linker inputs. Owned reset/vectors, declared native callbacks and a verified code-flash download produced working color/pace/hold behavior. The user also confirmed 10% software-PWM dimming. Exact current results live with the application; the baseline revision table above predates these working-tree implementation changes. The MCU article has been updated in this same change.

## Composer orchestration and complete I/O map

The user supplied the MP design package beside HelloBlinky, outside its repo.
D017572_04 issue 3.0 schematic/BOM/netlist now ground the complete 176-pin package
and 1149 board terminal facts, including 355 connector contacts and all 160 native
header contacts. The netlist and schematic resolve manual typos: Pmod1 reset is
P311, and Grove2 P506 is AN122. The I/O-map test checks the pinned netlist and all
package/header coverage; peripheral drivers remain separate implementation work.

HelloBlinky now builds and deploys directly with `composer compile --deploy`.
Composer owns in-process BAREWire checks/layout, ARM tools, image verification and
SEGGER SDK orchestration. The application Python/F# build scripts were removed.
The Composer-produced code binary is byte-for-byte identical to the accepted
10% PWM version, and Composer deployment/readback/reset/watch succeeded.
