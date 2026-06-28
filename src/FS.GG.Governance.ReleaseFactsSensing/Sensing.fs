// The PURE release-facts derivation (F054) — the heart of the row.
// Visibility lives in Sensing.fsi (Principle II); no `private`/`internal`/`public` modifiers here. The
// per-family classifiers (the dotted-numeric version compare, the metadata containment check, the pin
// resolution check, the posture subset check) and the snapshot builders are unexported by ABSENCE from the
// .fsi. `deriveFacts` is PURE and TOTAL (FR-008, FR-009): no I/O, clock, process, or document; defined for
// every expectation/recovered combination; never throws; structurally identical for identical input (SC-003).

namespace FS.GG.Governance.ReleaseFactsSensing

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sensing =

    let releaseFamilies =
        [ VersionBump
          PackageMetadata
          TemplatePins
          PublishPlan
          TrustedPublishing
          Provenance
          ApiCompatibility ]
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

        // 088: ApiCompatibility is sensed at the host detector overlay (cross-package worst-of, mirroring the
        // F065 pack join), NOT from repo files — so deriveFacts emits its State Unrecoverable (fail-safe: an
        // un-overlaid declared rule ⇒ Violated). It emits NO diagnostic: a diagnostic here would persist in
        // the (un-overlaid) snapshot even after the host overlays a real verdict — misleading — and would
        // drift every release.json. The host owns the ApiCompatibility evidence/finding entirely.
        let acState = Unrecoverable

        // Always all seven families (FR-009) — Map.ofList over the fixed seven (kind, state) pairs.
        let states =
            [ VersionBump, vState
              PackageMetadata, mState
              TemplatePins, piState
              PublishPlan, plState
              TrustedPublishing, tpState
              Provenance, prState
              ApiCompatibility, acState ]
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

    // ── 088 (T014): the pure ApiCompat-output parser (surface in Sensing.fsi). Total + fail-safe (FR-008). ──

    /// The first `'…'`-quoted member name in a diagnostic line, else the trimmed line.
    let extractQuoted (line: string) : string =
        let i = line.IndexOf('\'')

        if i >= 0 then
            let j = line.IndexOf('\'', i + 1)
            if j > i then line.Substring(i + 1, j - i - 1) else line.Trim()
        else
            line.Trim()

    /// `local` (default) | `inherited:<surface>` — FR-013 attribution carried from the detector annotation.
    let parseOrigin (token: string) : ApiBreakOrigin =
        if token.StartsWith("inherited:", StringComparison.OrdinalIgnoreCase) then
            ApiBreakOrigin.Inherited(SurfaceId(token.Substring("inherited:".Length)))
        else
            ApiBreakOrigin.Local

    let parseKind (token: string) : ApiBreakKind =
        match token.ToLowerInvariant() with
        | "removed" -> MemberRemoved
        | "signature" -> MemberSignatureChanged
        | "type-removed" -> TypeRemoved
        | other when other.StartsWith "other:" -> OtherIncompatibility(token.Substring(6))
        | other -> OtherIncompatibility other

    /// Recognize ONE break line — the normalized `BREAK <kind> <origin> <member…>` form OR a raw ApiCompat
    /// `CPxxxx` diagnostic (CP0001 type removed, CP0002 member removed, CP0003–CP0008 signature/accessibility).
    let parseBreakLine (line: string) : ApiBreak option =
        let upper = line.ToUpperInvariant()

        if upper.StartsWith "BREAK " then
            let parts = line.Substring(6).Trim().Split([| ' ' |], 3)

            if parts.Length >= 3 then
                Some
                    { Member = parts.[2]
                      Kind = parseKind parts.[0]
                      Origin = parseOrigin parts.[1] }
            elif parts.Length = 2 then
                Some
                    { Member = ""
                      Kind = parseKind parts.[0]
                      Origin = parseOrigin parts.[1] }
            else
                None
        elif upper.Contains "CP0001" then
            Some { Member = extractQuoted line; Kind = TypeRemoved; Origin = ApiBreakOrigin.Local }
        elif upper.Contains "CP0002" then
            Some { Member = extractQuoted line; Kind = MemberRemoved; Origin = ApiBreakOrigin.Local }
        elif [ "CP0003"; "CP0004"; "CP0005"; "CP0006"; "CP0007"; "CP0008" ] |> List.exists upper.Contains then
            Some
                { Member = extractQuoted line
                  Kind = MemberSignatureChanged
                  Origin = ApiBreakOrigin.Local }
        else
            None

    let parseApiCompatOutput (output: string) : ApiBreakSignal =
        let lines =
            if String.IsNullOrEmpty output then
                [||]
            else
                output.Replace("\r\n", "\n").Split('\n')
                |> Array.map (fun l -> l.Trim())
                |> Array.filter (fun l -> l <> "")

        if Array.isEmpty lines then
            ApiBreakSignal.Indeterminate "empty detector output" // fail-safe — NEVER NoBreakingChanges
        else
            let hasMarker (m: string) =
                lines |> Array.exists (fun l -> l.ToUpperInvariant() = m)

            // Exclusive control markers the detector emits for the non-comparison cases.
            if hasMarker "NOTPACKABLE" then
                ApiBreakSignal.NotPackable
            elif hasMarker "NOBASELINE" then
                ApiBreakSignal.NoBaseline
            else
                // Break lines are collected BEFORE the ERROR marker so a raw `error CPxxxx` line (a real break,
                // not a tool error) is classified as a break, not as Indeterminate.
                let breaks = lines |> Array.choose parseBreakLine |> Array.toList

                if not (List.isEmpty breaks) then
                    ApiBreakSignal.BreakingChanges breaks
                else
                    match lines |> Array.tryFind (fun l -> l.ToUpperInvariant().StartsWith "ERROR") with
                    | Some e ->
                        let reason = e.Substring(5).TrimStart(':', ' ')
                        ApiBreakSignal.Indeterminate(if reason = "" then "detector error" else reason)
                    | None ->
                        if hasMarker "OK" || hasMarker "NOBREAKINGCHANGES" then
                            ApiBreakSignal.NoBreakingChanges
                        else
                            ApiBreakSignal.Indeterminate "unrecognized detector output"
