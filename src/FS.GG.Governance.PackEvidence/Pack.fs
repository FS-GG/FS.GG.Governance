namespace FS.GG.Governance.PackEvidence

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.PackEvidence.Model

// The F26 packed-evidence + version-policy core (P1). PURE and TOTAL: a single linear pass over already-sensed
// pack outcomes, never reading a clock/filesystem/process. The packed artifact's version is the source of
// truth for releasability (research D1). `factContributions` feeds the EXISTING F53
// VersionBump/PackageMetadata/Provenance families — no new release-rule family (FR-003). The surface is
// Pack.fsi (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Pack =

    // ── value helpers (hidden — absent from Pack.fsi) ──

    let surfaceValue (SurfaceId s) = s

    let runOf (outcome: PackOutcome) : KindedCommandRun =
        match outcome with
        | Packed(_, run) -> run
        | PackedNoArtifact(_, _, run) -> run
        | PackFailed(_, _, run) -> run

    let surfaceOf (outcome: PackOutcome) : SurfaceId =
        match outcome with
        | Packed(art, _) -> art.Surface
        | PackedNoArtifact(surface, _, _) -> surface
        | PackFailed(surface, _, _) -> surface

    let artifactPathOf (outcome: PackOutcome) : string =
        match outcome with
        | Packed(art, _) -> art.ArtifactPath
        | PackedNoArtifact _ -> ""
        | PackFailed _ -> ""

    // ── semantic-version comparison: the single shared comparator (review M-ADPT-1) ──
    // Lives in `SemVer` (ReleaseRules) so this pack verdict and the F054 release-facts sensing can never
    // disagree; `versionPolicy`/`versionDelta`/`majorOf` below reuse it verbatim.

    // ── the public operations ──

    let versionPolicy (baseline: string option) (packed: string option) : VersionVerdict =
        match packed with
        | None -> NotPackable
        | Some p ->
            match baseline with
            | None -> NoBaseline p
            | Some b ->
                match sign (SemVer.compareVersions p b) with
                | 1 -> Bumped(b, p)
                | 0 -> Unbumped p
                | _ -> Downgraded(b, p)

    /// The product-neutral, self-explaining reason naming the project, outcome, and version basis.
    let reasonFor (surface: SurfaceId) (outcome: PackOutcome) (version: VersionVerdict) : string =
        let outcomeToken =
            match outcome with
            | Packed _ -> "packed"
            | PackedNoArtifact(_, NoArtifactEmitted, _) -> "packed but no artifact emitted"
            | PackedNoArtifact(_, ArtifactUnreadable msg, _) -> sprintf "packed but artifact unreadable: %s" msg
            | PackFailed(_, sentinel, _) -> sprintf "pack failed (sentinel exit %d)" sentinel

        let versionToken =
            match version with
            | Bumped(b, p) -> sprintf "version bumped %s -> %s" b p
            | Unbumped v -> sprintf "version unbumped at %s" v
            | Downgraded(b, p) -> sprintf "version downgraded %s -> %s" b p
            | NoBaseline p -> sprintf "no released baseline (packed %s)" p
            | NotPackable -> "no packable artifact"

        sprintf "%s: %s; %s" (surfaceValue surface) outcomeToken versionToken

    let evaluatePack (baselines: Map<SurfaceId, string>) (outcomes: PackOutcome list) : PackEvidenceSet =
        let toVerdict (outcome: PackOutcome) : PackVerdict =
            let surface = surfaceOf outcome

            let version =
                match outcome with
                | Packed(art, _) -> versionPolicy (Map.tryFind surface baselines) (Some art.PackedVersion)
                | PackedNoArtifact _
                | PackFailed _ -> NotPackable

            { Surface = surface
              Outcome = outcome
              Version = version
              Reason = reasonFor surface outcome version }

        let verdicts =
            outcomes
            |> List.map toVerdict
            |> List.sortWith (fun a b ->
                let s = String.CompareOrdinal(surfaceValue a.Surface, surfaceValue b.Surface)

                if s <> 0 then
                    s
                else
                    String.CompareOrdinal(artifactPathOf a.Outcome, artifactPathOf b.Outcome))

        { Verdicts = verdicts
          Runs = outcomes |> List.map runOf
          NoPackableProjects = List.isEmpty outcomes }

    let factContributions (set: PackEvidenceSet) : Map<ReleaseRuleKind, FactState> =
        if set.NoPackableProjects then
            Map.empty
        else
            let hasArtifact (v: PackVerdict) =
                match v.Outcome with
                | Packed _ -> true
                | _ -> false

            let versionOk (v: PackVerdict) =
                match v.Version with
                | Bumped _
                | NoBaseline _ -> true
                | Unbumped _
                | Downgraded _
                | NotPackable -> false

            let stateOf predicate =
                if set.Verdicts |> List.forall predicate then Met else Unmet

            Map.ofList
                [ VersionBump, stateOf versionOk
                  PackageMetadata, stateOf hasArtifact
                  Provenance, stateOf hasArtifact ]

    // ── 088 Breaking-Change (API-Compat) gate (surface in Pack.fsi) ──

    /// The numeric MAJOR segment of a version's core (build/pre-release stripped); a non-numeric or absent
    /// first segment counts as 0. Reuses `SemVer.splitMeta`; total, never throws.
    let majorOf (v: string) : int64 =
        let core, _ = SemVer.splitMeta v

        match core.Split('.') with
        | [||] -> 0L
        | parts ->
            match Int64.TryParse parts.[0] with
            | true, n -> n
            | _ -> 0L

    let versionDelta (baseline: string option) (packed: string option) : VersionDelta =
        match packed, baseline with
        | None, _ -> NoBaselineDelta
        | Some _, None -> NoBaselineDelta
        | Some p, Some b ->
            match sign (SemVer.compareVersions p b) with
            | 1 -> if majorOf p > majorOf b then MajorBump else MinorOrPatchBump
            | _ -> NoForwardChange // equal or downgrade — a break here is also under-bumped

    let apiCompatibilityFact (signal: ApiBreakSignal) (delta: VersionDelta) : FactState option =
        match signal with
        | ApiBreakSignal.NoBreakingChanges -> Some Met
        | ApiBreakSignal.BreakingChanges _ ->
            match delta with
            | MajorBump -> Some Met
            | MinorOrPatchBump
            | NoForwardChange
            | NoBaselineDelta -> Some Unmet // breaking change not covered by a major bump
        | ApiBreakSignal.NoBaseline -> Some Met // vacuous — never published (FR-009)
        | ApiBreakSignal.Indeterminate _ -> Some Unrecoverable // fail-safe (FR-008)
        | ApiBreakSignal.NotPackable -> None // not covered (FR-007)

    let coverageOutcome (signal: ApiBreakSignal) (delta: VersionDelta) : ApiCompatCoverageOutcome =
        match signal with
        | ApiBreakSignal.NotPackable -> NotCovered "not a packable target"
        | ApiBreakSignal.Indeterminate reason -> NotCovered(sprintf "API comparison indeterminate: %s" reason)
        | ApiBreakSignal.NoBaseline -> NoBaselineYet
        | ApiBreakSignal.NoBreakingChanges
        | ApiBreakSignal.BreakingChanges _ ->
            match apiCompatibilityFact signal delta with
            | Some fact -> Checked fact
            | None -> NotCovered "not covered"

    let apiCompatCoverage (packages: (SurfaceId * ApiBreakSignal * VersionDelta) list) : ApiCompatCoverage list =
        packages
        |> List.map (fun (surface, signal, delta) ->
            { Surface = surface
              Outcome = coverageOutcome signal delta })
        |> List.sortWith (fun a b -> String.CompareOrdinal(surfaceValue a.Surface, surfaceValue b.Surface))

    let apiCompatibilityRollup (packages: (ApiBreakSignal * VersionDelta) list) : FactState =
        let facts =
            packages |> List.choose (fun (signal, delta) -> apiCompatibilityFact signal delta)

        if facts |> List.contains Unrecoverable then Unrecoverable
        elif facts |> List.contains Unmet then Unmet
        else Met
