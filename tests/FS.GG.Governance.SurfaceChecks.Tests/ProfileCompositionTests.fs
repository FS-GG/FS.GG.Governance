module FS.GG.Governance.SurfaceChecks.Tests.ProfileCompositionTests

// FS-GG/FS.GG.Governance#385 — the composition tested AS COMPOSITION.
//
// Epic #367's four rule packs each shipped with their own green suite. Four green packs do not
// prove one green profile, and that is precisely the gap this item exists to close: the packs were
// never assembled, so nothing ever asked whether their rule identities collide, whether the profile
// still knows every rule they emit, or whether a rule from each one can actually fire through one
// assembled gate set.
//
// Every test here drives the packs' REAL evaluators — `FSharpSurface.evaluate`,
// `CodeChecks.analyze` (the actual FSharp.Compiler.Service parse/type-check),
// `FSharpEffectBoundary.evaluate`, `EvidenceBoundary.evaluate`. Nothing is stubbed, and no test
// asserts over a hand-written copy of what a pack is believed to emit.

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks

module SC = FS.GG.Governance.SurfaceChecks.Model
module CP = FS.GG.Governance.SurfaceChecks.Profile

module Surface = FS.GG.Governance.DesignChecks.FSharpSurface
module Effect = FS.GG.Governance.DesignChecks.FSharpEffectBoundary
module Evidence = FS.GG.Governance.DesignChecks.EvidenceBoundary
module Code = FS.GG.Governance.CodeChecks.CodeChecks
module CodeModel = FS.GG.Governance.CodeChecks.Model

// ── Requests, shared by the red and green halves of every pair ────────────────────────────────

let private surfaceRequest: SC.SurfaceCheckRequest =
    { Domain = SC.DesignDomain
      Surface = SurfaceId "fsharp-public-surface"
      Class = PackageSurface
      Path = normalizePath "src/App.fsproj"
      EvidenceTag = None }

let private effectRequest: SC.SurfaceCheckRequest =
    { surfaceRequest with
        Surface = SurfaceId "fsharp-effect-boundary"
        Class = DesignSurface }

// ── #366 fixtures ─────────────────────────────────────────────────────────────────────────────

let private moduleFacts: Surface.ModuleFacts =
    { Project = "src/App.fsproj"
      Source = normalizePath "src/Domain.fs"
      Signature = None
      SourceCompileIndex = 3
      SignatureCompileIndex = None
      IsTestProject = false
      IsExplicitlyInternal = false
      IsEntryPoint = false
      IsGenerated = false
      Exemption = Surface.NoExemption
      Declarations = []
      SignatureMatchesSource = true
      RequiresSurfaceBaseline = false
      SurfaceBaselineCurrent = true }

/// GREEN counterpart: the same module with a curated signature compiled immediately before it.
let private moduleFactsClean: Surface.ModuleFacts =
    { moduleFacts with
        Signature = Some(normalizePath "src/Domain.fsi")
        SignatureCompileIndex = Some 2 }

// ── #369 fixtures ─────────────────────────────────────────────────────────────────────────────

let private boundaryFacts: Effect.BoundaryFacts =
    { Project = "src/App.fsproj"
      Symbol = "transition"
      Source = normalizePath "src/Domain.fs"
      IsStatefulWorkflow = true
      IsPureParserOrValidator = false
      IsThinOneShotAdapter = false
      DirectEffects = []
      CallbackHiddenState = false
      ExceptionDrivenContinuation = false
      EdgeInterpreter = None
      Delivery = None
      Exemption = Effect.NoExemption }

/// RED: a declared pure transition that writes to the filesystem directly.
let private boundaryFactsRed: Effect.BoundaryFacts =
    { boundaryFacts with
        DirectEffects =
            [ { Category = Effect.Filesystem
                Symbol = "File.WriteAllText"
                Line = 8
                Column = 12 } ] }

// ── #368 fixtures ─────────────────────────────────────────────────────────────────────────────

let private codeRequest source : CodeModel.AnalysisRequest =
    { Head = "abc123"
      Documents =
        [ { Path = "src/Domain.fs"
            Source = source
            IsGenerated = false } ]
      PureDomainPrefixes = [ "src/" ]
      Thresholds =
        { ModuleLines = None
          TypeLines = None
          MemberLines = None
          DependencyFanOut = None }
      Justifications = []
      ApprovedPrimitives = [] }

/// RED: a class hierarchy where a DU would do — the constitution's Principle IV default.
let private codeRedSource =
    "module Domain\ntype Base() = class end\ntype Child() = inherit Base()\n"

