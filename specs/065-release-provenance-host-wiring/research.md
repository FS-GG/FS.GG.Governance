# Phase 0 Research — Release-Provenance Host Wiring (F26 wiring)

This row wires the seven already-built F26 surfaces into the two mature hosts `fsgg release`
(`FS.GG.Governance.ReleaseCommand`) and `fsgg verify` (`FS.GG.Governance.VerifyCommand`). All decisions below are
grounded in a direct reconnaissance of the two hosts' MVU `Loop`/`Interpreter` bodies, their `.fsproj`
ProjectReferences, and the F26 cores' `.fsi` signatures. The governing tension — packing-and-blocking for real
while every existing golden stays byte-identical — is resolved in **D1**; the rest follow from it.

## Reconnaissance facts (the ground truth the decisions rest on)

- **`ReleaseCommand` is a linear MVU with no execution port.** `init` emits `LoadDeclaration`; on
  `DeclarationLoaded(Ok decl)` it emits `SenseRelease`; on `Sensed sensed` it calls `Release.evaluateRelease
  decl.Rules sensed.Facts` **inside `update`**, maps `ExitCodeBasis` → `ExitDecision`, projects `release.json`
  (`ReleaseJson.ofRelease`) when JSON is requested, emits `WriteArtifact` then `EmitSummary`. Ports:
  `{ Files: Loader.FileReader; Sense: SourceLayout -> ReleaseExpectations -> SensedRelease; Write: ArtifactWriter;
  Out: OutputSink }`. The `.fsproj` references **only** `ReleaseRules`, `ReleaseFactsSensing`, `Config`,
  `ReleaseJson` — **no `GateExecution`**, no `ExecuteGates`/`PackProjects` effect, no `Execute` port.
- **`VerifyCommand` already carries the heavy machinery.** Its `.fsproj` references `GateExecution`,
  `CommandKind`, `Provenance`, `CostBudget`, `Snapshot`; its `Effect` already has `ExecuteGates of (GateId *
  GateCommand) list` and `SenseProvenance`; its `Ports` already has `Execute: ExecutionPort`, `SenseEnvironment`,
  `SenseBuilder`; its `Model` already has `Environment`, `Builder`, `CacheDecision`, and **`Audit: AuditSnapshot
  option`** (built in the 064 wiring). It does **not** sense release facts (F54) or evaluate release rules (F53).
- **The F26 contracts force a `ReleaseDecision` into the verify path.** `Report.preview : ReleaseReport ->
  VerifyReleasePreview` and `Report.assemble : ReleaseDecision -> SensedRelease -> PackEvidenceSet ->
  AttestationSummary -> ReleaseReport`. There is no lighter F26 entry that produces a `VerifyReleasePreview`
  without a `ReleaseDecision` + `SensedRelease`. So a verify preview must sense F54 and evaluate F53.
- **The shared port shapes are already proven.** `GateExecution.Model.ExecutionPort = GateCommand ->
  ExecutionOutcome`; `GateExecution.Interpreter.senseExecution : ExecutionPort -> GateCommand -> CommandRecord`;
  the atomic `ArtifactWriter = string -> string -> Result<unit,string>` (temp + rename) is identical in both
  hosts. `Audit.auditSnapshot` takes `sourceCommit/baseRevision/headRevision/ruleHash/generatorVersion/
  artifactDigests/runs/environment/builder`. `KindedCommandRun = { Kind: CommandKind; Record: CommandRecord }`.

---

## D1 — Pack-and-block for real, every existing golden byte-identical

**Decision.** `fsgg release` actually packs every declared packable project through the F51 `ExecutionPort` and
blocks on a failed/unbumped pack (FR-001/FR-002), while the release *evaluation* path is unchanged:
`Pack.factContributions` merges over the F54 sensed facts and `Release.evaluateRelease` is called **verbatim**, and
the `ReleaseDecision`/`ExitCodeBasis` are carried into the `ReleaseReport` without re-derivation (FR-003). The new
`release.json` v2 fields (`packageEvidence`/`versionPolicy`/`attestation`) are **appended** by
`ReleaseJson.ofReleaseReport`; `route.json`/`ship.json` belong to hosts this row does not touch; the `verify.json`
`releaseReadiness` block is emitted **only** when a release declaration is present. Byte-identity is then proven by
golden comparison against frozen pre-wiring baselines for: `route.json`/`ship.json` (untouched hosts), a
no-declaration `verify.json` (no block, no schema bump), and the **one** existing `release.json` golden re-blessed
v1→v2 (`schemaVersion` bumped + the three empty additive fields appended — the F26 `release.json` v2 golden already
re-blessed in F26 Phase 9 T056/T057).

