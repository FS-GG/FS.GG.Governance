# Implementation Plan: Governance `.fsgg` Slot Rename (`project.yml` → `governance.yml`)

**Branch**: `084-governance-yml-rename` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/084-governance-yml-rename/spec.md`

## Summary

Governance's config loader must read its primary `.fsgg` slot from `governance.yml`
instead of the SDD-owned `project.yml`, per ADR-0005's slot-ownership split (the two
files coexist in one shared `.fsgg/` directory). This is a **filename/slot rename only**:
schema, contents, typed facts, gate semantics, routing, and `schemaVersion` are all
unchanged. A half-done rename already exists in the working tree (36 primary-slot
moves staged — 34 config fixtures + 1 golden-fixture + 1 sample; loader/model/test-support/doc
edits partially applied, uncommitted). This
feature **finishes the rename coherently, gets build+test green, and commits it as one
change** so the branch is never left half-renamed.

Approach: a single clean-break slot-string switch in the loader (no `project.yml`
fallback — a fallback would re-introduce the collision the rename removes), the on-disk
fixture/sample renames, every test-support helper switched to the new name, the one
binding doc target (`README.md:97`), plus a new regression test that proves the
no-fallback contract (SC-004). Internal type names (`ProjectFacts`) are deliberately
retained (FR-009).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: `FS.GG.Governance.Config` (loader/schema/model); YamlDotNet
isolated inside Config (parse-to-node only); Expecto + YoloDev.Expecto.TestSdk for tests
(repo-wide framework). No new dependency is introduced (Tier 1 still, but dependency-free).

**Storage**: On-disk `.fsgg/` directory of YAML files read through `Loader.fileSystemReader`
/ injected `FileReader` functions. No database.

**Testing**: `dotnet test FS.GG.Governance.sln` (Expecto). Config loader/schema tests run
against **real on-disk fixture directories** (Principle V — real evidence) plus injected
in-memory `FileReader`s for the error/absent edges.

**Target Platform**: Linux/Windows dev + CI (cross-platform path handling already in Loader).

**Project Type**: Multi-project F# solution (single repository, library + command hosts + CLI).

**Performance Goals**: N/A — a filename constant change; no hot path touched.

**Constraints**: Clean break (no dual-read fallback, Assumptions); no `schemaVersion` bump;
internal naming retained; existing goldens that do not embed the primary filename stay
byte-identical; the change lands as one coherent commit (FR-010/SC-006).

**Scale/Scope**: 1 loader constant + 4 source doc-comment touches (`Loader.fs`, `Model.fs`,
`Model.fsi`, `Schema.fsi`); 34 config-test fixture renames + 1 golden-fixture rename + 1 sample
rename (= 36 primary-slot moves, all pure 0-byte-content moves); ~10 test-support/test files switched to the new
slot name; `README.md:97`; +1 new no-fallback regression test. No production core logic,
no gate/routing/handoff behavior, no public type rename.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — ✅ No public surface is *added*;
  the only `.fsi` edits are doc-comment text (`Model.fsi`, `Schema.fsi`). The behavior
  change (which filename the loader reads) is exercised through the public `Loader` surface
  by real-fixture tests and a new injected-reader no-fallback test. No new FSI shape to sketch.
- **II. Visibility lives in `.fsi`** — ✅ No access-modifier changes; no symbol added/removed.
  `.fsi` files change only in comment text. Surface-area baselines capture **signatures, not
  comments**, so no baseline re-bless is forced (confirm during Phase 1 — see research D3).
- **III. Idiomatic Simplicity** — ✅ A single string constant change (`"project.yml"` →
  `"governance.yml"`). No clever feature used; nothing to justify.
- **IV. Elmish/MVU boundary** — ✅ N/A. No new stateful/I/O workflow; the loader's existing
  `FileReader` injection seam is untouched in shape. Command hosts are not edited.
- **V. Test Evidence Is Mandatory** — ✅ Real on-disk fixtures drive the loader; a new test
  fails before (loader reading `project.yml`) / passes after the slot switch and proves the
  no-fallback contract. No synthetic evidence introduced.
- **VI. Observability and Safe Failure** — ✅ An absent `governance.yml` is reported as a
  missing/absent primary slot (a located diagnostic distinguishing missing input from a
  defect), not silently satisfied by an SDD `project.yml`. This *strengthens* the
  missing-vs-defect distinction Principle VI requires.

**Change Classification**: **Tier 1** (alters observable behavior covered by existing specs —
which filename the loader reads — and is a cross-product contract change per ADR-0005). The
Tier 1 obligations that apply: spec ✅, this plan ✅, `.fsi` updated (comment text only) ✅,
surface baselines (confirm no signature change → no re-bless needed; research D3), test
evidence ✅ (new no-fallback test), docs updated (`README.md:97`) ✅. No public type/member
rename (FR-009).

**Result: PASS** — no violations; Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/084-governance-yml-rename/
├── plan.md              # This file
├── research.md          # Phase 0 output — decisions D1–D7
├── data-model.md        # Phase 1 output — the FileSlot / ProjectFacts entities + slot map
├── quickstart.md        # Phase 1 output — runnable validation of the rename
├── contracts/
│   └── loader-slot.md   # Phase 1 output — the loader primary-slot contract (governance.yml, no fallback)
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Config/
├── Loader.fs            # slot "project.yml" → slot "governance.yml" (the load-bearing line) + comment
├── Model.fs             # `// ── governance.yml ──` section-comment touch (ProjectFacts name retained)
├── Model.fsi            # same comment touch (no signature change)
└── Schema.fsi           # GovernedRoot doc-comment: "comes from governance.yml"

tests/
├── FS.GG.Governance.Config.Tests/
│   ├── fixtures/<34 dirs>/.fsgg/governance.yml   # renamed from project.yml (pure moves)
│   ├── fixtures/README.md                         # fixture-doc slot name
│   └── LoaderTests.fs                             # injected-reader cases → "governance.yml"
│                                                  #   + NEW no-fallback regression test (SC-004)
├── golden-fixture/.fsgg/governance.yml            # renamed from project.yml
├── FS.GG.Governance.Tests.Common/TestsCommon.fs   # shared helper that names/writes the primary slot
├── FS.GG.Governance.CacheEligibilityCommand.Tests/Support.fs
├── FS.GG.Governance.FreshnessSensing.Tests/Support.fs
├── FS.GG.Governance.RouteCommand.Tests/Support.fs
├── FS.GG.Governance.VerifyCommand.Tests/Support.fs
├── FS.GG.Governance.ReleaseCommand.Tests/MergeableTests.fs
└── FS.GG.Governance.Scaffold.Tests/{Interpreter,Loop,NoProvider}Tests.fs

samples/sdd-reference-gate-set/
├── .fsgg/governance.yml   # renamed from project.yml (curated reference gate set)
└── README.md              # adopter doc — slot name

README.md                  # line ~97 (F14 four-file paragraph): Governance four-file enumeration → governance.yml (FR-007)
```

**Structure Decision**: Single-repository multi-project F# solution; no new project or
directory. The change is confined to `src/FS.GG.Governance.Config` (one functional line +
comments), the on-disk fixtures/samples, test-support files, and one README line. The
already-staged renames (34 config fixtures + 1 golden-fixture + 1 sample = 36 primary-slot moves)
are kept; the plan completes the source/doc/test
tail and adds the missing no-fallback test, then commits the whole set coherently.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
