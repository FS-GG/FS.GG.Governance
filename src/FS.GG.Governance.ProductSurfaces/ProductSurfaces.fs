// The pure product-surface classification + cost-tier selection core (F23). Visibility lives in
// ProductSurfaces.fsi (Principle II) — this file carries NO top-level access modifiers; every helper
// below is hidden by its absence from the signature. `classify` is PURE and TOTAL (FR-009): no I/O, no
// git, no clock, never throws, byte-for-byte identical for identical input. It consumes the already-typed
// F014 facts and the F015 route report verbatim; it re-parses no YAML, re-routes nothing, senses no git.

namespace FS.GG.Governance.ProductSurfaces

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.ProductSurfaces.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProductSurfaces =

    let private pathStr (GovernedPath s) = s
    let private sidStr (SurfaceId s) = s

    // ── Surface membership (contracts/classification.md §1): segment-prefix OR Glob.matches ──

    /// The meaningful segments of a normalized path (drop empty and `.`). The same pure splitting F015
    /// routing and F017 findings use on the `GovernedPath` form.
    let private segments (GovernedPath s) =
        s.Split('/') |> Array.filter (fun seg -> seg <> "" && seg <> ".")

    /// A literal surface path *s* covers a candidate *p* iff *s*'s segments are a segment-wise prefix of
    /// *p*'s — i.e. *p* equals or descends from *s*. The same relation F017 uses (research D3).
    let private isWithin (surfacePath: GovernedPath) (candidate: GovernedPath) =
        let r = segments surfacePath
        let p = segments candidate
        r.Length <= p.Length && Array.forall2 (=) r p.[0 .. r.Length - 1]

    /// A surface path covers a candidate by the segment-prefix relation OR, when the surface path is a
    /// glob, by the F015 `Glob.matches` matcher (never a fresh matcher) — exactly the membership rule the
    /// classification contract names.
    let private covers (surfacePath: GovernedPath) (candidate: GovernedPath) =
        isWithin surfacePath candidate || Glob.matches surfacePath candidate

    /// A candidate path is within a surface iff it is within ANY of the surface's declared paths.
    let private withinSurface (surface: Surface) (candidate: GovernedPath) =
        surface.Paths |> List.exists (fun sp -> covers sp candidate)

    // ── Precedence over SurfaceClass (contracts/classification.md §3) — most-protected first ──
    // Lower rank = higher precedence. Product vocabulary lives HERE, in the leaf adapter, never the kernel
    // (FR-014). A new SurfaceClass case is a compile error here until it gets a rank (closed set, no
    // wildcard).

    let private classPrecedence (cls: SurfaceClass) : int =
        match cls with
        | ReleaseSurface -> 0
        | PackageSurface -> 1
        | GeneratedProductRoot -> 2
        | DesignSurface -> 3
        | SkillSurface -> 4
        | DocsSurface -> 5
        | SampleAppSurface -> 6
        | GeneratedView -> 7
        | ProtectedSurface -> 8
        | GovernedRoot -> 9
        | Routine -> 10

    // ── Baseline tier per winning kind (contracts/classification.md §4 table) ──
    // `None` ⇒ a boundary-only kind (ProtectedSurface/GovernedRoot/Routine): NOT tiered — no entry.

    let private baselineTier (cls: SurfaceClass) : GeneratedProductTier option =
        match cls with
        | DocsSurface
        | SampleAppSurface -> Some StructuralScan
        | SkillSurface
        | DesignSurface
        | GeneratedView -> Some RestoreBuild
        | PackageSurface
        | GeneratedProductRoot -> Some FocusedTests
        | ReleaseSurface -> Some FullVerify
        | ProtectedSurface
        | GovernedRoot
        | Routine -> None

    // ── Tier ordering helpers (over the closed GeneratedProductTier rank) ──

    let private allTiers =
        [ StructuralScan; RestoreBuild; FocusedTests; FullVerify; ReleaseValidation ]

    let private tierOfRank (rank: int) : GeneratedProductTier =
        // Clamp into [1,5]; total — never throws.
        let r = max 1 (min 5 rank)
        allTiers |> List.find (fun t -> generatedProductTierRank t = r)

    // ── Profile escalation (positive-match only, FR-006) — read from the declared profiles ──

    /// A profile escalates the target only when it is one this build understands AND it is declared in the
    /// policy's `Profiles` (the edge passes a declared profile; an unknown one never escalates). A
    /// release-oriented profile raises a `ReleaseSurface` target to `ReleaseValidation`; a strict profile
    /// raises the target by one rank. Neither lowers below the baseline, and neither raises a kind that did
    /// not match (the baseline is per matched kind already).
    let private escalate (facts: TypedFacts) (profile: ProfileId) (cls: SurfaceClass) (baseline: GeneratedProductTier) : GeneratedProductTier =
        let declared =
            match facts.Policy with
            | Some pol -> pol.Profiles |> List.contains profile
            | None -> false

        if not declared then
            baseline
        else
            let (ProfileId name) = profile
            match name with
            | "release" -> if cls = ReleaseSurface then ReleaseValidation else baseline
            | "strict" -> tierOfRank (generatedProductTierRank baseline + 1)
            | _ -> baseline

    // ── Snap to a declared tier + cheaper-local alternative (contracts/classification.md §4/§5) ──

    /// The declared tiered checks in a capability domain (those carrying a `Tier`).
    let private domainTieredChecks (facts: TypedFacts) (domain: DomainId) : Check list =
        facts.Capabilities.Checks
        |> List.filter (fun c -> c.Domain = domain && Option.isSome c.Tier)

    let private tierOf (c: Check) = c.Tier |> Option.get

    /// Select the cost tier and whether it was declared. Among the domain's declared tiers: the deepest
    /// not exceeding the target; else the cheapest declared; else the target with `TierIsDeclared = false`
    /// (no tiered check declared — the F24-pending non-error note, FR-016).
    let private snapTier (tieredChecks: Check list) (target: GeneratedProductTier) : GeneratedProductTier * bool =
        let tiers = tieredChecks |> List.map tierOf

        match tiers with
        | [] -> target, false
        | _ ->
            let leqTarget = tiers |> List.filter (fun t -> generatedProductTierRank t <= generatedProductTierRank target)

            let selected =
                match leqTarget with
                | [] -> tiers |> List.minBy generatedProductTierRank
                | _ -> leqTarget |> List.maxBy generatedProductTierRank

            selected, true

    /// `CheaperLocalTier t` when a strictly-cheaper, locally-runnable declared tier exists for the domain
    /// (`Environment ∈ {Local; LocalOrCi}` and `Tier < SelectedTier`); the cheapest such, so it names the
    /// quickest local check to run first. Else `NoCheaperLocalTier`. Always present (FR-007).
    let private cheaperLocal (tieredChecks: Check list) (selected: GeneratedProductTier) : TierAlternative =
        let localBelow =
            tieredChecks
            |> List.filter (fun c ->
                (c.Environment = Local || c.Environment = LocalOrCi)
                && generatedProductTierRank (tierOf c) < generatedProductTierRank selected)
            |> List.map tierOf

        match localBelow with
        | [] -> NoCheaperLocalTier
        | _ -> CheaperLocalTier(localBelow |> List.minBy generatedProductTierRank)

    // ── Explanation (contracts/classification.md §6) — deterministic; only declared ids ──

    let private explain (capability: DomainId) (cls: SurfaceClass) (tier: GeneratedProductTier) (declared: bool) (alt: TierAlternative) : string =
        let (DomainId cap) = capability

        let tierNote =
            if declared then
                generatedProductTierToken tier
            else
                sprintf "%s (no tiered check declared — evidence pending F24)" (generatedProductTierToken tier)

        let altNote =
            match alt with
            | CheaperLocalTier t -> sprintf "cheaper local: %s" (generatedProductTierToken t)
            | NoCheaperLocalTier -> "no cheaper local tier"

        sprintf "capability '%s' · %s · tier %s · %s" cap (surfaceClassToken cls) tierNote altNote

    // ── Per-routing classification ──

    /// Choose the winning surface among those covering a path and the reason it won (D6). The covering set
    /// is non-empty. Sort by (precedence rank, ordinal SurfaceId); the head wins.
    let private winnerOf (covering: Surface list) : Surface * ClassificationReason =
        let sorted =
            covering
            |> List.sortWith (fun a b ->
                let byClass = compare (classPrecedence a.Class) (classPrecedence b.Class)
                if byClass <> 0 then byClass
                else System.String.CompareOrdinal(sidStr a.Id, sidStr b.Id))

        let winner = List.head sorted

        let reason =
            match sorted with
            | [ _ ] -> OnlySurface
            | _ ->
                // The top class is unique ⇒ won on precedence; else the ordinal-first co-kind surface won.
                let topClassCount =
                    covering |> List.filter (fun s -> s.Class = winner.Class) |> List.length

                if topClassCount = 1 then HighestPrecedenceKind else OrdinalSurfaceTiebreak

        winner, reason

    /// Classify one `Routed` path, or `None` when no product surface covers it (or only a boundary-only
    /// kind wins ⇒ not tiered, no entry).
    let private classifyRouting (facts: TypedFacts) (profile: ProfileId) (path: GovernedPath) (domain: DomainId) : ProductClassification option =
        let covering = facts.Capabilities.Surfaces |> List.filter (fun s -> withinSurface s path)

        match covering with
        | [] -> None
        | _ ->
            let winner, reason = winnerOf covering

            match baselineTier winner.Class with
            | None -> None // a boundary-only kind won — not tiered, no entry (light-by-default)
            | Some baseline ->
                let target = escalate facts profile winner.Class baseline
                let tieredChecks = domainTieredChecks facts domain
                let selected, declared = snapTier tieredChecks target
                let alt = cheaperLocal tieredChecks selected

                Some
                    { Path = path
                      Capability = domain
                      Surface = winner.Id
                      Class = winner.Class
                      SelectedTier = selected
                      TierIsDeclared = declared
                      Alternative = alt
                      Reason = reason
                      Explanation = explain domain winner.Class selected declared alt }

    // ── The entry point ──

    let classify (facts: TypedFacts) (report: RouteReport) (profile: ProfileId) : ProductSurfaceReport =
        let classifications =
            report.Routings
            |> List.choose (fun r ->
                match r.Result with
                // Only a Routed path carries the capability domain a classification names; UnmatchedInRoot
                // and OutOfScope produce no entry (light-by-default, FR-004).
                | Routed(domain, _, _) -> classifyRouting facts profile r.Path domain
                | UnmatchedInRoot
                | OutOfScope -> None)
            |> List.sortWith (fun a b ->
                let byPath = System.String.CompareOrdinal(pathStr a.Path, pathStr b.Path)
                if byPath <> 0 then byPath
                else System.String.CompareOrdinal(sidStr a.Surface, sidStr b.Surface))

        { Classifications = classifications }
