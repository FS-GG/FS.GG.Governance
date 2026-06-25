# Phase 1 Data Model: Verify & Release Publication Boundary (F26)

All types are **pure, product-neutral values** — no raw YAML, host-absolute path, clock reading, process exit
code, or product vocabulary beyond caller-supplied ids. Every type reuses the existing vocabulary verbatim
(opened, never redefined): `Config.Model` (`SurfaceId`, `Cost`, `EnvironmentClass`), `Enforcement`
(`Severity`, `Maturity`), `Ship.Model` (`Verdict`, `ExitCodeBasis`), `ReleaseRules.Model` (`ReleaseRuleKind`,
`FactState`, `ReleaseFacts`, `ReleaseDecision`), `ReleaseFactsSensing.Model` (`SensedRelease`, `ReleaseSnapshot`),
`CommandKind.Model` (`CommandKind`, `KindedCommandRun`, `AuditSnapshot`), `Provenance.Model`
(`Provenance`, `BuilderIdentity`), `FreshnessKey.Model` (`Revision`, `RuleHash`, `GeneratorVersion`,
`ArtifactHash`), `CostBudget` (the ordered `Cost` ceiling).

Section order mirrors the priority slices: PackEvidence (P1) → ReleaseReport (P1) → Attestation (P2) →
ValidationMatrix (P3).

---

## 1. `FS.GG.Governance.PackEvidence` (P1) — packed evidence + version policy

### `PackArtifact`
The real output of packing one project, read at the host edge.
- `Surface: SurfaceId` — the project's governed surface id (caller-supplied; product-neutral).
- `ArtifactPath: string` — the **normalized** (repo-relative, forward-slash) packed-artifact path.
- `PackedVersion: string` — the version read from the packed artifact (the source of truth, D1).
- `Digest: ArtifactHash` — the artifact content digest (reused F029 hash type).

### `NoArtifactReason`
Closed reason a zero-exit pack produced nothing usable.
- `NoArtifactEmitted` — pack exited zero, no artifact found.
- `ArtifactUnreadable of string` — artifact present but its version/digest could not be read (input signal).

### `PackOutcome`
The closed result of attempting to pack one project. Carries the recorded `Pack` run either way (never dropped).
- `Packed of artifact: PackArtifact * run: KindedCommandRun` — succeeded; `run.Kind = Pack`.
- `PackedNoArtifact of surface: SurfaceId * reason: NoArtifactReason * run: KindedCommandRun` — zero exit, no
  usable artifact.
- `PackFailed of surface: SurfaceId * sentinel: int * run: KindedCommandRun` — non-zero/sentinel pack exit; the
  failed run is recorded with its sentinel exit code.

### `VersionVerdict`
The closed per-project version-policy outcome (evaluated against the **packed** version vs the baseline, D1).
- `Bumped of baseline: string * packed: string` — packed version strictly above baseline.
- `Unbumped of version: string` — packed version equal to baseline.
- `Downgraded of baseline: string * packed: string` — packed version below baseline.
- `NoBaseline of packed: string` — no released baseline supplied (first release; treated per expectations).
- `NotPackable` — version could not be evaluated because no artifact was produced.

### `PackVerdict`
The per-project rollup: the `Surface`, its `PackOutcome`, its `VersionVerdict`, and a self-explaining
product-neutral `Reason` (names the project, outcome, and version basis). Exactly one per packable project.

### `PackEvidenceSet`
The whole-product pack evidence — the immutable input to the report and attestation.
- `Verdicts: PackVerdict list` — one per packable project, sorted by `SurfaceId` then `ArtifactPath` (D7).
- `Runs: KindedCommandRun list` — every recorded `Pack` run (order-significant for the snapshot, D7).
- `NoPackableProjects: bool` — `true` when the product declares nothing to pack (vacuously satisfied, D6).

### Operations (`Pack` module)
- `versionPolicy: baseline: string option -> packed: string option -> VersionVerdict` — total comparison; the
  packed-version-is-truth rule (D1). Pure.
- `evaluatePack: baselines: Map<SurfaceId,string> -> outcomes: PackOutcome list -> PackEvidenceSet` — builds the
  sorted, deterministic evidence set; total (empty outcomes ⇒ `NoPackableProjects = true`). Pure.
- `factContributions: set: PackEvidenceSet -> Map<ReleaseRuleKind, FactState>` — derives the `Met`/`Unmet`
  contributions for `VersionBump` / `PackageMetadata` / `Provenance` from real pack output; merged over the F54
  sensed facts at the host edge (packed evidence wins on those three families, D1). A `Packed` + `Bumped`
  project contributes `Met`; any `PackedNoArtifact` / `PackFailed` / `Unbumped` / `Downgraded` contributes
  `Unmet` for the relevant family. Pure, total.

---

## 2. `FS.GG.Governance.ReleaseReport` (P1) — the immutable single source of truth

