// The PURE release-facts derivation (F054) — the heart of the row.
// Visibility lives in Sensing.fsi (Principle II); no `private`/`internal`/`public` modifiers here. The
// per-family classifiers (the dotted-numeric version compare, the metadata containment check, the pin
// resolution check, the posture subset check) and the snapshot builders are unexported by ABSENCE from the
// .fsi. `deriveFacts` is PURE and TOTAL (FR-008, FR-009): no I/O, clock, process, or document; defined for
// every expectation/recovered combination; never throws; structurally identical for identical input (SC-003).

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sensing =

    let releaseFamilies =
        [ VersionBump
          PackageMetadata
          TemplatePins
          PublishPlan
          TrustedPublishing
          Provenance ]
        |> List.sortBy Release.releaseRuleKindOrdinal

    // ── Deterministic ordering helpers (D7) — ordinal so order never depends on culture ──

    let ordinalSort (xs: string list) : string list =
        xs |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))

    let ordinalSortByKey (xs: (string * string) list) : (string * string) list =
        xs |> List.sortWith (fun (a, _) (b, _) -> System.String.CompareOrdinal(a, b))

    // ── Diagnostic reasons (product-neutral text keyed by the F053 wire token) — D6 ──

    let noExpectationReason (kind: ReleaseRuleKind) : string =
        sprintf "no expectation declared for %s" (Release.releaseRuleKindToken kind)

    let unrecoverableReason (kind: ReleaseRuleKind) (reason: string) : string =
        sprintf "%s evidence unrecoverable: %s" (Release.releaseRuleKindToken kind) reason

    // ── The one family that needs an ordering: a small total dotted-numeric version compare (D6) ──
    // Each dotted segment is parsed as a non-negative integer (a non-numeric segment counts as 0), the two
    // segment lists are zero-padded to equal length, then compared lexicographically. Total — never throws.
    let compareVersions (a: string) (b: string) : int =
        let parts (v: string) =
            v.Split('.')
            |> Array.map (fun p ->
                match System.Int32.TryParse p with
                | true, n -> n
                | _ -> 0)
            |> List.ofArray

        let pa = parts a
        let pb = parts b
        let n = max (List.length pa) (List.length pb)
        let pad xs = xs @ List.replicate (n - List.length xs) 0
        compare (pad pa) (pad pb)

    // ── Per-family derivation: each yields (state, snapshot fact option, diagnostic option) — D6 ──
    // The shared D6 shape: an absent expectation OR an `Error` recovery ⇒ `Unrecoverable`/`None`/diagnostic
    // (fail-safe, never a fabricated `Met`); a recovered evidence that satisfies its expectation ⇒ `Met` +
    // the observed fact; recovered-but-unsatisfied ⇒ `Unmet` + the observed fact.

    let deriveVersion (exp: ReleaseExpectations) (recovered: RecoveredEvidence) =
        match exp.VersionBaseline, recovered.Version with
        | None, _ -> Unrecoverable, None, Some { Family = VersionBump; Reason = noExpectationReason VersionBump }
        | _, Error reason ->
            Unrecoverable, None, Some { Family = VersionBump; Reason = unrecoverableReason VersionBump reason }
        | Some baseline, Ok ev ->
            let fact = { Observed = ev.Declared; Baseline = baseline }
            let satisfied = compareVersions ev.Declared baseline > 0
            (if satisfied then Met else Unmet), Some fact, None

    let deriveMetadata (exp: ReleaseExpectations) (recovered: RecoveredEvidence) =
        match exp.RequiredMetadataFields, recovered.Metadata with
        | None, _ ->
            Unrecoverable, None, Some { Family = PackageMetadata; Reason = noExpectationReason PackageMetadata }
        | _, Error reason ->
            Unrecoverable, None, Some { Family = PackageMetadata; Reason = unrecoverableReason PackageMetadata reason }
        | Some required, Ok ev ->
            let missing = required |> List.filter (fun f -> not (List.contains f ev.PresentFields))
            let fact = { Present = ordinalSort ev.PresentFields; Missing = ordinalSort missing }
            (if List.isEmpty missing then Met else Unmet), Some fact, None

    let derivePins (exp: ReleaseExpectations) (recovered: RecoveredEvidence) =
        match exp.ExpectedPins, recovered.Pins with
        | None, _ -> Unrecoverable, None, Some { Family = TemplatePins; Reason = noExpectationReason TemplatePins }
        | _, Error reason ->
            Unrecoverable, None, Some { Family = TemplatePins; Reason = unrecoverableReason TemplatePins reason }
        | Some expected, Ok ev ->
            let drifted =
                expected
                |> Map.toList
                |> List.filter (fun (k, v) ->
                    match Map.tryFind k ev.Resolved with
                    | Some resolved -> resolved <> v
                    | None -> true)
                |> List.map fst
            let fact =
                { Resolved = ordinalSortByKey (Map.toList ev.Resolved)
                  Expected = ordinalSortByKey (Map.toList expected)
                  Drifted = ordinalSort drifted }
            (if List.isEmpty drifted then Met else Unmet), Some fact, None

    // The three posture/config/provenance families share one present-token subset rule (D6).
    let derivePosture (family: ReleaseRuleKind) (required: string list option) (recovered: Result<PostureEvidence, string>) =
        match required, recovered with
        | None, _ -> Unrecoverable, None, Some { Family = family; Reason = noExpectationReason family }
        | _, Error reason -> Unrecoverable, None, Some { Family = family; Reason = unrecoverableReason family reason }
        | Some req, Ok ev ->
            let missing = req |> List.filter (fun t -> not (List.contains t ev.Observed))
            let fact =
                { Observed = ordinalSort ev.Observed
                  Required = ordinalSort req
                  Missing = ordinalSort missing }
            (if List.isEmpty missing then Met else Unmet), Some fact, None

    let deriveFacts (expectations: ReleaseExpectations) (recovered: RecoveredEvidence) : SensedRelease =
        let vState, vFact, vDiag = deriveVersion expectations recovered
        let mState, mFact, mDiag = deriveMetadata expectations recovered
        let piState, piFact, piDiag = derivePins expectations recovered
        let plState, plFact, plDiag =
            derivePosture PublishPlan expectations.RequiredPublishPosture recovered.PublishPlan
        let tpState, tpFact, tpDiag =
            derivePosture TrustedPublishing expectations.RequiredTrustedPublishing recovered.TrustedPublishing
        let prState, prFact, prDiag =
            derivePosture Provenance expectations.RequiredProvenance recovered.Provenance

        // Always all six families (FR-009) — Map.ofList over the fixed six (kind, state) pairs.
        let states =
            [ VersionBump, vState
              PackageMetadata, mState
              TemplatePins, piState
              PublishPlan, plState
              TrustedPublishing, tpState
              Provenance, prState ]
            |> Map.ofList

        let diagnostics =
            [ vDiag; mDiag; piDiag; plDiag; tpDiag; prDiag ]
            |> List.choose id
            |> List.sortBy (fun d -> Release.releaseRuleKindOrdinal d.Family)

        { Facts = { States = states }
          Snapshot =
            { Surface = expectations.Surface
              Version = vFact
              Metadata = mFact
              Pins = piFact
              PublishPlan = plFact
              TrustedPublishing = tpFact
              Provenance = prFact
              Diagnostics = diagnostics } }
