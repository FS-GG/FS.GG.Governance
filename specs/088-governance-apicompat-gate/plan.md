# Implementation Plan: Breaking-Change (API-Compat) Gate for the Published Governance Packages

**Branch**: `088-governance-apicompat-gate` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/088-governance-apicompat-gate/spec.md`

## Summary

The Governance repo publishes ~70 `FS.GG.Governance.*` packages that consumers pin by **registry version range**. The auto-update fabric only flows safe versions if a package's version number tells the truth: a **breaking** change must carry a **major** bump. Today nothing enforces this — the existing `Pack.versionPolicy` returns `Bumped` for *any* version increase (major/minor/patch alike), so a breaking change shipped under a minor bump passes the release gate; and the in-repo `surface/*.surface.txt` drift guard only detects that a surface *changed*, not whether the change was *breaking* relative to the *last published package*.

**Technical approach** — add a breaking-change **detector** and feed its signal into the **existing release-rules core** as a new, additive rule kind:

1. **Detector (new, external)**: assembly/package-level **ApiCompat / .NET SDK Package Validation** compares each freshly-packed `.nupkg` against its **baseline package** (the last published version, resolved from the `~/.local/share/nuget-local/` folder feed) and yields a per-package break signal. This is the only F#-viable mechanism — `Microsoft.CodeAnalysis.PublicApiAnalyzers` is a Roslyn/C# source analyzer and **does not analyze F#**, so it is rejected for these packages (see research). The constitution's `.fsi` signature files + the reflective `surface.txt` baselines remain the *source-level* committed public-surface record (US3), unchanged.
2. **Verdict (reuse existing core)**: add an **additive** `ReleaseRuleKind.ApiCompatibility` case (the closed set is documented as extending additively — a new case + a new `factFor`/sensing key — without changing existing behavior). Its governing `FactState` is computed from `{ break signal × semantic-version delta }`: *breaking & not-a-major-bump* ⇒ `Unmet`; *breaking & major bump* ⇒ `Met`; *no break* ⇒ `Met`; *no baseline* ⇒ `Met` (vacuous, FR-009); *indeterminate* ⇒ `Unrecoverable` (fail-safe, FR-008). `VersionBump` semantics are left **unchanged**.
3. **Advisory → required ratchet**: the new rule's existing **`Maturity` lever** is the ratchet. It ships declared `Advisory` (violations visible, non-blocking — US1), and is later promoted to `BlockOnRelease` (violations block — US2) once existing surfaces are clean (SC-005). This is the repo's native advisory→required mechanism; no CI `continue-on-error` duplication. A CI job runs the detector + the gate, non-required first and added to branch-protection required checks on promotion.
4. **Coherence with the surface guard (FR-010)**: the new gate **complements** `surface.txt`. They answer different questions — `surface.txt` = "did the public surface change vs the committed in-repo snapshot" (HEAD-relative); ApiCompat = "is the change breaking vs the last *published* package, and does the version bump cover it" (release-relative). Both remain; consolidation is out of scope.

This is a **Tier 1 (contracted) change**: it adds public API (a new `ReleaseRuleKind` case, new PackEvidence/sensing surface, new break-signal types), so it requires `.fsi` updates, `surface.txt` baseline updates, test evidence, and docs.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (org baseline; SDK 10.0.x in CI per `gate.yml`).

**Primary Dependencies**:
- Existing core (reused verbatim): `FS.GG.Governance.ReleaseRules` (`ReleaseRuleKind`, `FactState`, `ReleaseRule.Maturity`, `evaluateRelease`), `FS.GG.Governance.PackEvidence` (`Pack.versionPolicy`/`evaluatePack`/`factContributions`), `FS.GG.Governance.ReleaseFactsSensing`, `FS.GG.Governance.ReleaseCommand` (MVU host), `FS.GG.Governance.PackageChecks` / `SurfaceChecks` (existing surface baselines).
- **New (external, detector only)**: **.NET SDK Package Validation / `Microsoft.DotNet.ApiCompat`** — assembly/package API-compatibility diff. Invoked at the I/O edge as a sensor; the pure core never references it. No new dependency enters the pure rule/evidence libraries (Constitution dependency-minimalism).
- **Rejected**: `Microsoft.CodeAnalysis.PublicApiAnalyzers` (C#/Roslyn-only; cannot analyze F#).

**Storage**: N/A (no datastore). Inputs: the packed `.nupkg` set + the baseline packages on the `~/.local/share/nuget-local/` folder feed; optional `CompatibilitySuppressions.xml` baseline files committed in-repo.

**Testing**: Expecto + FsCheck via `dotnet test` (YoloDev.Expecto.TestSdk). New pure tests for the break-aware verdict + the `ApiCompatibility` rule fact/rollup (real-evidence: feed real break signals + version deltas, assert findings). Sensor/interpreter tests run ApiCompat against real packed fixtures where feasible; otherwise `Synthetic`-tagged with disclosure (Principle V). Existing `surface.txt` drift tests get baseline refresh (`BLESS_SURFACE=1`) for the touched projects.

**Target Platform**: Linux/CI build (`ubuntu-latest`, .NET 10.0.x); the gate runs in `.github/workflows/gate.yml`.

**Project Type**: Single F# solution (`FS.GG.Governance.sln`) — pure leaf libraries + an MVU host command + CI wiring. Same shape as the surrounding release-gate features (F053/F055/F065).

**Performance Goals**: Deterministic, byte-identical findings for identical inputs (Principle VI / existing release-core invariant). ApiCompat over ~70 small assemblies completes well within a normal CI build; no hot-path concern.

**Constraints**:
- **Drift-locked files are off-limits**: `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json` are org-synced and byte-identity drift-checked (Job 2 of `gate.yml`). All new MSBuild props / tool installs go in **repo-owned** files (`Directory.Build.local.props`, a dedicated imported `.props`, or job-scoped `dotnet tool install`).
- **Baseline availability**: most `FS.GG.Governance.*` packages have never been published to a consumer feed, so at rollout most resolve to **NoBaseline** (vacuously clean, reported). Real enforcement engages per-package as baselines accrue (couples to the H4 org-feed publish work). This must be reported honestly, not as "all clean" (FR-007).
- **No hard-fail in the main build**: the detector must not turn `dotnet build`/pack red while the gate is Advisory; the verdict lives in the governance rule's `Maturity`, which is the single source of enforcement.

**Scale/Scope**: ~70 packable packages (`IsPackable=true`); ~82 `surface.txt` baselines already in-repo. No package runtime behavior changes.

## Constitution Check

*GATE: evaluated pre-Phase 0 and re-checked post-Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS | New surfaces (break-signal types, `ApiCompatibility` kind, sensor) are drafted as `.fsi` and exercised in FSI/prelude before `.fs`. Pure verdict tested first. |
| **II. Visibility in `.fsi`** | PASS (Tier 1 obligation) | Every new public module ships a curated `.fsi`; touched `surface.txt` baselines refreshed. No access modifiers in `.fs`. |
| **III. Idiomatic Simplicity** | PASS | Reuses the existing closed-union/record/`Map` release-core idiom; new kind is one additive case + one fact function. No SRTP/reflection/type providers/custom CEs introduced in the pure core. (Detector uses reflection-equivalent assembly diff, but that is the external SDK tool at the edge, not our code.) |
| **IV. Elmish/MVU boundary for I/O** | PASS | The detector is process/filesystem I/O → modeled as an effect sensed at the edge (extends the `ReleaseCommand` MVU host / `ReleaseFactsSensing`); `update` stays pure; the break signal enters the core as data. |
| **V. Test Evidence Mandatory** | PASS | Pure verdict tests fail-before/pass-after with real signals; sensor tests prefer real ApiCompat over fixtures; any synthetic break fixture is `Synthetic`-tagged + disclosed. |
| **VI. Observability & Safe Failure** | PASS | Indeterminate detector result ⇒ `Unrecoverable` ⇒ `Violated` (fail-safe, FR-008); coverage gaps reported explicitly (FR-007); findings name package + member + required remediation (FR-003). Distinguishes tool defect from missing baseline (FR-009 vs FR-008). |
| **Genericity / Operating rule** | PASS | Pure-core stays product-neutral (no rendering package ids/paths); operates over `FS.GG.Governance.*` self-identity only. Governance gates *itself* with its own release rules — the constitution's stated intent. |
| **Dependency-minimalism** | PASS | No new dependency in the pure rule/evidence leaves; ApiCompat is SDK-bundled and lives only at the I/O edge + CI. Recorded with need/version-pin/owner in research. |
| **Change Classification** | Tier 1 | Declared; `.fsi` + baseline + docs obligations tracked in tasks. |

**No violations** → Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/088-governance-apicompat-gate/
├── plan.md              # This file
├── research.md          # Phase 0 — mechanism, baseline source, advisory ratchet, drift-lock constraints
├── data-model.md        # Phase 1 — break-signal + ApiCompatibility-fact types & verdict mapping
├── quickstart.md        # Phase 1 — how to run/validate the gate locally and in CI
├── contracts/
│   ├── apicompat-detector.md      # the sensor contract (inputs → break signal)
│   ├── apicompatibility-rule.md   # the release-rule fact + verdict + maturity contract
│   └── ci-gate.md                 # the gate.yml job + advisory→required promotion contract
├── checklists/
│   └── requirements.md  # (from /speckit-specify)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.PackEvidence/        # EXTEND: break-aware version verdict (new pure helper +
│   ├── Pack.fsi                          #   types in Model). versionPolicy stays; add break-aware path.
│   └── Model.fsi
├── FS.GG.Governance.ReleaseRules/        # EXTEND: additive ReleaseRuleKind.ApiCompatibility case
│   └── Model.fsi                         #   (+ factFor key). evaluate/rollup unchanged.
├── FS.GG.Governance.ReleaseFactsSensing/ # EXTEND (edge): sense the ApiCompatibility FactState from
│   └── *.fsi                             #   the detector output. I/O at the edge only.
├── FS.GG.Governance.ReleaseCommand/      # WIRE: host requests the detector effect, joins its result
│   └── Loop.fsi                          #   into facts before evaluateRelease (mirrors F065 pack join).
└── (new sensor leaf — REJECTED per tasks T001: the ApiCompat output parse is folded into
     FS.GG.Governance.ReleaseFactsSensing instead, to keep dependency scope tightest)
    FS.GG.Governance.ApiCompat/           # NOT created — listed only to record the rejected option.
        └── ApiCompat.fsi                 #   (parse lives in ReleaseFactsSensing/Sensing.fs)

tests/
├── FS.GG.Governance.PackEvidence.Tests/      # pure: break × version-delta → verdict table
├── FS.GG.Governance.ReleaseRules.Tests/      # pure: ApiCompatibility fact → finding → rollup, Maturity
├── FS.GG.Governance.ReleaseCommand.Tests/    # host join + advisory/required exit-code behavior
└── (FS.GG.Governance.ApiCompat.Tests/        # REJECTED per tasks T001 — sensor parse tests live in
                                               #   FS.GG.Governance.ReleaseFactsSensing.Tests instead)

surface/                                    # REFRESH baselines for every touched packable project
.github/workflows/gate.yml                  # ADD advisory job (non-required → required on promotion)
Directory.Build.local.props (or new .props) # repo-owned ApiCompat/Package-Validation MSBuild settings
pack-and-apicheck.fsx (repo root)           # detector entrypoint (mirrors pack-reference-gate-set.fsx, also at repo root)
```

**Structure Decision**: Extend the existing release-gate libraries in place (PackEvidence, ReleaseRules, ReleaseFactsSensing, ReleaseCommand) rather than building a parallel gate, because the version-comparison, fact→finding→rollup, and advisory-maturity machinery already exist and the closed rule-kind set is explicitly designed for additive extension. The only genuinely new code is (a) the break-aware verdict helper, (b) the additive `ApiCompatibility` rule kind + its sensing, and (c) the ApiCompat detector at the I/O edge. Whether the detector is a new leaf (`FS.GG.Governance.ApiCompat`) or folded into `ReleaseFactsSensing` is settled in research/tasks; the pure core must not depend on it either way.

## Complexity Tracking

> No Constitution violations — section intentionally empty.
