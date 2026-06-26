# Tasks: Verify & Release Publication Boundary — Pack, Version, Publish-Plan, and Provenance Attestation (F26)

> **Implementation status (2026-06-25).** The five pure leaf cores (`PackEvidence`, `Attestation`,
> `ReleaseReport`, `ValidationMatrix`, `AttestationJson`) and the two additive projection extensions
> (`ReleaseJson` → `fsgg.release/v2`, `VerifyJson` `releaseReadiness` preview) are **implemented, wired into
> the solution, and fully green** — Setup (Phase 1), Foundational (Phase 2), and US1–US5 (Phases 3–7) are
> complete with real-evidence semantic tests over the loaded public surface (PackEvidence 28, ReleaseReport
> 16, Attestation 12, AttestationJson 10, ValidationMatrix 7; ReleaseJson 19 and VerifyJson 25 incl. the new
> v2/preview tests). All five new surface baselines are blessed; the `release.json` golden is re-blessed v1→v2;
> every existing `route.json`/`ship.json`/`verify.json` golden stays byte-identical (the schema bump is
> additive, the verify preview is byte-identical-when-absent). One downstream assertion (`ReleaseCommand.Tests`)
> was updated for the additive v1→v2 schemaVersion bump.
>
> **LANDED by `065-release-provenance-host-wiring`.** The Phase 8 host edge (the MVU host rework —
> T052/T053/T054/T055) is wired and green: `fsgg release` packs every declared packable project through the
> F051 `ExecutionPort` (the shared `FS.GG.Governance.ReleaseDeclaration` leaf supersedes the row-local
> `Declaration`), builds the `AuditSnapshot`/`AttestationSummary`/`ReleaseReport`, and writes `attestation.json`
> (`fsgg.attestation/v1`) + `release.json` v2 (`ofReleaseReport`); `fsgg verify` emits the advisory,
> declaration-gated `releaseReadiness` preview (byte-identical verify.json when absent). The host surfaces are
> re-blessed and the existing `route.json`/`ship.json`/no-declaration `verify.json` stay byte-identical.
> **Partial, tracked in `065` tasks.md:** the real-filesystem `dotnet pack` E2E (T048/T050/T051 → 065 T018),
> the mergeable-vs-releasable + FR-008 precondition fixture (T049 → 065 T023), and the frozen byte-identity
> host goldens / quickstart smoke (T057/T062 → 065 T009/T024) — the wiring is covered by pure-MVU transition +
> emitted-effect tests over the real F26 cores with disclosed-synthetic pack execution; the real-`dotnet pack`
> evidence and frozen goldens remain. Honest status: those `[US*]` E2E tasks stay `[ ]` until the real surface
> is exercised end-to-end (the vertical-slice rule).

**Input**: Design documents from `/specs/061-verify-release-provenance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md,
contracts/pack-evidence.md, contracts/release-report.md, contracts/attestation-summary.md,
contracts/validation-matrix.md, contracts/attestation-json.md, contracts/release-json-v2.md,
contracts/verify-json-preview.md, contracts/release-yml-packable.md

**Tier**: **Tier 1 (contracted change)** — full chain owed: `.fsi` for every new module, five new surface
baselines, real test evidence, and three documented JSON contracts (one new sidecar `fsgg.attestation/v1`, the
additive `release.json` → `fsgg.release/v2`, the additive `verify.json` `releaseReadiness` preview). New public
projects: four pure leaf cores (`PackEvidence`, `Attestation`, `ReleaseReport`, `ValidationMatrix`) and one
deterministic projection (`AttestationJson`); two extended projections (`ReleaseJson` → v2, `VerifyJson`
additive); two extended host projects (`ReleaseCommand`, `VerifyCommand`) gain an additive edge step. **No** new
dependency, **no** new release-rule family, **no** change to `ReleaseRuleKind`/`evaluateRelease`/the F53
partition, **no** change to F54 sensing, **no** change to the verify/release exit-code scheme, **no** change to
`CommandRecord`/`Provenance`/`AuditSnapshot` identity (FR-003, FR-005, D5). The only new observable host output
is one new `attestation.json` sidecar plus additive `release.json` v2 / `verify.json` fields; every existing
`route.json`/`ship.json` golden stays byte-identical. Tests are in scope (Constitution V; plan lists every
`.Tests` project).

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within a phase marked `[P]`
may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1`/`US2`/`US3`/`US4`/`US5`; setup/foundational/integration/polish tasks carry no story tag
- Discipline (Constitution I/II): for every **new** module draft its `.fsi` and a compiling stub before the
  real `.fs` body; semantic tests call the loaded public surface (`Pack.evaluatePack`/`versionPolicy`/
  `factContributions`, `Report.assemble`/`preview`, `Attestation.summarize`, `Matrix.decideMatrix`,
  `AttestationJson.ofAttestation`, `ReleaseJson.ofReleaseReport`, the `VerifyJson` preview projection), never
  internals (Constitution I).

**Design note — compose, don't fork (plan §Structure Decision, D5).** Every verdict/sensing/identity decision
already exists and is consumed **verbatim**: F53 `ReleaseRules` (`ReleaseRuleKind`, `FactState`, `ReleaseFacts`,
`ReleaseDecision`, `evaluateRelease`, `releaseRuleKindOrdinal`), F54 `ReleaseFactsSensing` (`SensedRelease`,
`ReleaseSnapshot`), F55 `ReleaseCommand` (five exit codes, `release.json`), F56 `VerifyCommand` (five exit
codes), F25 `CommandKind` (`Pack`, `KindedCommandRun`, `AuditSnapshot`, `auditSnapshot`) + `ProvenanceJson`
(sidecar precedent) + `CostBudget` (ordered `Cost` ceiling), F33 `Provenance` (`canonicalId`, `BuilderIdentity`),
F29 `FreshnessKey` (`ArtifactHash`, `Revision`, `RuleHash`, `GeneratorVersion`), F51 `GateExecution`
(`ExecutionPort`, `GateCommand`), F24 `Ship` (`Verdict`, `ExitCodeBasis`). This row supplies only the
**publication evidence**: the packed evidence + version policy (`PackEvidence`), the immutable single-source-of-
truth report (`ReleaseReport`), the SLSA/in-toto-shaped projection (`Attestation`), the scheduled-matrix
decision (`ValidationMatrix`), and the attestation sidecar (`AttestationJson`). No reused type gains or loses a
field; no new release-rule family is added (FR-003).

**Design note — pure cores + edge-only I/O (Constitution IV, FR-014).** `Pack.evaluatePack`/`versionPolicy`/
`factContributions`, `Report.assemble`/`preview`, `Attestation.summarize`, `Matrix.decideMatrix`, and all three
JSON projections are **pure, total** functions over already-sensed inputs (the F53/F54/F25 leaf-plus-sensor
precedent) — zero filesystem/process/registry/network dependency. The only new I/O — packing every packable
project through the existing F51 `GateExecution.ExecutionPort`, reading each pack output's artifact path/version/
digest, and writing the `attestation.json` sidecar through the existing `ArtifactWriter` — lives in the
**existing** `ReleaseCommand`/`VerifyCommand` MVU boundary: the pure cores are called in `update`; the pack runs
and sidecar write are `Effect`s executed only at the `Interpreter` edge.

**Design note — packed version is the source of truth (D1).** `versionPolicy` evaluates the version against the
**packed artifact's** version, so a source bump that never reaches the artifact still blocks. `factContributions`
derives the `Met`/`Unmet` contributions for `VersionBump`/`PackageMetadata`/`Provenance` from real pack output;
the host **merges them over** the F54 sensed facts (packed evidence wins on those three families) before calling
`Release.evaluateRelease` unchanged. `ReleaseReport` carries the F53 `ReleaseDecision` **verbatim** — it never
re-derives the verdict or the exit-code basis.

**Design note — what is foundational vs. story-owned.** The four `Model.fsi` type vocabularies
(`PackEvidence.Model`, `Attestation.Model`, `ReleaseReport.Model`, `ValidationMatrix.Model`) plus a compiling
stub for every entry point (`Pack`, `Report`, `Attestation`, `Matrix`, `AttestationJson`, the `ReleaseJson`
v2 `ofReleaseReport`, the `VerifyJson` preview projection), the surface-drift harnesses, and the shared test
support are **foundational** — the whole project graph (`PackEvidence` → `Attestation` → `ReleaseReport`; the
projections over their cores) must compile before any story body lands. Each story then replaces its stubbed body
with the real one, adds its fixtures, and its tests: **packed evidence + version policy** in US1; **the immutable
report + boundary + release.json v2 + verify preview** in US2; **the attestation summary + attestation.json** in
US3; **the publish-plan / posture / template-pin precondition surfacing** in US4; **the scheduled-matrix
decision** in US5. The host edge wiring (pack every project, build the snapshot/attestation/report, write the
sidecar + v2) lands in the Integration phase once the cores exist.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the five new `src` projects, extend the two projection projects' references, create the five
new test projects + extend the two host test projects, and wire the solution. Mirror the F25
`CostBudget`/`CommandKind` (Model + pure pack in one project) pure-leaf precedent exactly.

