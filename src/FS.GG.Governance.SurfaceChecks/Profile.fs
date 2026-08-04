namespace FS.GG.Governance.SurfaceChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement

// docs/decisions/0012: the composed F# constitution profile. Visibility lives in Profile.fsi
// (Principle II) — this file carries NO access modifiers on top-level bindings. PURE and TOTAL.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Profile =

    type Pack =
        | PublicSurface
        | IdiomaticSimplicity
        | EffectBoundary
        | EvidenceBoundary

    let profileKey = TemplateProfile "fsharp-constitution"

    let domain = DomainId "fsharp"

    // Ascending by `packToken`, so `checks` is sorted by construction and the generated YAML region
    // is byte-stable without a sort at the render edge.
    let packs =
        [ EffectBoundary; EvidenceBoundary; IdiomaticSimplicity; PublicSurface ]

    let packToken (pack: Pack) =
        match pack with
        | PublicSurface -> "public-surface"
        | IdiomaticSimplicity -> "idiomatic-simplicity"
        | EffectBoundary -> "effect-boundary"
        | EvidenceBoundary -> "evidence-boundary"

    let authoringItem (pack: Pack) =
        match pack with
        | PublicSurface -> "FS-GG/FS.GG.Governance#366"
        | IdiomaticSimplicity -> "FS-GG/FS.GG.Governance#368"
        | EffectBoundary -> "FS-GG/FS.GG.Governance#369"
        | EvidenceBoundary -> "FS-GG/FS.GG.Governance#370"

    // ── The composed rule table ──────────────────────────────────────────────────────────────
    // Each pack's identities exactly as that pack authored them. `ReferenceProfileComposition` C1
    // executes each pack's real evaluator and asserts every code it emits appears here, so this
    // table cannot quietly fall behind the packs it claims to compose.
    //
    // #366's `fsharp.surface-malformed` and #369's `fsharp.effect-boundary-malformed` are emitted
    // by the pack's CALLER on an unreadable project (VerifyCommand/Interpreter.fs), not by the
    // evaluator. They are the packs' fail-closed input-state identities and belong to the packs
    // that own them; leaving them out would make the profile's inventory a partial one.

    let ruleIds (pack: Pack) =
        match pack with
        | PublicSurface ->
            [ "fsharp.exemption-invalid"
              "fsharp.signature-compile-order"
              "fsharp.signature-docs"
              "fsharp.signature-missing"
              "fsharp.signature-source-mismatch"
              "fsharp.surface-baseline-stale"
              "fsharp.surface-malformed" ]
        | IdiomaticSimplicity ->
            // `CodeChecks.Model.findingIdToken` is the pack's own closed DU projection; these are
            // its twelve tokens verbatim. C2 asserts the two lists are equal, so a case added to
            // the DU without a row here reds the composition rather than shipping a profile that
            // silently under-declares the pack.
            [ "abstract-class-hierarchy"
              "compiler-analysis-failed"
              "dependency-fan-out-review"
              "duplicate-home-grown-abstraction"
              "imperative-in-pure-domain"
              "inheritance-hierarchy"
              "member-size-review"
              "module-size-review"
              "public-class-shape"
              "reflection-or-metaprogramming"
              "shared-mutable-state"
              "type-size-review" ]
        | EffectBoundary ->
            [ "fsharp.callback-hidden-state"
              "fsharp.effect-boundary-malformed"
              "fsharp.effect-delivery-missing"
              "fsharp.effect-edge-missing"
              "fsharp.effect-exemption-invalid"
              "fsharp.effect-idempotency-missing"
              "fsharp.effect-in-transition"
              "fsharp.effect-result-message-missing"
              "fsharp.effect-retry-missing"
              "fsharp.exception-continuation" ]
        | EvidenceBoundary ->
            [ "evidence.command-failed"
              "evidence.generated-compatibility-missing"
              "evidence.generated-consumer-missing"
              "evidence.generated-regeneration-nondeterministic"
              "evidence.generated-source-missing"
              "evidence.observed-outcome-missing"
              "evidence.optional-outcome-missing"
              "evidence.partial-write"
              "evidence.producer-inventory-empty"
              "evidence.producer-mutation-incomplete"
              "evidence.provenance-incomplete"
              "evidence.real-boundary-required"
              "evidence.render-not-executed"
              "evidence.render-receipt-unstable"
              "evidence.synthetic-undisclosed"
              "evidence.unknown-input" ]

    // ── Maturity: the packs' declared disagreement, resolved as APPLICABILITY ─────────────────
    // #366 binds at `Warn` and #369 at `BlockOnPr`. That is not a conflict to break by precedence,
    // and the composition deliberately does NOT flatten them to one value: they are different
    // rules, and what differs is APPLICABILITY, not severity taste.
    //
    //   #366 sweeps EVERY compiled `.fsproj` in the repository unconditionally
    //   (VerifyCommand/Interpreter.fs — the sweep runs whether or not a product declared a design
    //   surface, on purpose, so an executable such as Rogue3 is governed). Binding that at
    //   block-on-pr would block every adopting repository on the day it resolved the profile. It is
    //   a dated migration at `Warn`, exactly the posture #366 shipped and #385's Notes preserve.
    //
    //   #369 fires ONLY where an author has opted a transition in with a `// fsgg:effect-boundary`
    //   marker (FSharpEffectBoundary.fs — parsers, validators and thin adapters are excluded by
    //   sensed fact). `BlockOnPr` therefore binds only work that declared itself in scope, and no
    //   repository acquires a blocking gate by resolving the profile.
    //
    // #368 and #370 join at `Warn`: both are new to the composed profile and neither has a
    // production consumer yet (docs/decisions/0011 §Consequences records that gap), so advisory
    // first, per #366's precedent. None of the four is a permanent blanket exemption — the dated
    // route to blocking is the profile's own maturity, raised here and republished, which every
    // consumer picks up by re-pinning.

    let declaredMaturity (pack: Pack) =
        match pack with
        | PublicSurface -> Warn
        | IdiomaticSimplicity -> Warn
        | EffectBoundary -> BlockOnPr
        | EvidenceBoundary -> Warn

    let maturityRationale (pack: Pack) =
        match pack with
        | PublicSurface ->
            "advisory during migration: the pack sweeps every compiled project unconditionally, so blocking on adoption would block every adopting repository (#366's shipped posture)"
        | IdiomaticSimplicity ->
            "advisory: composed into the profile by #385 and not yet reached by a production consumer, so it is surfaced before it blocks (#366's precedent)"
        | EffectBoundary ->
            "blocking on PR: the pack is applicable only where an author opted a transition in with an in-source effect-boundary marker, so no repository acquires a blocking gate merely by resolving the profile (#369's shipped posture)"
        | EvidenceBoundary ->
            "advisory: composed into the profile by #385 and not yet reached by a production consumer, so it is surfaced before it blocks (#366's precedent)"

    // ── Ownership lookup ─────────────────────────────────────────────────────────────────────
    // Built by folding `packs`, so it cannot name a pack the profile does not contain. On a
    // collision the FIRST pack in `packs` order wins the map entry — deterministic rather than
    // arbitrary — but the well-formedness of the table does not rest on that: `collisions()` is
    // asserted empty, so the tie-break is unreachable in a valid profile and exists only so this
    // lookup stays total if one is ever introduced.

    let ownerByRuleId: Map<string, Pack> =
        packs
        |> List.collect (fun pack -> ruleIds pack |> List.map (fun id -> id, pack))
        |> List.fold (fun acc (id, pack) -> if Map.containsKey id acc then acc else Map.add id pack acc) Map.empty

    let ruleOwner (ruleId: string) : Pack option = Map.tryFind ruleId ownerByRuleId

    let maturityOf (ruleId: string) : Maturity option =
        ruleOwner ruleId |> Option.map declaredMaturity

    // ── The published gate set ───────────────────────────────────────────────────────────────

    let packCost (pack: Pack) =
        match pack with
        // The compiler-service parse/type-check sweep is the expensive one; the rest read sources.
        | IdiomaticSimplicity -> High
        | PublicSurface -> Medium
        | EffectBoundary -> Medium
        | EvidenceBoundary -> Cheap

    let checkFor (pack: Pack) : Check =
        { Id = CheckId(packToken pack)
          Domain = domain
          // Command-unbound by design, exactly like the `gameplay` floor: the pack's own sensing
          // and the handoff evidence satisfy it, not a `tooling.yml` command a consumer could
          // redefine. It also keeps the published set free of a dangling command reference in every
          // repository that resolves it without adopting this repo's tooling.
          Command = None
          Owner = Owner "platform"
          Cost = packCost pack
          Environment = Ci
          Maturity = declaredMaturity pack
          Tier = None }

    let checks: Check list =
        packs |> List.map checkFor |> List.sortBy (fun c -> let (CheckId id) = c.Id in id)

    // ── Conflict resolution ──────────────────────────────────────────────────────────────────

    type Collision = { RuleId: string; Packs: Pack list }

    let collisions () : Collision list =
        packs
        |> List.collect (fun pack -> ruleIds pack |> List.map (fun id -> id, pack))
        |> List.groupBy fst
        |> List.choose (fun (id, entries) ->
            match entries |> List.map snd with
            | _ :: _ :: _ as owners -> Some { RuleId = id; Packs = owners }
            | _ -> None)
        |> List.sortBy (fun c -> c.RuleId)

    let composeMaturity (left: Maturity) (right: Maturity) : Maturity =
        if maturityRank right > maturityRank left then right else left

    // ── Normalization ────────────────────────────────────────────────────────────────────────

    let findingOf
        (pack: Pack)
        (ruleId: string)
        (request: Model.SurfaceCheckRequest)
        (source: GovernedPath)
        (detail: string)
        (message: string)
        : Model.SurfaceFinding =
        // An id this pack does not declare is an INPUT-STATE finding, not a rule violation and not
        // a silent drop: the composition could not attribute it, and says so at the pack's own
        // maturity with the id in the message.
        let declared = ruleIds pack |> List.contains ruleId

        let text =
            if declared then
                message
            else
                sprintf
                    "rule id '%s' is not declared by the '%s' pack of the composed F# constitution profile (%s); the finding is reported as malformed input rather than attributed to a rule that does not exist: %s"
                    ruleId
                    (packToken pack)
                    (authoringItem pack)
                    message

        Model.mkFinding
            Model.DesignDomain
            (declaredMaturity pack)
            request
            source
            ruleId
            detail
            Blocking
            (not declared)
            text
