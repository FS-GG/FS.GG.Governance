# Contract: fs-gg-ui rename guard (R1–R7)

The guard exposes **no public F# surface** (Tier 2). Its "contract" is the observable behavior of
the `FS.GG.Governance.RenameGuard.Tests` Expecto suite, frozen as rules R1–R7. Each maps to spec
requirements and is realized by a named test.

## R1 — Clean tree passes (FR-001, FR-008, SC-001)

Running `scanTrackedTree ()` over the current `main` returns an **empty** `Violation list`. The
guard scans only `git ls-files` output (excludes `bin/`/`obj/`/untracked), minus the
`ProvenanceAllowlist` (four documentary files) and the `GuardSelfExclusion`
(`tests/FS.GG.Governance.RenameGuard.Tests/`, which holds the red-path input literals — see R6).
Test: `Production scan of the tracked tree finds zero legacy version-machinery identifiers`.

## R2 — Provenance passes untouched (FR-003, FR-006, SC-003)

With the four allowlisted provenance files present and unmodified, R1 still passes: their bare
`FS-Skia-UI` prose matches no pattern, and they are allowlisted regardless. The feature changes
none of their bytes. Test: `The four provenance files are present, allowlisted, and not flagged`.

## R3 — A legacy CPM property is caught (FR-002, FR-005, SC-002)

`scanText "fake/Directory.Packages.props" "<FsSkiaUiVersion>1.0.0</FsSkiaUiVersion>"` returns ≥1
`Violation` whose `Class` is the CPM property, `Matched` contains `FsSkiaUiVersion`, and
`Replacement` is `FsGgUiVersion`. Test: `A FsSkiaUiVersion reference is caught with the FsGgUiVersion replacement`.

## R4 — Legacy contract ids and tag namespace are caught (FR-002, FR-004)

`scanText` over literal input strings containing `fs-skia-ui-version`, `fs-skia-ui-bom`, and
`fs-skia-ui/v1` each yields a `Violation` naming the correct `fs-gg-ui-*` / `fs-gg-ui/v*`
replacement. Test: `Legacy contract ids and the fs-skia-ui tag namespace are caught`.

## R5 — Separator/case variants caught; bare repo name not (Edge: variants; FR-003)

`scanText` matches `Fs_Skia_Ui_Version`, `fs.skia.ui.bom`, `FS-SKIA-UI/V2` (variants in a
version-pinning context) **and** returns **empty** for the bare prose
`source-analysis of FS-Skia-UI` and `https://github.com/EHotwagner/FS-Skia-UI/blob/main/x.md`.
Test: `Case and separator variants match; the bare FS-Skia-UI repo name does not`.

## R6 — Canonical fs-gg-ui passes; the guard does not self-match (Edge: canonical present / own fixtures)

`scanText` over content using `FsGgUiVersion` / `fs-gg-ui-version` / `fs-gg-ui-bom` / `fs-gg-ui/v1`
returns **empty** (the guard forbids the legacy root, not UI version references). And the guard's
own test source, when scanned by the production path (R1), produces no violation. Self-non-match is
secured by **two complementary mechanisms** for **two distinct sources** (see data-model
§GuardSelfExclusion):

1. the `ForbiddenToken.Pattern` regexes are **assembled from string fragments**, not literal
   suffix-bearing tokens — so the *pattern definitions* are not legacy tokens; and
2. the red-path *input literals* (R3–R5) — which MUST appear verbatim as the strings the tests
   assert `scanText` matches — live under the guard's own test directory
   `tests/FS.GG.Governance.RenameGuard.Tests/`, which `scanTrackedTree` **excludes** from the
   production scan (allowlisted-by-construction, distinct from the provenance allowlist).

No committed tripwire fixture exists outside that excluded directory. Test:
`Canonical fs-gg-ui identifiers pass and the guard source does not self-trip` (asserts both the
canonical-passes case and that `scanTrackedTree ()` is empty with the red-path literals present in
the excluded test source).

## R7 — Diagnostic is actionable and self-describing (FR-005, SC-006)

A produced `Violation` renders to a message naming the **file**, the **line**, the **offending
identifier**, and the **canonical replacement** — sufficient for a reviewer to distinguish a real
straggler from provenance using the message and the allowlist alone, with no external context.
Test: `A violation message names the file, identifier, and fs-gg-ui replacement`.

## Tier-2 invariants (verified outside the suite, recorded here)

- **I1 (SC-004 / FR-007)**: `git diff` over all `*.fsi` files and every surface-area baseline is
  empty. No production `src/` file changes.
- **I2 (SC-003)**: `git diff` over the four provenance files is empty.
- **I3 (FR-009)**: the project's only `ProjectReference` is `FS.GG.Governance.Tests.Common`; it has
  no reference to any `src/` governance library and runs under `dotnet test`.