/// GREEN: the same shape expressed as a DU plus a pure transition.
let private codeGreenSource =
    "module Domain\n\ntype State = Ready | Running of int\nlet advance state = match state with Ready -> Running 1 | x -> x\n"

let private analyzeNow request =
    Code.analyze request |> Async.RunSynchronously

// ── #370 fixtures ─────────────────────────────────────────────────────────────────────────────

let private realEvidence subject kind observation : Evidence.EvidenceRecord =
    { Subject = subject
      Kind = kind
      Provenance = Evidence.Real
      Command = "dotnet test"
      ExitCode = 0
      SourceDigest = "sha256:abc"
      Fresh = true
      Observation = observation }

/// The three always-required evidence classes, all present and real, so a red case isolates the
/// ONE rule under test instead of drowning it in `evidence.real-boundary-required`.
let private requiredEvidence observation =
    [ realEvidence "regression" Evidence.SemanticRegression observation
      realEvidence "boundary" Evidence.BoundaryFixture observation
      realEvidence "golden" Evidence.GoldenOrSchema observation ]

let private evidenceRequest observation : Evidence.Request =
    { RequiresProductionJourney = false
      RequiresObservedOutcome = true
      OptionalIntegrations = []
      Evidence = requiredEvidence observation
      GeneratedArtifacts = []
      Mitigations = []
      Render = None }

// ── Helpers ───────────────────────────────────────────────────────────────────────────────────

/// Every rule identity the composed profile declares, across all four packs.
let private allDeclaredIds =
    CP.packs |> List.collect CP.ruleIds |> Set.ofList

/// Normalize one native finding of any of the four shapes into the composed profile's single
/// `SurfaceFinding` vocabulary. This is the assembled profile's own entry point — the red cases
/// below go through it, not around it.
let private through pack ruleId subject =
    CP.findingOf pack ruleId surfaceRequest (normalizePath "src/Domain.fs") subject "composed"

