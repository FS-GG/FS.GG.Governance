# Implementation Plan: Release-Provenance Host Wiring — `fsgg release` Pack/Version Boundary, the Attestation Sidecar, `release.json` v2, and the `fsgg verify` Release-Readiness Preview (F26 wiring)

**Branch**: `065-release-provenance-host-wiring` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/065-release-provenance-host-wiring/spec.md`

## Summary

F26 (`061-verify-release-provenance`) landed seven surfaces — the pure cores `PackEvidence`
(`evaluatePack`/`versionPolicy`/`factContributions`), `Attestation` (`summarize`), `ReleaseReport`
(`assemble`/`preview`), `ValidationMatrix` (`decideMatrix`), plus the projections `AttestationJson`
(`ofAttestation`, `fsgg.attestation/v1`), the additive `ReleaseJson.ofReleaseReport` (`fsgg.release/v2`), and the
additive `VerifyJson.ofVerifyDecisionWithPreview` (`releaseReadiness` block) — fully built, packed, and green (117
tests; five blessed surface baselines), but **wired into no command host**. Today `fsgg release` evaluates declared
release rules over F54-sensed facts but never **packs** anything, builds no attestation, and writes neither
`attestation.json` nor `release.json` v2; `fsgg verify` emits no release-readiness preview.

This row consumes those seven surfaces **additively** at the MVU interpreter edge of the two mature hosts
`fsgg release` (`FS.GG.Governance.ReleaseCommand`) and `fsgg verify` (`FS.GG.Governance.VerifyCommand`). It adds
**no** new pure evaluation core, **no** new report object, **no** new release-rule family, and **no** new external
dependency — only ProjectReferences onto the already-built F26 libraries, a shared declaration adapter, and the
F51 execution port the release host does not yet carry.

**Technical approach, grounded in a host reconnaissance** (research.md D1–D8). The two hosts are *not* symmetric
here, and the reconnaissance pins down exactly why:

- **The release host has no execution port today.** `ReleaseCommand` is a **linear** MVU
  (`parse → LoadDeclaration → SenseRelease → evaluate-in-update → WriteArtifact → EmitSummary`) whose
  `.fsproj` references only `ReleaseRules`/`ReleaseFactsSensing`/`Config`/`ReleaseJson` — it has **no**
  `GateExecution` reference, **no** `ExecuteGates`/`PackProjects` effect, and **no** `Execute` port. The genuinely
  new edge this row adds lives almost entirely here: a `PackProjects` effect that runs each declared pack
  `GateCommand` through the F51 `GateExecution.ExecutionPort` (the same port `verify`/`ship` already use), reads
  each produced artifact's path/version/digest, and feeds back a `PackOutcome` per project.
- **The verify host already carries everything the snapshot needs.** `VerifyCommand` *already* references
  `GateExecution`, *already* senses the normalized `EnvironmentClass`/`BuilderIdentity` (`SenseProvenance`, 064),
  and *already* builds an `AuditSnapshot` (`Model.Audit`). So the verify preview adds **no** new execution or
  provenance sense — only the release-fact sensing + rule evaluation needed to assemble a previewable
  `ReleaseReport`, plus the additive `releaseReadiness` projection.
- **`Report.preview` requires a `ReleaseDecision`.** The F26 `VerifyReleasePreview` is `Report.preview` of a full
  `ReleaseReport`, and `Report.assemble` takes a `ReleaseDecision` + `SensedRelease`. Verify produces a
  `ShipDecision`, not a `ReleaseDecision`, and senses no release facts today — so to preview release readiness it
  must sense the release facts (F54) and evaluate the release rules (F53), which means it needs the **same
  `.fsgg/release.yml` declaration** the release host parses. To avoid a host→host reference (the release host is
  an exe), the declaration adapter is lifted into a thin shared leaf both hosts consume.

So the new code is concentrated at the release edge (the pack runs + the report/attestation/sidecar assembly) with
a lighter, advisory addition at the verify edge (sense-evaluate-preview), plus one thin shared declaration leaf.
The seven F26 surfaces are consumed verbatim.

**The central reconciliation (research.md D1) — pack-and-block vs. existing-golden byte-identity.** The spec
demands two things that pull against each other: FR-001/FR-002 say `fsgg release` MUST actually pack every project
and **block** on a failed/unbumped pack (changing the release verdict's *evidence basis*), while FR-012/SC-005 say
every existing `route.json` / `ship.json` golden — and a `verify.json` run with no release declaration, and a
`release.json` whose appended v2 fields are empty — MUST stay **byte-identical**. These reconcile through three
facts the reconnaissance pins down: (a) the release host's *evaluation* path is unchanged — `factContributions`
merges over the F54 sensed facts and `Release.evaluateRelease` is called **verbatim**, so a product whose declared
families were already `Met` and whose packs succeed at a bumped version produces the *same* `ReleaseDecision` the
v1 path produced; the v2 additions (`packageEvidence`/`versionPolicy`/`attestation`) are **appended** fields that
are byte-identical to v1 only when empty, and v1→v2 is therefore a deliberate, explicitly-versioned bump (the one
existing release golden that changes, re-blessed v1→v2); (b) `route.json`/`ship.json` are written by *other* hosts
this row does not touch — byte-identical by construction; (c) the `verify.json` `releaseReadiness` block is emitted
**only** when a release declaration is present (`ofVerifyDecisionWithPreview` writes nothing for `None`), so a
product with no `.fsgg/release.yml` yields a byte-identical `verify.json` with no schema bump. The plan's safety
obligation is to prove (a)/(b)/(c) by golden comparison against frozen pre-wiring baselines (SC-005), and to
exercise the real pack/block behaviour only through **new** fixtures producing the **new** outputs.

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **The release host gains the F51 execution port and a `PackProjects` effect; the pure budget/version/report
   work stays in `update` (D1, D3).** On a loaded declaration the host emits `PackProjects` (run each declared pack
   `GateCommand` through the F51 `ExecutionPort`, never dropping a failed pack — sentinel exit recorded) alongside
   `SenseRelease`; when both the pack outcomes and the sensed facts have landed, `update` builds the
   `PackEvidenceSet` (`Pack.evaluatePack`), merges `Pack.factContributions` over the F54 sensed facts (packed
   evidence wins on `VersionBump`/`PackageMetadata`/`Provenance`), calls `Release.evaluateRelease` **verbatim**,
   and carries the resulting `ReleaseDecision`/`ExitCodeBasis` into the report unchanged (FR-001, FR-002, FR-003).
2. **Provenance inputs for the release attestation are sensed normalized at the edge; the artifact digests are the
   real packed digests (D2).** `Audit.auditSnapshot` needs `sourceCommit`/`baseRevision`/`headRevision`, rule
   hash, generator version, artifact digests, the kinded runs, environment, and builder. The kinded runs are the
   `Pack` runs from (1); the artifact digests are the real `PackArtifact.Digest`s from the `PackEvidenceSet`; the
   head/source revision is sensed once through the **existing** F016 `Snapshot` port (`base = head = sourceCommit`
   for the release boundary, which attests a *product state* not a diff range — deterministic and standalone); the
   rule hash is derived from the declared rules, the generator version is the normalized `fsgg` constant, and the
   environment/builder reuse the same normalized senses 064 added to verify (no username/host/clock). This keeps
   `attestation.json` byte-deterministic across machines/re-runs (FR-007, FR-011).
3. **Two new release outputs through the existing atomic writer; the report is the single source of truth (D3).**
   `update` projects `release.json` v2 (`ReleaseJson.ofReleaseReport report`) and `attestation.json`
   (`AttestationJson.ofAttestation report.Attestation`) from the immutable `ReleaseReport`, and emits two
   `WriteArtifact` effects through the host's **existing** temp+rename `ArtifactWriter` (a new `ArtifactKind`
   discriminator distinguishes the release doc from the sidecar — the `verify` 064 precedent). No existing write
   path changes (FR-004, FR-010).
4. **The verify preview is advisory, declaration-gated, and does NOT pack (D4).** `fsgg verify` is the inner-loop
   boundary: it does **not** run the expensive per-project pack. When `.fsgg/release.yml` is present it senses the
   release facts (F54), evaluates the release rules (F53) into a `ReleaseDecision`, assembles a `ReleaseReport`
   with an **empty** `PackEvidenceSet` (`NoPackableProjects`/no `Packed` outcome ⇒ no attested subject, FR-007)
   and an `AttestationSummary` projected from verify's **existing** `AuditSnapshot`, and `Report.preview`s it into
   the advisory `releaseReadiness` block via `ofVerifyDecisionWithPreview`. The preview never changes verify's
   exit code (it is the document's last optional field) and is **absent** when no declaration is present —
   `verify.json` byte-identical, no schema bump (FR-006, FR-012).
5. **The scheduled exhaustive matrix is decided, never invoked, in both hosts (D5).** `Matrix.decideMatrix`
   records a declared matrix `Deferred` at the verify inner-loop boundary (`InnerLoop`) and `RunNow` (admitted) at
   the release boundary (`ScheduledOrRelease`); **neither host actually invokes** the admitted matrix — that stays
   the F26 D4 host/CI follow-up out of this row's scope. An undeclared matrix is never invented (FR-009).
6. **A thin shared declaration leaf supersedes the release-local adapter (D6).** The `.fsgg/release.yml` parsing —
   the existing F55 `ReleaseDeclaration` (rules/expectations/layout) **plus** the additive `PackableProjects`
   (`SurfaceId * GateCommand * baseline option`) and optional `Matrix` — is lifted into a new thin leaf
   `FS.GG.Governance.ReleaseDeclaration` that both hosts reference, so verify can assemble a `ReleaseReport`
   without referencing the release **exe**. This is an adapter relocation (no new *evaluation* core); the release
   host's behaviour through it is unchanged for the rules/expectations/layout it already parsed.
7. **Findings stay advisory; the boundary stays distinct from ship; no exit-code/truth-table change (D7).** The
   release verdict and its `ExitCodeBasis` are the F53 `ReleaseDecision` carried verbatim (the report never
   re-derives them); the publish-plan/posture/template-pin preconditions surface through the existing v1 `rules`
   array (no new field) and the new `Preconditions` projection; the verify preview is advisory only. No new
   verdict, no new exit-code scheme, no enforcement-truth-table change (FR-005, FR-008).
8. **Standalone path uses product-local sources only; missing/unreadable inputs surface a clear input signal
   (D8).** The pack/version/publish/attestation path draws only on the product's own declared packable projects,
   version baselines, publish plan/pins/posture, and recorded provenance — no monorepo path. A product with no
   packable projects is vacuously satisfied and says so (`NoPackableProjects`); an unreadable pack output, absent
   provenance input, or missing publish plan keeps the host's existing input-vs-defect diagnostic (the release
   host's `InputUnavailable`/`ToolError` categories), blocks release, and emits no hollow attestation and no
   fabricated pass (FR-013, FR-014).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).

**Primary Dependencies**: **No new external/NuGet dependency.** The two hosts add **ProjectReferences** onto the
already-built F26 libraries (`PackEvidence`, `Attestation`, `ReleaseReport`, `ValidationMatrix`, `AttestationJson`,
and the extended `ReleaseJson`/`VerifyJson`), onto the new thin shared declaration leaf
`FS.GG.Governance.ReleaseDeclaration`, and — for the release host only — onto `FS.GG.Governance.GateExecution`
(F51, the pack execution port it does not yet carry), `FS.GG.Governance.CommandKind` (the `Pack` kind +
`auditSnapshot`), `FS.GG.Governance.Provenance` (F033 `BuilderIdentity`), `FS.GG.Governance.CostBudget` (the
`decideMatrix` ceiling), and `FS.GG.Governance.Snapshot` (F016, the head-revision sense). Verify already references
`GateExecution`/`CommandKind`/`Provenance`/`CostBudget`/`Snapshot` (064/F056); it adds only `ReleaseRules`/
`ReleaseFactsSensing`/the F26 cores/the shared declaration leaf. The JSON serialization uses the BCL
`System.Text.Json` already used by every `*Json` projection. All seven F26 surfaces are consumed verbatim.

**Storage**: One **new** deterministic JSON sidecar (`attestation.json`, `fsgg.attestation/v1`) and one additive
schema bump (`release.json`, `fsgg.release/v1 → v2`) written by the release host through its **existing** atomic
`WriteArtifact` port; the verify host's `verify.json` gains an additive, optional `releaseReadiness` block (no
schema bump, `fsgg.verify/v1` unchanged). Every existing artifact (`route.json`/`ship.json` goldens, the
`verify.json` v1 fields, the evidence-reuse store) keeps its existing write path, byte-identical for identical
repository state (FR-012). The pack runs write `.nupkg` artifacts to the constitution's
`~/.local/share/nuget-local/` via the existing F51 execution port (the host edge, not a core).

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). Each host's existing `.Tests`
project is **extended** with real-filesystem (real `dotnet pack`) end-to-end fixtures: (a) a **pack-boundary**
fixture (multiple declared packable projects — every project bumped ⇒ preconditions `Met`, exit 0; one pack fails
⇒ blocked with a named reason + the failed `Pack` run recorded with its sentinel; one packs unbumped/downgraded ⇒
blocked naming the project/version; `release.json` v2 + `attestation.json` written, byte-identical on re-run —
SC-001, SC-003); (b) a **mergeable-but-not-releasable** + **fully-releasable** pair (`fsgg ship` exit 0 while
`fsgg release` exit 1 with a distinct basis; a releasable product exits 0 — SC-002); (c) a **verify
release-preview** fixture (advisory `releaseReadiness` present with the same sensed evidence, verify exit unchanged;
a declared matrix recorded deferred; a no-declaration run byte-identical to its frozen golden — SC-004, SC-005);
(d) a **standalone + safe-failure** set (no packable projects vacuously satisfied; unreadable pack output / absent
provenance / missing publish plan ⇒ clear input diagnostic, blocked, no hollow attestation; reordering
projects/runs ⇒ byte-identical — SC-006); (e) **byte-identity goldens** for `route.json`/`ship.json`, the empty-v2
`release.json`, and the no-declaration `verify.json` against frozen pre-wiring baselines (SC-005). The pure
`evaluatePack`/`versionPolicy`/`factContributions`/`summarize`/`assemble`/`preview`/`decideMatrix` and all three
projections are already covered by F26's 117 tests and are reused, not re-tested. Real cores are never mocked; only
the edge ports (execution, pack-output reader, artifact writer, release-fact sensor, head-revision sense) are
faked, and any synthetic pack output carries `Synthetic` in the test name and a use-site disclosure (Constitution
V).

**Target Platform**: Cross-platform .NET CLI executables (Linux/macOS/Windows). `attestation.json`, `release.json`
v2, and the `verify.json` preview block are normalized (no path/username/clock/environment leakage; pack duration
retained as sensed metadata only) so they are byte-identical across machines (FR-007, FR-011).

**Project Type**: Host-edge wiring + one thin shared adapter leaf — no new pure *evaluation* core, no new report
object. Extends **2** existing command-host MVU edges (`ReleaseCommand`, `VerifyCommand`); adds **1** thin shared
declaration leaf (`ReleaseDeclaration`); consumes seven already-built F26 surfaces; single-solution F# layout.

**Performance Goals**: Not a hot inner-loop path. The pure cores are single linear passes; the only real expense —
the per-project `dotnet pack` — is the act the release boundary exists to enforce and runs through the existing F51
port, and verify (the inner loop) does **not** pack (D4). The broad exhaustive matrix is only *decided* (never
invoked) by `decideMatrix`, so neither host pays to run it (FR-009).

**Constraints**: Every existing persisted/`--json` contract (`route.json`, `ship.json`, the `verify.json` v1
fields, a no-declaration `verify.json`, an empty-additive-field `release.json`) stays **byte-identical** for
identical repository state (FR-012, SC-005). `attestation.json`, `release.json` v2, and the preview block are
deterministic — stable ordering, normalized paths, no wall-clock/username/environment dependence, pack duration as
sensed metadata only — so identical inputs yield byte-identical output and reordering projects/runs changes nothing
(FR-007, FR-011, SC-003). The publication verdict is the F53 `ReleaseDecision` verbatim, blocking and distinct from
the ship merge verdict; the verify preview is advisory: no new verdict, no new exit-code, no
enforcement-truth-table change (FR-005, FR-006, FR-008). No filesystem/process/registry dependency enters any pure
core; the only new I/O (the pack runs, the pack-output reads, the head-revision sense, the two release writes)
lives at the interpreter edge through existing-shaped ports (FR-010). Safe failure preserved: a missing/unreadable
pack output, absent provenance input, or missing publish plan surfaces a clear input-vs-defect signal with no
swallowed error, no fabricated pack, no hollow attestation, and no fabricated pass (FR-014, Constitution VI).

**Scale/Scope**: Extends **2 hosts** and adds **1 thin leaf**. `ReleaseCommand` (the heavy side) gains the F51
execution port + a `PackProjects` effect + a pack-output-reader edge, a head-revision/environment/builder
provenance sense, the `PackEvidenceSet`/`AuditSnapshot`/`AttestationSummary`/`ReleaseReport` assembly in `update`,
two new `WriteArtifact` outputs (`release.json` v2 + `attestation.json`) under a new `ArtifactKind`, the
`decideMatrix` record, and ProjectReferences onto the F26 cores + `GateExecution`/`CommandKind`/`Provenance`/
`CostBudget`/`Snapshot` + the shared leaf. `VerifyCommand` (the light side) gains release-fact sensing + rule
evaluation + the advisory `ReleaseReport.preview` projection (declaration-gated) + the `decideMatrix` record + refs
onto `ReleaseRules`/`ReleaseFactsSensing`/the F26 cores/the shared leaf. The new `FS.GG.Governance.ReleaseDeclaration`
leaf holds the combined declaration parse (rules/expectations/layout + packable projects + matrix). **No** new pure
evaluation core, report object, verdict, exit-code scheme, release-rule family, or external dependency.
P1 = the release pack/version boundary + `release.json` v2 + `attestation.json` + byte-identity (US1+US2);
P2 = the verify advisory preview (US3) + standalone/safe-failure (US4).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. The seven consumed F26 surfaces already exist as
  `.fsi` and are exercised by F26's 117 tests. The only new public surface is each host's grown
  `RunRequest`/`Effect`/`Msg`/`Model`/`ArtifactKind`, the release host's new `Execute`/pack-reader/provenance
  ports, and the shared `ReleaseDeclaration` leaf's `.fsi`; each is drafted in a curated `.fsi`, exercised through
  the loaded host surface (parse/dispatch/persist) and the loaded leaf (`parse`), then surface-baselined.
- **II. Visibility Lives in `.fsi`** — PASS. Each extended host ships curated `.fsi` files
  (`Loop.fsi`/`Interpreter.fsi`); the new `ArtifactKind` cases, `Effect`/`Msg` cases, `Model` fields, ports, and
  the shared leaf's `parse` are declared there; `.fs` bodies carry no access modifiers. Host surface baselines
  re-blessed; the new `ReleaseDeclaration` baseline added; the seven F26 baselines unchanged (consumed, not
  modified).
- **III. Idiomatic Simplicity** — PASS. Plain pattern matches on `PackOutcome`/`VersionVerdict`/`MatrixPlan`,
  pipelines, exhaustive matches; no SRTP/reflection/type-providers/non-trivial CEs/active patterns. The pack-output
  reader and head-revision sense are plain edge functions. No new external dependency.
- **IV. Elmish/MVU Is the Boundary** — PASS. Both hosts are already MVU `Loop`/`Interpreter`/`Program`. The
  `PackEvidenceSet`/snapshot/attestation/report assembly and the verify preview are pure additions inside
  `update`; the pack runs, pack-output reads, head-revision sense, and the two release writes are `Effect`s
  executed at the interpreter edge through existing-shaped ports. Pure-transition coverage (the right gates pack,
  the decision is carried verbatim, the sidecars project deterministically) plus interpreter-edge coverage (real
  `dotnet pack`, real atomic write) both land.
- **V. Test Evidence Is Mandatory** — PASS. Fail-before/pass-after fixtures over the **real** F26 cores and real
  hosts: the real-`dotnet pack` pack-boundary fixture, the mergeable-but-not-releasable pair, the verify
  release-preview fixture, the standalone + safe-failure set, the byte-identity goldens vs. frozen baselines. Edge
  ports are faked; synthetic pack outputs are `Synthetic`-named and disclosed.
- **VI. Observability and Safe Failure** — PASS. A failed/unbumped pack blocks release with a named reason and the
  failed `Pack` run recorded (never silent, never a pass); a product with no packable projects is vacuously
  satisfied and says so; an unreadable pack output / absent provenance input / missing publish plan keeps the
  host's input-vs-defect diagnostic and yields no hollow attestation. No swallowed error, no fabricated
  pack/attestation/pass.

**Change Classification: Tier 1 (contracted change)** — adds one new deterministic JSON contract
(`attestation.json` `fsgg.attestation/v1`), an additive `release.json` v1→v2 bump, and an additive `verify.json`
`releaseReadiness` block; changes the two wired hosts' public effect/model/request/declaration surface and adds one
thin shared leaf (re-blessing the two host baselines + adding the `ReleaseDeclaration` baseline), while leaving
every existing `route.json`/`ship.json` golden — and a no-declaration `verify.json`, and an empty-v2 `release.json`
— byte-identical and introducing no new external dependency. The full chain applies: spec, plan, host/leaf `.fsi`
updates, re-blessed surface baselines, real-`dotnet pack` test evidence, and docs (including flipping F26 Phase 8
to complete in `specs/061-verify-release-provenance/tasks.md` and updating the roadmap's "Remaining" note).

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/065-release-provenance-host-wiring/
├── plan.md                          # This file (/speckit-plan output)
├── research.md                      # Phase 0 — D1..D8 (incl. the pack-and-block vs. byte-identity reconciliation)
├── data-model.md                    # Phase 1 — host-edge glue: PackOutcome build, snapshot inputs, grown
│                                    #   RunRequest/Effect/Msg/Model/ArtifactKind, shared declaration leaf
├── quickstart.md                    # Phase 1 — per-story validation scenarios
├── contracts/                       # Phase 1
│   ├── pack-boundary.md             #   declared packable projects → ExecutionPort → PackOutcome → evaluatePack →
│   │                                #     factContributions merged over F54 → evaluateRelease (verbatim)
│   ├── attestation-snapshot.md      #   pack runs + normalized provenance senses → auditSnapshot → summarize →
│   │                                #     attestation.json (fsgg.attestation/v1); base=head=sourceCommit rationale
│   ├── release-outputs.md           #   ofReleaseReport (release.json v2) + ofAttestation; two WriteArtifact
│   │                                #     effects; v1→v2 byte-identical-when-empty; byte-identity anchors
│   ├── verify-preview.md            #   sense F54 + evaluate F53 → assemble (empty pack) → preview →
│   │                                #     ofVerifyDecisionWithPreview; declaration-gated; verify exit unchanged
│   └── shared-declaration.md        #   FS.GG.Governance.ReleaseDeclaration: combined parse (rules/expectations/
│                                    #     layout + PackableProjects + Matrix); both hosts consume it
├── checklists/
│   └── requirements.md              # (already present — spec quality checklist)
└── tasks.md                         # Phase 2 (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.ReleaseDeclaration/                   # NEW (thin leaf) — combined .fsgg/release.yml parse
│   ├── Declaration.fsi / Declaration.fs                   #   rules/expectations/layout (re-homed from ReleaseCommand)
│   │                                                      #     + PackableProjects (SurfaceId*GateCommand*baseline option)
│   │                                                      #     + optional Matrix (ExhaustiveMatrix); PURE, total parse
│   └── FS.GG.Governance.ReleaseDeclaration.fsproj         #   refs: ReleaseRules, ReleaseFactsSensing, GateExecution,
│                                                          #     ValidationMatrix, Config (+ YamlDotNet, already pinned)
├── FS.GG.Governance.ReleaseCommand/                       # EXTEND (heavy) — pack boundary + attestation + v2 sidecar
│   ├── Loop.fsi / Loop.fs                                 #   new Effect PackProjects + ArtifactKind (ReleaseArtifact|
│   │                                                      #     AttestationArtifact); new Msg (PacksRun, ProvenanceSensed);
│   │                                                      #     Model gains pack outcomes / snapshot / attestation / report;
│   │                                                      #     update: evaluatePack → factContributions merge →
│   │                                                      #     evaluateRelease (verbatim) → auditSnapshot → summarize →
│   │                                                      #     assemble → ofReleaseReport (v2) + ofAttestation
│   ├── Interpreter.fsi / Interpreter.fs                   #   new ports: Execute (F51 ExecutionPort), PackRead (read a
│   │                                                      #     pack output's path/version/digest), SenseHead/Environment/
│   │                                                      #     Builder; two WriteArtifact through existing atomic writer
│   ├── Program.fs                                         #   wire the new real ports
│   └── FS.GG.Governance.ReleaseCommand.fsproj             #   + ProjectReference PackEvidence, Attestation, ReleaseReport,
│   │                                                      #     ValidationMatrix, AttestationJson, ReleaseDeclaration,
│   │                                                      #     CommandKind, Provenance, CostBudget, GateExecution, Snapshot
│   └── (Declaration.fs/.fsi REMOVED — superseded by the shared leaf)
├── FS.GG.Governance.VerifyCommand/                        # EXTEND (light) — advisory release-readiness preview
│   ├── Loop.fsi / Loop.fs                                 #   Model.ReleasePreview: VerifyReleasePreview option (never
│   │                                                      #     affects Exit); update (declaration-gated): sense F54 →
│   │                                                      #     evaluateRelease → assemble (empty PackEvidenceSet,
│   │                                                      #     attestation from existing Audit) → preview →
│   │                                                      #     ofVerifyDecisionWithPreview; decideMatrix (InnerLoop)
│   ├── Interpreter.fsi / Interpreter.fs                   #   new port: SenseRelease (F54) gated on a present declaration
│   ├── Program.fs                                         #   wire the new real port
│   └── FS.GG.Governance.VerifyCommand.fsproj              #   + ProjectReference ReleaseRules, ReleaseFactsSensing,
│                                                          #     PackEvidence, Attestation, ReleaseReport, ValidationMatrix,
│                                                          #     ReleaseDeclaration (GateExecution/CommandKind/Provenance/
│                                                          #     CostBudget/Snapshot already referenced)
└── (the seven F26 surfaces are CONSUMED VERBATIM — unchanged):
      FS.GG.Governance.PackEvidence / Attestation / ReleaseReport / ValidationMatrix / AttestationJson /
      ReleaseJson (ofReleaseReport) / VerifyJson (ofVerifyDecisionWithPreview)

tests/
├── FS.GG.Governance.ReleaseCommand.Tests/                # EXTEND — real-dotnet-pack pack boundary; mergeable-but-not-
│                                                         #   releasable; release.json v2 + attestation.json determinism;
│                                                         #   standalone + safe-failure; route/ship byte-identity anchors
├── FS.GG.Governance.VerifyCommand.Tests/                 # EXTEND — advisory preview present + exit-unchanged; matrix
│                                                         #   deferred; no-declaration verify.json byte-identical
└── FS.GG.Governance.ReleaseDeclaration.Tests/            # NEW — combined parse: rules/expectations/layout +
                                                          #   PackableProjects + Matrix; fail-safe on malformed input

surface/
├── FS.GG.Governance.ReleaseDeclaration.surface.txt        # NEW
├── FS.GG.Governance.ReleaseCommand.surface.txt            # RE-BLESS — grown Effect/Msg/Model/ArtifactKind/ports
├── FS.GG.Governance.VerifyCommand.surface.txt             # RE-BLESS — Model.ReleasePreview + preview projection
└── (PackEvidence/Attestation/ReleaseReport/ValidationMatrix/AttestationJson/ReleaseJson/VerifyJson baselines)  # UNCHANGED

Directory.Packages.props                                  # UNCHANGED — no new external/NuGet dependency
FS.GG.Governance.sln                                      # EDIT — add the ReleaseDeclaration src + test projects
```

