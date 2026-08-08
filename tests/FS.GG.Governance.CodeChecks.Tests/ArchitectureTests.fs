module FS.GG.Governance.CodeChecks.Tests.ArchitectureTests

open Expecto
open FS.GG.Governance.CodeChecks.Model
open FS.GG.Governance.CodeChecks.CodeChecks

let thresholds = { ModuleLines = None; TypeLines = None; MemberLines = None; DependencyFanOut = None }

let request source =
    { Head = "abc123"
      // SYNTHETIC: planted compiler input isolates architecture facts; packed public-route smoke is T009.
      Documents = [ { Path = "src/Domain.fs"; Source = source; IsGenerated = false; References = [] } ]
      PureDomainPrefixes = [ "src/" ]
      Thresholds = thresholds
      Justifications = []
      ApprovedPrimitives = [] }

let ids report = report.Findings |> List.map _.Id |> Set.ofList
let analyzeNow req = analyze req |> Async.RunSynchronously

[<Tests>]
let tests =
    testList "F# simplicity architecture sensor" [
        test "Idiomatic modules records and DUs Synthetic pass" {
            let source = "module Domain\n\ntype State = Ready | Running of int\ntype Item = { Id: int }\nlet advance state = match state with Ready -> Running 1 | x -> x\n"
            let report = request source |> analyzeNow
            Expect.isEmpty report.Findings (sprintf "%A" report.Findings)
        }

        test "Hierarchy shared registry and reflection Synthetic require justification" {
            let source = "module Domain\n[<AbstractClass>]\ntype Base() = class end\ntype Child() = inherit Base()\nlet mutable registry : int list = []\nlet inspect () = typeof<Child>.GetMethods()\n"
            let found = request source |> analyzeNow |> ids
            Expect.isTrue (found.Contains AbstractClassHierarchy) "abstract hierarchy"
            Expect.isTrue (found.Contains InheritanceHierarchy) "inheritance"
            Expect.isTrue (found.Contains SharedMutableState) "shared mutation"
            Expect.isTrue (found.Contains ReflectionOrMetaprogramming) "reflection"
        }

        test "Unused reflection namespace open Synthetic is not reflection use" {
            let source = "module Domain\nopen System.Reflection\ntype State = Ready | Running\nlet next = function Ready -> Running | Running -> Ready\n"
            let report = request source |> analyzeNow
            Expect.isFalse ((ids report).Contains ReflectionOrMetaprogramming) (sprintf "%A" report.Findings)
        }

        test "System Type reflection call without namespace open Synthetic is detected" {
            let source = "module Domain\ntype C() = class end\nlet inspect () = typeof<C>.GetMethods()\n"
            let reflection =
                request source |> analyzeNow |> fun report ->
                    report.Findings |> List.filter (fun finding -> finding.Id = ReflectionOrMetaprogramming)
            Expect.isNonEmpty reflection "actual reflection call is detected"
            Expect.isTrue (reflection |> List.exists (fun finding -> finding.Symbol = "System.Type.GetMethods")) "finding names the invoked reflection API"
        }

        test "Measured array loop Synthetic current justification suppresses exact construct" {
            let source = "module Domain\nlet sum (xs: int array) =\n    let mutable total = 0\n    for value in xs do total <- total + value\n    total\n"
            let initial = request source |> analyzeNow
            let imperative = initial.Findings |> List.filter (fun f -> f.Id = ImperativeInPureDomain)
            Expect.isNonEmpty imperative "planted loop/mutation found"
            Expect.isTrue (imperative |> List.exists (fun f -> f.Symbol.EndsWith(":FOR"))) "compiler-tokenized loop found"
            let justifications =
                imperative |> List.map (fun f ->
                    { Path = f.Path; Symbol = f.Symbol; Head = "abc123"; SourceDigest = sourceDigest source
                      SimplerAlternative = "Array.sum"; Reason = Measured; Evidence = "benchmark: 18% less allocation" })
            let report = { request source with Justifications = justifications } |> analyzeNow
            Expect.isFalse ((ids report).Contains ImperativeInPureDomain) "exact bindings suppress"
        }

        test "Material edit and head change Synthetic stale justifications" {
            let source = "module Domain\nlet mutable registry : int list = []\n"
            let first = request source |> analyzeNow |> fun r -> r.Findings |> List.find (fun f -> f.Id = SharedMutableState)
            let current =
                { Path = first.Path; Symbol = first.Symbol; Head = "abc123"; SourceDigest = sourceDigest source
                  SimplerAlternative = "thread state"; Reason = Interoperability; Evidence = "framework callback" }
            let changed = { request (source + "let x = 1\n") with Justifications = [ current ] } |> analyzeNow
            Expect.equal (changed.Findings |> List.find (fun f -> f.Id = SharedMutableState)).Justification StaleSource "source binding stales"
            let moved = { request source with Head = "def456"; Justifications = [ current ] } |> analyzeNow
            Expect.equal (moved.Findings |> List.find (fun f -> f.Id = SharedMutableState)).Justification StaleHead "head binding stales"
        }

        test "Configured thresholds and declared primitive duplicate Synthetic are explicit" {
            let source = "module Domain\ntype Dispatcher() = class end\nlet one = 1\nlet two = 2\n"
            let req =
                { request source with
                    Thresholds = { thresholds with ModuleLines = Some 2 }
                    ApprovedPrimitives =
                        [ { Capability = "dispatch"; ApprovedSymbols = [ "FS.GG.Dispatch" ]; CandidateSymbols = [ "Domain.Dispatcher" ] } ] }
            let found = req |> analyzeNow |> ids
            Expect.isTrue (found.Contains ModuleSizeReview) "configured size trigger"
            Expect.isTrue (found.Contains DuplicateHomeGrownAbstraction) "declared duplicate"
            Expect.isFalse ((request source |> analyzeNow |> ids).Contains ModuleSizeReview) "no implicit threshold"
        }

        test "Rename mutation Synthetic preserves structural verdict" {
            let duA = request "module Domain\ntype Status = A | B\n" |> analyzeNow |> ids
            let duB = request "module Domain\ntype Renamed = First | Second\n" |> analyzeNow |> ids
            Expect.equal duA duB "non-contractual rename does not alter structure"
            let hierarchy = request "module Domain\ntype X() = class end\ntype Y() = inherit X()\n" |> analyzeNow |> ids
            Expect.isTrue (hierarchy.Contains InheritanceHierarchy) "relationship mutation reds"
        }

        test "Legitimate adapter class Synthetic passes with bound justification" {
            let source = "module Adapter\ntype FrameworkAdapter() = member _.Run() = 1\n"
            let first = request source |> analyzeNow
            let justifications =
                first.Findings |> List.distinctBy (fun f -> f.Path, f.Symbol) |> List.map (fun f ->
                    { Path = f.Path; Symbol = f.Symbol; Head = "abc123"; SourceDigest = sourceDigest source
                      SimplerAlternative = "module function"; Reason = Interoperability; Evidence = "host requires construction" })
            let report = { request source with Justifications = justifications; PureDomainPrefixes = [] } |> analyzeNow
            Expect.isEmpty report.Findings (sprintf "%A" report.Findings)
        }

        test "Parse failure Synthetic fails closed" {
            let report = request "module Domain\nlet =" |> analyzeNow
            Expect.equal report.Findings.Head.Id CompilerAnalysisFailed "typed failure"
            Expect.isNonEmpty report.Diagnostics "actionable compiler diagnostics"
        }

        test "Declared project reference Synthetic reaches real compiler rules and removal fails closed" {
            let source =
                "module Domain\nopen FS.GG.Governance.CodeChecks.Model\ntype Base() = class end\ntype Child() = inherit Base()\nlet token = CompilerAnalysisFailed\n"
            let reference = typeof<FindingId>.Assembly.Location
            let withReference =
                { request source with
                    Documents = [ { Path = "src/Domain.fs"; Source = source; IsGenerated = false; References = [ reference ] } ] }
                |> analyzeNow
            Expect.isTrue ((ids withReference).Contains InheritanceHierarchy) "declared sibling assembly enables real inheritance analysis"
            Expect.isFalse ((ids withReference).Contains CompilerAnalysisFailed) "declared sibling assembly does not short-circuit"

            let withoutReference = request source |> analyzeNow
            Expect.equal withoutReference.Findings.Head.Id CompilerAnalysisFailed "removing the declared reference remains fail-closed"
        }

        test "Generated document Synthetic is explicitly excluded" {
            let req = { request "module Domain\nlet mutable registry = 0" with Documents = [ { Path = "src/G.fs"; Source = "module Domain\nlet mutable registry = 0"; IsGenerated = true; References = [] } ] }
            Expect.isEmpty (analyzeNow req).Findings "explicit generated exclusion"
        }
    ]
