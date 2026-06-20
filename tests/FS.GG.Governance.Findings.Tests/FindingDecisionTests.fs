module FS.GG.Governance.Findings.Tests.FindingDecisionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Findings
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Tests.Support

// US1: a non-routine `UnmatchedInRoot` path becomes exactly one ordinary `UnknownGovernedPath`
// finding; routed siblings produce none (SC-001).
// US2: `OutOfScope` paths and `UnmatchedInRoot` paths within a declared `Routine` surface stay
// silent; non-routine in-root unknowns still flag — no global default-deny (SC-002).

// Governed root "src"; a path map covering src/Kernel/** only. "src/New.fs" is in-root but
// matches no glob → UnmatchedInRoot; "docs/..." is OutOfScope.
let private baseFacts surfaces =
    facts "src" [ "src/Kernel/**", "kernel" ] surfaces

let private findingFor (report: FindingReport) (path: string) =
    report.Findings |> List.filter (fun f -> f.Path = normalizePath path)

[<Tests>]
let tests =
    testList
        "FindingDecision"
        [ test "a non-routine UnmatchedInRoot path yields exactly one ordinary finding (US1 AS1, SC-001)" {
              let facts = baseFacts []
              let report = routeOf facts [ "src/New.fs"; "src/Kernel/Eval.fs" ]
              let result = Findings.findUnknownGovernedPaths facts report

              let f = findingFor result "src/New.fs"
              Expect.hasLength f 1 "exactly one finding for the in-root unknown"
              let one = List.head f
              Expect.equal one.Id UnknownGovernedPath "ordinary id"
              Expect.equal one.Zone GovernedRootUnknown "ordinary zone"
              Expect.equal one.Path (normalizePath "src/New.fs") "located on the offending normalized path"
              Expect.isNotEmpty one.Message "carries a fix-hint message"
          }

          test "a routed sibling produces no finding (US1 AS1, FR-005)" {
              let facts = baseFacts []
              let report = routeOf facts [ "src/New.fs"; "src/Kernel/Eval.fs" ]
              let result = Findings.findUnknownGovernedPaths facts report

              Expect.isEmpty (findingFor result "src/Kernel/Eval.fs") "the routed path is never an unknown"
          }

          test "mixed Routed/UnmatchedInRoot/OutOfScope → a finding only for each non-routine in-root unknown (US1 AS2)" {
              let facts = baseFacts []

              let report =
                  routeOf facts [ "src/Kernel/Eval.fs"; "src/New.fs"; "src/Other.fs"; "docs/guide.md" ]

              let result = Findings.findUnknownGovernedPaths facts report

              let paths = result.Findings |> List.map (fun f -> f.Path)

              Expect.equal
                  paths
                  [ normalizePath "src/New.fs"; normalizePath "src/Other.fs" ]
                  "only the two in-root unknowns flag — routed and out-of-scope are silent"
          }

          test "an all-Routed/OutOfScope set yields an empty, successful FindingReport (US1 AS3, FR-012)" {
              let facts = baseFacts []
              let report = routeOf facts [ "src/Kernel/Eval.fs"; "docs/guide.md" ]
              let result = Findings.findUnknownGovernedPaths facts report

              Expect.equal result { Findings = [] } "empty finding set is a valid success, not an error"
          }

          test "an OutOfScope path is never a finding regardless of count (US2 AS1, FR-003)" {
              let facts = baseFacts []
              let report = routeOf facts [ "docs/a.md"; "docs/b.md"; "elsewhere/c.txt" ]
              let result = Findings.findUnknownGovernedPaths facts report

              Expect.isEmpty result.Findings "no global default-deny for out-of-scope paths"
          }

          test "an UnmatchedInRoot path within a declared Routine surface is suppressed (US2 AS2, FR-004)" {
              let facts = baseFacts [ surface Routine "legacy" [ "src/Legacy" ] ]
              let report = routeOf facts [ "src/Legacy/Old.fs" ]
              let result = Findings.findUnknownGovernedPaths facts report

              Expect.isEmpty result.Findings "a declared routine region is, by declaration, not an unknown"
          }

          test "out-of-scope + routine + a third in-root unknown → only the third flags (US2 AS3, SC-002)" {
              let facts = baseFacts [ surface Routine "legacy" [ "src/Legacy" ] ]

              let report =
                  routeOf facts [ "docs/guide.md"; "src/Legacy/Old.fs"; "src/New.fs" ]

              let result = Findings.findUnknownGovernedPaths facts report

              Expect.equal
                  (result.Findings |> List.map (fun f -> f.Path))
                  [ normalizePath "src/New.fs" ]
                  "zero for out-of-scope and routine, exactly one for the non-routine in-root unknown"
          } ]