**Structure Decision**: Consume, don't create a new evaluation core — and wire at the edge. The seven F26 surfaces
are consumed at each host's existing MVU interpreter edge. The genuinely new code is concentrated at the **release**
edge (the F51 pack runs, the pack-output reads, the head/environment/builder provenance senses, the
report/attestation/sidecar assembly, the two new writes), lighter and advisory at the **verify** edge (sense F54 →
evaluate F53 → assemble empty-pack report → preview), with one **thin shared declaration leaf**
(`FS.GG.Governance.ReleaseDeclaration`) lifted out so verify can assemble a previewable `ReleaseReport` without a
host→host reference. The byte-identity anchor (research.md D1) bounds the safety surface to verifiable golden
comparisons — `route.json`/`ship.json`, a no-declaration `verify.json`, and an empty-v2 `release.json` against
frozen baselines — while the real pack/block behaviour and the new outputs are exercised by new fixtures. The
FR-010 boundary stays checkable: each host references the F26 cores and the execution port; no pure core gains any
filesystem/process dependency.

**Host asymmetry — why the release side is heavy and the verify side is light** (research.md D1/D4). The
reconnaissance shows the two hosts are not symmetric: `ReleaseCommand` is a linear MVU with **no** execution port,
so it gains the most (the F51 port, the pack runs, the pack-output reader, the provenance senses, the
report/attestation assembly, two new writes); `VerifyCommand` already carries the execution port, the normalized
provenance senses, and an `AuditSnapshot` (064/F056), so it gains only the declaration-gated, advisory
sense-evaluate-preview path. The shared `ReleaseDeclaration` leaf is what lets the light verify side reach the same
declaration the heavy release side parses, without depending on the release executable.

## Complexity Tracking

> No Constitution Check violations. **No new external dependency** (only ProjectReferences onto already-built F26
> libraries + the F51 execution port the release host did not yet carry). The one genuinely new project — the thin
> `FS.GG.Governance.ReleaseDeclaration` leaf — is an adapter relocation (not a new evaluation core) justified by
> the no-host→host-reference rule: verify must reach the `.fsgg/release.yml` declaration to assemble a previewable
> `ReleaseReport`, and the release host is an executable. The pack-and-block vs. byte-identity reconciliation
> (research.md D1) is resolved by golden comparison against frozen baselines plus declaration-gated/empty-field
> additive emission, not by any escape hatch. This section is intentionally empty.
