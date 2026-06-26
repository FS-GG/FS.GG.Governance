# Phase 1 Data Model — Release-Provenance Host Wiring (F26 wiring)

This row introduces **no new pure evaluation type**. It consumes the seven F26 surfaces verbatim and adds only
host-edge glue (grown `Effect`/`Msg`/`Model`/`ArtifactKind`, new interpreter ports) plus one thin shared
declaration adapter. Every type below is either reused verbatim (cited with its owning module) or a host-local /
adapter addition.

## 1. Consumed F26 surfaces (verbatim — no change)

| Surface | Signature consumed | Where it runs |
|---|---|---|
| `PackEvidence.Pack.evaluatePack` | `Map<SurfaceId,string> -> PackOutcome list -> PackEvidenceSet` | release `update` |
| `PackEvidence.Pack.versionPolicy` | `string option -> string option -> VersionVerdict` | (inside `evaluatePack`) |
| `PackEvidence.Pack.factContributions` | `PackEvidenceSet -> Map<ReleaseRuleKind,FactState>` | release `update` (merged over F54 facts) |
| `Attestation.Attestation.summarize` | `AuditSnapshot -> PackEvidenceSet -> AttestationSummary` | release `update`; verify `update` (empty pack) |
| `ReleaseReport.Report.assemble` | `ReleaseDecision -> SensedRelease -> PackEvidenceSet -> AttestationSummary -> ReleaseReport` | release `update`; verify `update` |
| `ReleaseReport.Report.preview` | `ReleaseReport -> VerifyReleasePreview` | verify `update` |
| `ValidationMatrix.Matrix.decideMatrix` | `CostBudget -> MatrixBoundary -> ExhaustiveMatrix option -> MatrixPlan` | both `update` |
| `AttestationJson.AttestationJson.ofAttestation` | `AttestationSummary -> string` (`fsgg.attestation/v1`) | release `update` |
| `ReleaseJson.ReleaseJson.ofReleaseReport` | `ReleaseReport -> string` (`fsgg.release/v2`) | release `update` |
| `VerifyJson.VerifyJson.ofVerifyDecisionWithPreview` | `ShipDecision -> CacheEligibilityReport option -> (GateId*GateOutcome) list -> SurfaceFinding list -> VerifyReleasePreview option -> string` | verify `update` |

Key reused value types (owning module): `PackOutcome` / `PackArtifact` / `PackEvidenceSet` / `VersionVerdict`
(`PackEvidence.Model`); `AuditSnapshot` / `KindedCommandRun` / `CommandKind.Pack` (`CommandKind.Model`);
`AttestationSummary` (`Attestation.Model`); `ReleaseReport` / `VerifyReleasePreview` / `PreconditionEvidence`
(`ReleaseReport.Model`); `MatrixPlan` / `ExhaustiveMatrix` / `MatrixBoundary` (`ValidationMatrix.Model`);
`ReleaseDecision` / `ReleaseRule` / `FactState` (`ReleaseRules.Model`); `SensedRelease` (`ReleaseFactsSensing
.Model`); `GateCommand` / `ExecutionPort` / `ExecutionOutcome` (`GateExecution.Model`); `CommandRecord`
(`CommandRecord.Model`); `ArtifactHash` / `Revision` / `RuleHash` / `GeneratorVersion` (`FreshnessKey.Model`);
`BuilderIdentity` (`Provenance.Model`); `EnvironmentClass` / `Cost` / `SurfaceId` (`Config.Model`); `CostBudget` /
`budgetFor` (`CostBudget`).

## 2. New shared adapter leaf — `FS.GG.Governance.ReleaseDeclaration`

The combined `.fsgg/release.yml` parse, lifted out of `ReleaseCommand` so both hosts can consume it (research D6).
The rules/expectations/layout shape is preserved from the F55 `ReleaseCommand.Declaration`; two additive fields are
introduced.