**Rationale.** The reconciliation rests on three facts the reconnaissance pins down: (a) the verdict basis is
unchanged because `evaluateRelease` is verbatim and `factContributions` only *grounds* the three pack-related
families in real evidence (a product already `Met` whose packs succeed at a bumped version yields the same
decision); (b) the two truly-frozen goldens (`route.json`/`ship.json`) are produced by other hosts; (c) the
additive verify block and the v2 additive fields are byte-identical-when-empty/absent by the F26 projections'
construction (`ofVerifyDecisionWithPreview` writes nothing for a `None` preview; `ofReleaseReport` appends fixed
empty arrays/identity refs). The release host's `release.json` *does* change v1→v2 — that is a deliberate,
explicitly-versioned bump, the single existing release golden that moves, already blessed in F26.

**Alternatives rejected.** (i) Re-bless every golden silently — hides whether the wiring is truly additive;
violates SC-005. (ii) Make the v2 fields conditional on non-emptiness to keep v1 byte-identical without a bump —
rejected because F26 already chose the explicit v1→v2 bump and re-blessed the golden; re-litigating it here would
fork the contract.

## D2 — Provenance inputs for the release attestation: normalized edge senses + real packed digests

**Decision.** `Audit.auditSnapshot`'s inputs in the release host are sourced as: **runs** = the `Pack`
`KindedCommandRun`s from D1; **artifactDigests** = the real `PackArtifact.Digest`s from the `PackEvidenceSet`
(`Packed` outcomes only); **head/base/sourceCommit** = a single head revision sensed through the **existing** F016
`Snapshot` port, with `base = head = sourceCommit` (the release boundary attests a *product state*, not a diff
range); **ruleHash** = derived from the declared release rules; **generatorVersion** = the normalized `fsgg`
constant; **environment** = the `CI`/`Local` sense; **builder** = the normalized `BuilderIdentity "fsgg"` (no
username/host/clock). These reuse the exact normalized senses 064 added to verify.

**Rationale.** Every input is either a real, deterministic build artifact (the packed digests, the recorded pack
runs) or a normalized value with no machine/clock/username leakage — guaranteeing `attestation.json` is
byte-identical across machines and re-runs (FR-007, FR-011, SC-003). `base = head = sourceCommit` is the honest
choice for a release boundary: unlike `verify`/`ship` (which attest a *change range*), `fsgg release` attests the
*built product* at the current commit, so there is no second revision to sense, and the choice keeps the host
standalone (no diff range required). Reusing the F016 `Snapshot` port avoids inventing a new revision sense.

**Alternatives rejected.** (i) Sense a base/head diff range in the release host — adds a monorepo-shaped concern
the release boundary does not need and risks non-determinism. (ii) Fabricate a synthetic commit when git is
absent — violates FR-014 (no fabricated input); instead an absent revision degrades to a documented normalized
sentinel surfaced as an input note, never a hollow attestation.

## D3 — Two release outputs through the existing atomic writer; the report is the single source of truth

**Decision.** `update` projects `release.json` v2 via `ReleaseJson.ofReleaseReport report` and `attestation.json`
via `AttestationJson.ofAttestation report.Attestation`, both from the immutable `ReleaseReport`, and emits two
`WriteArtifact` effects distinguished by a new `ArtifactKind` (`ReleaseArtifact | AttestationArtifact`) through the
host's **existing** temp+rename `ArtifactWriter`. No existing write path changes.

**Rationale.** The `ReleaseReport` is the single source of truth (FR-012): both projections render from it, so the
F27 human projections will later render from the same object. The `ArtifactKind` discriminator on `WriteArtifact`
+ `Wrote(kind, result)` is the exact pattern 064 added to verify for its two sidecars — proven, additive, and it
keeps a write failure mapped to `ToolError` (never a blocked verdict). Default paths follow the host's existing
`<repo>/...` convention (`release.json` at the existing release path; `attestation.json` beside it, e.g.
`readiness/attestation.json`), overridable by a new `--attestation-out` flag mirroring 064's `--cost-budget-out`/
`--provenance-out`.

