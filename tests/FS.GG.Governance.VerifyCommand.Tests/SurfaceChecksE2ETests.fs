module FS.GG.Governance.VerifyCommand.Tests.SurfaceChecksE2ETests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// 067 (F24 verify-host wiring) — the end-to-end proofs that `fsgg verify` now classifies → senses → runs the
// product-surface checks, folds a blocking finding into the verdict, and projects the additive `surfaceChecks`
// section. The surface SENSORS run for real over a real temp tree (the feature under test — real package/docs/
// skill/design file reads through the READ-ONLY package port); git is faked with FIXED revisions and the gate
// EXEC is faked (the established pattern in this suite) so the rest of `verify.json` is byte-deterministic —
// commit SHAs from a real `git` would otherwise vary run-to-run. Synthetic inputs (the advisory finding the
// real sensors cannot yet emit from disk) are disclosed at their use site (Constitution V).

let private srcCandidate = "src/Api.fsi"

// Deterministic ports: the in-memory `catalog` drives classification (so `update` sees the declared product
// surface); FIXED-SHA faked git reports `changed` as the routed path; the REAL surface sense reads `dir`'s
// files; the gate exec is `exec`; writes/stdout are captured. Real sensors, faked git/exec — disclosed.
let private detPorts (catalog) (dir: string) (changed: string) (exec) (cap: Capture) : Interpreter.Ports =
    { fakePortsExec catalog (gitWithChanges [ 'M', changed ]) fakeSensor absentStoreReader exec cap with
        SenseSurfaces = realSurfaceSense dir }

let private goldenDir =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.VerifyCommand.Tests", "goldens")

// Compare against a frozen golden; `BLESS_GOLDEN=1` (re)writes it. A missing golden fails loudly (never a
// silent self-fulfilling pass) unless blessing.
let private goldenAssert (name: string) (actual: string) =
    let path = Path.Combine(goldenDir, name)
    if Environment.GetEnvironmentVariable "BLESS_GOLDEN" = "1" then
        File.WriteAllText(path, actual)
    Expect.isTrue (File.Exists path) (sprintf "golden %s exists (run BLESS_GOLDEN=1 dotnet test to mint it)" name)
    Expect.equal actual (File.ReadAllText path) (sprintf "byte-identical to golden %s (BLESS_GOLDEN=1 to refresh)" name)

let private contentOf (cap: Capture) : string =
    match writtenVerify cap with
    | Some(_, c) -> c
    | None -> failtest "expected a verify.json write"

let private senseBoundarySource (source: string) (assertions: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list -> unit) =
    withTempRepo (fun dir ->
        writeFile dir "src/Boundary.fsproj" """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="Boundary.fs" /></ItemGroup></Project>"""
        writeFile dir "src/Boundary.fs" source
        let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
        realSurfaceSense dir report |> assertions)

let private effectCodes (findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list) =
    findings
    |> List.filter (fun finding -> finding.Code.StartsWith("fsharp.effect", StringComparison.Ordinal) || finding.Code = "fsharp.callback-hidden-state")
    |> List.map _.Code

