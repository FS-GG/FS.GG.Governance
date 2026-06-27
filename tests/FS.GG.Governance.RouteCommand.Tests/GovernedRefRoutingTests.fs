module FS.GG.Governance.RouteCommand.Tests.GovernedRefRoutingTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F082 — the declared `governedReferences` of a consumable handoff become first-class routing
// candidates: the surface a work item declares it governs drives domain-gate selection through the
// REAL Config→Gates→Routing→Route pipeline, even when the sensed change set is empty (FR-001/002).
//
// NOTE on the catalog (recorded deviation): the spec/tasks prose names `build`/`test` domains
// illustratively; the reference `validCatalog` these host suites share routes `src/**`→`package-api`
// (gates `format`,`build`) and `work/**`→`workflow` (gate `audit`). The binding behavior is identical —
// a declared `src/**` path selects the package-api gates; a declared `work/**` path selects audit.

// A git port reporting one committed src/** edit (selects the package-api gates on DefaultRange).
let private gitSrcChange = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

let private handoffRead json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

// A consumable v1 handoff declaring the given governedReferences paths under work item WI-1.
let private declaringJson (paths: string) : string =
    sprintf
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
             "governedReferences": [ { "workItem": "WI-1", "paths": [ %s ] } ] }"""
        paths

// A consumable v1 handoff with an EMPTY governedReferences list (declares no governed surface).
let private emptyRefsJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
         "governedReferences": [] }"""

// A malformed (un-parseable) document.
let private malformedJson = """{ "contractVersion": "1.0.0", "schemaVersion": 1, "evidence": { "nodes": [ this is not json """

let private runWith scope handoffs =
    let req = requestFor scope Loop.Text
    let cap = newCapture ()
    let ports = { fakePorts validCatalog gitSrcChange cap req with Handoffs = fun _ -> handoffs }
    Interpreter.run ports req

let private selectedIds (m: Loop.Model) = m.SelectedGates |> List.map (fun g -> gateIdValue g.Id)

let private domainIds (m: Loop.Model) =
    selectedIds m |> List.filter (fun id -> not (id.StartsWith "sdd-handoff:"))

let private selectedGateFor (m: Loop.Model) (id: string) : SelectedGate =
    (Option.get m.Result).SelectedGates
    |> List.find (fun sg -> gateIdValue sg.Gate.Id = id)

let private selectedGateContaining (m: Loop.Model) (substr: string) : SelectedGate =
    (Option.get m.Result).SelectedGates
    |> List.find (fun sg -> (gateIdValue sg.Gate.Id).Contains substr)

[<Tests>]
let tests =
    testList
        "GovernedRefRouting"
        [ // ── US1 V1 (SC-001): declared surface drives selection with an EMPTY sensed change set ──
          test "V1 — a declared governed path selects its domain gates with no sensed change (SC-001)" {
              let m = runWith (Loop.ExplicitPaths []) [ handoffRead (declaringJson "\"src/Lib/Thing.fs\", \"work/flow/Step.fs\"") ]
              let ids = selectedIds m

              Expect.contains ids "package-api:build" "the declared src path selects package-api:build"
              Expect.contains ids "package-api:format" "the declared src path selects package-api:format"
              Expect.contains ids "workflow:audit" "the declared work path selects workflow:audit"

              // The selecting path on a declared-driven gate names the declared path + the real matched glob.
              let build = selectedGateFor m "package-api:build"

              Expect.exists
                  build.SelectingPaths
                  (fun sp -> sp.Path = GovernedPath "src/Lib/Thing.fs" && sp.MatchedGlob = GovernedPath "src/**")
                  "the selecting path names the declared path and the real src/** glob"
          }

          // ── US2 V2(a) (SC-006): real-glob provenance, not the consume self-glob ──
          test "V2a — a declared-driven domain gate carries the REAL path-map glob, not the self-glob (SC-006)" {
              let m = runWith (Loop.ExplicitPaths []) [ handoffRead (declaringJson "\"src/Lib/Thing.fs\"") ]

              let build = selectedGateFor m "package-api:build"

              Expect.all
                  build.SelectingPaths
                  (fun sp -> sp.MatchedGlob = GovernedPath "src/**" && sp.MatchedGlob <> sp.Path)
                  "every selecting path on the domain gate carries the real src/** glob (Path <> MatchedGlob)"

              // FR-009 unchanged: the handoff's OWN gate still carries its self-glob (Path = MatchedGlob).
              let evidence = selectedGateContaining m "sdd-handoff:evidence"

              Expect.all
                  evidence.SelectingPaths
                  (fun sp -> sp.MatchedGlob = sp.Path)
                  "the handoff's own gate keeps its self-glob pre-selection (Path = MatchedGlob)"
          }

          // ── US2 V2(b) (SC-003): a path in BOTH sources is merged + counted once ──
          test "V2b — a path present in both the sensed set and governedReferences selects once (SC-003)" {
              // DefaultRange over gitSrcChange senses src/Lib/Thing.fs; the handoff declares the SAME path.
              let m = runWith Loop.DefaultRange [ handoffRead (declaringJson "\"src/Lib/Thing.fs\"") ]

              let build = selectedGateFor m "package-api:build"

              let forThisPath =
                  build.SelectingPaths |> List.filter (fun sp -> sp.Path = GovernedPath "src/Lib/Thing.fs")

              Expect.equal (List.length forThisPath) 1 "the doubly-sourced path yields exactly one selecting-path entry (deduped)"

              // package-api:build appears once in the selection (counted once in the cost rollup).
              Expect.equal
                  (selectedIds m |> List.filter ((=) "package-api:build") |> List.length)
                  1
                  "the gate is selected exactly once"
          }

          // ── US3 V5(a) (SC-002): no handoff ⇒ byte-identical to the pre-feature projection ──
          test "V5a — no handoff ⇒ the route doc is byte-identical to the pre-F082 projection (SC-002)" {
              let m = runWith Loop.DefaultRange []
              let candidates = candidatesOf gitSrcChange defaultOpts
              let _, expectedRoute = projectExpected validCatalog candidates (Some(snapshotOf gitSrcChange defaultOpts))
              Expect.equal (Option.get m.RouteDoc) expectedRoute "the absent-handoff route doc matches the pre-seam pipeline (the identity merge)"
          }

          // ── US3 V5(b) (SC-002): an empty-governedReferences handoff adds no routing candidate ──
          test "V5b — an empty governedReferences handoff contributes no routing candidate (SC-002)" {
              let baseline = runWith Loop.DefaultRange [] |> domainIds
              let m = runWith Loop.DefaultRange [ handoffRead emptyRefsJson ]

              Expect.equal (domainIds m) baseline "the empty-list handoff selects the same domain gates as no handoff"
              Expect.exists (selectedIds m) (fun id -> id.Contains "sdd-handoff:evidence") "the handoff's own evidence gate still pre-selects (F081 unchanged)"
          }

          // ── US3 V6 (SC-005): a bad document contributes zero candidates yet still fires its integrity gate ──
          test "V6 — a malformed handoff adds zero routing candidates yet keeps its blocking integrity gate (SC-005)" {
              let baseline = runWith Loop.DefaultRange [] |> domainIds
              let m = runWith Loop.DefaultRange [ handoffRead malformedJson ]

              Expect.equal (domainIds m) baseline "the bad document widens NO routing enforcement (zero candidates)"
              Expect.exists (selectedIds m) (fun id -> id.Contains "sdd-handoff:integrity") "its blocking integrity gate still appears (consume, unchanged)"
          } ]