**Alternatives rejected.** Embedding the full attestation inside `release.json` — rejected by F26 D2 (the
attestation is a projection of the F25 `AuditSnapshot`, so it gets its own sidecar exactly as the snapshot got
`provenance.json`); `release.json` v2 carries only a self-contained `attestation` identity reference.

## D4 — The verify preview is advisory, declaration-gated, and does NOT pack

**Decision.** `fsgg verify` is the inner-loop boundary and does **not** run the per-project pack. When
`.fsgg/release.yml` is present it senses the release facts (F54 `senseRelease`), evaluates the release rules (F53
`evaluateRelease`) into a `ReleaseDecision`, assembles a `ReleaseReport` with an **empty** `PackEvidenceSet`
(no `Packed` outcome ⇒ no attested subject, FR-008) and an `AttestationSummary` projected from verify's **existing**
`Model.Audit` snapshot, and `Report.preview`s it into the advisory `releaseReadiness` block via
`ofVerifyDecisionWithPreview`. The preview is the document's last optional field, emitted only for `Some` — so a
product with **no** declaration yields a byte-identical `verify.json`, no schema bump (FR-006, FR-012). The preview
never participates in verify's `Exit`.

**Rationale.** "Inner loop stays fast" (US3) forbids verify from packing; FR-009 makes the broad pack-across-every-
project the deferred work. So verify previews the *cheap* release evidence — the sensed publish-plan/posture/
template-pin preconditions and the declared-version-bump intent — with pack evidence honestly absent (a
maintainer sees "you will need to bump version X / configure the publish plan before release," and that packing is
deferred to the release boundary). The empty `PackEvidenceSet` yields no attested subject, which is exactly the
FR-008 no-fabricated-subject guarantee. Reusing verify's existing `AuditSnapshot` for the attestation materials
means **no** new execution or provenance sensing in verify.

**Alternatives rejected.** (i) Verify packs every project to populate real pack evidence — violates "inner loop
stays fast" and duplicates the release boundary's expensive act. (ii) Verify surfaces preconditions without a
`ReleaseReport`/`ReleaseDecision` — impossible against the F26 contract (`Report.preview` requires a
`ReleaseReport` assembled from a `ReleaseDecision`).

## D5 — The scheduled exhaustive matrix is decided, never invoked, in both hosts

**Decision.** `Matrix.decideMatrix budget boundary declared` records the declared matrix `Deferred
(DeferredToScheduledBoundary …)` at the verify `InnerLoop` boundary and `RunNow` (admitted) at the release
`ScheduledOrRelease` boundary; **neither host actually invokes** the admitted matrix. An undeclared matrix
(`None`) is `NotDeclared` — never invented (FR-009).

**Rationale.** This is the F26 D4 contract reused verbatim: the budget ceiling (`CostBudget`) is the gate, and the
actual CI cron trigger / invocation of the admitted matrix is a host/CI follow-up explicitly out of F26's (and
this row's) scope. Both hosts therefore only *record* the decision (surfaced in the report / preview); the
deferral keeps the inner loop free of the broad matrix's cost.

**Alternatives rejected.** Invoking the admitted matrix at the release boundary — out of scope per F26 SC-006 note
and the spec's FR-009 parenthetical; would require a new broad-matrix execution harness this row does not build.

## D6 — A thin shared declaration leaf supersedes the release-local adapter

**Decision.** Lift the `.fsgg/release.yml` parsing into a new thin leaf `FS.GG.Governance.ReleaseDeclaration` that
both hosts reference. It carries the existing F55 `ReleaseDeclaration` shape (rules/expectations/layout) **plus**
the additive `PackableProjects: (SurfaceId * GateCommand * string option) list` (surface id, pack `GateCommand`,
released baseline — `None` ⇒ first release) and the optional `Matrix: ExhaustiveMatrix option`. The release host's
local `Declaration.fs(i)` is removed and re-homed here; the parse semantics it already had are preserved.