- [X] T001 [P] Create `src/FS.GG.Governance.PackEvidence/FS.GG.Governance.PackEvidence.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.Config`, `FS.GG.Governance.ReleaseRules`,
  `FS.GG.Governance.CommandKind`, `FS.GG.Governance.CommandRecord`, `FS.GG.Governance.FreshnessKey`) with compile
  order `Model.fsi` → `Model.fs` → `Pack.fsi` → `Pack.fs`. Mirror `FS.GG.Governance.CacheEligibility.fsproj`.
- [X] T002 [P] Create `src/FS.GG.Governance.Attestation/FS.GG.Governance.Attestation.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.CommandKind`, `FS.GG.Governance.Provenance`,
  `FS.GG.Governance.CommandRecord`, `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config`,
  `FS.GG.Governance.PackEvidence`) with compile order `Model.fsi` → `Model.fs` → `Attestation.fsi` →
  `Attestation.fs`. Depends on T001 existing (project reference).
- [X] T003 [P] Create `src/FS.GG.Governance.ReleaseReport/FS.GG.Governance.ReleaseReport.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.ReleaseRules`,
  `FS.GG.Governance.ReleaseFactsSensing`, `FS.GG.Governance.PackEvidence`, `FS.GG.Governance.Attestation`,
  `FS.GG.Governance.Ship`) with compile order `Model.fsi` → `Model.fs` → `Report.fsi` → `Report.fs`. Depends on
  T001/T002 existing.
- [X] T004 [P] Create `src/FS.GG.Governance.ValidationMatrix/FS.GG.Governance.ValidationMatrix.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.Config`, `FS.GG.Governance.CostBudget`)
  with compile order `Model.fsi` → `Model.fs` → `Matrix.fsi` → `Matrix.fs`.
- [X] T005 [P] Create `src/FS.GG.Governance.AttestationJson/FS.GG.Governance.AttestationJson.fsproj` (net10.0,
  `GenerateDocumentationFile`, `IsPackable=true`; refs `FS.GG.Governance.Attestation`) with compile order
  `AttestationJson.fsi` → `AttestationJson.fs`. Depends on T002 existing. Mirror `FS.GG.Governance.ProvenanceJson.fsproj`.
- [X] T006 Extend the two projection projects' references (no new project): add a `FS.GG.Governance.ReleaseReport`
  project reference to `src/FS.GG.Governance.ReleaseJson/FS.GG.Governance.ReleaseJson.fsproj` (for
  `ofReleaseReport`) and to `src/FS.GG.Governance.VerifyJson/FS.GG.Governance.VerifyJson.fsproj` (for the
  `VerifyReleasePreview` projection). Depends on T003 existing.
- [X] T007 [P] Create the five new test projects (Expecto + Expecto.FsCheck/FsCheck, each with a `Main.fs` Expecto
  entry — mirror `tests/FS.GG.Governance.CommandKind.Tests`): `tests/FS.GG.Governance.PackEvidence.Tests` (refs
  `PackEvidence`, `Config`, `ReleaseRules`, `CommandKind`, `CommandRecord`, `FreshnessKey`, and
  `GateExecution` — the real `ExecutionPort` for the pack-evidence fixtures);
  `tests/FS.GG.Governance.Attestation.Tests` (refs `Attestation`, `CommandKind`, `Provenance`, `CommandRecord`,
  `FreshnessKey`, `Config`, `PackEvidence`); `tests/FS.GG.Governance.ReleaseReport.Tests` (refs `ReleaseReport`,
  `ReleaseRules`, `ReleaseFactsSensing`, `PackEvidence`, `Attestation`, `Ship`);
  `tests/FS.GG.Governance.ValidationMatrix.Tests` (refs `ValidationMatrix`, `Config`, `CostBudget`);
  `tests/FS.GG.Governance.AttestationJson.Tests` (refs `AttestationJson`, `Attestation`, `CommandKind`,
  `Provenance`, `CommandRecord`, `FreshnessKey`, `PackEvidence`). The existing `ReleaseJson.Tests` /
  `VerifyJson.Tests` / `ReleaseCommand.Tests` / `VerifyCommand.Tests` projects are **extended in place** (no new
  project) by later phases — add the `ReleaseReport` project reference to `ReleaseJson.Tests`/`VerifyJson.Tests`
  (for the v2/preview projection tests) here, and add any core refs the host E2E tests construct types from
  (otherwise the host-output-inspecting E2E tests need only the existing host reference).
- [X] T008 Add the five `src` + five `tests` projects to `FS.GG.Governance.sln` (mirror the F25 solution-folder
  entries) and confirm `dotnet build FS.GG.Governance.sln` resolves the new graph with empty/stub modules and
  **no reference cycle** (`PackEvidence` → existing leaves; `Attestation` → `PackEvidence` + existing leaves;
  `ReleaseReport` → `PackEvidence` + `Attestation` + existing; `ValidationMatrix` → existing; `AttestationJson` →
  `Attestation`; the two projection extends → `ReleaseReport`; the two hosts untouched in this phase). Depends on
  T001–T007.

**Checkpoint**: Solution restores and builds with empty/stub modules; the reference directions
(`PackEvidence` → `Attestation` → `ReleaseReport`; projections → their core) are acyclic.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land every new **type vocabulary** (real) plus a compiling stub for each entry point, the
surface-drift harnesses, and the shared test support. **No story body may begin until the whole graph compiles
and the contracts are exercisable.**

**⚠️ CRITICAL**: Blocks US1–US5.

- [X] T009 [P] Author `src/FS.GG.Governance.PackEvidence/Model.fsi` **and** `Model.fs` together (real, compile as
  a pair): `PackArtifact = { Surface: SurfaceId; ArtifactPath: string; PackedVersion: string; Digest: ArtifactHash }`;
  `NoArtifactReason = NoArtifactEmitted | ArtifactUnreadable of string`; `PackOutcome = Packed of artifact:
  PackArtifact * run: KindedCommandRun | PackedNoArtifact of surface: SurfaceId * reason: NoArtifactReason * run:
  KindedCommandRun | PackFailed of surface: SurfaceId * sentinel: int * run: KindedCommandRun`; `VersionVerdict =
  Bumped of baseline: string * packed: string | Unbumped of version: string | Downgraded of baseline: string *
  packed: string | NoBaseline of packed: string | NotPackable`; `PackVerdict = { Surface: SurfaceId; Outcome:
  PackOutcome; Version: VersionVerdict; Reason: string }`; `PackEvidenceSet = { Verdicts: PackVerdict list; Runs:
  KindedCommandRun list; NoPackableProjects: bool }`. No access modifiers in `.fs` (Constitution II). Field/case
  order = the contract's declaration order (data-model §1; contracts/pack-evidence.md `Model.fsi`).
- [X] T010 [P] Author `src/FS.GG.Governance.Attestation/Model.fsi` + real `Model.fs`: `AttestationSubject =
  { Name: string; Digest: ArtifactHash; Version: string }`; `AttestationMaterials = { RuleHash: RuleHash;
  GeneratorVersion: GeneratorVersion; BaseRevision: Revision; HeadRevision: Revision; SourceCommit: Revision;
  ArtifactDigests: ArtifactHash list; Environment: EnvironmentClass }`; `AttestationInvocation = { Runs:
  KindedCommandRun list }`; `ComplianceMarker = CompatibleShapeNotFormalCompliance`; `AttestationSummary =
  { Subjects: AttestationSubject list; Builder: BuilderIdentity; Materials: AttestationMaterials; Invocation:
  AttestationInvocation; Identity: string; Compliance: ComplianceMarker }` (data-model §3;
  contracts/attestation-summary.md `Model.fsi`). Depends on T009 (no PackEvidence type used in the model, but the
  project reference must resolve).
- [X] T011 [P] Author `src/FS.GG.Governance.ReleaseReport/Model.fsi` + real `Model.fs`: `PreconditionEvidence =
  { Kind: ReleaseRuleKind; State: FactState; Reason: string }`; `ReleaseReport = { Decision: ReleaseDecision;
  Package: PackEvidenceSet; Preconditions: PreconditionEvidence list; Attestation: AttestationSummary;
  ReleaseExitCodeBasis: ExitCodeBasis }`; `VerifyReleasePreview = { Verdict: Verdict; Package: PackEvidenceSet;
  Preconditions: PreconditionEvidence list; Attestation: AttestationSummary; Advisory: bool }` (data-model §2;
  contracts/release-report.md `Model.fsi`). Depends on T009/T010.