### `PreconditionEvidence`
One publication precondition surfaced first-class (FR-006), projected from the F54 `ReleaseSnapshot` + the F53
finding. Closed, product-neutral.
- `Kind: ReleaseRuleKind` — the family (`PublishPlan` / `TrustedPublishing` / `TemplatePins` / etc.).
- `State: FactState` — `Met` / `Unmet` / `Unrecoverable` (verbatim from the sensed facts).
- `Reason: string` — the self-explaining reason (names the family + basis).

### `ReleaseReport`
The whole publication boundary as one immutable, presentation-free value (FR-012). Carries the F53 decision
verbatim — never re-derives a verdict.
- `Decision: ReleaseDecision` — the F53 verdict + `ExitCodeBasis` + three-way partition, **verbatim**.
- `Package: PackEvidenceSet` — the pack evidence (§1).
- `Preconditions: PreconditionEvidence list` — publish-plan / posture / pins / version / metadata / provenance
  evidence, ordered by `releaseRuleKindOrdinal` (D7).
- `Attestation: AttestationSummary` — the SLSA/in-toto-shaped summary (§3).
- `ReleaseExitCodeBasis: ExitCodeBasis` — the publication exit-code basis (= `Decision.ExitCodeBasis`, named at
  the report level so the boundary reads as first-class and distinct from ship — FR-004).

### `VerifyReleasePreview`
The **advisory** projection `fsgg verify` surfaces (FR-005) — the same evidence, explicitly non-blocking.
- `Verdict: Verdict` — the previewed publication verdict (advisory).
- `Package: PackEvidenceSet`
- `Preconditions: PreconditionEvidence list`
- `Attestation: AttestationSummary`
- `Advisory: bool` — always `true`; the marker that verify is never the blocking release gate.

### Operations (`Report` module)
- `assemble: decision: ReleaseDecision -> sensed: SensedRelease -> pack: PackEvidenceSet -> attestation:
  AttestationSummary -> ReleaseReport` — pure assembly from the four already-computed inputs; preserves the F53
  partition order and the F54 ordinal ordering; total.
- `preview: report: ReleaseReport -> VerifyReleasePreview` — the advisory subset; total, drops nothing, sets
  `Advisory = true`.

---

## 3. `FS.GG.Governance.Attestation` (P2) — SLSA/in-toto-shaped projection of the F25 snapshot

### `AttestationSubject`
One attested artifact (the in-toto "subject"). From a `Packed` `PackArtifact` only — a failed/no-artifact pack
yields **no** subject (FR-008).
- `Name: string` — the normalized artifact path.
- `Digest: ArtifactHash` — the artifact content digest.
- `Version: string` — the packed version.

### `AttestationMaterials`
The in-toto "materials" (the reproducible build inputs), projected verbatim from the F33 `Provenance`.
- `RuleHash: RuleHash`
- `GeneratorVersion: GeneratorVersion`
- `BaseRevision: Revision`
- `HeadRevision: Revision`
- `SourceCommit: Revision`
- `ArtifactDigests: ArtifactHash list` — treated as a set in identity (D7).
- `Environment: EnvironmentClass`

### `AttestationInvocation`
The in-toto "invocation" — the recorded command runs (order-significant, D7).
- `Runs: KindedCommandRun list` — every recorded run from the snapshot (build/test/pack/…); duration carried
  only inside each embedded `CommandRecord`, excluded from identity (D7).

### `ComplianceMarker`
The explicit not-a-claim marker (FR-008). A closed value, never derived.
- `CompatibleShapeNotFormalCompliance` — the only case; renders to a fixed token + human note in JSON.

### `AttestationSummary`
The whole summary — the projection of the F25 `AuditSnapshot` + pack subjects.
- `Subjects: AttestationSubject list` — the packed artifacts (sorted by name; empty when nothing was produced).
- `Builder: BuilderIdentity` — who/what built it (F33 verbatim).
- `Materials: AttestationMaterials`
- `Invocation: AttestationInvocation`
- `Identity: string` — `Provenance.canonicalId snapshot.Provenance` (F33 verbatim — changes only on a
  reproducible-input change; duration never affects it).
- `Compliance: ComplianceMarker` — always `CompatibleShapeNotFormalCompliance` (never overclaims).

### Operations (`Attestation` module)
- `summarize: snapshot: AuditSnapshot -> pack: PackEvidenceSet -> AttestationSummary` — pure projection; builds
  `Subjects` from the `Packed` outcomes only (no fabricated subject for a failed build, FR-008), `Materials`/
  `Invocation` from the snapshot's `Provenance`/`Runs`, `Identity` from `Provenance.canonicalId`. Byte-identical
  for identical inputs; changes only when a reproducible input changes (SC-005). Total.

---

## 4. `FS.GG.Governance.ValidationMatrix` (P3) — declared exhaustive matrix + boundary decision

### `ExhaustiveMatrix`
A declared broad validation matrix (the declaration, product-neutral).
- `Name: string` — the matrix label.
- `Cost: Cost` — its declared cost tier (expected `Exhaustive`).
- `Dimensions: string list` — the declared axes it covers (e.g. packable projects × targets), opaque tokens.