[<Tests>]
let tests =
    testList
        "SurfaceChecksE2E"
        [
          // ── US1 (T004 / SC-001): a drifted package surface blocks `fsgg verify` ──
          test "T004 drifted package surface ⇒ surfaceChecks package.baseline-drift, blocking exit, evidence tag, no leakage" {
              withDriftedPackageRepo (fun dir ->
                  let cap = newCapture ()
                  let model = Interpreter.run (detPorts surfaceCatalog dir srcCandidate fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  // The build gate passes (faked exit 0) ⇒ the ONLY blocker is the surface finding.
                  Expect.equal model.Exit Loop.Blocked "drifted surface ⇒ Blocked at Verify under Strict"
                  Expect.equal (Loop.exitCode model.Exit) 1 "exit 1 (distinct from tool errors)"

                  let content = contentOf cap
                  Expect.stringContains content "surfaceChecks" "the additive section is emitted"
                  Expect.stringContains content "package.baseline-drift" "the drift finding is reported"
                  Expect.stringContains content "api-contract" "the declared evidenceTag is carried"
                  // FR-006: no absolute path / temp-dir leakage in the emitted bytes.
                  Expect.isFalse (content.Contains dir) "no absolute repo path leaks into surfaceChecks"
                  Expect.isFalse (content.Contains(Path.GetTempPath())) "no temp path leaks") }

          // ── US2 (T005 / SC-002): no declared surface ⇒ byte-identical, section omitted ──
          test "T005 no declared product surface ⇒ verify.json byte-identical to the pre-wiring golden, no surfaceChecks" {
              // The no-surface catalog (its only surface is `protected`) ⇒ the real sense returns [] regardless
              // of the tree, so the default empty sense + FIXED-SHA faked git give a stable anchor.
              let cap = newCapture ()
              let candidates = [ gp "src/Lib/Thing.fs" ]
              let model = Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortPass cap) (requestFor (Loop.ExplicitPaths candidates) Loop.Text)
              let content = contentOf cap
              Expect.equal model.Exit Loop.Success "no surface, passing gates ⇒ Success"
              Expect.isFalse (content.Contains "surfaceChecks") "no surfaceChecks section when there are no findings"
              // Independent no-regression anchor: equals the genuine pre-wiring projection of the same inputs
              // (ExplicitPaths senses no snapshot ⇒ baseHead None, matching the actual run).
              Expect.equal content (verifyExpectedWith fakeExecPortPass validCatalog candidates Standard None) "byte-identical to the genuine VerifyJson.ofVerifyDecision projection"
              goldenAssert "verify-no-surfaces.json" content }

          // ── US3 (T006 / SC-003): an advisory-only finding surfaces without escalating ──
          test "T006 advisory-only surface finding ⇒ surfaceChecks advisory entry, exit equals a clean run (Synthetic)" {
              // SYNTHETIC: the advisory finding is injected through the surface-sense port — the real disk
              // sensors emit only Blocking findings today (the lone Advisory check, docs.example-freshness, the
              // real docs sensor does not yet populate). The fold + projection are exercised for real.
              let runWith findings =
                  let cap = newCapture ()
                  let ports =
                      { fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortPass cap with
                          SenseSurfaces = syntheticSurfaceSense findings }
                  Interpreter.run ports (requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text), cap

              let clean, _ = runWith []
              let advised, capA = runWith [ advisorySurfaceFinding ]
              let content = contentOf capA
              Expect.stringContains content "surfaceChecks" "the advisory finding is visible in surfaceChecks"
              Expect.stringContains content "advisory" "carried with advisory severity"
              Expect.equal advised.Exit clean.Exit "advisory never changes the exit code"
              Expect.equal advised.Exit Loop.Success "advisory-only ⇒ Success" }

          // ── US1 (T008 / SC-004): determinism + the absent-baseline read-only case ──
          test "T008 re-running over unchanged inputs ⇒ byte-identical verify.json (deterministic)" {
              withDriftedPackageRepo (fun dir ->
                  let run () =
                      let cap = newCapture ()
                      Interpreter.run (detPorts surfaceCatalog dir srcCandidate fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                      |> ignore
                      contentOf cap

                  Expect.equal (run ()) (run ()) "two runs over unchanged inputs are byte-identical") }

          test "T008b absent baseline ⇒ two runs byte-identical and the working tree is unchanged (read-only)" {
              withAbsentBaselineRepo (fun dir ->
                  let baselineFile = Path.Combine(dir, "src", "Api.fsi.baseline")
                  let run () =
                      let cap = newCapture ()
                      Interpreter.run (detPorts surfaceCatalogNoGates dir srcCandidate fakeExecPortFail cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                      |> ignore
                      contentOf cap

                  let first = run ()
                  Expect.isFalse (File.Exists baselineFile) "the read-only port never writes the absent baseline"
                  let second = run ()
                  Expect.equal first second "absent-baseline runs are byte-identical (no first-run-writes divergence)"
                  Expect.stringContains first "package.baseline-absent" "the absent baseline is reported (blocking)") }

          // ── US2 (T009b / FR-012): read-only, no working-tree write, no spawned process ──
          test "T009b read-only verify ⇒ no .baseline written and no process spawned by surface sensing" {
              withAbsentBaselineRepo (fun dir ->
                  let baselineFile = Path.Combine(dir, "src", "Api.fsi.baseline")
                  let counter = { Calls = 0 }
                  let cap = newCapture ()
                  // No gates in this catalog, so the ONLY thing that could spawn a process is a transcript run —
                  // which the read-only package port suppresses (ListTranscripts ⇒ Ok []).
                  let model = Interpreter.run (detPorts surfaceCatalogNoGates dir srcCandidate (countingExecPort counter 1) cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  Expect.equal counter.Calls 0 "the read-only package port spawns no process (no transcript executed)"
                  Expect.isFalse (File.Exists baselineFile) "no .baseline is written to the working tree"
                  Expect.equal model.Exit Loop.Blocked "the package.baseline-absent finding still blocks under Strict"
                  Expect.stringContains (contentOf cap) "package.baseline-absent" "the absent baseline is reported, written nowhere") }

          // ── US3 (T009 / FR-010): safe failure on an unreadable / missing surface source ──
          test "T009 a routed-but-missing surface source ⇒ a disclosed sensor outcome, not a crash or silent pass" {
              withDriftedPackageRepo (fun dir ->
                  let cap = newCapture ()
                  // Faked git reports a GHOST `.fsi` that is not on disk — the real package sensor surfaces a
                  // disclosed `package.baseline-unreadable` input-state finding (FR-010), never a crash/silent pass.
                  let model = Interpreter.run (detPorts surfaceCatalog dir "src/Ghost.fsi" fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  let content = contentOf cap
                  Expect.stringContains content "package.baseline-unreadable" "a missing source is a disclosed sensor outcome"
                  Expect.equal model.Exit Loop.Blocked "the disclosed input-state finding blocks under Strict (not a silent pass)") }

          // ── ADPT-1 (fail-closed): an INVALID catalog must not collapse surface sensing to [] ──
          test "ADPT-1 an invalid governance catalog ⇒ a Blocking input-state surface finding, never a silent empty pass" {
              // Drive the REAL surface sense (`realSurfaceSense` = the production `Interpreter.realPorts` field)
              // over a temp tree whose on-disk `.fsgg` catalog does not validate (schemaVersion 999). Before
              // ADPT-1 this returned `[]` — verify would PASS with zero surface evidence exactly when the
              // catalog was too broken to gather any. It must instead fail CLOSED with a disclosed finding.
              let dir = Path.Combine(Path.GetTempPath(), "fsgg-verify-adpt1-" + Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory dir |> ignore

              try
                  for KeyValue(name, content) in invalidCatalog do
                      writeFile dir (".fsgg/" + name) content

                  // The Invalid path short-circuits before the report is consumed, so an empty report suffices.
                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  Expect.isNonEmpty findings "an invalid catalog must NOT collapse to [] (that passes with zero surface evidence)"
                  Expect.all findings (fun f -> f.IsInputState) "every reified failure is an input-state finding, not a fabricated rule violation"
                  Expect.all findings (fun f -> f.BaseSeverity = Blocking) "reified as Blocking so verify fails closed"

                  Expect.isTrue
                      (findings |> List.exists (fun f -> f.Code = "surface.catalog-invalid"))
                      "the catalog-invalid code is reported"

                  // Fail-closed means it actually BLOCKS at Verify under Strict (via the existing deriveEffectiveSeverity).
                  Expect.isTrue (SurfaceFold.surfaceBlocks Strict findings) "the reified finding blocks the verify verdict"
              finally
                  try Directory.Delete(dir, true) with _ -> () }

          test "declared F# transition with direct I/O ⇒ production Verify sensing emits a blocking effect finding" {
              // This exercises the concrete `Interpreter.realPorts` sense path, including its project
              // enumeration and the ProjectSensing seam; it is not a hand-built BoundaryFacts fixture.
              withTempRepo (fun dir ->
                  writeFile dir "src/Boundary.fsproj" """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="Boundary.fs" /></ItemGroup></Project>"""
                  writeFile dir "src/Boundary.fs" """module internal Boundary
// fsgg:effect-boundary transition
let transition value = File.WriteAllText("out.txt", value)"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  Expect.isFalse
                      (findings |> List.exists (fun finding -> finding.Code = "fsharp.effect-boundary-malformed"))
                      (sprintf "the production sensor accepted the project fixture; findings: %A" findings)

                  Expect.isTrue
                      (findings |> List.exists (fun finding -> finding.Code = "fsharp.effect-in-transition" && finding.BaseSeverity = Blocking))
                      "a declared transition which performs direct I/O blocks through the production Verify sense") }

          test "effect-boundary production controls use real symbols and exact declaration tokens" {
              let good = """module internal Boundary
type Message = Saved | SaveFailed
type Effect = Persist of string
// fsgg:effect-boundary advance edge=interpret success=Saved failure=SaveFailed retry=never idempotency=document-id
let advance model = model, [ Persist model ]
let interpret effect = task {
    match effect with
    | Persist text ->
        try
            do! File.WriteAllTextAsync("document.txt", text)
            return Saved
        with _ ->
            return SaveFailed
}"""
              senseBoundarySource good (fun findings ->
                  Expect.isEmpty (effectCodes findings) "a pure transition plus explicit real edge contract passes")

              let documented = """module internal Boundary
type Message = Saved | SaveFailed
type Effect = Persist of string
// fsgg:effect-boundary advance edge=interpret success=Saved failure=SaveFailed retry=never idempotency=document-id
let advance model = model, [ Persist model.Document ] // pure: no File.WriteAllText here
let interpret effect = task {
    match effect with
    | Persist text ->
        do! File.WriteAllTextAsync("document.txt", text)
        return Saved
}"""
              senseBoundarySource documented (fun findings ->
                  Expect.isEmpty (effectCodes findings) "the exact documented pure-line comment does not manufacture an effect")

              let lexicalNoise = """module internal Boundary
// fsgg:effect-boundary advance
let advance model =
    (* Process.Start("ignored") *)
    let ordinary = "File.WriteAllText(ignored)"
    let interpolated = $"File.WriteAllText({model})"
    let FileWriteAllText = ordinary
    model"""
              senseBoundarySource lexicalNoise (fun findings ->
                  Expect.isEmpty (effectCodes findings) "comments, string text, interpolation text, and identifiers are not calls")

              let interpolatedCall = "module internal Boundary\n// fsgg:effect-boundary advance\nlet advance (path: string) (model: string) = $\"{File.WriteAllText(path, model)}\""
              senseBoundarySource interpolatedCall (fun findings ->
                  let effect = findings |> List.find (fun finding -> finding.Code = "fsharp.effect-in-transition")
                  Expect.stringContains effect.Location.Detail "File.WriteAllText@3:49" "production sensing preserves executable call identity inside an interpolation hole")

              let actualCall = """module internal Boundary
// fsgg:effect-boundary advance
let advance model =
    File.WriteAllText("document.txt", model)"""
              senseBoundarySource actualCall (fun findings ->
                  let effect = findings |> List.find (fun finding -> finding.Code = "fsharp.effect-in-transition")
                  Expect.stringContains effect.Location.Detail "File.WriteAllText@4:5" "production diagnostics retain actual call identity and location")

              let unrelatedEdge = """module internal Boundary
// fsgg:effect-boundary advance
let advance model = model
let interpret text = File.WriteAllText("document.txt", text)"""
              senseBoundarySource unrelatedEdge (fun findings ->
                  Expect.isFalse (effectCodes findings |> List.contains "fsharp.effect-in-transition") "later edge I/O is outside the declared transition body")

              let missingSymbol = """module internal Boundary
// fsgg:effect-boundary missing
let actual model = model"""
              senseBoundarySource missingSymbol (fun findings ->
                  Expect.contains (effectCodes findings) "fsharp.effect-boundary-malformed" "a missing named symbol fails closed")

              let malformed = """module internal Boundary
// fsgg:effect-boundary transition not-edge
let transition model = model"""
              senseBoundarySource malformed (fun findings ->
                  Expect.contains (effectCodes findings) "fsharp.effect-boundary-malformed" "bare and substring-shaped options are rejected")

              let unknown = """module internal Boundary
// fsgg:effect-boundary transition edgy=true
let transition model = model"""
              senseBoundarySource unknown (fun findings ->
                  Expect.contains (effectCodes findings) "fsharp.effect-boundary-malformed" "unknown exact options are rejected") }

          test "effect-boundary production controls cover applicability delivery callbacks clocks and exemptions" {
              let nonApplicable kind = sprintf """module internal Boundary
// fsgg:effect-boundary read kind=%s
let read value = File.WriteAllText("out.txt", value)""" kind
              for kind in [ "parser"; "thin-adapter" ] do
                  senseBoundarySource (nonApplicable kind) (fun findings ->
                      Expect.isEmpty (effectCodes findings) (sprintf "%s is explicitly non-applicable" kind))

              let callback = """module internal Boundary
// fsgg:effect-boundary transition
let transition work = Async.Start work"""
              senseBoundarySource callback (fun findings ->
                  Expect.contains (effectCodes findings) "fsharp.callback-hidden-state" "callback-hidden state blocks")

              let incompleteDelivery = """module internal Boundary
// fsgg:effect-boundary transition edge=interpret success=Saved
let transition value = value
let interpret value = value"""
              senseBoundarySource incompleteDelivery (fun findings ->
                  let codes = effectCodes findings
                  Expect.contains codes "fsharp.effect-result-message-missing" "failure message is required"
                  Expect.contains codes "fsharp.effect-retry-missing" "retry semantics are required"
                  Expect.contains codes "fsharp.effect-idempotency-missing" "idempotency semantics are required")

              let injectedClock = """module internal Boundary
// fsgg:effect-boundary transition
let transition clock model = clock(), model"""
              senseBoundarySource injectedClock (fun findings ->
                  Expect.isFalse (effectCodes findings |> List.contains "fsharp.effect-in-transition") "an injected clock is pure at the transition")

              let exempt reviewBy = sprintf """module internal Boundary
// fsgg:effect-boundary transition exemption-owner=platform exemption-rationale="legacy bridge" exemption-review-by=%s
let transition value = File.WriteAllText("out.txt", value)""" reviewBy
              senseBoundarySource (exempt "2099-01-01") (fun findings ->
                  Expect.isEmpty (effectCodes findings) "a complete unexpired symbol exemption is narrow and non-applicable")
              senseBoundarySource (exempt "2000-01-01") (fun findings ->
                  Expect.contains (effectCodes findings) "fsharp.effect-exemption-invalid" "an expired exemption blocks")

              let partialExemption = """module internal Boundary
// fsgg:effect-boundary transition exemption-owner=platform
let transition model = model"""
              senseBoundarySource partialExemption (fun findings ->
                  Expect.contains (effectCodes findings) "fsharp.effect-boundary-malformed" "a partial exemption schema fails closed") }

          // ── #390: the two packs #385 published and nothing evaluated ──────────────────────────────
          //
          // Every test below drives `realSurfaceSense` — `Interpreter.realPorts repo |> _.SenseSurfaces`,
          // the PRODUCTION verify route, project enumeration and all — over a real temp tree. Nothing is
          // stubbed: the planted repository declares the pack's scope exactly the way an adopting
          // repository would, and the assertions read the findings the real sweep returns.
          //
          // Before #390 EVERY one of these was unreachable: `grep -rn "CodeChecks.analyze\|
          // EvidenceBoundary.evaluate" src/` returned nothing, so `fsharp:idiomatic-simplicity` and
          // `fsharp:evidence-boundary` were green because no code path could look.

          test "#390 a planted inheritance hierarchy ⇒ #368's pack fires through the production verify sense" {
              withTempRepo (fun dir ->
                  writeFile dir ".fsgg/fsharp-simplicity.json" """{ "sources": [ "src/Planted.fs" ] }"""
                  writeFile dir "src/Planted.fs" """module Planted

type Base() =
    member _.Name = "base"

type Derived() =
    inherit Base()
"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  let inheritance =
                      findings
                      |> List.tryFind (fun finding -> finding.Code = "inheritance-hierarchy")

                  Expect.isSome
                      inheritance
                      (sprintf "#368's evaluator must be reached by the production sweep; findings: %A" findings)

                  let finding = Option.get inheritance

                  Expect.equal
                      finding.Surface
                      (SurfaceId "fsharp-idiomatic-simplicity")
                      "the finding carries the pack's own surface id"

                  Expect.isFalse
                      finding.IsInputState
                      "a real rule violation is not an input-state finding — the id IS declared by the pack"

                  // AC2: normalized through `Profile.findingOf`, so it carries the COMPOSED PROFILE's declared
                  // maturity for #368 rather than a second, local conversion.
                  Expect.equal
                      finding.Maturity
                      (FS.GG.Governance.SurfaceChecks.Profile.declaredMaturity
                          FS.GG.Governance.SurfaceChecks.Profile.IdiomaticSimplicity)
                      "the maturity is READ from the composed profile, not restated at the call site"

                  // …and it reaches the existing enforcement rollup at exactly that maturity.
                  Expect.equal
                      (deriveEffectiveSeverity
                          (FS.GG.Governance.SurfaceChecks.Model.enforcementInputOf finding Verify Strict))
                          .EffectiveSeverity
                      Advisory
                      "#368 binds at Warn, so the finding reaches deriveEffectiveSeverity and resolves advisory"

                  Expect.isFalse
                      (SurfaceFold.surfaceBlocks Strict findings)
                      "an advisory pack surfaces without blocking a repository that was green") }

          test "#390 the planted-clean counterpart of #368's pack passes through the same route" {
              withTempRepo (fun dir ->
                  writeFile dir ".fsgg/fsharp-simplicity.json" """{ "sources": [ "src/Planted.fs" ] }"""
                  writeFile dir "src/Planted.fs" """module Planted

type Shape =
    | Circle of radius: float
    | Square of side: float

let area shape =
    match shape with
    | Circle radius -> radius * radius
    | Square side -> side * side
"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  let simplicity =
                      findings
                      |> List.filter (fun finding -> finding.Surface = SurfaceId "fsharp-idiomatic-simplicity")

                  Expect.isEmpty
                      simplicity
                      (sprintf "a clean declared source produces no #368 finding; got: %A" simplicity)) }

          test "#390 a declared evidence obligation with no observed outcome ⇒ #370's pack fires" {
              withTempRepo (fun dir ->
                  // A real, complete evidence inventory for the three unconditionally required classes — so the
                  // three `evidence.real-boundary-required` findings do NOT fire and the ONLY thing left for the
                  // evaluator to report is the missing observed outcome the declaration demands.
                  writeFile dir ".fsgg/evidence-boundary.json" """{
  "requiresObservedOutcome": true,
  "evidence": [
    { "subject": "parser", "kind": "semantic-regression", "provenance": "real", "command": "dotnet test",
      "exitCode": 0, "sourceDigest": "abc123", "fresh": true, "observation": "dispatch-only" },
    { "subject": "parser", "kind": "boundary-fixture", "provenance": "real", "command": "dotnet test",
      "exitCode": 0, "sourceDigest": "abc123", "fresh": true, "observation": "dispatch-only" },
    { "subject": "parser", "kind": "golden-or-schema", "provenance": "real", "command": "dotnet test",
      "exitCode": 0, "sourceDigest": "abc123", "fresh": true, "observation": "dispatch-only" }
  ]
}"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  let outcome =
                      findings
                      |> List.tryFind (fun finding -> finding.Code = "evidence.observed-outcome-missing")

                  Expect.isSome
                      outcome
                      (sprintf "#370's evaluator must be reached by the production sweep; findings: %A" findings)

                  let finding = Option.get outcome

                  Expect.equal
                      finding.Surface
                      (SurfaceId "fsharp-evidence-boundary")
                      "the finding carries the pack's own surface id"

                  Expect.isFalse finding.IsInputState "a real rule violation is not an input-state finding"

                  Expect.equal
                      finding.Maturity
                      (FS.GG.Governance.SurfaceChecks.Profile.declaredMaturity
                          FS.GG.Governance.SurfaceChecks.Profile.EvidenceBoundary)
                      "the maturity is READ from the composed profile"

                  Expect.equal
                      (deriveEffectiveSeverity
                          (FS.GG.Governance.SurfaceChecks.Model.enforcementInputOf finding Verify Strict))
                          .EffectiveSeverity
                      Advisory
                      "#370 binds at Warn, so the finding reaches deriveEffectiveSeverity and resolves advisory"

                  Expect.isFalse
                      (findings |> List.exists (fun f -> f.Code = "evidence.real-boundary-required"))
                      "the declared inventory satisfies the three unconditional classes") }

          test "#390 the planted-clean counterpart of #370's pack passes through the same route" {
              withTempRepo (fun dir ->
                  writeFile dir ".fsgg/evidence-boundary.json" """{
  "requiresObservedOutcome": true,
  "evidence": [
    { "subject": "parser", "kind": "semantic-regression", "provenance": "real", "command": "dotnet test",
      "exitCode": 0, "sourceDigest": "abc123", "fresh": true, "observation": "observed-outcome" },
    { "subject": "parser", "kind": "boundary-fixture", "provenance": "real", "command": "dotnet test",
      "exitCode": 0, "sourceDigest": "abc123", "fresh": true, "observation": "observed-outcome" },
    { "subject": "parser", "kind": "golden-or-schema", "provenance": "real", "command": "dotnet test",
      "exitCode": 0, "sourceDigest": "abc123", "fresh": true, "observation": "observed-outcome" }
  ]
}"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  let evidence =
                      findings
                      |> List.filter (fun finding -> finding.Surface = SurfaceId "fsharp-evidence-boundary")

                  Expect.isEmpty
                      evidence
                      (sprintf "a satisfied declared obligation produces no #370 finding; got: %A" evidence)) }

          test "#390 an undeclared repository acquires neither pack — applicability is declared, not assumed" {
              withTempRepo (fun dir ->
                  writeFile dir "src/Ordinary.fs" """module Ordinary
type Base() = class end
type Derived() =
    inherit Base()
"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  // The same inheritance hierarchy the declared case reports — silent here, because the
                  // repository declared no scope. That is the #369 posture, and for #368 it is also what keeps
                  // `compiler-analysis-failed` off every cross-project source in an adopting repository.
                  Expect.isEmpty
                      (findings
                       |> List.filter (fun f ->
                           f.Surface = SurfaceId "fsharp-idiomatic-simplicity"
                           || f.Surface = SurfaceId "fsharp-evidence-boundary"))
                      "no declaration ⇒ no obligation ⇒ no findings from either newly wired pack") }

          test "#390 a malformed declaration fails CLOSED and does not erase the other packs' findings" {
              withTempRepo (fun dir ->
                  // Malformed for BOTH packs at once, alongside a real #369 violation that must survive.
                  writeFile dir ".fsgg/fsharp-simplicity.json" """{ "sources": "not-an-array" }"""
                  writeFile dir ".fsgg/evidence-boundary.json" """{ "evidence": [ { "kind": "no-such-kind" } ] }"""
                  writeFile dir "src/Boundary.fsproj" """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="Boundary.fs" /></ItemGroup></Project>"""
                  writeFile dir "src/Boundary.fs" """module internal Boundary
// fsgg:effect-boundary transition
let transition value = File.WriteAllText("out.txt", value)"""

                  let report: FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport = { Classifications = [] }
                  let findings = realSurfaceSense dir report

                  for surface in [ "fsharp-idiomatic-simplicity"; "fsharp-evidence-boundary" ] do
                      let reified =
                          findings
                          |> List.tryFind (fun f -> f.Surface = SurfaceId surface && f.Code = "surface.sense-error")

                      Expect.isSome
                          reified
                          (sprintf "a malformed %s declaration must be REPORTED, never a silent empty pass" surface)

                      let finding = Option.get reified
                      Expect.isTrue finding.IsInputState "a malformed declaration is an input-state finding"
                      Expect.equal finding.BaseSeverity Blocking "reified as Blocking so verify fails closed"

                  Expect.isTrue
                      (SurfaceFold.surfaceBlocks Strict findings)
                      "the reified failure actually blocks the verify verdict"

                  // ADPT-1's isolation property, extended to the two new sweeps: a broken declaration for these
                  // packs must not discard #369's real finding from the same run.
                  Expect.isTrue
                      (findings
                       |> List.exists (fun f -> f.Code = "fsharp.effect-in-transition"))
                      "a malformed declaration for one pack does not erase another pack's real findings") }

          // ── T020 / contract C2: the non-empty surfaceChecks projection is frozen byte-identically ──
          test "T020 non-empty surfaceChecks projection is deterministic and byte-identical to the golden" {
              withDriftedPackageRepo (fun dir ->
                  let cap = newCapture ()
                  Interpreter.run (detPorts surfaceCatalog dir srcCandidate fakeExecPortPass cap) (requestForProfile Loop.DefaultRange Loop.Text Strict)
                  |> ignore
                  goldenAssert "verify-surfacechecks.json" (contentOf cap)) }
        ]
