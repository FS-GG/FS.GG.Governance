namespace FS.GG.Governance.Inheritance

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model

// ADR-0049 / WI-5: profile-bound gate inheritance. Visibility lives in Inheritance.fsi (Principle II);
// this file carries NO top-level access modifiers — the reference-floor table (`referenceChecks`) and
// the `TypedFacts` skeleton (`refFacts`) are hidden by their ABSENCE from the .fsi (the Enforcement.fs
// `maturityFloor`/`profileTighten` hidden-helper precedent). PURE and TOTAL: no I/O, no git, no clock;
// deterministic; never throws. Inherited gates are single-sourced through `Gates.buildRegistry`.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Inheritance =

    // ── The embedded org-owned reference floor (hidden; absent from the .fsi) ──

    // The reference checks bound to a template-profile. `buildRegistry` projects these into gates
    // identically to a product's own declared checks (FR-009). publish-before-flip (FR-008): the `game`
    // gameplay gate binds at `warn` — non-blocking, so this contract changes no product's ship verdict;
    // WI-8 (FS-GG/FS.GG.Governance#276) raises it to `block-on-ship`. An unknown/unbound profile yields
    // `[]` — never a fabricated gate.
    let referenceChecks (profile: TemplateProfile) : Check list =
        let (TemplateProfile p) = profile

        match p with
        | "game" ->
            // The per-FR gameplay-obligation gate every `game` product inherits as a floor (epic
            // FS-GG/.github#1190). Command left unbound (no `tooling.yml` dependency) — the reference
            // sample's `gameplay:fr-covered` check carries the identical command-free, `block-on-ship`
            // shape. WI-8 (FS-GG/FS.GG.Governance#276) flipped the maturity from `warn` to
            // `block-on-ship`, the ADR-0049 profile binding at full teeth (WI-7's reference-game proof
            // green): every `game` product now inherits this gate as a NON-LOWERABLE block-on-ship floor.
            [ { Id = CheckId "fr-covered"
                Domain = DomainId "gameplay"
                Command = None
                Owner = Owner "platform"
                Cost = High
                Environment = Ci
                Maturity = BlockOnShip
                Tier = None } ]
        | _ -> []

    // A minimal `TypedFacts` carrying ONLY the reference checks. `Gates.buildRegistry` reads only
    // `Capabilities.Checks` (and `Tooling.Commands`, absent here) — everything else is ignored — so the
    // rest of this skeleton never surfaces. Hidden; absent from the .fsi.
    let refFacts (checks: Check list) : TypedFacts =
        let domains = checks |> List.map (fun c -> c.Domain) |> List.distinct

        { Project =
            { SchemaVersion = SchemaVersion 1
              Id = ProjectId "fsgg-reference-gate-set"
              Domains = domains
              GovernedRoot = GovernedPath "."
              PackageSurfaces = []
              PolicyRef = None
              CapabilitiesRef = None }
          Policy = None
          Capabilities =
            { SchemaVersion = SchemaVersion 2
              Domains = domains
              PathMap = []
              Surfaces = []
              Checks = checks }
          Tooling = None }

    // ── Public surface ──

    let referenceGatesFor (profile: TemplateProfile) : Gate list =
        match referenceChecks profile with
        | [] -> []
        | checks -> (Gates.buildRegistry(refFacts checks)).Gates

    let productTemplateProfiles (facts: TypedFacts) : TemplateProfile list =
        facts.Capabilities.Surfaces
        |> List.choose (fun s -> s.TemplateProfile)
        |> List.distinct
        |> List.sortBy (fun (TemplateProfile p) -> p)

    let inheritedGatesFor (facts: TypedFacts) : Gate list =
        productTemplateProfiles facts
        |> List.collect referenceGatesFor
        |> List.distinctBy (fun g -> gateIdValue g.Id)
        |> List.sortBy (fun g -> gateIdValue g.Id)

    let composeEffectiveGates (inherited: Gate list) (local: Gate list) : Gate list =
        // shared id -> local gate at the STRICTER maturity (local may raise, never lower the floor);
        // inherited-only -> added; local-only -> kept. Sorted by `GateId` ordinal (determinism).
        let inheritedById = inherited |> List.map (fun g -> gateIdValue g.Id, g) |> Map.ofList

        let raisedLocal =
            local
            |> List.map (fun lg ->
                match Map.tryFind (gateIdValue lg.Id) inheritedById with
                | Some ig when maturityRank ig.Maturity > maturityRank lg.Maturity -> { lg with Maturity = ig.Maturity }
                | _ -> lg)

        let localIds = local |> List.map (fun g -> gateIdValue g.Id) |> Set.ofList

        let inheritedOnly =
            inherited |> List.filter (fun ig -> not (Set.contains (gateIdValue ig.Id) localIds))

        raisedLocal @ inheritedOnly |> List.sortBy (fun g -> gateIdValue g.Id)

    let applyInheritance (facts: TypedFacts) (route: RouteResult) : RouteResult =
        match inheritedGatesFor facts with
        // Identity: no bound template-profile / no binding => the route is returned byte-for-byte,
        // mirroring the existing pre-rollup consume-union fold's absent-handoff identity.
        | [] -> route
        | inherited ->
            let local = route.SelectedGates |> List.map (fun sg -> sg.Gate)
            let effective = composeEffectiveGates inherited local

            let existingById =
                route.SelectedGates
                |> List.map (fun sg -> gateIdValue sg.Gate.Id, sg)
                |> Map.ofList

            let selected =
                effective
                |> List.map (fun g ->
                    match Map.tryFind (gateIdValue g.Id) existingById with
                    // Preserve the selection trace; apply any maturity the floor raised.
                    | Some sg -> { sg with Gate = g }
                    // Inherited-only: present because inherited, not path-selected (empty trace).
                    | None -> { Gate = g; SelectingPaths = [] })

            { route with SelectedGates = selected }
