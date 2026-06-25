# Contract: Package/API checks — `.fsi` baseline drift + FSI transcripts (F24, P1)

**Library**: `FS.GG.Governance.PackageChecks` | **Story**: US1 | **FR**: 001, 002, 003, 009, 010, 012

The highest-stakes domain. Pure `evaluate` over sensed `PackageFacts`; the `Interpreter` sensor reads the
`.fsi` surface and runs the published FSI transcripts through the existing `GateExecution.ExecutionPort`.

## C1 — `.fsi` baseline drift (FR-001, SC-001)

- The sensor regenerates the surface's public-token set and compares it to the committed baseline as a
  **normalized token diff**, not a raw text diff (D5) — whitespace/order/formatting never report as drift.
- `BaselineMatches` ⇒ **no finding** (unchanged surface reports zero drift, SC-001).
- `BaselineDrift (added, removed)` ⇒ one **Blocking** `package.baseline-drift` finding whose `Message` names
  the added and removed members (sorted), `Location.Detail` = the changed-member summary.
- The regenerated baseline is the produced evidence, tied to the surface's `EvidenceTag` (FR-009).

## C2 — First-run baseline absent (FR-002, acceptance 1.2)

- `BaselineAbsent generated` ⇒ the sensor has **generated and written** the baseline (first run), and the
  pack emits one `IsInputState = true` `package.baseline-absent` finding: a clear, fixable "baseline
  generated, commit it" state — **never** a tool defect and **never** a silent pass.
- `BaselineUnreadable source` ⇒ `IsInputState` `package.baseline-unreadable` naming the source (FR-012).

## C3 — FSI transcript currency (FR-003, SC-001)

- Each published transcript (a public example / package contract) runs through `RunTranscript` (shells FSI via
  the injected `ExecutionPort`).
- `TranscriptPasses` ⇒ no finding; the pass is recorded as evidence.
- `TranscriptCompileFailed detail` ⇒ Blocking `package.transcript-compile` finding naming the failing example
  (`ExampleId` + `Source`).
- `TranscriptResultChanged (expected, actual)` ⇒ Blocking `package.transcript-result` finding naming the
  example and both values.
- `TranscriptUnlocatable source` ⇒ `IsInputState` `package.transcript-unlocatable` naming the source (FR-012).

## C4 — Determinism (FR-010, SC-005)

- `evaluate` sorts findings by `(ExampleId / member-name locus, Source)`; identical `PackageFacts` ⇒
  byte-identical findings. No clock, abs-path, username, or environment input.

## C5 — Purity & seam (FR-007)

- `PackageChecks.evaluate` is pure/total/no-I/O. The **only** filesystem/process seam is
  `Interpreter.PackagePort`; `realPort` reuses the F051/F052 `ExecutionPort` for FSI runs — **no**
  `FSharp.Compiler.Service` dependency.

## Acceptance (maps to spec US1 scenarios)

1. Committed baseline + changed public surface ⇒ `package.baseline-drift` naming the change; unchanged ⇒ no
   finding (SC-001).
2. No committed baseline ⇒ baseline generated + `package.baseline-absent` input-state finding (1.2).
3. Passing transcript ⇒ no finding + evidence; broken/changed-result transcript ⇒ the matching transcript
   finding naming the example (1.3).
4. A package-routed change ⇒ the produced evidence is tied to the surface's declared `EvidenceTag` (1.4).
5. Determinism: repeated `evaluate` over the same facts ⇒ byte-identical (SC-005).
