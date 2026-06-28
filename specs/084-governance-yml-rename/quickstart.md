# Quickstart: Validate the Governance `.fsgg` Slot Rename

**Feature**: 084-governance-yml-rename

Runnable checks that prove the rename is complete and coherent. Run from the repository root
on branch `084-governance-yml-rename`. See [contracts/loader-slot.md](./contracts/loader-slot.md)
for the behavioral contract and [data-model.md](./data-model.md) for the slot map.

## Prerequisites

- .NET `net10.0` SDK (constitution Engineering Constraints).
- The half-done rename completed: `README.md:97` switched, the no-fallback test added, and
  the working tree staged for one coherent commit.

## 1. No primary-slot `project.yml` remains under tests/ or samples/ (SC-001)

```bash
find tests samples -path '*/.fsgg/*' -name 'project.yml'   # expect: no output
find tests/golden-fixture -name 'project.yml'              # expect: no output
```

Expected: **empty** — all 36 fixtures + golden + sample renamed to `governance.yml`.

## 2. The loader reads `governance.yml` and does NOT fall back (SC-004)

```bash
dotnet test tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj
```

Expected: **all green**, including the new no-fallback regression test (contract C2):
`project.yml` present + `governance.yml` absent ⇒ **Invalid / missing primary slot** (the
SDD file is not consumed). The real-fixture loader/schema tests load `Valid` from
`governance.yml` (C1) and reproduce each malformed diagnostic (C4).

## 3. The reference gate set loads `Valid` from `governance.yml` (Story 2 #2)

```bash
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests/FS.GG.Governance.ReferenceGateSet.Tests.fsproj
```

Expected: **green** — the curated sample at `samples/sdd-reference-gate-set/.fsgg/` loads
`Valid` from `governance.yml` with the same invariants (gate count, `defaultProfile`, no
dangling refs) it held under `project.yml`.

## 4. Full build + suite green; no regression (SC-002, SC-003)

```bash
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln
```

Expected: build with **no errors**; every previously-green test project stays green
(including all command-host suites whose `Support.fs`/test helpers now write/locate
`governance.yml`). Surface-drift baselines stay green with **no re-bless** (only `.fsi`
comment text changed). Pre-existing out-of-scope flakes (CLI `dotnet pack` local-feed
MSBuild-node timeout) are noted but not caused by this change.

## 5. Docs name the Governance slot `governance.yml` (SC-005)

```bash
sed -n '97p' README.md     # the FS.GG.Governance.Config four-.fsgg-files enumeration
```

Expected: the Governance four-file line names **`governance.yml`** (not `project.yml`).
SDD-context `project.yml` references in `docs/` remain unchanged (FR-008).

## 6. Coherent commit, no half-rename left (SC-006)

```bash
git status --short    # expect: clean working tree after the single rename commit
git show --stat HEAD  # expect: fixture moves + loader/model/Schema + test-support + README + new test, together
```

Expected: one commit on the feature branch containing the file moves and the source/test/doc
edits together; the working tree is clean (no uncommitted half-rename).