### `MatrixBoundary`
Which run boundary is executing — closed.
- `InnerLoop` — a pre-PR / sandbox / focused run.
- `ScheduledOrRelease` — the scheduled / release boundary.

### `DeferReason`
Why a declared matrix did not run now.
- `DeferredToScheduledBoundary of name: string * cost: Cost` — named, deterministic ("deferred to the
  scheduled/release boundary").

### `MatrixPlan`
The decision — closed.
- `RunNow of ExhaustiveMatrix` — the boundary admits the matrix's `Exhaustive` cost (reuses the F25 ceiling).
- `Deferred of DeferReason` — declared but the inner-loop boundary does not admit it.
- `NotDeclared` — no matrix declared; never an invented matrix (FR-009, SC-006).

### Operations (`Matrix` module)
- `decideMatrix: budget: CostBudget -> boundary: MatrixBoundary -> declared: ExhaustiveMatrix option ->
  MatrixPlan` — pure: `None` ⇒ `NotDeclared`; `Some m` ⇒ `RunNow m` iff the boundary's F25 `CostBudget` ceiling
  admits `m.Cost` (i.e. `ScheduledOrRelease`), else `Deferred (DeferredToScheduledBoundary …)`. Total.

---

## 5. Host-edge wiring types (not new pure cores — extensions of existing modules)

### `ReleaseCommand.Declaration.ReleaseDeclaration` (additive)
Extended with the packable-project + matrix declaration (parsed from `.fsgg/release.yml`, additive only — the
F53/F54 fields are unchanged):
- `PackableProjects: (SurfaceId * GateCommand * string option) list` — per project: its surface id, the pack
  command-to-run (an F051 `GateCommand`), and its released version baseline (`None` ⇒ first release).
- `Matrix: ExhaustiveMatrix option` — the optional declared exhaustive matrix.

### `ReleaseCommand.Loop.Effect` (additive)
- `PackProjects of (SurfaceId * GateCommand) list` — run each pack through the F051 `ExecutionPort`, returning
  the recorded `KindedCommandRun`s + read artifacts (a `Packed`/`PackedNoArtifact`/`PackFailed` per project).

### `ReleaseCommand.Loop.Model` (additive)
- `PackEvidence: PackEvidenceSet option`
- `Snapshot: AuditSnapshot option`
- `Attestation: AttestationSummary option`
- `Report: ReleaseReport option`
- `AttestationDoc: string option` — the projected `attestation.json` text (computed in `update`, written by the
  interpreter).

### `VerifyCommand.Loop.Model` (additive)
- `ReleasePreview: VerifyReleasePreview option` — the advisory preview (never affects `Exit`).

---

## Validation rules summary (traceability)

| Rule | Type / op | FR | SC |
|------|-----------|----|----|
| Pack every packable project, record each `Pack` run, never drop a failed pack | `evaluatePack`, `PackOutcome` | FR-001 | SC-001 |
| Version evaluated against the **packed** artifact; unbumped/downgraded blocks | `versionPolicy`, `VersionVerdict` | FR-002 | SC-001 |
| Pack evidence feeds the **existing** F53 families (no new family) | `factContributions` → `evaluateRelease` | FR-003 | SC-001 |
| Release is a first-class blocking boundary distinct from ship | `ReleaseReport.ReleaseExitCodeBasis` (= F53 basis) | FR-004 | SC-002 |
| `fsgg verify` advisory release-readiness preview, never the gate | `preview`, `VerifyReleasePreview.Advisory` | FR-005 | SC-003 |
| Publish-plan / posture / pins surfaced first-class | `PreconditionEvidence` | FR-006 | SC-004 |
| SLSA/in-toto-shaped attestation, deterministic | `summarize`, `AttestationSummary` | FR-007 | SC-005 |
| Never overclaims; no subject for a failed build | `ComplianceMarker`, `summarize` | FR-008 | SC-005 |
| Declared exhaustive matrix deferred/run by boundary; never invented | `decideMatrix`, `MatrixPlan` | FR-009 | SC-006 |
| Deterministic, byte-identical, duration excluded | all (D7) | FR-010 | SC-005/007 |
| Input vs tool defect; no fabricated pack/attestation/pass | `NoArtifactReason`, `NoPackableProjects`, edge diagnostics | FR-011 | SC-008 |
| Immutable, presentation-free report objects | `ReleaseReport`, `VerifyReleasePreview` | FR-012 | SC-007 |
| Standalone, no monorepo/network in cores | all cores pure over sensed inputs | FR-013 | — |
| Pure total functions; I/O only at the host edge | all `Pack`/`Report`/`Attestation`/`Matrix` ops | FR-014 | — |
| Additive `schemaVersion`-headed JSON; existing goldens untouched | `fsgg.attestation/v1`, `release/v2`, verify additive | FR-015 | SC-007 |