```fsharp
module Declaration =                                   // FS.GG.Governance.ReleaseDeclaration
    /// One declared packable project. Baseline None ⇒ first release (versionPolicy ⇒ NoBaseline).
    type PackableProject =
        { Surface: SurfaceId
          PackCommand: GateExecution.Model.GateCommand
          Baseline: string option }

    /// The combined declaration — the F55 trio PLUS the additive packable projects + optional matrix.
    type ReleaseDeclaration =
        { Rules: ReleaseRule list
          Expectations: ReleaseExpectations
          Layout: SourceLayout
          PackableProjects: PackableProject list        // additive; [] ⇒ NoPackableProjects (vacuously satisfied)
          Matrix: ValidationMatrix.Model.ExhaustiveMatrix option }   // additive; None ⇒ NotDeclared

    type DeclError = { Reason: string }                 // unchanged spirit (input-unavailable ⇒ exit 3)

    /// PURE, TOTAL parse over the raw file lines (content arrives via the F014 FileReader at the edge).
    /// A malformed packable-project entry or matrix declaration ⇒ Error DeclError (never partial facts).
    val parse: lines: string list -> Result<ReleaseDeclaration, DeclError>
```

- **Baselines map** for `evaluatePack` is derived at the edge: `decl.PackableProjects |> List.choose (fun p ->
  p.Baseline |> Option.map (fun b -> p.Surface, b)) |> Map.ofList`.
- **Pack commands** for the `PackProjects` effect: `decl.PackableProjects |> List.map (fun p -> p.Surface,
  p.PackCommand)`.

## 3. Release host (`ReleaseCommand`) — grown MVU surface (additive)

### 3.1 `ArtifactKind` (new discriminator on the write effect — the 064 verify precedent)

```fsharp
type ArtifactKind =
    | ReleaseArtifact       // release.json (v2 via ofReleaseReport)
    | AttestationArtifact   // attestation.json (fsgg.attestation/v1)
```

### 3.2 `Effect` (additive cases; existing cases unchanged)

```fsharp
type Effect =
    | LoadDeclaration of repo: string
    | SenseRelease of layout: SourceLayout * expectations: ReleaseExpectations
    | PackProjects of (SurfaceId * GateCommand) list    // NEW: run each pack via the F51 ExecutionPort
    | SenseProvenance                                    // NEW: normalized head/env/builder senses (D2)
    | WriteArtifact of kind: ArtifactKind * path: string * content: string   // kind ADDED to existing case
    | EmitSummary of text: string
```

### 3.3 `Msg` (additive cases)

```fsharp
type Msg =
    | Begin
    | DeclarationLoaded of Result<Declaration.ReleaseDeclaration, Declaration.DeclError>
    | Sensed of SensedRelease
    | PacksRun of PackOutcome list                        // NEW: the recorded pack outcomes (failed packs included)
    | ProvenanceSensed of head: Revision * environment: EnvironmentClass * builder: BuilderIdentity   // NEW
    | Wrote of kind: ArtifactKind * result: Result<unit, string>   // kind ADDED
    | Emitted
```

### 3.4 `Model` (additive fields; existing fields unchanged)

```fsharp
type Model =
    { Request: RunRequest                 // gains AttestationOut: string (default <repo>/readiness/attestation.json)
      Phase: Phase
      Declaration: Declaration.ReleaseDeclaration option
      Sensed: SensedRelease option
      Packs: PackOutcome list option       // NEW: set by PacksRun
      Head: Revision option                // NEW: set by ProvenanceSensed
      Environment: EnvironmentClass option // NEW
      Builder: BuilderIdentity option      // NEW
      PackEvidence: PackEvidenceSet option // NEW: built in update
      Snapshot: AuditSnapshot option       // NEW
      Attestation: AttestationSummary option // NEW
      Report: ReleaseReport option         // NEW (single source of truth for both writes)
      Matrix: MatrixPlan option            // NEW: decideMatrix at ScheduledOrRelease
      Decision: ReleaseDecision option     // unchanged (carried verbatim into Report)
      ReleaseDoc: string option            // unchanged (now ofReleaseReport v2)
      AttestationDoc: string option        // NEW
      Diagnostics: Diagnostic list
      Exit: ExitDecision }
```

