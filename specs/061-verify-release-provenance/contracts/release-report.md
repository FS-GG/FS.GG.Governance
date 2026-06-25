# Contract: `FS.GG.Governance.ReleaseReport` (pure, P1)

The immutable, presentation-free **single source of truth** (FR-012): the F53 `ReleaseDecision` verbatim + the
pack evidence + the precondition evidence + the attestation summary. `ReleaseJson` and the verify
`releaseReadiness` preview both render from it; the F27 human projections will too. The release exit-code basis
is named at the report level so publication reads as a first-class boundary distinct from ship (FR-004).

## `Model.fsi` (draft)

```fsharp
namespace FS.GG.Governance.ReleaseReport

open FS.GG.Governance.Ship.Model               // Verdict, ExitCodeBasis
open FS.GG.Governance.ReleaseRules.Model         // ReleaseDecision, ReleaseRuleKind, FactState
open FS.GG.Governance.PackEvidence.Model          // PackEvidenceSet
open FS.GG.Governance.Attestation.Model           // AttestationSummary

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// One publication precondition surfaced first-class (FR-006), projected from the F54 ReleaseSnapshot +
    /// the F53 finding. Product-neutral.
    type PreconditionEvidence =
        { Kind: ReleaseRuleKind
          State: FactState
          Reason: string }

    /// The whole publication boundary as one immutable, presentation-free value (FR-012). Carries the F53
    /// decision VERBATIM — never re-derives a verdict or exit-code basis.
    type ReleaseReport =
        { Decision: ReleaseDecision
          Package: PackEvidenceSet
          Preconditions: PreconditionEvidence list
          Attestation: AttestationSummary
          ReleaseExitCodeBasis: ExitCodeBasis }

    /// The ADVISORY projection fsgg verify surfaces (FR-005): the same evidence, explicitly non-blocking.
    type VerifyReleasePreview =
        { Verdict: Verdict
          Package: PackEvidenceSet
          Preconditions: PreconditionEvidence list
          Attestation: AttestationSummary
          Advisory: bool }
```

## `Report.fsi` (draft)

```fsharp
namespace FS.GG.Governance.ReleaseReport

open FS.GG.Governance.ReleaseRules.Model            // ReleaseDecision
open FS.GG.Governance.ReleaseFactsSensing.Model      // SensedRelease
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.ReleaseReport.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Report =

    /// Assemble the single-source-of-truth report from the four already-computed inputs. Preserves the F53
    /// partition order and the F54 ordinal ordering; ReleaseExitCodeBasis = decision.ExitCodeBasis. PURE,
    /// TOTAL — never re-derives the verdict (FR-012).
    val assemble:
        decision: ReleaseDecision ->
        sensed: SensedRelease ->
        pack: PackEvidenceSet ->
        attestation: AttestationSummary ->
            ReleaseReport

    /// The advisory verify projection (FR-005): drops nothing, sets Advisory = true. The previewed Verdict is
    /// report.Decision.Verdict — advisory only; verify never blocks on it. PURE, TOTAL.
    val preview: report: ReleaseReport -> VerifyReleasePreview
```

## Behavioral guarantees (tested)

- A mergeable-but-not-releasable product (ship would pass; an unbumped version) ⇒ `Decision.Verdict = Fail`,
  `ReleaseExitCodeBasis = Blocked`, the failing precondition explicit in `Preconditions` (SC-002 / Story 2.1).
- A fully releasable product ⇒ `Verdict = Pass`, `ReleaseExitCodeBasis = Clean` (SC-002 / Story 2.2).
- `preview report` is advisory (`Advisory = true`) and carries the same evidence (SC-003 / Story 2.3).
- Report-object parity: the report is byte-stable for identical state; the JSON projections render from it and
  reordering the inputs never changes it (SC-007).
- The report never re-derives the verdict — `assemble` carries `decision` verbatim (FR-012).