- [X] T012 [P] Author `src/FS.GG.Governance.ValidationMatrix/Model.fsi` + real `Model.fs`: `ExhaustiveMatrix =
  { Name: string; Cost: Cost; Dimensions: string list }`; `MatrixBoundary = InnerLoop | ScheduledOrRelease`;
  `DeferReason = DeferredToScheduledBoundary of name: string * cost: Cost`; `MatrixPlan = RunNow of
  ExhaustiveMatrix | Deferred of DeferReason | NotDeclared` (data-model §4; contracts/validation-matrix.md
  `Model.fsi`).
- [X] T013 Author `src/FS.GG.Governance.PackEvidence/Pack.fsi` + a compiling stub `Pack.fs`: signatures
  `versionPolicy: string option -> string option -> VersionVerdict` (stub `NotPackable`), `evaluatePack:
  Map<SurfaceId,string> -> PackOutcome list -> PackEvidenceSet` (stub `{ Verdicts = []; Runs = [];
  NoPackableProjects = true }`), `factContributions: PackEvidenceSet -> Map<ReleaseRuleKind,FactState>` (stub
  `Map.empty`) per contracts/pack-evidence.md `Pack.fsi`. Depends on T009.
- [X] T014 Author `src/FS.GG.Governance.Attestation/Attestation.fsi` + a compiling stub `Attestation.fs`:
  signature `summarize: AuditSnapshot -> PackEvidenceSet -> AttestationSummary` returning a degenerate summary
  (`Subjects = []`, `Compliance = CompatibleShapeNotFormalCompliance`, `Identity = ""`, builder/materials/
  invocation built from the snapshot's `Provenance`/`Runs` so the type is inhabited) per
  contracts/attestation-summary.md `Attestation.fsi`. Depends on T010.
- [X] T015 Author `src/FS.GG.Governance.ReleaseReport/Report.fsi` + a compiling stub `Report.fs`: signatures
  `assemble: ReleaseDecision -> SensedRelease -> PackEvidenceSet -> AttestationSummary -> ReleaseReport` and
  `preview: ReleaseReport -> VerifyReleasePreview` per contracts/release-report.md `Report.fsi`. Stub `assemble`
  carries the `Decision`/`Package`/`Attestation` verbatim, `Preconditions = []`, `ReleaseExitCodeBasis =
  decision.ExitCodeBasis`; stub `preview` returns the advisory subset with `Advisory = true`. Depends on
  T009/T010/T011.
- [X] T016 Author `src/FS.GG.Governance.ValidationMatrix/Matrix.fsi` + a compiling stub `Matrix.fs`: signature
  `decideMatrix: CostBudget -> MatrixBoundary -> ExhaustiveMatrix option -> MatrixPlan` returning `NotDeclared`
  (stub) per contracts/validation-matrix.md `Matrix.fsi`. Depends on T012.
- [X] T017 [P] Author `src/FS.GG.Governance.AttestationJson/AttestationJson.fsi` + a compiling stub
  `AttestationJson.fs`: `schemaVersion = "fsgg.attestation/v1"`; `ofAttestation: AttestationSummary -> string`
  returns `"{}"` (stub) per contracts/attestation-json.md. Depends on T010.
- [X] T018 [P] Extend `src/FS.GG.Governance.ReleaseJson/ReleaseJson.fsi` + a compiling stub in `ReleaseJson.fs`:
  add `ofReleaseReport: ReleaseReport -> string` returning `"{}"` (stub) and bump the `schemaVersion` constant to
  `"fsgg.release/v2"`; **leave every existing `val` signature unchanged** (additive only — the F53 release
  projection stays). Depends on T011. (contracts/release-json-v2.md.)
- [X] T019 [P] Extend `src/FS.GG.Governance.VerifyJson/VerifyJson.fsi` + a compiling stub in `VerifyJson.fs`: add
  the additive preview projection (`ofVerifyDecisionWithPreview` carrying a `VerifyReleasePreview option`, per
  contracts/verify-json-preview.md — the F24 `ofVerifyDecisionWithSurfaceChecks` optional-additive precedent)
  returning the existing verify text **unchanged** when the preview is `None` (stub); **leave every existing
  `val` signature unchanged** and **do not bump** `schemaVersion` (`fsgg.verify/v1` stays). Depends on T011.
- [X] T020 Exercise every `.fsi` against its `.fs` and prove the public surface composes before the real bodies
  (Constitution I): `dotnet build FS.GG.Governance.sln` checks each pair, and a smoke semantic test in
  `tests/FS.GG.Governance.PackEvidence.Tests/SmokeTests.fs` loads and calls `Pack.evaluatePack`/`versionPolicy`/
  `factContributions` (stub), `Attestation.summarize` (stub), `Report.assemble`/`preview` (stub),
  `Matrix.decideMatrix` (stub), `AttestationJson.ofAttestation` (stub), `ReleaseJson.ofReleaseReport` (stub), and
  the `VerifyJson` preview projection (stub). Depends on T009–T019.
- [X] T021 [P] Add a `SurfaceDriftTests.fs` to each of the five new test projects — load the project's public
  surface, compare to `surface/FS.GG.Governance.<Project>.surface.txt`, honor `BLESS_SURFACE=1` (mirror the
  existing surface-drift test). Covers `PackEvidence`, `Attestation`, `ReleaseReport`, `ValidationMatrix`,
  `AttestationJson`. Baselines committed in Phase 9 once `.fs` bodies stabilize. Depends on T020.
- [X] T022 [P] Add shared test support: `tests/FS.GG.Governance.PackEvidence.Tests/Support.fs` — builders for
  `PackArtifact`/`PackOutcome` from **real** `CommandKind.KindedCommandRun` values (real `CommandRecord`s, never
  mocked) incl. a `Pack`-kind failed run carrying its sentinel exit code, plus a `Map<SurfaceId,string>` baseline
  helper; `tests/FS.GG.Governance.Attestation.Tests/Support.fs` — an `AuditSnapshot` builder over real
  `Provenance.build` inputs + `Pack` runs; `tests/FS.GG.Governance.ReleaseReport.Tests/Support.fs` — a
  `SensedRelease`/`ReleaseDecision` builder reusing **real** `ReleaseRules`/`ReleaseFactsSensing` values (never
  mocked) for mergeable-but-not-releasable and fully-releasable fixtures. Depends on T020.

**Checkpoint**: Every new type is real and compiles; `Pack`/`Attestation`/`Report`/`Matrix`/`AttestationJson` +
the two projection extends are stubs that compile; the smoke test exercises the public surface; the surface-drift
harnesses and shared test support are in place — story work can begin.

---

## Phase 3: User Story 1 — Every packable project must pack at a bumped version before release passes (Priority: P1) 🎯 MVP

**Goal**: `Pack.versionPolicy baseline packed` is a total comparison against the **packed** version (D1) —
`Bumped` iff strictly above baseline, `Unbumped` iff equal, `Downgraded` iff below, `NoBaseline` when none
supplied, `NotPackable` when no artifact. `Pack.evaluatePack baselines outcomes` builds the deterministic
`PackEvidenceSet`: one `PackVerdict` per packable project (sorted by `SurfaceId` then `ArtifactPath`), the
recorded `Pack` runs in carried order (a failed pack's run never dropped), and `NoPackableProjects = true` on
empty input. `Pack.factContributions` derives `Met` for a `Packed`+`Bumped` project and `Unmet` (for the
relevant `VersionBump`/`PackageMetadata`/`Provenance` family) for any `PackedNoArtifact`/`PackFailed`/`Unbumped`/
`Downgraded`. A project that fails to pack, or packs at an unbumped/downgraded version, contributes `Unmet` so
the existing `Release.rollup` blocks it with a named reason — no new release-rule family (FR-003).

**Independent Test**: A version-bump matrix (bumped / unbumped / downgraded / packed-no-artifact / no-baseline
across multiple packable projects) plus a pack-evidence fixture driven through a **real** `ExecutionPort`
recording `Pack` runs incl. a failed pack with its sentinel: every-project-bumped ⇒ all `Met`; a failed pack ⇒
`PackFailed(sentinel)` ⇒ `Unmet`, the failed run present in `Runs`; an unbumped/downgraded pack ⇒ `Unmet` naming
the project + version; reordering the outcomes yields a byte-identical evidence set (SC-001, SC-008).

### Tests for User Story 1 ⚠️ (write first, must FAIL before impl)

- [X] T023 [P] [US1] `tests/FS.GG.Governance.PackEvidence.Tests/VersionPolicyMatrixTests.fs` — assert
  `versionPolicy` over the full matrix: `Some "1.3.0"` packed over `Some "1.2.0"` baseline ⇒ `Bumped`; equal ⇒
  `Unbumped`; below ⇒ `Downgraded`; `baseline = None` ⇒ `NoBaseline packed`; `packed = None` ⇒ `NotPackable`;
  semantic-version ordering (not lexical — `1.10.0 > 1.9.0`) and pre-release/build-metadata edges per
  contracts/pack-evidence.md; total over every (baseline, packed) shape (FR-002, SC-001).
- [X] T024 [P] [US1] `tests/FS.GG.Governance.PackEvidence.Tests/EvaluatePackTests.fs` — drive `evaluatePack`
  over multiple packable projects: one `PackVerdict` per project, sorted by `(SurfaceId, ArtifactPath)`; the
  `Reason` names the project + outcome + version basis; every recorded `Pack` run present in `Runs` (incl. a
  `PackFailed` run with its sentinel — never dropped); empty outcomes ⇒ `NoPackableProjects = true` (vacuously
  satisfied); a `PackedNoArtifact (NoArtifactEmitted)` and an `ArtifactUnreadable msg` each surfaced as the
  closed reason — never a fabricated artifact (FR-001, FR-011, SC-001, SC-008; acceptance 1.1–1.3, edge cases).
- [X] T025 [P] [US1] `tests/FS.GG.Governance.PackEvidence.Tests/FactContributionsTests.fs` — assert
  `factContributions` maps a `Packed`+`Bumped` project to `Met` for `VersionBump`/`PackageMetadata`/`Provenance`,
  and any `PackedNoArtifact`/`PackFailed`/`Unbumped`/`Downgraded` to `Unmet` for the relevant family; the map is
  total over the evidence set and product-neutral; assert it composes with **real** `ReleaseRules.evaluateRelease`
  (merged over a real `SensedRelease`, packed evidence winning on those three families) so an unbumped project
  yields a **blocked** `ReleaseDecision` naming the project — no new family introduced (FR-002, FR-003, SC-001).
- [X] T026 [P] [US1] `tests/FS.GG.Governance.PackEvidence.Tests/RealPackEvidenceTests.fs` — a **real**
  `GateExecution.ExecutionPort` packs a trivial project (`dotnet pack`-class) recording a `Pack`
  `KindedCommandRun`, and a deliberately failing pack records its F51 sentinel; `evaluatePack` over the real
  outcomes is byte-identical on a re-derive and reorder-invariant; pack duration is sensed metadata inside the
  `CommandRecord` only and never affects the evidence set or the verdict (FR-010, SC-001 acceptance 1.4). Any
  synthetic pack output (where a real pack is infeasible in CI) is disclosed at the use site and carries
  `Synthetic` in the test name (Constitution V).

### Implementation for User Story 1

- [X] T027 [US1] `src/FS.GG.Governance.PackEvidence/Pack.fs` — replace the stubs with the real bodies:
  `versionPolicy` parses + compares versions semantically (the packed-version-is-truth rule, D1), total over the
  option shapes; `evaluatePack` builds one `PackVerdict` per outcome with a self-explaining `Reason`, sorts
  `Verdicts` by `(SurfaceId, ArtifactPath)`, preserves `Runs` order, and sets `NoPackableProjects` on empty;
  `factContributions` derives the `Met`/`Unmet` map for the three families. Pure, total, exhaustive matches (no
  wildcard). Makes T023–T026 pass. Depends on T013.

**Checkpoint**: MVP — every packable project is evaluated against its real packed artifact; an unbumped,
downgraded, no-artifact, or failed pack contributes `Unmet` and (via the existing F53 families) blocks release
with a named reason; the evidence set is deterministic and reorder-invariant. The report, attestation, matrix,
and host wiring are not yet landed.

---

## Phase 4: User Story 2 — Publication is a blocking boundary distinct from ship (Priority: P1)

**Goal**: `Report.assemble decision sensed pack attestation` builds the immutable, presentation-free
`ReleaseReport` (FR-012) — carrying the F53 `ReleaseDecision` **verbatim** (never re-deriving the verdict or
exit-code basis), the pack evidence, the `Preconditions` projected from the F54 `SensedRelease` (ordered by
`releaseRuleKindOrdinal`, D7), the attestation summary, and `ReleaseExitCodeBasis = decision.ExitCodeBasis`
(named at the report level so the boundary reads as first-class and distinct from ship, FR-004).
`Report.preview` is the advisory subset (`Advisory = true`, drops nothing). `ReleaseJson.ofReleaseReport`
projects the report to `fsgg.release/v2` (additive `packageEvidence`/`versionPolicy`/`attestation` fields);
`VerifyJson`'s preview projection renders the advisory `releaseReadiness` block. A change that is mergeable
(`fsgg ship` passes) may be **not releasable**; the two verdicts and bases are independent.

**Independent Test**: A mergeable-but-not-releasable fixture (ship passes, an unbumped version) ⇒ the
`ReleaseReport` carries the failing precondition and `ReleaseExitCodeBasis` distinct from ship; a fully-
releasable fixture ⇒ `ReleaseExitCodeBasis = Clean`. `ofReleaseReport` over the same report is byte-identical and
order-independent, and `release.json` carries the verdict/basis/each unmet precondition; the verify preview
carries `advisory: true` and the same evidence (SC-002, SC-003, SC-007).

### Tests for User Story 2 ⚠️ (write first, must FAIL before impl)

- [X] T028 [P] [US2] `tests/FS.GG.Governance.ReleaseReport.Tests/AssembleTests.fs` — `assemble` carries the F53
  `Decision` **verbatim** (same verdict, same `ExitCodeBasis`, same three-way partition — asserted by equality,
  never re-derived), carries the `PackEvidenceSet` + `AttestationSummary` unchanged, sets `ReleaseExitCodeBasis =
  Decision.ExitCodeBasis`, and orders `Preconditions` by `releaseRuleKindOrdinal`; a mergeable-but-not-releasable
  `SensedRelease`/`ReleaseDecision` (real, from Support) ⇒ the report's basis is **distinct** from a ship pass and
  names the unmet precondition; a fully-releasable fixture ⇒ `ReleaseExitCodeBasis = Clean` (FR-004, FR-012,
  SC-002; acceptance 2.1, 2.2, 2.4).
- [X] T029 [P] [US2] `tests/FS.GG.Governance.ReleaseReport.Tests/PreviewTests.fs` — `preview report` returns the
  advisory subset (`Verdict`/`Package`/`Preconditions`/`Attestation` from the report, `Advisory = true`), drops
  nothing, and is a pure projection that never carries an exit-code basis — the marker that verify is never the
  blocking release gate (FR-005, SC-003; acceptance 2.3).
- [X] T030 [P] [US2] `tests/FS.GG.Governance.ReleaseReport.Tests/DeterminismTests.fs` — repeated `assemble`/
  `preview` over identical inputs ⇒ byte-identical report; FsCheck: reordering the `SensedRelease` precondition
  inputs / the pack verdicts leaves the report's ordered fields unchanged; no abs-path/clock/username in any
  `Reason` (FR-010, SC-007).
- [X] T031 [P] [US2] `tests/FS.GG.Governance.ReleaseJson.Tests/OfReleaseReportTests.fs` — `ofReleaseReport report`
  emits `schemaVersion = "fsgg.release/v2"`; **every existing v1 field unchanged in shape/order** (incl. the v1
  `rules` array, whose per-`ReleaseRuleKind` `factState` + `reason` already surface the publish-plan / posture /
  template-pin precondition state — **no new `preconditions` field**, per contracts/release-json-v2.md); the
  **three additive** fields appended after `evidence` in a fixed order verified by raw-text `IndexOf` —
  `packageEvidence` (per-project surface/version-verdict/digest) < `versionPolicy` < `attestation` (identity
  reference, or null); the verdict + `exitCodeBasis` reused from the F53 decision verbatim; empty evidence ⇒
  well-formed empty arrays / null attestation; byte-identical for identical input. Also assert the **retained**
  `ofRelease decision sensed` emits a v2 document **byte-identical** to `ofReleaseReport` over the equivalent
  empty-package/empty-attestation report (existing call sites keep compiling, the v1→v2 shape is well-formed)
  (FR-015, SC-007; contracts/release-json-v2.md).
- [X] T032 [P] [US2] `tests/FS.GG.Governance.VerifyJson.Tests/ReleaseReadinessPreviewTests.fs` — the preview
  projection emits an optional `releaseReadiness` block with `advisory: true` and the same evidence the release
  boundary would, **never** altering verify's existing fields, its `schemaVersion` (`fsgg.verify/v1` unchanged —
  no bump), or its exit code (the F56 scheme unchanged); a run with no release preview ⇒ the block is **omitted**
  and every existing `verify.json` golden is byte-identical (FR-005, FR-015, SC-003; contracts/verify-json-preview.md).

### Implementation for User Story 2

- [X] T033 [US2] `src/FS.GG.Governance.ReleaseReport/Report.fs` — replace the stubs: `assemble` projects the F54
  `SensedRelease` facts into `PreconditionEvidence` (one per family, `Kind`/`State`/`Reason` from the sensed
  facts, ordered by `releaseRuleKindOrdinal`), carries the `Decision`/`Package`/`Attestation` verbatim, and sets
  `ReleaseExitCodeBasis = decision.ExitCodeBasis`; `preview` returns the advisory subset with `Advisory = true`.
  Immutable, presentation-free, pure, total. Makes T028–T030 pass. Depends on T015.
- [X] T034 [US2] `src/FS.GG.Governance.ReleaseJson/ReleaseJson.fs` — replace the stub `ofReleaseReport`: a
  hand-driven `Utf8JsonWriter` walk (the F25 `ProvenanceJson`/`RouteJson` precedent) emitting **every v1 field in
  its existing order** (incl. the `rules` array carrying each family's `factState` + `reason` — the publish-plan/
  posture/pin preconditions surface here, **no new `preconditions` field**) then the three additive fields
  `packageEvidence` < `versionPolicy` < `attestation`; closed-enum token helpers (exhaustive, no wildcard) for
  the version verdict; the verdict + `exitCodeBasis` rendered from the carried F53 decision verbatim (no
  re-derivation); arrays always present / attestation null when empty. Also update the **retained**
  `ofRelease: ReleaseDecision -> SensedRelease -> string` to emit a v2 document with empty `packageEvidence`/
  `versionPolicy` and null `attestation` (byte-identical to `ofReleaseReport` over an empty report — the contract
  requires existing call sites to keep compiling and emit a well-formed v2). Pure/total/no-I/O. Bump the shared
  `schemaVersion` constant to `fsgg.release/v2`. Makes T031 pass. Depends on T018/T033. (contracts/release-json-v2.md.)
- [X] T035 [US2] `src/FS.GG.Governance.VerifyJson/VerifyJson.fs` — replace the stub: render the optional
  `releaseReadiness` block from a `VerifyReleasePreview option` (`advisory: true` + the `packageEvidence`/
  `versionPolicy`/`attestation` shape mirroring release.json v2 — no `preconditions` field); absent preview ⇒
  the block is **omitted** and the existing `verify.json` bytes are byte-identical (no `schemaVersion` bump —
  `fsgg.verify/v1` unchanged). Pure/total/no-I/O. Makes T032 pass. Depends on T019/T033.

**Checkpoint**: US1 + US2 — the publication boundary is a first-class, immutable report carrying the F53 verdict
verbatim with a release exit-code basis distinct from ship; `release.json` (v2) surfaces the package evidence,
version policy, preconditions, and attestation reference; `fsgg verify` previews the same evidence advisorily.
The attestation summary, the publish/posture/pin precondition fixtures, the matrix, and the host wiring are not
yet landed.

---

## Phase 5: User Story 3 — SLSA/in-toto-shaped attestation summary, without overclaiming (Priority: P2)

**Goal**: `Attestation.summarize snapshot pack` projects the F25 `AuditSnapshot` (+ the pack subjects) into an
`AttestationSummary`: `Subjects` built from the `Packed` outcomes **only** (sorted by name; no subject for a
failed/no-artifact build, FR-008), `Materials`/`Invocation` from the snapshot's `Provenance`/`Runs` verbatim,
`Identity = Provenance.canonicalId snapshot.Provenance` (F33 verbatim — changes only on a reproducible-input
change, never on duration), and `Compliance = CompatibleShapeNotFormalCompliance` always (never overclaims).
`AttestationJson.ofAttestation` projects it to the `fsgg.attestation/v1` sidecar deterministically.

**Independent Test**: From a fixed `AuditSnapshot` (packed subjects, builder, materials, command runs):
`summarize` populates subject/builder/materials/invocation in an in-toto-compatible shape, is byte-identical for
identical inputs, changes `Identity` only on a reproducible-input change (a duration-only change leaves it
unchanged), and carries the not-formal-compliance marker; a failed-build snapshot ⇒ `Subjects = []` with the
failed run still under `Invocation.Runs`; `ofAttestation` round-trips byte-identically and order-independently
(SC-005).

### Tests for User Story 3 ⚠️ (write first, must FAIL before impl)

- [X] T036 [P] [US3] `tests/FS.GG.Governance.Attestation.Tests/SummarizeTests.fs` — `summarize` over a fixed
  snapshot + `PackEvidenceSet`: `Subjects` from `Packed` outcomes only (sorted by `Name`), each carrying
  `{ Name; Digest; Version }`; `Materials` = the snapshot's `RuleHash`/`GeneratorVersion`/base/head/source-commit/
  artifact-digests (as a set)/environment verbatim; `Invocation.Runs` = the snapshot's runs in carried order;
  `Identity = Provenance.canonicalId snapshot.Provenance` verbatim; `Compliance =
  CompatibleShapeNotFormalCompliance`. A failed-build snapshot (a `PackFailed`/no `Packed`) ⇒ `Subjects = []`
  while the failed run is still under `Invocation.Runs` — never an attested subject that was not produced (FR-007,
  FR-008, SC-005; acceptance 3.1, 3.3, edge cases).
- [X] T037 [P] [US3] `tests/FS.GG.Governance.Attestation.Tests/StabilityTests.fs` — `summarize` is byte-identical
  for identical inputs; a no-op re-derive is stable; changing **only** a `SensedDuration` leaves `Identity` (and
  the whole summary's identity-bearing fields) unchanged; changing a reproducible input (a subject digest, a
  material, an artifact digest, a command run) changes `Identity`; reordering the runs/digests inputs leaves the
  set-valued `ArtifactDigests` and the summary unchanged where order is non-significant (FR-007, FR-010, SC-005;
  acceptance 3.2).
- [X] T038 [P] [US3] `tests/FS.GG.Governance.AttestationJson.Tests/OfAttestationTests.fs` — `ofAttestation summary`
  emits `schemaVersion = "fsgg.attestation/v1"`; a fixed field order (`schemaVersion` < `identity` < `subjects` <
  `builder` < `materials` < `invocation` < `compliance`, per contracts/attestation-json.md) verified by raw-text
  `IndexOf`; `subjects` sorted by name (empty ⇒ `[]`); `materials.artifactDigests` rendered **sorted** (set);
  `invocation.runs` in carried order, each `{ kind, identity, exitCode, durationNanos }` with `kind` the
  exhaustive token and `identity = CommandRecord.canonicalId run.Record`; `compliance:
  compatible-shape-not-formal-compliance` + the human note; `identity` = the F33 `canonicalId` verbatim; no
  clock/host-path/username leak (FR-007, FR-008, FR-015, SC-005).
- [X] T039 [P] [US3] `tests/FS.GG.Governance.AttestationJson.Tests/DeterminismTests.fs` — `ofAttestation` is
  byte-identical for identical input; a duration-only change leaves every `identity` field unchanged (only
  `durationNanos` differs); reordering the runs/digests inputs cannot change the text where order is
  non-significant; the `compliance` marker is **always** present — never omitted, never an overclaim (FR-010,
  FR-011, SC-005).

### Implementation for User Story 3

- [X] T040 [US3] `src/FS.GG.Governance.Attestation/Attestation.fs` — replace the stub `summarize`: build
  `Subjects` from the `Packed` outcomes only (sorted by name, no fabricated subject for a failed build),
  `Materials`/`Invocation` from the snapshot's `Provenance`/`Runs`, `Identity = Provenance.canonicalId`,
  `Compliance = CompatibleShapeNotFormalCompliance`. Pure/total/no-I/O. Makes T036/T037 pass. Depends on T014.
- [X] T041 [US3] `src/FS.GG.Governance.AttestationJson/AttestationJson.fs` — replace the stub `ofAttestation`: a
  hand-driven `Utf8JsonWriter` walk in the fixed field order; `subjects` sorted by name; `artifactDigests` sorted
  (set); `runs` in carried order with the exhaustive `kind` token and the sensed `durationNanos` clearly separate
  from identity; `identity`/per-run `identity` reused from F33/F32 verbatim (no new fingerprint); the
  `compliance` token + note always present; empty subjects/runs ⇒ `[]`. Pure/total/no-I/O. Makes T038/T039 pass.
  Depends on T017/T040.

**Checkpoint**: US1–US3 — the F25 provenance audit snapshot projects to a deterministic, duration-invariant
SLSA/in-toto-shaped attestation summary that attests only produced subjects and never overclaims compliance,
emitted to the `attestation.json` (`fsgg.attestation/v1`) sidecar.

---

## Phase 6: User Story 4 — Publish-plan, trusted-publishing posture, and template-pin evidence (Priority: P2)

**Goal**: The `PreconditionEvidence` list `Report.assemble` builds from the F54 `SensedRelease` surfaces the
declared **publish plan**, **trusted-publishing posture**, and **template pins** first-class (FR-006) — each with
its `State` (`Met`/`Unmet`/`Unrecoverable` verbatim from the sensed facts) and a self-explaining `Reason`. A
missing/unresolved publish plan, unconfigured posture, or drifted pin ⇒ `Unmet`/`Unrecoverable` ⇒ (via the
existing F53 families) a **blocked** release naming the precondition; a satisfied set passes. No new sensing, no
new family — only first-class surfacing in the report + `release.json` v2.

**Independent Test**: Publish-plan, posture, and template-pin-drift fixtures (reusing the F54 sensed snapshot):
a resolved plan ⇒ `PublishPlan` precondition `Met`; a missing plan / unconfigured posture / drifted pin ⇒ the
relevant precondition `Unmet`/`Unrecoverable` and the release blocked naming it; each precondition's state +
reason appears in `release.json` v2 (SC-004).

### Tests for User Story 4 ⚠️ (write first, must FAIL before impl)

- [X] T042 [P] [US4] `tests/FS.GG.Governance.ReleaseReport.Tests/PreconditionEvidenceTests.fs` — over **real**
  F54 `SensedRelease` fixtures (no new sensing): a resolved publish plan ⇒ a `PublishPlan` `PreconditionEvidence`
  with `State = Met`; a missing/unresolved publish plan ⇒ `Unmet`/`Unrecoverable` naming it; an unconfigured
  trusted-publishing posture ⇒ a `TrustedPublishing` precondition `Unmet`; a drifted template pin ⇒ a
  `TemplatePins` precondition `Unmet`/`Unrecoverable`; the preconditions are ordered by `releaseRuleKindOrdinal`
  and each carries a product-neutral `Reason` (FR-006, SC-004; acceptance 4.1–4.3).
- [X] T043 [P] [US4] `tests/FS.GG.Governance.ReleaseReport.Tests/PreconditionBlocksTests.fs` — composing the
  precondition evidence with **real** `ReleaseRules.evaluateRelease`: an `Unmet` publish-plan/posture/pin ⇒ a
  **blocked** `ReleaseDecision` (the report's `ReleaseExitCodeBasis` distinct from a pass) naming the precondition;
  a fully-satisfied set ⇒ a clean decision — never assumed satisfied (FR-006, SC-004; acceptance 4.2).
- [X] T044 [P] [US4] `tests/FS.GG.Governance.ReleaseJson.Tests/PreconditionsRenderTests.fs` — `ofReleaseReport`
  renders each publish-plan / posture / template-pin precondition's `factState` + `reason` through the **existing
  v1 `rules` array** (one entry per `ReleaseRuleKind`, ordinal-ordered — there is **no** separate `preconditions`
  field, per contracts/release-json-v2.md); an `Unmet`/`Unrecoverable` family is visibly distinct from a `Met`
  one; the array is always present and unchanged in shape from v1 (FR-006, FR-015, SC-004).

> No production change in this phase beyond US2's `Report.assemble`/`ofReleaseReport`: the precondition
> projection (T033) and the v2 rendering (T034) already build/render the publish-plan/posture/pin evidence from
> the F54 sensed facts; US4 asserts the first-class surfacing across the publish-plan, posture, and pin-drift
> fixtures. If a fixture reveals a missing precondition family in `assemble`, extend T033's projection here and
> note the dependency on this line.

**Checkpoint**: US1–US4 — the publish-plan, trusted-publishing-posture, and template-pin preconditions each
surface their satisfied/unmet state + reason first-class in the report and `release.json` v2, blocking release on
any unmet precondition (via the existing F53 families, no new family).

---

## Phase 7: User Story 5 — Scheduled exhaustive validation hooks for broad matrices (Priority: P3)

**Goal**: `Matrix.decideMatrix budget boundary declared` is a pure decision reusing the F25 `CostBudget` ordered
ceiling verbatim: `None` ⇒ `NotDeclared` (never an invented matrix); `Some m` ⇒ `RunNow m` iff the boundary's F25
ceiling admits `m.Cost` (i.e. `ScheduledOrRelease`), else `Deferred (DeferredToScheduledBoundary (m.Name,
m.Cost))`. An inner-loop run records the declared matrix deferred and never runs it; an undeclared matrix is
never invented (FR-009, SC-006).

**Independent Test**: A declared `Exhaustive` matrix + `InnerLoop` ⇒ `Deferred (DeferredToScheduledBoundary …)`;
the same matrix + `ScheduledOrRelease` ⇒ `RunNow`; `None` declared ⇒ `NotDeclared` at every boundary; the
decision reuses the F25 ceiling (a future `Cost` tier is folded by the existing comparator, no arbitrary
weights) (SC-006).

### Tests for User Story 5 ⚠️ (write first, must FAIL before impl)

- [X] T045 [P] [US5] `tests/FS.GG.Governance.ValidationMatrix.Tests/DecideMatrixTests.fs` — drive `decideMatrix`
  with **real** `CostBudget` values: a declared `Exhaustive` matrix + `InnerLoop` (a budget whose ceiling is
  below `Exhaustive`) ⇒ `Deferred (DeferredToScheduledBoundary (name, Exhaustive))`; the same matrix +
  `ScheduledOrRelease` (a budget admitting `Exhaustive`) ⇒ `RunNow m`; a lower-cost declared matrix that the
  inner-loop ceiling admits ⇒ `RunNow` even at `InnerLoop` (the ceiling is the gate, not the boundary label);
  `None` ⇒ `NotDeclared` at both boundaries — never invented (FR-009, SC-006; acceptance 5.1–5.3).
- [X] T046 [P] [US5] `tests/FS.GG.Governance.ValidationMatrix.Tests/DeterminismTests.fs` — `decideMatrix` is
  total and byte-identical for identical inputs; the `DeferReason` names the matrix + cost deterministically with
  no clock/path/env; the decision reuses the F25 ordered `Cost` ceiling verbatim (a `Cost` ordering change is a
  compile-time concern, not re-encoded here) (FR-009, FR-010, SC-006).

### Implementation for User Story 5

- [X] T047 [US5] `src/FS.GG.Governance.ValidationMatrix/Matrix.fs` — replace the stub `decideMatrix`: `None` ⇒
  `NotDeclared`; `Some m` ⇒ `RunNow m` iff the budget's F25 ceiling admits `m.Cost` (reuse the `CostBudget`
  comparator — `fits`/the ordered `Cost`), else `Deferred (DeferredToScheduledBoundary (m.Name, m.Cost))`. Pure,
  total, exhaustive matches. Makes T045/T046 pass. Depends on T016.

**Checkpoint**: All five stories — a declared exhaustive matrix runs only when the boundary's F25 cost ceiling
admits it, is otherwise deferred with a named reason, and is never invented; the inner loop stays fast.

---

## Phase 8: Integration — host edge: `fsgg release` packs every project, builds the report/attestation, writes the sidecar + v2; `fsgg verify` previews (FR-014, SC-002, D3)

**Purpose**: Wire the pure cores into the existing `fsgg release` / `fsgg verify` hosts additively. `fsgg release`
packs every packable project through the F51 `ExecutionPort` (recording each as a `Pack` `KindedCommandRun`,
never dropping a failure), reads each pack output, builds the `PackEvidenceSet`, merges `factContributions` over
the F54 sensed facts, runs `Release.evaluateRelease` unchanged, builds the `AuditSnapshot` + `AttestationSummary`
+ `ReleaseReport`, projects `release.json` (v2) + writes the `attestation.json` sidecar; `decideMatrix` records
the declared matrix run/deferred. `fsgg verify` builds the same evidence advisory and emits the
`releaseReadiness` preview — never the blocking gate. Every existing `route.json`/`ship.json` golden stays
byte-identical; the release/verify exit-code schemes are unchanged. MVU discipline: the cores in `update`; the
pack runs + sidecar write are `Effect`s at the `Interpreter` edge through the existing `ExecutionPort`/
`ArtifactWriter` (Constitution IV). Depends on US1–US5.

### Tests ⚠️ (write first, must FAIL before impl)

- [ ] T048 [P] `tests/FS.GG.Governance.ReleaseCommand.Tests/PackBoundaryE2ETests.fs` — real-filesystem `fsgg
  release` (standalone, no monorepo): a product whose every packable project packs at a bumped version ⇒ the
  pack/version preconditions are `Met` and the release exits `0`; one project's pack exits non-zero ⇒ release
  **blocked** (exit `1`), the reason names the project + pack failure, and the failed `Pack` run is in the
  snapshot with its sentinel; one project packs at an unbumped/downgraded version ⇒ release **blocked** naming
  the project + version; `release.json` is `fsgg.release/v2` and `attestation.json` (`fsgg.attestation/v1`) is
  written; both are byte-identical on a re-run with unchanged inputs (pack duration retained only as sensed
  `durationNanos`); every existing `route.json`/`ship.json` golden stays **byte-identical** (FR-001, FR-002,
  FR-014, FR-015, SC-001, SC-002; quickstart Scenarios 1–2).
- [ ] T049 [P] `tests/FS.GG.Governance.ReleaseCommand.Tests/MergeableNotReleasableE2ETests.fs` — a product that
  passes `fsgg ship` (mergeable) but is not releasable (unbumped version / missing publish plan / drifted pin) ⇒
  `fsgg ship` exits `0` while `fsgg release` exits `1` with a release exit-code basis **distinct** from ship,
  the `release.json` report carrying the failing precondition; a fully-releasable product ⇒ `fsgg release` exits
  `0` with `ReleaseExitCodeBasis = Clean`; the two verdicts are reported independently, neither masking the other
  (FR-004, SC-002; quickstart Scenario 2, Scenario 4).
- [ ] T050 [P] `tests/FS.GG.Governance.VerifyCommand.Tests/ReleaseReadinessPreviewE2ETests.fs` — real-filesystem
  `fsgg verify` on a pre-PR scope ⇒ `verify.json` carries a `releaseReadiness` block (`advisory: true`) with the
  same evidence the release boundary would; an unreleasable-but-mergeable product still exits per the **unchanged**
  F56 verify scheme (the preview never changes verify's exit code); a declared exhaustive matrix at the inner-loop
  boundary is recorded **deferred** and does not run; the existing `verify.json` golden fields are unchanged
  (FR-005, FR-009, SC-003, SC-006; quickstart Scenarios 3, 6).
- [ ] T051 [P] `tests/FS.GG.Governance.ReleaseCommand.Tests/SafeFailureE2ETests.fs` — a product with **no**
  packable projects ⇒ `NoPackableProjects = true`, the pack precondition vacuously satisfied and the report
  states "no packable projects" — never a fabricated pack; an unreadable pack output / absent provenance input /
  missing publish plan ⇒ a clear input signal, the release **blocks**, no hollow attestation, no fabricated pass
  — distinguished from a tool defect (input-unavailable vs tool-error host exit codes); presenting the packable
  projects / command runs in a different order ⇒ byte-identical evidence, verdict, attestation, and report
  (FR-011, SC-007, SC-008; quickstart Scenario 7, edge cases).

### Implementation

- [X] T052 (landed by 065) `src/FS.GG.Governance.ReleaseCommand/Declaration.fs(i)` — additive: parse `.fsgg/release.yml`'s
  `PackableProjects: (SurfaceId * GateCommand * string option) list` (per project: surface id, the F51 pack
  `GateCommand`, the released version baseline — `None` ⇒ first release) and the optional `Matrix:
  ExhaustiveMatrix option`; the F53/F54 declaration fields unchanged (additive only). Add the
  `PackEvidence`/`Attestation`/`ReleaseReport`/`ValidationMatrix`/`AttestationJson`/`ReleaseJson` project
  references to `FS.GG.Governance.ReleaseCommand.fsproj`. (contracts/release-yml-packable.md.) Depends on
  T027/T040/T041/T047/T033.
- [X] T053 (landed by 065) `src/FS.GG.Governance.ReleaseCommand/Loop.fs` (+ `Loop.fsi` for the `Model`/`Msg`/`Effect` surface) —
  additive `Effect.PackProjects of (SurfaceId * GateCommand) list`; additive `Model` fields (`PackEvidence:
  PackEvidenceSet option`, `Snapshot: AuditSnapshot option`, `Attestation: AttestationSummary option`, `Report:
  ReleaseReport option`, `AttestationDoc: string option`). In `update`: from the recorded pack outcomes build the
  `PackEvidenceSet` (`Pack.evaluatePack`), merge `Pack.factContributions` over the F54 sensed facts (packed wins
  on `VersionBump`/`PackageMetadata`/`Provenance`), call `Release.evaluateRelease` unchanged, build the
  `AuditSnapshot` (`Audit.auditSnapshot` over the recorded runs), the `AttestationSummary`
  (`Attestation.summarize`), the `ReleaseReport` (`Report.assemble`), the `attestation.json`
  (`AttestationJson.ofAttestation`) and `release.json` v2 (`ReleaseJson.ofReleaseReport`), and decide the matrix
  (`Matrix.decideMatrix`). Pure `update`; no I/O here. Depends on T052.
- [X] T054 (landed by 065) `src/FS.GG.Governance.ReleaseCommand/Interpreter.fs(i)` — at the edge: run each pack via the F51
  `GateExecution.ExecutionPort` (sentinel exit on failure, never dropped), read each output's artifact path +
  packed version + digest into a `PackOutcome`, and write the `attestation.json` sidecar through the existing
  `ArtifactWriter` (temp+rename); the extended `release.json` v2 is written by the existing release `ArtifactWriter`
  path. Empty inputs ⇒ the sidecar/JSON stay well-formed; existing goldens untouched; the F55 exit-code scheme
  unchanged. Makes T048/T049/T051 pass. Depends on T053.
- [X] T055 (landed by 065) `src/FS.GG.Governance.VerifyCommand/Loop.fs(i)` — additive `Model.ReleasePreview:
  VerifyReleasePreview option` (never affects `Exit`). In `update` at the verify boundary: build the same
  evidence advisory (pack evidence + sensed preconditions + attestation summary), assemble a `ReleaseReport`, and
  `Report.preview` it; record `Matrix.decideMatrix` (deferred at the inner loop). Project the additive
  `releaseReadiness` block via the `VerifyJson` preview projection. Add the core/projection project references to
  `FS.GG.Governance.VerifyCommand.fsproj`. The F56 exit scheme + the `VerifyCommand` `Interpreter` are otherwise
  reused unchanged (the preview adds no blocking gate). Makes T050 pass. Depends on T033/T035/T047/T054.

**Checkpoint**: `fsgg release` packs every project, grounds the existing release rules in real pack evidence,
blocks distinctly from ship, and emits `release.json` v2 + the `attestation.json` sidecar deterministically;
`fsgg verify` previews the same evidence advisorily; the declared exhaustive matrix is deferred in the inner
loop; every existing golden byte-identical, standalone preserved.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Bless the new/changed surface baselines, document the new/changed JSON contracts, run the
determinism/standalone/reuse guards, update docs, and run the quickstart validation.

- [X] T056 Bless and commit the five new surface baselines (`BLESS_SURFACE=1 dotnet test …`), then re-run drift
  green: `surface/FS.GG.Governance.PackEvidence.surface.txt`, `…Attestation.surface.txt`,
  `…ReleaseReport.surface.txt`, `…ValidationMatrix.surface.txt`, `…AttestationJson.surface.txt`; re-bless
  `…ReleaseJson.surface.txt` / `…VerifyJson.surface.txt` (changed only by the added projection `val`s — additive,
  no existing signature altered) and `…ReleaseCommand.surface.txt` / `…VerifyCommand.surface.txt` (changed only
  by the additive `Model`/`Loop` surface — T053/T055).
- [ ] T057 [P] Commit/refresh the deterministic goldens under the projection test projects' `golden/` dirs: an
  `attestation.json` golden (packed subjects + a sentinel-exit run, the not-formal-compliance marker), a
  `release.json` v2 golden (a mixed package-evidence report), and a `verify.json` golden carrying the advisory
  `releaseReadiness` block; **re-bless the existing `release.json` golden** in `ReleaseJson.Tests` for the
  retained `ofRelease` path (v1→v2: `schemaVersion` bumped + the three empty additive fields appended — the only
  existing release golden that changes); the `verify.json` golden for a run with **no** release declaration stays
  byte-identical (no schema bump). Plus the pre-F26 `route.json`/`ship.json` byte-identity anchors used by the
  T048/T049 untouched-golden assertions (reuse the existing host goldens).
- [X] T058 [P] `tests/FS.GG.Governance.PackEvidence.Tests/ReuseGuardTests.fs` — the no-new-family guard: assert
  this row adds **no** new `ReleaseRuleKind` case, **no** change to `evaluateRelease`/the F53 partition, **no**
  change to F54 sensing, and **no** change to the verify/release exit-code scheme — `factContributions` feeds the
  **existing** families and `assemble` carries the F53 `ReleaseDecision` verbatim (a future `ReleaseRuleKind`
  case is a compile error via the exhaustive match) (FR-003, FR-005, D5).
- [X] T059 [P] `tests/FS.GG.Governance.Attestation.Tests/IdentityReuseGuardTests.fs` — assert the attestation
  `Identity` computes **no** new fingerprint (it equals `Provenance.canonicalId` verbatim) and the `CommandKind`
  never participates in any identity (a kind-only / duration-only change leaves `Identity` equal) — the
  descriptive-metadata + no-overclaim guarantee (FR-007, FR-008, D5).
- [X] T060 [P] Document the three JSON contracts already drafted under `contracts/` and reconcile them with the
  shipped projections: `contracts/attestation-json.md` (`fsgg.attestation/v1`), `contracts/release-json-v2.md`
  (the three additive `packageEvidence`/`versionPolicy`/`attestation` fields + the v1→v2 migration, preconditions
  surfacing through the existing v1 `rules` array),
  `contracts/verify-json-preview.md` (the additive `releaseReadiness` block). Confirm each matches the
  byte-for-byte field order the projection tests assert (T031/T032/T038).
- [X] T061 [P] Update `CLAUDE.md` and the roadmap row: F26 `061-verify-release-provenance` complete — the
  enforced pack-and-version-bump evidence (`PackEvidence`, packed version is the source of truth), the immutable
  single-source-of-truth `ReleaseReport` + boundary distinct from ship + `release.json` v2 + the advisory
  `fsgg verify` release-readiness preview, the SLSA/in-toto-shaped `Attestation` summary + `attestation.json`
  (`fsgg.attestation/v1`) without overclaiming, the publish-plan/posture/template-pin preconditions surfaced
  first-class, and the scheduled-exhaustive-matrix decision (`ValidationMatrix`, reusing the F25 cost ceiling);
  note the F53/F54/F55/F56/F25/F33/F51 reuse (no new release-rule family, no exit-code-scheme change, no identity
  change, no new dependency).
- [ ] T062 Run the `quickstart.md` validation end to end (all seven scenarios + the constitution-gate checks):
  `dotnet build FS.GG.Governance.sln` clean (warnings-as-errors); the five new test projects + the two extended
  host test projects + the whole solution green (no regression); the five new surface baselines + the four
  re-blessed baselines match; the version-bump matrix, the real-`ExecutionPort` pack fixture (incl. failed-pack
  sentinel), the mergeable-but-not-releasable + fully-releasable fixtures, the verify preview fixture, the
  publish-plan/posture/pin-drift fixtures, the attestation snapshot + stability + failed-build-no-subject + marker
  fixtures, the scheduled-matrix fixture, and the determinism/reorder + safe-failure tests all pass; a real-host
  `fsgg release` smoke run shows `release.json` v2 + `attestation.json` written deterministically with every
  existing `route.json`/`ship.json` golden byte-identical. Record the evidence on this line.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → no deps; T001–T005/T007 parallel (T002 needs T001, T003 needs T001/T002, T005 needs T002
  to reference; T006 needs T003), T008 after all.
- **Foundational (Phase 2)** → after Setup. T009 first (`PackEvidence.Model`); T010 after T009; T011 after
  T009/T010; T012 independent; T013 after T009; T014 after T010; T015 after T011; T016 after T012; T017 after
  T010; T018/T019 after T011; T020 after T009–T019; T021/T022 after T020. **Blocks all stories.**
- **US1 (Phase 3)** → after Foundational. MVP. (`Pack.fs` real body.)
- **US2 (Phase 4)** → after US1 (the report carries the pack evidence; `ofReleaseReport` renders it) +
  Foundational (`Attestation.Model` type only — US2 builds an `AttestationSummary` value directly, not US3's body).
- **US3 (Phase 5)** → after Foundational; **independent of US1/US2** (different libraries — `Attestation` +
  `AttestationJson`), though `summarize` consumes a `PackEvidenceSet` value (the type, from Foundational).
- **US4 (Phase 6)** → after US2 (the precondition projection + v2 rendering land in `Report.assemble`/
  `ofReleaseReport`; US4 asserts the publish-plan/posture/pin surfacing).
- **US5 (Phase 7)** → after Foundational; **fully independent** (`ValidationMatrix` shares no file with the rest).
- **Integration (Phase 8)** → after all four cores + both projections (US1–US5); the hosts pack, build the
  report/attestation, write the sidecar + v2, and preview.
- **Polish (Phase 9)** → after the desired stories + Integration; T056/T057/T060/T062 need the `.fs` bodies +
  host wiring stable; T058/T059 (reuse guards) need the real `factContributions`/`assemble`/`summarize`.

### Within each story

- Tests first and FAILING, then implementation (Constitution V).
- For every new module, `.fsi` + compiling stub (Phase 2) before the real `.fs`; `Model` (Phase 2) before
  `Pack`/`Report`/`Attestation`/`Matrix`; the cores before the projections before the host wiring.

### Parallel opportunities

- Phase 1: T001–T005/T007 together (T008 after).
- Phase 2: T009→T010→T011 in sequence (project-ref chain); T012/T017/T018/T019 in parallel after their model dep;
  T013/T014/T015/T016 in parallel after their model dep; T021/T022 in parallel after T020.
- **US3 (Phase 5) and US5 (Phase 7) are independent of US1/US2/US4** (`Attestation`/`AttestationJson` and
  `ValidationMatrix` share no file with `PackEvidence`/`ReleaseReport`) and can be staffed in parallel from the
  end of Foundational.
- Each story's `[P]` test tasks run together; Phase 8 tests T048–T051 in parallel; Phase 9 T057/T058/T059/T060/
  T061 are independent `[P]` tasks.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — every type + stubs compile) → 3. Phase 3 US1 → **STOP &
   VALIDATE** (SC-001/SC-008: the version-bump matrix + real-`ExecutionPort` pack fixture; an unbumped/downgraded/
   failed/no-artifact pack contributes `Unmet` and blocks via the existing F53 families with a named reason; the
   evidence set is deterministic and reorder-invariant). The packed evidence grounds the release rules with no
   report, attestation, matrix, or host wiring yet.

### Incremental delivery

Setup + Foundational → US1 (packed evidence + version policy, MVP) → US2 (immutable report + boundary +
`release.json` v2 + verify preview) → US3 (attestation summary + `attestation.json`, independent) → US4
(publish-plan/posture/pin precondition surfacing) → US5 (scheduled matrix, independent) → Integration (host edge:
pack every project, build the report/attestation/sidecar, preview) → Polish. Each slice adds value without
breaking the prior; the `attestation.json` is a new artifact and the release/verify additions are explicitly
versioned, so every existing `route.json`/`ship.json` golden stays byte-identical.

### Parallel team strategy

After Foundational, Developer A takes US1→US2→US4 (the `PackEvidence` core + `ReleaseReport` + the
`ReleaseJson`/`VerifyJson` projections), Developer B takes US3 (`Attestation` + `AttestationJson`), Developer C
takes US5 (`ValidationMatrix`). They converge on Integration (Phase 8) once the cores land; Polish follows.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- **Reuse, don't reinvent** (D5): `factContributions` feeds the **existing** F53 `VersionBump`/`PackageMetadata`/
  `Provenance` families (no new family); `assemble` carries the F53 `ReleaseDecision` + `ExitCodeBasis` verbatim
  (never re-derives the verdict); `summarize`'s `Identity` reuses `Provenance.canonicalId` verbatim (the
  `CommandKind` is descriptive, never in identity); `decideMatrix` reuses the F25 `CostBudget` ordered ceiling;
  the host edge reuses the existing F51 `GateExecution.ExecutionPort` (pack runs) and `ArtifactWriter` (the
  sidecar + v2). Upstream cores (`ReleaseRules`/`ReleaseFactsSensing`/`CommandKind`/`Provenance`) are never
  mocked in semantic tests (Constitution V).
- **Packed version is the source of truth** (D1): `versionPolicy` compares the **packed** artifact's version, so
  a source bump that never reaches the artifact still blocks; `factContributions` is merged over the F54 sensed
  facts with packed evidence winning on the three families.
- **No new family / no exit-code-scheme change / no identity change** (FR-003, FR-005, D5): no new
  `ReleaseRuleKind`; no change to `evaluateRelease`/the F53 partition/F54 sensing/the verify/release exit codes;
  no change to `CommandRecord`/`Provenance`/`AuditSnapshot` identity; the only new wire contracts are the new
  `attestation.json` sidecar (`fsgg.attestation/v1`) and the additive `release.json` v2 / `verify.json` fields.
- **Determinism is mandatory** (FR-010, SC-005, SC-007): `evaluatePack` sorts `Verdicts` by `(SurfaceId,
  ArtifactPath)`; `assemble` orders `Preconditions` by `releaseRuleKindOrdinal`; `summarize` sorts `Subjects` by
  name + treats `ArtifactDigests` as a set; all three projections emit a fixed field order; no clock/abs-path/
  username/environment in any verdict, evidence, attestation, report, or JSON; pack duration is sensed metadata
  only and never affects identity. The per-core determinism tests (T030/T037/T039/T046) + the reorder tests
  enforce it.
- **Elmish/MVU applicability**: `Pack.evaluatePack`/`versionPolicy`/`factContributions`, `Report.assemble`/
  `preview`, `Attestation.summarize`, `Matrix.decideMatrix`, and all three projections are **pure, total leaves**
  — no MVU ceremony (the F41/F46/F53 precedent). The behavioral change (packing every packable project, recording
  `Pack` runs, reading outputs, building the snapshot + attestation + report, writing the sidecar + v2) is in the
  **existing** `ReleaseCommand`/`VerifyCommand` boundary: the cores in `update`; the pack runs + sidecar write are
  `Effect`s at the `Interpreter` edge (T053/T054/T055). Pure transitions are exercised directly
  (T023–T030/T036–T039/T042–T046); real-port/real-fs evidence is T026/T048–T051.
- **Safe failure** (FR-011, Constitution VI): no packable project ⇒ vacuously satisfied + reported; an unreadable
  pack output / absent provenance input / missing publish plan ⇒ a clear input signal distinguished from a tool
  defect, blocking release — never a fabricated pack, a hollow attestation (no subject for a failed build), or a
  fabricated pass (T051); a blocked release is reported as such, never as a pass.
- Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow scope and document on the
  task line.