### 3.5 Transition flow (the join)

`init` → `[LoadDeclaration; SenseProvenance]`. On `DeclarationLoaded(Ok decl)` → emit `SenseRelease(decl.Layout,
decl.Expectations)` **and** `PackProjects (packCommands decl)`. The composition fires when **all three** of
`Sensed`, `PacksRun`, and `ProvenanceSensed` have landed (a small three-way join, mirroring verify's
freshness+store join):

1. `pack = evaluatePack (baselines decl) outcomes`
2. `mergedFacts = sensed.Facts` with `factContributions pack` overlaid on `VersionBump`/`PackageMetadata`/
   `Provenance` (packed evidence wins, D1)
3. `decision = Release.evaluateRelease decl.Rules mergedFacts`  *(verbatim)*
4. `snapshot = Audit.auditSnapshot sourceCommit=head base=head head=head ruleHash genVer (digests pack) pack.Runs
   environment builder`  *(D2; digests from `Packed` artifacts only)*
5. `attestation = Attestation.summarize snapshot pack`
6. `report = Report.assemble decision sensed pack attestation`
7. `matrix = Matrix.decideMatrix (budgetFor profile Release) ScheduledOrRelease decl.Matrix`
8. `ReleaseDoc = ReleaseJson.ofReleaseReport report`; `AttestationDoc = AttestationJson.ofAttestation
   report.Attestation`
9. emit `WriteArtifact(ReleaseArtifact, …, ReleaseDoc)` then `WriteArtifact(AttestationArtifact, …, AttestationDoc)`
10. on both `Wrote(_, Ok)` → `EmitSummary`; `Exit` mapped from `decision.ExitCodeBasis` (`Clean→Success`,
    `Blocked→Blocked`), exactly as today; a `Wrote(Error)` → `ToolError` (never `Blocked`).

A failed pack (`PackFailed`) or unbumped/downgraded version (`Unbumped`/`Downgraded`) flows through
`factContributions` → `Unmet` → `evaluateRelease` blocks it with a named reason (no host re-derivation).

### 3.6 New interpreter ports (release host)

```fsharp
type Ports =
    { Files: Loader.FileReader                                  // existing
      Sense: SourceLayout -> ReleaseExpectations -> SensedRelease // existing
      Execute: GateExecution.Model.ExecutionPort                // NEW (F51): run a pack GateCommand
      PackRead: SurfaceId -> ExecutionOutcome -> PackOutcome    // NEW: read a pack output's path/version/digest
      SenseHead: unit -> Revision                               // NEW (F016 Snapshot): the head/source revision
      SenseEnvironment: unit -> EnvironmentClass                // NEW (normalized; 064 precedent)
      SenseBuilder: unit -> BuilderIdentity                     // NEW (normalized; 064 precedent)
      Write: ArtifactWriter                                     // existing (temp+rename atomic)
      Out: OutputSink }                                         // existing
```

- `PackProjects requests` is interpreted per project: `senseExecution ports.Execute command` → `CommandRecord` →
  wrap `{ Kind = Pack; Record = record }`; then `ports.PackRead surface outcome` reads the produced `.nupkg`
  (path/version/digest) and classifies `Packed | PackedNoArtifact | PackFailed` (sentinel exit ⇒ `PackFailed`,
  zero-exit-no-artifact ⇒ `PackedNoArtifact`). Result fed back as `PacksRun outcomes` (request order preserved).
- `SenseProvenance` → `ProvenanceSensed(SenseHead(), SenseEnvironment(), SenseBuilder())`.
- The real `PackRead` locates the artifact under the pack output dir (constitution `~/.local/share/nuget-local/`),
  reads its version + computes its `ArtifactHash`; an unreadable artifact ⇒ `PackedNoArtifact (ArtifactUnreadable
  reason)` (input signal, never a throw — Constitution VI).

## 4. Verify host (`VerifyCommand`) — grown MVU surface (additive, advisory)

