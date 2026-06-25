namespace FS.GG.Governance.ReleaseReport

open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.ReleaseReport.Model

// The F26 publication-report assembly (P1). PURE and TOTAL: assembles the immutable single source of truth
// from the four already-computed inputs. Carries the F53 ReleaseDecision VERBATIM (never re-derives a verdict
// or exit-code basis, FR-012); projects the F54 SensedRelease facts into ordered PreconditionEvidence; names
// ReleaseExitCodeBasis at the report level so publication reads as first-class and distinct from ship
// (FR-004). The surface is Report.fsi (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Report =

    // ── precondition projection (hidden — absent from Report.fsi) ──

    let private stateToken (state: FactState) : string =
        match state with
        | Met -> "met"
        | Unmet -> "unmet"
        | Unrecoverable -> "unrecoverable"

    /// The self-explaining, product-neutral reason naming the family + basis, enriched by the matching
    /// sensing diagnostic when one is present.
    let private preconditionReason (sensed: SensedRelease) (kind: ReleaseRuleKind) (state: FactState) : string =
        let token = Release.releaseRuleKindToken kind

        match sensed.Snapshot.Diagnostics |> List.tryFind (fun d -> d.Family = kind) with
        | Some d -> sprintf "%s: %s (%s)" token (stateToken state) d.Reason
        | None -> sprintf "%s: %s" token (stateToken state)

    let private preconditionsOf (sensed: SensedRelease) : PreconditionEvidence list =
        sensed.Facts.States
        |> Map.toList
        |> List.sortBy (fun (kind, _) -> Release.releaseRuleKindOrdinal kind)
        |> List.map (fun (kind, state) ->
            { Kind = kind
              State = state
              Reason = preconditionReason sensed kind state })

    // ── the public operations ──

    let assemble
        (decision: ReleaseDecision)
        (sensed: SensedRelease)
        (pack: PackEvidenceSet)
        (attestation: AttestationSummary)
        : ReleaseReport =
        { Decision = decision
          Sensed = sensed
          Package = pack
          Preconditions = preconditionsOf sensed
          Attestation = attestation
          ReleaseExitCodeBasis = decision.ExitCodeBasis }

    let preview (report: ReleaseReport) : VerifyReleasePreview =
        { Verdict = report.Decision.Verdict
          Package = report.Package
          Preconditions = report.Preconditions
          Attestation = report.Attestation
          Advisory = true }