[<Tests>]
let composition =
    testList
        "ReferenceProfileComposition"
        [
          // ── C1 — the profile still knows every rule its packs emit ──
          // The inventory is a claim about four independently-evolving packs. Executed against
          // their real evaluators, in both directions where the pack's applicability allows it,
          // so a pack that grows a rule without the profile learning about it reds here rather
          // than shipping a published gate set that under-declares what it governs.
          test "C1 every code the four real evaluators emit is declared by the composed profile" {
              let emitted =
                  [ // #366 — a Rogue3-shaped unpaired module, plus an invalid exemption.
                    yield! Surface.evaluate surfaceRequest [ moduleFacts ] |> List.map (fun f -> f.Code)
                    yield!
                        Surface.evaluate
                            surfaceRequest
                            [ { moduleFacts with
                                  Exemption = Surface.InvalidExemption "review date expired" } ]
                        |> List.map (fun f -> f.Code)

                    // #369 — a direct effect in a declared transition, and an expired exemption.
                    yield! Effect.evaluate effectRequest [ boundaryFactsRed ] |> List.map (fun f -> f.Code)
                    yield!
                        Effect.evaluate
                            effectRequest
                            [ { boundaryFacts with
                                  CallbackHiddenState = true
                                  ExceptionDrivenContinuation = true } ]
                        |> List.map (fun f -> f.Code)

                    // #370 — the evidence family, driven wide enough to emit several classes.
                    yield!
                        Evidence.evaluate
                            { evidenceRequest Evidence.DispatchOnly with
                                Evidence = []
                                GeneratedArtifacts =
                                    [ { Path = "gen/contract.json"
                                        Source = None
                                        RegenerationDeterministic = false
                                        Consumer = None
                                        HasGoldenOrSchema = false } ] }
                        |> List.map (fun f -> f.Code)

                    // #368 — the real compiler-service sensor over a planted hierarchy.
                    yield!
                        analyzeNow (codeRequest codeRedSource)
                        |> fun r -> r.Findings |> List.map (fun f -> CodeModel.findingIdToken f.Id) ]
                  |> List.distinct
                  |> List.sort

              Expect.isNonEmpty emitted "the fixtures must actually make the packs emit, or C1 asserts nothing"

              let undeclared = emitted |> List.filter (fun code -> not (Set.contains code allDeclaredIds))

              Expect.isEmpty
                  undeclared
                  "a pack emitted a rule identity the composed F# constitution profile does not declare — add it to SurfaceChecks.Profile.ruleIds, so the published gate set keeps naming everything it governs"
          }

          // ── C2 — #368's inventory is complete, not merely consistent ──
          // C1 can only see the rules a fixture provokes. #368 is the one pack with a closed,
          // typed rule DU, so its whole inventory IS mechanically checkable — and reflection over
          // the union is the only way to make "no case was forgotten" an executed fact rather than
          // a promise. That is why it is used here and nowhere in production code.
          test "C2 the profile declares exactly CodeChecks' twelve closed rule tokens" {
              let fromDu =
                  Reflection.FSharpType.GetUnionCases typeof<CodeModel.FindingId>
                  |> Array.map (fun case ->
                      match Reflection.FSharpValue.MakeUnion(case, [||]) with
                      | :? CodeModel.FindingId as id -> CodeModel.findingIdToken id
                      | other -> failtestf "FindingId union case '%s' did not construct a FindingId: %A" case.Name other)
                  |> List.ofArray
                  |> List.sort

              Expect.equal
                  (CP.ruleIds CP.IdiomaticSimplicity |> List.sort)
                  fromDu
                  "the composed profile's #368 inventory must equal CodeChecks' own closed FindingId projection — a case added to the DU without a row in the profile is a rule the published set silently does not name"
          }

          // ── C3 — no duplicate rule identity across the four packs (AC6) ──
          // This is the invariant that makes "whichever pack loads last wins" unrepresentable: if
          // two packs never share an identity, there is no last-writer to be. `ruleOwner` is the
          // total lookup that depends on it.
          test "C3 the four packs' rule identities are disjoint" {
              Expect.isEmpty
                  (CP.collisions CP.declarations)
                  "two packs declare the same rule identity — the composed profile would have to pick one owner, which is exactly the load-order accident #385 exists to prevent. Rename one identity or record an explicit precedence."

              for pack in CP.packs do
                  for id in CP.ruleIds pack do
                      Expect.equal
                          (CP.ruleOwner id)
                          (Some pack)
                          (sprintf "rule '%s' must resolve to the pack that declares it" id)
          }

          // ── C4 — the collision detector actually detects (AC6) ──
          // C3 asserts an empty list, and an empty list is also what a broken detector returns.
          // This one plants a duplicate and requires the REAL `collisions` to see it.
          //
          // #385 repair round 1: this test used to re-implement the grouping locally and assert its
          // own pipeline, so breaking `collisions` outright left it green — it proved a copy of the
          // detector, not the detector. The root cause was the nullary signature: over the fixed
          // table the function could only ever return `[]`, so there was no way to hand it a
          // positive case. `collisions` now takes the declarations it inspects, and C3 passes the
          // profile's own (`CP.declarations`) while this passes a planted set — the same function
          // on both paths.
          test "C4 a planted duplicate identity is reported, and the stricter maturity wins" {
              let planted =
                  [ CP.PublicSurface, "fsharp.shared-identity"
                    CP.EffectBoundary, "fsharp.shared-identity"
                    CP.EvidenceBoundary, "evidence.unique" ]

              let detected = CP.collisions planted

              Expect.equal
                  (detected |> List.map (fun c -> c.RuleId))
                  [ "fsharp.shared-identity" ]
                  "the REAL `collisions` must report a planted duplicate — and only it; otherwise C3's empty result proves nothing"

              Expect.equal
                  (detected |> List.exactlyOne).Packs
                  [ CP.PublicSurface; CP.EffectBoundary ]
                  "and it must name every pack declaring the identity, in declaration order, so a reader can see who to arbitrate between"

              // The negative control: the same function over a duplicate-free set is empty, so C4
              // is not passing merely because `collisions` returns everything it is given.
              Expect.isEmpty
                  (CP.collisions [ CP.PublicSurface, "a"; CP.EffectBoundary, "b" ])
                  "a duplicate-free declaration set must produce no collision"

              // ADR-0009 §Decision 3 reused verbatim: a shared identity resolves UP, never down.
              Expect.equal
                  (CP.composeMaturity (CP.declaredMaturity CP.PublicSurface) (CP.declaredMaturity CP.EffectBoundary))
                  BlockOnPr
                  "a rule identity declared at two maturities resolves to the HIGHER-ranked one — a product may raise a floor, never lower it"

              Expect.equal
                  (CP.composeMaturity (CP.declaredMaturity CP.EffectBoundary) (CP.declaredMaturity CP.PublicSurface))
                  BlockOnPr
                  "and the resolution is commutative, so it cannot depend on the order the packs are folded in"

              Expect.equal (CP.composeMaturity Observe Observe) Observe "equal maturities resolve to themselves"
          }

          // ── C5 — the named disagreement is DECIDED, and the packs read the decision ──
          // #366 emits at `Warn` and #369 at `BlockOnPr`. Before #385 those were two hard-coded
          // literals at two emit sites with nothing relating them. This test asserts the values
          // are unchanged AND that they now come from the profile: it compares what the real
          // evaluators produce against `declaredMaturity`, so moving the profile's value moves the
          // pack's — which is what makes it one authority rather than a third copy.
          test "C5 each pack's real findings carry the maturity the composed profile declares" {
              let surfaceFindings = Surface.evaluate surfaceRequest [ moduleFacts ]
              Expect.isNonEmpty surfaceFindings "#366 must emit for this fixture"

              for f in surfaceFindings do
                  Expect.equal
                      f.Maturity
                      (CP.declaredMaturity CP.PublicSurface)
                      "#366's emitted maturity is the profile's declaration, not a literal at the emit site"

              let effectFindings = Effect.evaluate effectRequest [ boundaryFactsRed ]
              Expect.isNonEmpty effectFindings "#369 must emit for this fixture"

              for f in effectFindings do
                  Expect.equal
                      f.Maturity
                      (CP.declaredMaturity CP.EffectBoundary)
                      "#369's emitted maturity is the profile's declaration, not a literal at the emit site"

              // The shipped postures, pinned: the composition preserved both rather than flattening
              // them to one value. `maturityRationale` carries WHY as data.
              Expect.equal (CP.declaredMaturity CP.PublicSurface) Warn "#366 stays advisory during migration"
              Expect.equal (CP.declaredMaturity CP.EffectBoundary) BlockOnPr "#369 stays blocking for opted-in transitions"
              Expect.notEqual
                  (CP.maturityRationale CP.PublicSurface)
                  (CP.maturityRationale CP.EffectBoundary)
                  "the two postures carry distinct recorded reasons — a difference with one shared reason would be a copy, not a decision"
          }

          // ── C6 — a rule from EACH pack fires through the assembled profile, red and green ──
          // AC5. Four separate pairs, each driving its pack's real evaluator, each normalized
          // through `Profile.findingOf` so the assertion is about the ASSEMBLED profile's output
          // vocabulary and maturity, not about the pack in isolation.
          test "C6 #366 public-surface fires red through the profile and passes clean" {
              let red = Surface.evaluate surfaceRequest [ moduleFacts ]
              let code = "fsharp.signature-missing"
              Expect.contains (red |> List.map (fun f -> f.Code)) code "the unpaired compiled module must be caught"

              let composed = through CP.PublicSurface code "src/Domain.fs"
              Expect.equal composed.Code code "the composed finding keeps the pack's own stable identity"
              Expect.equal composed.Domain SC.DesignDomain "it lands in the composed profile's domain"
              Expect.equal composed.Maturity Warn "at the profile's declared maturity for #366"
              Expect.isFalse composed.IsInputState "a declared rule is a rule violation, not malformed input"

              Expect.isEmpty
                  (Surface.evaluate surfaceRequest [ moduleFactsClean ])
                  "the planted-clean counterpart — a curated .fsi compiled immediately before its .fs — must pass"
          }

          test "C6 #368 idiomatic-simplicity fires red through the profile and passes clean" {
              let red = analyzeNow (codeRequest codeRedSource)

              let tokens = red.Findings |> List.map (fun f -> CodeModel.findingIdToken f.Id)

              Expect.contains tokens "inheritance-hierarchy" "the planted class hierarchy must be caught"

              let composed = through CP.IdiomaticSimplicity "inheritance-hierarchy" "Domain.Child"
              Expect.equal composed.Code "inheritance-hierarchy" "the composed finding keeps the pack's own stable identity"
              Expect.equal composed.Maturity Warn "at the profile's declared maturity for #368"
              Expect.isFalse composed.IsInputState "a declared rule is a rule violation, not malformed input"

              // This pack emits NO severity of its own — before #385 its findings could not reach
              // `Enforcement.deriveEffectiveSeverity` at any maturity, because there was nothing to
              // give them one. The composition is what put it on the severity axis.
              Expect.isEmpty
                  ((analyzeNow (codeRequest codeGreenSource)).Findings)
                  "the planted-clean counterpart — the same shape as a DU and a pure transition — must pass"
          }

          test "C6 #369 effect-boundary fires red through the profile and passes clean" {
              let red = Effect.evaluate effectRequest [ boundaryFactsRed ]
              let code = "fsharp.effect-in-transition"
              Expect.contains (red |> List.map (fun f -> f.Code)) code "the direct filesystem effect must be caught"

              let composed = through CP.EffectBoundary code "filesystem File.WriteAllText@8:12"
              Expect.equal composed.Code code "the composed finding keeps the pack's own stable identity"
              Expect.equal composed.Maturity BlockOnPr "at the profile's declared maturity for #369 — the one pack that blocks"
              Expect.isFalse composed.IsInputState "a declared rule is a rule violation, not malformed input"

              Expect.isEmpty
                  (Effect.evaluate effectRequest [ boundaryFacts ])
                  "the planted-clean counterpart — the same declared transition with no direct effect — must pass"
          }

          test "C6 #370 evidence-boundary fires red through the profile and passes clean" {
              let red = Evidence.evaluate (evidenceRequest Evidence.DispatchOnly)
              let code = "evidence.observed-outcome-missing"
              Expect.contains (red |> List.map (fun f -> f.Code)) code "dispatch-only evidence cannot satisfy an observed-outcome obligation"

              let composed = through CP.EvidenceBoundary code "native-boundary obligation"
              Expect.equal composed.Code code "the composed finding keeps the pack's own stable identity"
              Expect.equal composed.Maturity Warn "at the profile's declared maturity for #370"
              Expect.isFalse composed.IsInputState "a declared rule is a rule violation, not malformed input"

              Expect.isEmpty
                  (Evidence.evaluate (evidenceRequest Evidence.ObservedOutcome))
                  "the planted-clean counterpart — the same request with a real observed outcome — must pass"
          }

          // ── C7 — traceability back to the authoring child (AC1) ──
          test "C7 every rule in the composed profile traces to the child that authored it" {
              let expected =
                  [ CP.PublicSurface, "FS-GG/FS.GG.Governance#366"
                    CP.IdiomaticSimplicity, "FS-GG/FS.GG.Governance#368"
                    CP.EffectBoundary, "FS-GG/FS.GG.Governance#369"
                    CP.EvidenceBoundary, "FS-GG/FS.GG.Governance#370" ]

              for pack, item in expected do
                  Expect.equal (CP.authoringItem pack) item "each pack names the child that authored it"
                  Expect.isNonEmpty (CP.ruleIds pack) "and contributes at least one rule"

              Expect.equal (List.length CP.packs) 4 "the composed profile is exactly epic #367's four children"

              Expect.equal
                  (CP.packs |> List.map CP.authoringItem |> List.distinct |> List.length)
                  4
                  "each pack traces to a DISTINCT child — two packs claiming one author would break the traceability AC1 asks for"

              // Every declared identity resolves to a maturity, so nothing in the profile is
              // declared-but-unenforceable.
              for id in allDeclaredIds do
                  Expect.isSome (CP.maturityOf id) (sprintf "rule '%s' must resolve to a maturity" id)

              Expect.isNone (CP.ruleOwner "not.a.rule") "an unknown identity yields None — never a guessed owner"
          }

          // ── C8 — an undeclared identity fails CLOSED through the profile ──
          test "C8 a finding for an identity the profile does not declare is reported as malformed input" {
              let composed = through CP.PublicSurface "fsharp.invented-rule" "src/Domain.fs"

              Expect.isTrue
                  composed.IsInputState
                  "an unattributable rule identity is malformed input to the composition, never a silent pass and never a fabricated rule violation"

              Expect.stringContains
                  composed.Message
                  "fsharp.invented-rule"
                  "and the diagnostic names the undeclared identity so the cause is bounded"
          }

          // ── C9 — the published gate set IS the composed profile ──
          // The link between the rule-level table and the four gates a consumer resolves. Without
          // this, the profile could be right and the published set could still say something else.
          test "C9 the composed profile projects to exactly one gate per pack" {
              Expect.equal (List.length CP.checks) 4 "one gate per pack"

              let ids =
                  CP.checks
                  |> List.map (fun c ->
                      let (CheckId id) = c.Id
                      id)
                  |> Set.ofList

              Expect.equal
                  ids
                  (CP.packs |> List.map CP.packToken |> Set.ofList)
                  "every pack contributes its own gate, named by its own token"

              for c in CP.checks do
                  Expect.equal c.Domain CP.domain "every composed gate declares the profile's single domain"
                  Expect.isNone c.Command "composed gates are command-free — a tooling.yml binding would dangle in every repository that resolved the published set"
                  Expect.isNone c.Tier "composed gates are not cost-tiered generated-product checks"

                  let (CheckId id) = c.Id

                  match CP.packs |> List.tryFind (fun p -> CP.packToken p = id) with
                  | Some pack ->
                      Expect.equal
                          c.Maturity
                          (CP.declaredMaturity pack)
                          "the published gate's maturity is the profile's declaration for its pack"
                  | None -> failtestf "gate '%s' does not correspond to any pack" id
          }
        ]
