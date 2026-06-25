module FS.GG.Governance.Findings.Tests.BoundaryWideningTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Findings
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Tests.Support

// F23 (FR-003, SC-002) — the escalating-boundary set widens from `ProtectedSurface` alone to
// `ProtectedSurface ∪ PackageSurface ∪ ReleaseSurface ∪ GeneratedProductRoot`. An UnmatchedInRoot path
// under any of those is `UnknownProtectedBoundaryPath` (never a silent pass); under the four non-protected
// product kinds (docs/skill/design/sampleApp) and the inert MVP classes it stays an ordinary
// `UnknownGovernedPath`; under a routine surface it is suppressed; a Routed path is never a finding.
// Exercised over REAL facts + REAL Routing.route (no mocks).

let private testFacts =
    facts
        "."
        [ "routed/**", "d" ]
        [ surface PackageSurface "pkg" [ "pkg-area" ]
          surface ReleaseSurface "rel" [ "rel-area" ]
          surface GeneratedProductRoot "groot" [ "gp-area" ]
          surface DocsSurface "docs" [ "docs-area" ]
          surface SkillSurface "skill" [ "skill-area" ]
          surface DesignSurface "design" [ "design-area" ]
          surface SampleAppSurface "sample" [ "sample-area" ]
          surface GeneratedView "gv" [ "gv-area" ]
          surface Routine "routine" [ "routine-area" ] ]

let private report =
    Findings.findUnknownGovernedPaths
        testFacts
        (routeOf
            testFacts
            [ "pkg-area/Api.fsi"
              "rel-area/notes.md"
              "gp-area/file.fs"
              "docs-area/guide.md"
              "skill-area/s.md"
              "design-area/t.json"
              "sample-area/app.fs"
              "gv-area/gen.fs"
              "routine-area/free.txt"
              "routed/ok.fs" ])

let private find path =
    report.Findings |> List.tryFind (fun f -> f.Path = normalizePath path)

let private expectProtected path sid' =
    let f = find path
    Expect.isSome f (sprintf "%s should be a finding" path)
    Expect.equal f.Value.Id UnknownProtectedBoundaryPath (sprintf "%s escalates" path)
    Expect.equal f.Value.Zone (ProtectedBoundaryUnknown(SurfaceId sid')) (sprintf "%s zone names the boundary" path)

let private expectOrdinary path =
    let f = find path
    Expect.isSome f (sprintf "%s should be a finding" path)
    Expect.equal f.Value.Id UnknownGovernedPath (sprintf "%s is ordinary" path)
    Expect.equal f.Value.Zone GovernedRootUnknown (sprintf "%s zone is governed-root" path)

[<Tests>]
let tests =
    testList
        "Findings.BoundaryWidening.F23"
        [ test "PackageSurface boundary escalates" { expectProtected "pkg-area/Api.fsi" "pkg" }
          test "ReleaseSurface boundary escalates" { expectProtected "rel-area/notes.md" "rel" }
          test "GeneratedProductRoot boundary escalates" { expectProtected "gp-area/file.fs" "groot" }

          test "DocsSurface boundary stays ordinary" { expectOrdinary "docs-area/guide.md" }
          test "SkillSurface boundary stays ordinary" { expectOrdinary "skill-area/s.md" }
          test "DesignSurface boundary stays ordinary" { expectOrdinary "design-area/t.json" }
          test "SampleAppSurface boundary stays ordinary" { expectOrdinary "sample-area/app.fs" }
          test "GeneratedView boundary stays ordinary" { expectOrdinary "gv-area/gen.fs" }

          test "routine surface suppresses the unknown" { Expect.isNone (find "routine-area/free.txt") "routine suppresses" }
          test "a Routed path is never a finding" { Expect.isNone (find "routed/ok.fs") "routed ⇒ no finding" }

          test "the Findings public surface (Findings.surface.txt) is unchanged — behavior-only widening" {
              // No public type changed; the widening is internal. (Surface drift is asserted in
              // SurfaceDriftTests; this is the documentary anchor for the behavior-only contract.)
              Expect.isLessThan 0 (List.length report.Findings) "the widening produces real findings"
          } ]