**Rationale.** `Report.preview` forces verify to assemble a `ReleaseReport` from a `ReleaseDecision`, which forces
verify to parse the same declaration the release host parses (D4). The release host is an **executable**, so verify
cannot reference it for the `Declaration` module — a host→host (and exe) reference is the wrong dependency
direction. A thin shared leaf is the minimal, honest fix: it is an *adapter relocation*, not a new pure evaluation
core (the spec's "no new pure core" is about evaluation cores like `PackEvidence`). The leaf depends only on the
already-pinned YamlDotNet (the F014 `Schema.fs` parse-to-node precedent) plus `ReleaseRules`/`ReleaseFactsSensing`/
`GateExecution`/`ValidationMatrix`/`Config` — no new external dependency.

**Alternatives rejected.** (i) Duplicate the declaration parser in verify — two sources of truth for the same
file. (ii) Reference the release exe from verify — wrong dependency direction; an exe is not a library. (iii)
Inline the additive `PackableProjects`/`Matrix` parsing only in the release host and have verify re-sense F54
without the declared rules — impossible, since `evaluateRelease` needs the declared `ReleaseRule list`.

## D7 — Findings advisory, boundary distinct from ship, no exit-code / truth-table change

**Decision.** The release verdict and its `ExitCodeBasis` are the F53 `ReleaseDecision` carried verbatim (the
report never re-derives them); the publish-plan/posture/template-pin preconditions surface through the existing v1
`rules` array (no new field) and the additive `Preconditions` projection in the report; the verify preview is
advisory only and never touches verify's exit code. No new verdict, no new exit-code scheme, no
enforcement-truth-table change.

**Rationale.** F26 already fixed the verdict and the five-code exit schemes for both hosts (F55/F56); this row
*consumes* them. The publication boundary is already distinct from `fsgg ship`'s merge verdict (different host,
different decision), and a mergeable-but-not-releasable product is proven by a fixture where `fsgg ship` exits 0
while `fsgg release` exits 1 with the F55 `Blocked` basis (SC-002).

**Alternatives rejected.** Adding a `preconditions` JSON field or a new release-rule family — rejected by F26 D5
(no new family) and D2 (preconditions render through the existing `rules` array).

## D8 — Standalone, product-local sources only; missing/unreadable inputs are input-not-defect

**Decision.** The pack/version/publish/attestation path draws only on the product's own declared packable
projects, version baselines, publish plan/pins/posture, and recorded provenance — no monorepo path. A product with
no packable projects is vacuously satisfied (`PackEvidenceSet.NoPackableProjects = true`) and the report says so;
an unreadable pack output (`PackedNoArtifact (ArtifactUnreadable …)`), an absent provenance/head-revision input, or
a missing publish plan keeps the release host's existing `InputUnavailable`/`ToolError` categorization, blocks
release, and emits no hollow attestation and no fabricated pass. Reordering the declared projects or the recorded
runs changes no evidence/verdict/attestation/output bytes.

**Rationale.** This is the F23/F24/F25 standalone guarantee extended to the publication boundary, and the
Constitution VI input-vs-defect discipline every prior row holds. The F26 cores are already total over these edge
cases (`evaluatePack` sets `NoPackableProjects` on empty outcomes; `summarize` emits no subject for a non-`Packed`
outcome); the host only routes the edge signals into its existing diagnostic categories.

**Alternatives rejected.** Treating a missing publish plan or unreadable pack as a tool defect (exit 4) — rejected;
these are *input* conditions (exit 3 `InputUnavailable`), distinguished from a genuine tool failure per
Constitution VI.

---

## Resolved unknowns

- **Does the release host already have an execution port?** No — it must be added (D1). This is the single biggest
  new edge in the row.
- **Does verify pack for its preview?** No — declaration-gated, advisory, empty pack evidence (D4).
- **Where do the attestation's revision/material inputs come from in the release host?** Normalized edge senses +
  the real packed digests; `base = head = sourceCommit` (D2).
- **How does verify reach the declaration without referencing the release exe?** A thin shared declaration leaf
  (D6).
- **Is anything in the seven F26 surfaces modified?** No — all consumed verbatim; the only new code is host-edge
  wiring + the shared declaration adapter.

No open `NEEDS CLARIFICATION` remains.
