namespace FS.GG.Governance.Inheritance.Tests

// Real fixture builders for the WI-5 / ADR-0049 profile-bound inheritance tests. No mocks (Principle
// V): every value is a real F014/F018/F019 typed value, constructed literally. The `mkGate`/
// `mkSelectedGate`/`mkRoute` helpers follow the F025/EnforcementFixtures `Support.fs` precedent.

module Support =

    open FS.GG.Governance.Config.Model
    open FS.GG.Governance.Gates.Model
    open FS.GG.Governance.Route.Model

    /// A complete F018 `Gate` from an id and a maturity — the carried cost/timeout/owner/freshness
    /// fields are real but only `Id`/`Maturity` matter to inheritance composition.
    let mkGate (rawId: string) (maturity: Maturity) : Gate =
        let domain = DomainId "build"

        { Id = GateId rawId
          Domain = domain
          Description = sprintf "gate %s" rawId
          Prerequisites = []
          Cost = Cheap
          Timeout = TimeoutLimit 60
          Owner = Owner "team"
          Maturity = maturity
          ProductCheck = false
          FreshnessKey =
            { Check = CheckId rawId
              Domain = domain
              Cost = Cheap
              Environment = Local
              Command = None } }

    /// Wrap a `Gate` as a `SelectedGate` with a representative selection trace.
    let mkSelectedGate (gate: Gate) : SelectedGate =
        { Gate = gate
          SelectingPaths = [ { Path = GovernedPath "src/a.fs"; MatchedGlob = GovernedPath "src/**" } ] }

    /// A `RouteResult` from selected gates; findings empty and cost all-zero (never read here).
    let mkRoute (gates: SelectedGate list) : RouteResult =
        { SelectedGates = gates
          Findings = { Findings = [] }
          Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

    /// A minimal, valid-shaped `TypedFacts` whose single `generatedProduct` surface carries the given
    /// `templateProfile` (or none). Only `Capabilities.Surfaces` is read by `productTemplateProfiles`;
    /// the rest is a coherent skeleton.
    let factsWithProfiles (profiles: string list) : TypedFacts =
        let surfaces =
            profiles
            |> List.mapi (fun i p ->
                { Id = SurfaceId(sprintf "product-%d" i)
                  Class = GeneratedProductRoot
                  Paths = [ GovernedPath "." ]
                  Owner = Owner "platform"
                  Maturity = Warn
                  EvidenceTag = None
                  TemplateProfile = Some(TemplateProfile p)
                  Baseline = None })

        { Project =
            { SchemaVersion = SchemaVersion 1
              Id = ProjectId "product-under-test"
              Domains = [ DomainId "build" ]
              GovernedRoot = GovernedPath "."
              PackageSurfaces = []
              PolicyRef = None
              CapabilitiesRef = None }
          Policy = None
          Capabilities =
            { SchemaVersion = SchemaVersion 2
              Domains = [ DomainId "build" ]
              PathMap = []
              Surfaces = surfaces
              Checks = [] }
          Tooling = None }