### 4.1 `Model` (one additive field; never affects `Exit`)

```fsharp
    // ... existing fields (incl. Audit: AuditSnapshot option, Environment, Builder from 064) ...
      ReleasePreview: VerifyReleasePreview option   // NEW: advisory; projected into releaseReadiness, never blocking
      ReleaseMatrix: MatrixPlan option              // NEW: decideMatrix at InnerLoop (recorded deferred)
```

### 4.2 New port + transition (declaration-gated)

```fsharp
    // Ports gains (the release-fact sensor, reusing F54 verbatim):
      SenseRelease: SourceLayout -> ReleaseExpectations -> SensedRelease   // NEW (declaration-gated)
```

On catalog load, the verify interpreter additionally attempts `LoadDeclaration repo` through the existing
`Files` port; **if `.fsgg/release.yml` is present and parses**, it senses release facts and, in `update`:

1. `decision = Release.evaluateRelease decl.Rules sensed.Facts`
2. `report = Report.assemble decision sensed PackEvidenceSet.empty (Attestation.summarize model.Audit
   PackEvidenceSet.empty)`  *(empty pack ⇒ no attested subject, FR-007; attestation materials from verify's
   existing `Audit`)*
3. `ReleasePreview = Some (Report.preview report)`; `ReleaseMatrix = Some (decideMatrix (budgetFor profile Verify)
   InnerLoop decl.Matrix)`
4. the verify.json projection switches to `VerifyJson.ofVerifyDecisionWithPreview decision cache execution findings
   model.ReleasePreview` — byte-identical to the existing projection when `ReleasePreview = None`.

If `.fsgg/release.yml` is **absent or unparsable**, `ReleasePreview = None` and `verify.json` is byte-identical to
its pre-wiring golden (no schema bump, no block, exit unchanged — D4, FR-012).

> Note: `PackEvidenceSet.empty` is the value `{ Verdicts=[]; Runs=[]; NoPackableProjects=true }` constructed at the
> verify edge (no new core function); `Attestation.summarize` over it yields zero subjects.

## 5. JSON contracts touched

| Document | Owner host | Change | Byte-identity rule |
|---|---|---|---|
| `attestation.json` (`fsgg.attestation/v1`) | release | **new** sidecar | deterministic; identical inputs ⇒ identical bytes |
| `release.json` (`fsgg.release/v1 → v2`) | release | additive `packageEvidence`/`versionPolicy`/`attestation` | the one existing release golden re-blessed v1→v2 (done in F26) |
| `verify.json` (`fsgg.verify/v1`) | verify | additive optional `releaseReadiness` block | **byte-identical when no declaration** (block absent, no schema bump) |
| `route.json`, `ship.json` | other hosts | **none** | byte-identical (untouched hosts) |

## 6. Invariants (asserted by tests)

1. A failed/unbumped/downgraded pack ⇒ `evaluateRelease` blocks; the failed `Pack` run is recorded (sentinel) and
   the project named — never dropped, never a pass (FR-001/FR-002).
2. `evaluateRelease` is called **verbatim**; the `ReleaseDecision`/`ExitCodeBasis` is carried into the report
   without re-derivation (FR-003, FR-012).
3. `attestation.json`, `release.json` v2, and the verify preview block are byte-identical on re-run and
   order-independent over reordered projects/runs (FR-007, FR-011, SC-003).
4. `route.json`/`ship.json`, a no-declaration `verify.json`, and an empty-additive `release.json` byte-identical to
   frozen baselines (FR-012, SC-005).
5. A no-`Packed` outcome yields no attested subject; the compliance marker is always present (FR-008).
6. No packable projects ⇒ vacuously satisfied + reported; unreadable pack / absent provenance / missing publish
   plan ⇒ input-not-defect diagnostic, blocked, no hollow attestation (FR-013, FR-014).
7. A declared matrix is `Deferred` at verify `InnerLoop` and `RunNow` at release `ScheduledOrRelease`; neither host
   invokes it; an undeclared matrix is `NotDeclared` (FR-009).
