# Implementation Plan: Verify & Release Publication Boundary — Pack, Version, Publish-Plan, and Provenance Attestation (F26)

**Branch**: `061-verify-release-provenance` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/061-verify-release-provenance/spec.md`

## Summary

After F25, a governed run is cost-bounded and produces a deterministic provenance audit snapshot
(`FS.GG.Governance.CommandKind.AuditSnapshot`) of every expensive command it performed — including the `Pack`
kind — projected by `ProvenanceJson` (`fsgg.provenance/v1`). After F53–F56, the six release-rule families are
modelled (`ReleaseRules`, the closed `ReleaseRuleKind` set + `evaluateRelease`), sensed from a real repository
(`ReleaseFactsSensing.SensedRelease`), run by the `fsgg release` host (`ReleaseCommand`, five exit codes,
`release.json` = `fsgg.release/v1`), and previewed by `fsgg verify` (`VerifyCommand`, five exit codes). **F25's
cost/command/provenance cores are pure leaves that no host has wired yet** (no host references `CommandKind` /
`AuditSnapshot` / `ProvenanceJson` today) — F26 is the first row to drive the provenance snapshot at a host edge.

What is missing is the **publication evidence those release rules are evaluated against** and the
**publication-grade attestation** of what ran. This row supplies exactly that and reuses everything else
unchanged:

1. **Enforced pack-and-version-bump evidence (P1).** Before the release verdict may pass, the `fsgg release`
   host **packs every packable project** through the existing F051 `GateExecution.ExecutionPort` — recording
   each as a `CommandKind.Pack` `KindedCommandRun` (sentinel exit code on failure, never dropped) — reads each
   pack output's artifact path + packed version + digest, and a new pure core
   (`FS.GG.Governance.PackEvidence`) reconciles the **packed** version against its released baseline and emits
   the `FactState` contributions that ground the existing F53 `VersionBump` / `PackageMetadata` / `Provenance`
   families. A project that fails to pack, or packs at an unbumped/downgraded version, contributes `Unmet` →
   the existing `Release.rollup` blocks it with a named reason. **No new release-rule family** (FR-003).
2. **Publication boundary distinct from ship, surfaced through an immutable report (P1).** A new pure core
   (`FS.GG.Governance.ReleaseReport`) assembles the **single source of truth**: the F53 `ReleaseDecision`
   (verdict + `ExitCodeBasis`, reused verbatim), the package evidence, the version policy, the F54 publish-plan
   / posture / pins evidence, and the attestation summary. The `fsgg release` exit-code basis is unchanged
   (F55's five codes, `Blocked` distinct from every failure-to-run); the boundary is already distinct from
   `fsgg ship`'s merge verdict. `fsgg verify` projects the **advisory release-readiness preview** of the same
   report (`ReleaseReport.preview`) — never itself the blocking gate, F56 exit scheme unchanged (FR-005).
3. **Publish-plan / trusted-publishing / template-pin evidence surfaced first-class (P2).** Already sensed by
   F54 (`ReleaseSnapshot.PublishPlan` / `TrustedPublishing` / `Pins`) and evaluated by F53; F26 surfaces each
   precondition's satisfied/unmet state + reason in the `ReleaseReport` (and its `release.json` projection) —
   no new sensing, no new family (FR-006).
4. **SLSA/in-toto-shaped attestation summary, without overclaiming (P2).** A new pure core
   (`FS.GG.Governance.Attestation`) projects the F25 `AuditSnapshot` (+ pack-evidence subjects) into an
   `AttestationSummary` — **subject** (packed artifacts + digests), **builder identity**, **materials** (rule
   hash, generator version, base/head, artifact digests, environment class), **invocation** (the recorded
   command runs) — carrying an explicit `compatible-shape, not formal compliance` marker and never asserting a
   subject that was not produced (FR-007, FR-008). Projected to an `attestation.json` sidecar
   (`fsgg.attestation/v1`) by a new `FS.GG.Governance.AttestationJson` (the F25 `ProvenanceJson` precedent).
5. **Scheduled exhaustive validation hooks (P3).** A new pure core (`FS.GG.Governance.ValidationMatrix`) decides
   — reusing the F25 `CostBudget` ordered `Cost` ceiling verbatim — whether a **declared** exhaustive matrix
   runs now or is **deferred** to the scheduled/release boundary; an inner-loop run records it deferred and
   never runs it, and an undeclared matrix is never invented (FR-009).

The work **composes the leaf-plus-sensor precedent** (F029/F041/F051/F053/F054/F25): every new evaluation is a
**pure, total function over already-sensed inputs**; the only new I/O — packing the projects through the F051
port and reading the pack outputs — lives at the `ReleaseCommand` host edge through the **existing** execution
port (FR-014). Surfacing follows the established **deterministic-JSON, `schemaVersion`-headed, byte-identical-
when-empty** discipline: one new sidecar (`attestation.json`), `release.json` bumped to `fsgg.release/v2`, and
`verify.json` gaining an **optional** `releaseReadiness` block **without** a schema bump (`fsgg.verify/v1`
unchanged — the F24 `ofVerifyDecisionWithSurfaceChecks` byte-identical-when-empty precedent), leaving every
existing `route.json` / `ship.json` / `verify.json` golden byte-identical and adding explicitly-versioned fields
only to the release golden (FR-015).

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **Packed version is the source of truth for releasability (D1).** `PackEvidence` evaluates the version
   policy against the **packed artifact's** version (not the source-declared version), so a bump in source that
   does not reach the artifact still blocks. The packed `FactState` for `VersionBump` / `PackageMetadata` /
   `Provenance` is computed from real pack output and merged over the F54 sensed facts (packed evidence wins),
   then fed to `Release.evaluateRelease` unchanged.
2. **Attestation is its own sidecar; release.json bumps to v2 additively (D2).** The `AttestationSummary` is a
   projection of the F25 `AuditSnapshot`, so it gets a dedicated `attestation.json` (`fsgg.attestation/v1`)
   exactly as F25 gave the snapshot its own `provenance.json`. `release.json` becomes `fsgg.release/v2`,
   **adding** `packageEvidence`, `versionPolicy`, and an `attestation` identity reference (and rendering the
   publish-plan / posture / template-pin precondition state + reason through the **existing** v1 `rules` array —
   no new `preconditions` field). `verify.json` gains an **optional** `releaseReadiness` preview block with **no**
   schema bump (`fsgg.verify/v1` unchanged, byte-identical when the block is absent — the F24
   `ofVerifyDecisionWithSurfaceChecks` precedent). The `release.json` v2 additions are explicitly versioned;
   the `verify.json` addition is byte-identical-when-empty (FR-015).
3. **Report object is the single source of truth; JSON renders from it (D3).** `ReleaseReport` is an immutable,
   presentation-free value; `ReleaseJson.ofReleaseReport` and the verify preview render from it, so the F27
   human projections render from the same object (FR-012). The report **carries** the F53 `ReleaseDecision`
   verbatim — it never re-derives the verdict or exit-code basis.
4. **Scheduled matrix = declared marker + boundary gate reusing the F25 budget (D4).** No scheduler, no cron, no
   network in the cores. `ValidationMatrix` is a pure decision: a declared `Exhaustive`-cost matrix runs only
   when the run boundary admits `Exhaustive` (the F25 `CostBudget` ceiling — `Release`/scheduled), else it is
   `Deferred` with a named reason; the actual CI cron trigger is a host/CI concern out of this row's scope.
5. **Reuse F053/F054/F055/F056/F25/F033 unchanged (D5).** No new release-rule family, no change to
   `ReleaseRuleKind` / `evaluateRelease` / the F53 partition, no change to F54 sensing, no change to the
   verify/release exit-code scheme, no change to `CommandRecord` / `Provenance` / `AuditSnapshot` identity, no
   new dependency. Pack duration stays sensed metadata excluded from identity (F25/F032).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).

**Primary Dependencies**: **FSharp.Core 10.1.301 only** for the pure cores; the JSON projection uses **BCL
`System.Text.Json`** (`Utf8JsonWriter`, already used by every `*Json` projection) — **no new package**. The
host-edge pack runs reuse the existing `FS.GG.Governance.GateExecution.ExecutionPort` (F051, sentinel exit
codes) and `Config.Loader.FileReader` (F014) — **no new dependency anywhere**. Project references reused
verbatim: `Config` (`SurfaceId`, `Cost`, `EnvironmentClass`), `Enforcement` (`Severity`, `Maturity`,
`deriveEffectiveSeverity`), `Ship` (`Verdict`, `ExitCodeBasis`), `ReleaseRules` (`ReleaseRuleKind`, `FactState`,
`ReleaseFacts`, `ReleaseDecision`, `evaluateRelease`), `ReleaseFactsSensing` (`SensedRelease`, `ReleaseSnapshot`,
`ReleaseExpectations`, `SourceLayout`), `CommandRecord` (`CommandRecord`, `canonicalId`), `CommandKind`
(`CommandKind.Pack`, `KindedCommandRun`, `AuditSnapshot`, `auditSnapshot`), `Provenance`
(`Provenance`, `BuilderIdentity`), `FreshnessKey` (`Revision`, `RuleHash`, `GeneratorVersion`, `ArtifactHash`),
`CostBudget` (the ordered `Cost` ceiling for `ValidationMatrix`), `GateExecution` (`ExecutionPort`,
`GateCommand`, `ExecutionOutcome`), `ProvenanceJson` (sidecar precedent).

**Storage**: None new in the cores (pure evaluation over caller-supplied sensed inputs). The only new write is
the deterministic **`attestation.json` sidecar** through the existing temp+rename `ArtifactWriter` edge; the
extended `release.json` / `verify.json` are written by the existing `ReleaseCommand` / `VerifyCommand`
`ArtifactWriter`. No database, no network, no registry. Packing writes pack artifacts to the constitution's
`~/.local/share/nuget-local/` via the existing execution port (the host edge, not a core).

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). One new test project per new
library. The matrices the spec demands: a **version-bump matrix** (bumped / unbumped / downgraded / packed-no-
artifact across multiple packable projects — SC-001); a **pack-evidence fixture** with a real `ExecutionPort`
recording `Pack` runs incl. a failed pack with its sentinel (SC-001, SC-008); a **mergeable-but-not-releasable**
+ **fully-releasable** fixture (SC-002); a **verify release-readiness preview** fixture (advisory, never the
gate — SC-003); **publish-plan / posture / template-pin-drift** fixtures (SC-004); **attestation-summary
snapshot** fixtures incl. a no-op-input-change stability check, a failed-build no-attested-subject case, and the
not-formal-compliance marker (SC-005); a **scheduled-matrix** fixture (deferred in inner loop, runs at the
boundary, never invented — SC-006); **report-object parity + determinism/reordering** per projection (SC-007);
and **safe-failure** fixtures distinguishing missing/malformed input from tool defect (SC-008). FSI semantic
tests load the public surface (`evaluatePack`, `versionPolicy`, `summarize`, `assemble`, `preview`, `decideMatrix`,
`ofReleaseReport`, `ofAttestation`), never internals (Constitution I).

**Target Platform**: Cross-platform .NET libraries + the existing `fsgg release` / `fsgg verify` CLI
executables (Linux/macOS/Windows); standalone (no monorepo) and monorepo usage (FR-013).

**Project Type**: Publication-boundary expansion — four pure leaf cores, one deterministic JSON projection, two
extended JSON projections, and an additive host-edge wiring of two existing commands; single-solution F# layout.

**Performance Goals**: Not a hot inner-loop path. The pure cores are single linear passes (per-project version
comparison, per-snapshot projection). The only expense is the *real* pack runs the boundary exists to enforce —
those run through the existing F051 port at the `Pack` tier; the broad matrix is deferred to the scheduled
boundary by `ValidationMatrix` so the inner loop never pays for it (FR-009).

**Constraints**: Deterministic, **byte-identical** evidence, version policy, attestation, report, and JSON for
identical repository state (no wall-clock / abs-path / username / environment / input-order dependence; stable
ordering and path normalization; pack duration retained as sensed metadata only — FR-010, SC-005, SC-007). The
pure cores carry **zero** filesystem / process / registry / network dependency; all I/O is at the
`ReleaseCommand` host edge through existing ports (FR-014). Input-vs-tool-defect diagnostics preserved: no
packable project (vacuously satisfied + reported), an unreadable pack output, an absent provenance/attestation
input, a missing publish plan each produce a clear input signal — **no fabricated pack, no hollow attestation,
no fabricated pass** (FR-011, Constitution VI). Standalone with no monorepo dependency (FR-013). The release/
verify exit-code schemes, the F53 partition, F54 sensing, and `CommandRecord`/`Provenance`/`AuditSnapshot`
identity all untouched (FR-003, FR-005, D5).

**Scale/Scope**: 5 new `src` libraries (`PackEvidence`, `Attestation`, `ReleaseReport`, `ValidationMatrix`,
`AttestationJson`) + 5 new test projects; 2 extended projections (`ReleaseJson` → `fsgg.release/v2`, `VerifyJson`
additive); 2 extended host pipelines (`ReleaseCommand` packs + builds the report/attestation/sidecar;
`VerifyCommand` emits the advisory preview); 5 new committed surface baselines; new fixtures per core. **No**
new release-rule family, **no** enforcement-truth-table change, **no** verify/release exit-code change, **no**
new dependency. P1 = pack/version evidence + report + boundary (`PackEvidence`, `ReleaseReport`, release/verify
wiring); P2 = attestation + publish/posture/pin surfacing (`Attestation`, `AttestationJson`, release.json v2);
P3 = scheduled matrix (`ValidationMatrix`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Every new public module (`PackEvidence.Model`/
  `Pack`, `Attestation.Model`/`Attestation`, `ReleaseReport.Model`/`Report`, `ValidationMatrix.Model`/`Matrix`,
  `AttestationJson`) is drafted as `.fsi` and exercised through the packed/loaded public surface before any
  `.fs` body exists (the F041/F052/F053–F057 precedent). Semantic tests call `evaluatePack`, `versionPolicy`,
  `summarize`, `assemble`, `preview`, `decideMatrix`, `ofReleaseReport`, `ofAttestation` — never internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every new public module ships a curated `.fsi`; `.fs` bodies carry
  no access modifiers. Five new committed surface baselines (`PackEvidence`, `Attestation`, `ReleaseReport`,
  `ValidationMatrix`, `AttestationJson`); the `ReleaseJson` / `VerifyJson` baselines change only by added `val`s
  for the new projections (additive, the new schema version) — no existing signature is altered.
- **III. Idiomatic Simplicity** — PASS. Closed DUs (`PackOutcome`, `VersionVerdict`, attestation field records,
  `MatrixPlan`), plain records, pipelines, exhaustive matches; no SRTP / reflection / type-providers / custom
  CEs / non-trivial active patterns; no new dependency. The version comparison reuses the F54 baseline-compare
  idiom; the matrix reuses the F25 **ordered** `Cost` ceiling (no arbitrary weights). Any local mutation in a
  JSON writer follows the disclosed `ProvenanceJson` / `ReleaseJson` precedent with a one-line reason.
- **IV. Elmish/MVU Is the Boundary** — PASS. `PackEvidence` / `Attestation` / `ReleaseReport` /
  `ValidationMatrix` are pure, total leaves — no MVU ceremony (the F041/F046/F053 precedent). The behavioral
  change (packing every packable project, recording `Pack` runs, reading outputs, building the snapshot +
  attestation + report, writing the sidecar) happens inside the **existing** `ReleaseCommand` MVU boundary: the
  pure cores are called in `update`; the pack runs and sidecar writes are `Effect`s executed only at the
  `Interpreter` edge through the existing `ExecutionPort` / `ArtifactWriter` ports. The verify preview is a pure
  projection in the existing `VerifyCommand` `update`. No new I/O seam is introduced in a pure core.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail-before/pass-after against real cores
  (`ReleaseRules` / `ReleaseFactsSensing` / `Enforcement` / `CommandKind` / `Provenance` never mocked) and a
  real `ExecutionPort` for the pack-evidence fixtures (real `dotnet pack` invocations, as F052/F24/F25 did). A
  failed pack is exercised with its real sentinel exit code. Any synthetic pack output (e.g. a hand-written
  `.nupkg` digest where a real pack is infeasible in CI) is disclosed at the use site, carries `Synthetic` in
  the test name, and is listed in the PR.
- **VI. Observability and Safe Failure** — PASS. Publication evaluation distinguishes a missing/malformed
  **input** (no packable project ⇒ vacuously satisfied + reported; an unreadable pack output; an absent
  provenance/attestation input ⇒ blocks rather than a hollow attestation; a missing publish plan ⇒ blocks
  naming it) from a **tool defect**, naming the offending source (FR-011); no swallowed errors; no fabricated
  pack, no hollow attestation, no fabricated pass. A blocked release is reported as such, never as a pass.

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (five new libraries) and new
observable host output (one new `attestation.json` sidecar + additive `release.json` v2 / `verify.json` fields).
Requires the full chain: spec, plan, `.fsi` for every new module, five new surface-area baselines, test
evidence, and documentation of the new/changed JSON contracts. It adds **no** dependency, **no** new
release-rule family, **no** enforcement-truth-table change, and **no** verify/release exit-code change, so the
migration surface is limited to the new `attestation.json` and the additive release/verify schema bumps
(documented in `contracts/attestation-json.md`, `contracts/release-json-v2.md`, `contracts/verify-json-preview.md`).

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/061-verify-release-provenance/
├── plan.md                                # This file (/speckit-plan output)
├── research.md                            # Phase 0 — D1..D7 decisions
├── data-model.md                          # Phase 1 — pack evidence, version policy, attestation, report, matrix
├── quickstart.md                          # Phase 1 — per-story validation scenarios
├── contracts/                             # Phase 1
│   ├── pack-evidence.md                   #   PackEvidence + evaluatePack + versionPolicy (pure)
│   ├── attestation-summary.md             #   AttestationSummary + summarize over the F25 AuditSnapshot (pure)
│   ├── release-report.md                  #   ReleaseReport (SoT) + assemble + preview (pure)
│   ├── validation-matrix.md               #   declared exhaustive matrix + decideMatrix (pure, reuses CostBudget)
│   ├── attestation-json.md                #   fsgg.attestation/v1 sidecar (byte-identical, order-independent)
│   ├── release-json-v2.md                 #   fsgg.release/v2 additive fields (packageEvidence/versionPolicy/attestation)
│   ├── verify-json-preview.md             #   verify.json additive releaseReadiness preview block
│   └── release-yml-packable.md            #   .fsgg/release.yml additive packableProjects + matrix declaration
├── checklists/
│   └── requirements.md                    # (already present — spec quality checklist)
└── tasks.md                               # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.PackEvidence/                 # NEW (P1) — pure: packed evidence + version policy + FactState contributions
│   ├── Model.fsi / Model.fs                       #   PackEvidence, PackOutcome, VersionVerdict, PackVerdict,
│   │                                              #     PackEvidenceSet, NoArtifactReason
│   ├── Pack.fsi / Pack.fs                          #   evaluatePack : baseline -> PackOutcome list -> PackEvidenceSet;
│   │                                              #     versionPolicy : baseline -> packed -> VersionVerdict;
│   │                                              #     factContributions : PackEvidenceSet -> Map<ReleaseRuleKind,FactState>
│   └── FS.GG.Governance.PackEvidence.fsproj        #   refs: Config, ReleaseRules, CommandKind, CommandRecord, FreshnessKey
├── FS.GG.Governance.Attestation/                  # NEW (P2) — pure: SLSA/in-toto-shaped projection of the F25 AuditSnapshot
│   ├── Model.fsi / Model.fs                       #   AttestationSubject, AttestationMaterials, AttestationInvocation,
│   │                                              #     AttestationSummary, ComplianceMarker
│   ├── Attestation.fsi / Attestation.fs            #   summarize : AuditSnapshot -> PackEvidenceSet -> AttestationSummary
│   └── FS.GG.Governance.Attestation.fsproj         #   refs: CommandKind, Provenance, CommandRecord, FreshnessKey, Config, PackEvidence
├── FS.GG.Governance.ReleaseReport/                # NEW (P1) — pure: the immutable, presentation-free SoT report
│   ├── Model.fsi / Model.fs                       #   ReleaseReport, VerifyReleasePreview, PreconditionEvidence
│   ├── Report.fsi / Report.fs                      #   assemble : ReleaseDecision -> SensedRelease -> PackEvidenceSet ->
│   │                                              #     AttestationSummary -> ReleaseReport;  preview : ReleaseReport -> VerifyReleasePreview
│   └── FS.GG.Governance.ReleaseReport.fsproj       #   refs: ReleaseRules, ReleaseFactsSensing, PackEvidence, Attestation, Ship
├── FS.GG.Governance.ValidationMatrix/             # NEW (P3) — pure: declared exhaustive matrix + boundary decision
│   ├── Model.fsi / Model.fs                       #   ExhaustiveMatrix, MatrixBoundary, MatrixPlan, DeferReason
│   ├── Matrix.fsi / Matrix.fs                      #   decideMatrix : CostBudget -> MatrixBoundary -> ExhaustiveMatrix option -> MatrixPlan
│   └── FS.GG.Governance.ValidationMatrix.fsproj    #   refs: Config, CostBudget
├── FS.GG.Governance.AttestationJson/              # NEW (P2) — projection
│   ├── AttestationJson.fsi / AttestationJson.fs   #   ofAttestation : AttestationSummary -> string  (fsgg.attestation/v1)
│   └── FS.GG.Governance.AttestationJson.fsproj     #   refs: Attestation
├── FS.GG.Governance.ReleaseJson/                  # EXTEND — fsgg.release/v2 (additive): ofReleaseReport
│   └── ReleaseJson.fsi / ReleaseJson.fs            #   + ofReleaseReport : ReleaseReport -> string; schemaVersion -> v2
├── FS.GG.Governance.VerifyJson/                   # EXTEND — additive releaseReadiness preview block
│   └── VerifyJson.fsi / VerifyJson.fs              #   + ofVerifyDecisionWithPreview (or extended ofVerifyDecision) carrying the preview
├── FS.GG.Governance.ReleaseCommand/               # EXTEND — pack projects; build snapshot+attestation+report; write attestation.json
│   ├── Declaration.fs(i)                           #   additive: packableProjects + exhaustive-matrix declaration
│   ├── Loop.fs(i)                                  #   new Effects: PackProjects; build PackEvidenceSet, AuditSnapshot,
│   │                                              #     AttestationSummary, ReleaseReport in update; project v2 + attestation.json
│   └── Interpreter.fs(i)                           #   run each pack via the F051 ExecutionPort; read outputs; write the sidecar
└── FS.GG.Governance.VerifyCommand/                # EXTEND — advisory release-readiness preview (never the gate)
    ├── Loop.fs(i)                                  #   build the same evidence advisory; ReleaseReport.preview in update
    └── Interpreter.fs(i)                           #   (reuse) — preview adds no blocking gate, F56 exit scheme unchanged

tests/
├── FS.GG.Governance.PackEvidence.Tests/           # NEW — version-bump matrix, packed-no-artifact, failed-pack sentinel,
│                                                   #   factContributions, determinism/reorder (real ExecutionPort fixture)
├── FS.GG.Governance.Attestation.Tests/            # NEW — subject/builder/materials/invocation, byte-identity,
│                                                   #   no-op-input-change stability, failed-build no-subject, marker present
├── FS.GG.Governance.ReleaseReport.Tests/          # NEW — report-object parity, mergeable-but-not-releasable, preview advisory
├── FS.GG.Governance.ValidationMatrix.Tests/       # NEW — deferred-in-inner-loop, runs-at-boundary, never-invented
├── FS.GG.Governance.AttestationJson.Tests/        # NEW — schemaVersion/field order, byte-for-byte, order-independence
├── FS.GG.Governance.ReleaseCommand.Tests/         # EXTEND — real-fs end-to-end: packs, blocks on unbumped, writes v2 + attestation.json
└── FS.GG.Governance.VerifyCommand.Tests/          # EXTEND — preview present + advisory, exit scheme unchanged

surface/
├── FS.GG.Governance.PackEvidence.surface.txt       # NEW
├── FS.GG.Governance.Attestation.surface.txt        # NEW
├── FS.GG.Governance.ReleaseReport.surface.txt      # NEW
├── FS.GG.Governance.ValidationMatrix.surface.txt   # NEW
├── FS.GG.Governance.AttestationJson.surface.txt    # NEW
├── FS.GG.Governance.ReleaseJson.surface.txt        # EDIT — + ofReleaseReport
└── FS.GG.Governance.VerifyJson.surface.txt         # EDIT — + preview projection

FS.GG.Governance.sln                                # EDIT — add 5 src + 5 test projects
```

**Structure Decision**: Compose, don't fork. F26 **consumes** the F053 `ReleaseDecision` + partition, the F054
sensed snapshot, the F25 `AuditSnapshot` + `ProvenanceJson` precedent, the F032/F033
`CommandRecord`/`Provenance` identity, the F051 `ExecutionPort`, and the F25 `CostBudget` ordered ceiling —
adding the publication evidence *around* them, never inside them. Pack/version evaluation is one pure leaf
(`PackEvidence`); the SLSA-shaped projection is a second (`Attestation`); the immutable single-source-of-truth
report is a third (`ReleaseReport`); the scheduled-matrix decision is a fourth (`ValidationMatrix`); the
attestation sidecar is a dedicated projection (`AttestationJson`) following the F25 `ProvenanceJson` precedent.
The behavioral change — packing every packable project and grounding the existing release rules in real pack
evidence — is wired at the existing `ReleaseCommand` host edge through the established F051 `ExecutionPort`,
additively, with every existing `route.json`/`ship.json` golden left byte-identical and only explicitly-
versioned fields added to the release/verify goldens.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
