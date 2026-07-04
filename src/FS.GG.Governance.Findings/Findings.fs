// The `findUnknownGovernedPaths` entry point for unknown-governed-path findings (F017). The
// public surface is fixed by Findings.fsi (Principle II) — only `findUnknownGovernedPaths` is
// exported; every helper below is hidden by the signature (no access modifiers needed).
// `findUnknownGovernedPaths` is PURE and TOTAL (FR-011, FR-012): no I/O, no git, no clock,
// never throws, byte-for-byte identical for identical input (FR-009, SC-004). It consumes the
// already-typed F014 facts and the F015 routing outcomes; it re-parses no YAML, re-normalizes
// no path, re-validates no catalog, and senses no git (FR-011, FR-014). This is the decision
// F015 deferred (its `UnmatchedInRoot` "carries no domain and asserts no finding/severity").
//
// The whole decision is the documented precedence ladder Protected > Routine > Ordinary
// (contracts/precedence.md); this file implements and is tested against it, it does not
// re-decide it.

namespace FS.GG.Governance.Findings

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Findings =

    let pathStr (GovernedPath s) = s
    let sidStr (SurfaceId s) = s

    // ── Surface membership: the segment-prefix relation (T011, precedence.md §"Surface membership") ──

    /// Split a normalized path into its meaningful segments: split on '/', drop empty and `.`
    /// segments. The same pure splitting F015's `inRoot` used — decided on the normalized
    /// `GovernedPath` form only, never raw or host paths (FR-014).
    let segments (GovernedPath s) =
        s.Split('/') |> Array.filter (fun seg -> seg <> "" && seg <> ".")

    /// A candidate path *p* is within a single declared surface path *s* iff *s*'s segments are a
    /// (segment-wise) prefix of *p*'s segments — i.e. *p* equals or is a descendant of *s*. The
    /// same relation F015 used for the governed root, reproduced locally (research D3). It does
    /// NOT re-derive the governed root; it trusts the routing outcome.
    let isWithin (surfacePath: GovernedPath) (candidate: GovernedPath) =
        let r = segments surfacePath
        let p = segments candidate
        r.Length <= p.Length && Array.forall2 (=) r p.[0 .. r.Length - 1]

    /// A candidate path is within a surface iff it is within ANY of the surface's declared paths.
    /// A surface with an empty `Paths` list matches nothing (`List.exists` over `[]`).
    let withinSurface (surface: Surface) (candidate: GovernedPath) =
        surface.Paths |> List.exists (fun sp -> isWithin sp candidate)

    /// The ordinal-first `SurfaceId` among a non-empty set of matching surfaces (the documented
    /// tiebreak; independent of authoring order). Compares by `String.CompareOrdinal` on the
    /// underlying string.
    let ordinalFirstId (surfaces: Surface list) =
        surfaces
        |> List.map (fun s -> s.Id)
        |> List.sortWith (fun a b -> System.String.CompareOrdinal(sidStr a, sidStr b))
        |> List.head

    // ── Messages (T017/T023, precedence.md §"Messages", FR-008/SC-006) ──
    // Each message names the offending normalized path and offers ≥1 concrete remediation. The
    // protected message names the escalating surface and, on a routine overlap, the routine
    // surface plus the precedence that applied. No raw YAML, host path, timestamp, or product
    // vocabulary beyond declared ids.

    let ordinaryMessage (path: GovernedPath) =
        sprintf
            "Path '%s' is inside the governed root but no capability glob classified it and no declared surface covers it. Declare a path-map glob that matches it, mark the region routine, or classify the surface."
            (pathStr path)

    let protectedMessage (path: GovernedPath) (protectedId: SurfaceId) (routineId: SurfaceId option) =
        let head =
            sprintf
                "Path '%s' is inside the protected surface '%s' but no capability glob classified it. Declare a path-map glob that matches it or classify the surface."
                (pathStr path)
                (sidStr protectedId)

        match routineId with
        | None -> head
        | Some rid ->
            sprintf
                "%s It is also declared within the routine surface '%s', but protected precedence applied (Protected > Routine), so it is escalated rather than suppressed; resolve the contradictory declaration."
                head
                (sidStr rid)

    // ── The precedence ladder for an `UnmatchedInRoot` path (T016/T019/T022, precedence.md) ──

    /// Classify one `UnmatchedInRoot` candidate path against the declared surfaces by the
    /// `Protected > Routine > Ordinary` ladder. Returns `None` when the path is routine-suppressed
    /// (and not protected); otherwise `Some` finding. Pure and total.
    let classifyUnmatched
        (protectedSurfaces: Surface list)
        (routineSurfaces: Surface list)
        (path: GovernedPath)
        : UnknownGovernedPathFinding option =
        let matchedProtected = protectedSurfaces |> List.filter (fun s -> withinSurface s path)
        let matchedRoutine = routineSurfaces |> List.filter (fun s -> withinSurface s path)

        match matchedProtected with
        | _ :: _ ->
            // Rung 1 — escalate. Protected outranks routine: an overlapping routine+protected path
            // is escalated, never silenced (FR-006/FR-007).
            let sid = ordinalFirstId matchedProtected

            let routineId =
                match matchedRoutine with
                | [] -> None
                | _ -> Some(ordinalFirstId matchedRoutine)

            Some
                { Id = UnknownProtectedBoundaryPath
                  Path = path
                  Zone = ProtectedBoundaryUnknown sid
                  Message = protectedMessage path sid routineId }
        | [] ->
            match matchedRoutine with
            | _ :: _ ->
                // Rung 2 — suppress. An explicitly-declared unmanaged region is, by declaration,
                // not an unknown governed path (FR-004).
                None
            | [] ->
                // Rung 3 — ordinary in-root unknown (FR-002). Inert surface classes
                // (GovernedRoot/GeneratedView/ReleaseSurface) neither suppress nor escalate, so a
                // path covered only by them falls through to here.
                Some
                    { Id = UnknownGovernedPath
                      Path = path
                      Zone = GovernedRootUnknown
                      Message = ordinaryMessage path }

    // ── Deduplication + deterministic ordering (T012, precedence.md §"Deduplication"/§"Ordering") ──

    /// Group routings by normalized path and keep one per path (FR-010). The kept value is
    /// unambiguous: `Routing.route` is a pure function of the path, so every duplicate in a
    /// path-group carries an identical `RoutingResult`; `List.groupBy` preserves first-occurrence
    /// order and the choice is value-immaterial, so permutation-invariance holds.
    let dedupRoutings (routings: PathRouting list) =
        routings
        |> List.groupBy (fun r -> pathStr r.Path)
        |> List.map (fun (_, group) -> List.head group)

    /// Sort findings by normalized path (ordinal) then finding-id token (ordinal) — the defensive
    /// secondary key, since two findings can never share a path after dedup (FR-009, SC-004).
    let sortFindings (findings: UnknownGovernedPathFinding list) =
        findings
        |> List.sortWith (fun a b ->
            let byPath = System.String.CompareOrdinal(pathStr a.Path, pathStr b.Path)

            if byPath <> 0 then
                byPath
            else
                System.String.CompareOrdinal(findingIdToken a.Id, findingIdToken b.Id))

    // ── Escalating-boundary set (F23 FR-003, SC-002) ──

    /// The surface classes that escalate an unclassified in-root path to `UnknownProtectedBoundaryPath`
    /// rather than an ordinary `UnknownGovernedPath`. F23 widens this from `ProtectedSurface` alone to the
    /// full protected-boundary set so a NEW protected surface (`package`/`release`/`generatedProduct`)
    /// placed under a governed root with no path-map glob is never a silent pass. The four non-protected
    /// product kinds (`docs`/`skill`/`design`/`sampleApp`) and the inert MVP classes stay ordinary. Closed
    /// match, no wildcard — a future `SurfaceClass` case is a compile error here until it is placed.
    let isEscalatingBoundary (cls: SurfaceClass) : bool =
        match cls with
        | ProtectedSurface
        | PackageSurface
        | ReleaseSurface
        | GeneratedProductRoot -> true
        | Routine
        | GovernedRoot
        | GeneratedView
        | DocsSurface
        | SkillSurface
        | DesignSurface
        | SampleAppSurface -> false

    // ── The entry point (T013, FR-001/FR-011/FR-012) ──

    let findUnknownGovernedPaths (facts: TypedFacts) (report: RouteReport) : FindingReport =
        let surfaces = facts.Capabilities.Surfaces
        let protectedSurfaces = surfaces |> List.filter (fun s -> isEscalatingBoundary s.Class)
        let routineSurfaces = surfaces |> List.filter (fun s -> s.Class = Routine)

        let findings =
            dedupRoutings report.Routings
            |> List.choose (fun r ->
                match r.Result with
                // Routed — never a finding, even on a protected boundary (FR-005). OutOfScope —
                // never a finding; no global default-deny (FR-003). Diagnostics are not consumed.
                | Routed _ -> None
                | OutOfScope -> None
                | UnmatchedInRoot -> classifyUnmatched protectedSurfaces routineSurfaces r.Path)
            |> sortFindings

        { Findings = findings }
