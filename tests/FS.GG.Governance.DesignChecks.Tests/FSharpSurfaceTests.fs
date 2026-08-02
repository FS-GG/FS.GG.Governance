module FS.GG.Governance.DesignChecks.Tests.FSharpSurfaceTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DesignChecks.FSharpSurface

module SC = FS.GG.Governance.SurfaceChecks.Model

let private request : SC.SurfaceCheckRequest =
    { Domain = SC.DesignDomain
      Surface = SurfaceId "fsharp-public-surface"
      Class = PackageSurface
      Path = normalizePath "src/App.fsproj"
      EvidenceTag = None }

let private moduleFacts =
    { Project = "src/App.fsproj"
      Source = normalizePath "src/Domain.fs"
      Signature = None
      SourceCompileIndex = 3
      SignatureCompileIndex = None
      IsTestProject = false
      IsExplicitlyInternal = false
      IsEntryPoint = false
      IsGenerated = false
      Exemption = NoExemption
      Declarations = []
      SignatureMatchesSource = true
      RequiresSurfaceBaseline = false
      SurfaceBaselineCurrent = true }

let private temporaryProject (files: (string * string) list) action =
    let root = Path.Combine(Path.GetTempPath(), "fsgg-fsharp-surface-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory root |> ignore
    try
        for relative, content in files do
            let path = Path.Combine(root, relative)
            let directory = Path.GetDirectoryName(path) |> Option.ofObj |> Option.defaultValue root
            Directory.CreateDirectory(directory) |> ignore
            let buildableContent =
                if relative.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)
                   && not (content.Contains("<TargetFramework>", StringComparison.Ordinal)) then
                    let openingEnd = content.IndexOf('>') + 1
                    content.Insert(openingEnd, "<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>")
                else content
            File.WriteAllText(path, buildableContent)
        action root
    finally
        Directory.Delete(root, true)

[<Tests>]
let tests =
    testList
        "FSharpSurface.evaluate"
        [ test "Rogue3-shaped executable module without a signature produces an advisory migration finding" {
              let findings = evaluate request [ moduleFacts ]
              let finding = findings |> List.exactlyOne
              Expect.equal finding.Code "fsharp.signature-missing" "names the policy"
              Expect.equal finding.Domain SC.DesignDomain "uses the reusable design-check pack"
              Expect.stringContains finding.Message "curated .fsi" "gives bounded remediation"
          }

          test "internal modules, entry points, generated sources, and complete exemptions do not need a signature" {
              let variants =
                  [ { moduleFacts with IsExplicitlyInternal = true }
                    { moduleFacts with IsEntryPoint = true }
                    { moduleFacts with IsGenerated = true }
                    { moduleFacts with Exemption = ActiveExemption("owner", "generated bridge", "2026-10-01") } ]

              Expect.isEmpty (evaluate request variants) "each explicit non-public or governed exception clears the rule"
          }

          test "paired signatures report order, docs, source mismatch, and applicable baseline independently" {
              let facts =
                  { moduleFacts with
                      Signature = Some(normalizePath "src/Domain.fsi")
                      SignatureCompileIndex = Some 1
                      Declarations = [ { Name = "parse"; HasXmlDocumentation = false } ]
                      SignatureMatchesSource = false
                      RequiresSurfaceBaseline = true
                      SurfaceBaselineCurrent = false }

              let codes = evaluate request [ facts ] |> List.map (fun finding -> finding.Code)
              Expect.equal
                  codes
                  [ "fsharp.signature-compile-order"; "fsharp.signature-docs"; "fsharp.signature-source-mismatch"; "fsharp.surface-baseline-stale" ]
                  "each independent contract failure is named"
          }

          test "test-project modules are excluded" {
              Expect.isEmpty (evaluate request [ { moduleFacts with IsTestProject = true } ]) "test source is outside policy"
          }

          test "project sensor preserves Compile order and sees a Rogue3-shaped source as public by default" {
              temporaryProject
                  [ "App.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include=\"Program.fs\" /><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Program.fs", "[<EntryPoint>] let main _ = 0"
                    "Domain.fs", "module Domain\nlet value = 1" ]
                  (fun root ->
                      match senseProject root "App.fsproj" false false true with
                      | Error error -> failtest error
                      | Ok facts ->
                          Expect.equal (facts |> List.map (fun f -> f.SourceCompileIndex)) [ 0; 1 ] "uses project compile order"
                          let findings = evaluate request facts
                          Expect.equal (findings |> List.map (fun f -> f.Code)) [ "fsharp.signature-missing" ] "entry point is exempt while public domain module is not")
          }

          test "project sensor reads documented signatures and fail-closes missing compiled source" {
              temporaryProject
                  [ "Lib.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fsi\" /><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Domain.fsi", "/// Domain\nmodule Domain\n/// Parses input\nval parse: string -> int"
                    "Domain.fs", "module Domain\nlet parse (text: string) = text.Length" ]
                  (fun root ->
                      match senseProject root "Lib.fsproj" false true true with
                      | Error error -> failtest error
                      | Ok facts -> Expect.isEmpty (evaluate request facts) "a paired documented source is clean")
          }

          test "project sensor detects an adjacent signature declaration absent from implementation" {
              temporaryProject
                  [ "Lib.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fsi\" /><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Domain.fsi", "/// Domain\nmodule Domain\n/// Parses input\nval parse: string -> int"
                    "Domain.fs", "module Domain\nlet format (text: string) = text.Length" ]
                  (fun root ->
                      match senseProject root "Lib.fsproj" false false true with
                      | Error error -> failtest error
                      | Ok facts ->
                          let codes = evaluate request facts |> List.map (fun finding -> finding.Code)
                          Expect.contains codes "fsharp.signature-source-mismatch" "compilation adjacency is not mistaken for contract compatibility")
          }

          test "compiler rejects an adjacent same-name declaration with an incompatible type" {
              temporaryProject
                  [ "Lib.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fsi\" /><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Domain.fsi", "/// Domain\nmodule Domain\n/// value\nval value: string"
                    "Domain.fs", "module Domain\nlet value = 1" ]
                  (fun root ->
                      let facts = senseProject root "Lib.fsproj" false false true |> Result.defaultWith failtest
                      Expect.contains (evaluate request facts |> List.map _.Code) "fsharp.signature-source-mismatch" "real F# compiler owns compatibility")
          }

          test "compiler validates a signature in its real earlier-source project context" {
              temporaryProject
                  [ "Probe.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Types.fs\" /><Compile Include=\"Public.fsi\" /><Compile Include=\"Public.fs\" /></ItemGroup></Project>"
                    "Types.fs", "module Types\ntype SharedThing = SharedThing of int"
                    "Public.fsi", "/// Public API\nmodule Public\n/// Accepts a shared value\nval accept: Types.SharedThing -> int"
                    "Public.fs", "module Public\nlet accept (Types.SharedThing value) = value" ]
                  (fun root ->
                      let facts = senseProject root "Probe.fsproj" false false true |> Result.defaultWith failtest
                      Expect.isFalse
                          (evaluate request facts |> List.exists (fun finding -> finding.Code = "fsharp.signature-source-mismatch"))
                          "earlier compiled types participate in compatibility")
          }

          test "compiler validates a signature with the real project-reference context" {
              temporaryProject
                  [ "Dependency/Dependency.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Shared.fs\" /></ItemGroup></Project>"
                    "Dependency/Shared.fs", "namespace Dependency\ntype SharedThing = SharedThing of int"
                    "Probe.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"Dependency/Dependency.fsproj\" /><Compile Include=\"Public.fsi\" /><Compile Include=\"Public.fs\" /></ItemGroup></Project>"
                    "Public.fsi", "/// Public API\nmodule Public\n/// Accepts a shared value\nval accept: Dependency.SharedThing -> int"
                    "Public.fs", "module Public\nlet accept (Dependency.SharedThing value) = value" ]
                  (fun root ->
                      let facts = senseProject root "Probe.fsproj" false false true |> Result.defaultWith failtest
                      Expect.isFalse
                          (evaluate request facts |> List.exists (fun finding -> finding.Code = "fsharp.signature-source-mismatch"))
                          "project references participate in compatibility")
          }

          test "live policy carries valid exemptions and stale applicable baselines" {
              temporaryProject
                  [ "App.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Domain.fs", "module Domain\nlet value = 1"
                    ".fsgg/fsharp-surface.json", "{\"declaredGlob\":\"src/**/Public*.fsi\",\"projects\":{\"App.fsproj\":{\"requiresBaseline\":true,\"baselineCurrent\":false}},\"exemptions\":[{\"module\":\"Domain.fs\",\"owner\":\"team\",\"rationale\":\"migration\",\"reviewBy\":\"2099-01-01\"}]}" ]
                  (fun root ->
                      let facts = senseProject root "App.fsproj" false false true |> Result.defaultWith failtest
                      let codes = evaluate request facts |> List.map _.Code
                      Expect.isFalse (List.contains "fsharp.signature-missing" codes) "valid exemption is reachable"
                      Expect.contains codes "fsharp.surface-baseline-stale" "configured applicable baseline is reachable"
                      Expect.equal (receipt root "App.fsproj" false false true request).DeclaredGlob "src/**/Public*.fsi" "typed config supplies receipt glob")
          }

          test "expired live exemption fails closed" {
              temporaryProject
                  [ "App.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Domain.fs", "module Domain\nlet value = 1"
                    ".fsgg/fsharp-surface.json", "{\"exemptions\":[{\"module\":\"Domain.fs\",\"owner\":\"team\",\"rationale\":\"migration\",\"reviewBy\":\"2000-01-01\"}]}" ]
                  (fun root ->
                      let facts = senseProject root "App.fsproj" false false true |> Result.defaultWith failtest
                      Expect.contains (evaluate request facts |> List.map _.Code) "fsharp.exemption-invalid" "expired exemption is input-state evidence")
          }

          test "versioned receipt is deterministic and malformed project input fails closed" {
              temporaryProject
                  [ "src/Lib/Lib.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fsi\" /><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "src/Lib/Domain.fsi", "/// Domain\nmodule Domain\n/// value\nval value: int"
                    "src/Lib/Domain.fs", "module Domain\nlet value = 1" ]
                  (fun root ->
                      let first = receipt root "src/Lib/Lib.fsproj" false false true request |> receiptJson
                      let second = receipt root "src/Lib/Lib.fsproj" false false true request |> receiptJson
                      Expect.equal first second "the receipt has no clock or machine-specific fields"
                      Expect.stringContains first "\"kind\":\"fsharp-public-surface\"" "identifies stable contract"
                      Expect.stringContains first "\"applicability\":\"applicable\"" "disposition is explicit"
                      Expect.stringContains first "\"matchedModules\":[\"src/Lib/Domain.fsi\"]" "matched modules are repo-relative and explicit"
                      Expect.stringContains first "\"findings\":[]" "clean signature carries an explicit empty finding set"
                      Expect.stringContains first "\"cardinality\":\"one\"" "one match is explicit"
                      let malformed = receipt root "Missing.fsproj" false false true request
                      Expect.isSome malformed.Malformed "missing project is explicit, never a pass"
                      Expect.isNone malformed.FreshnessDigest "malformed input has no freshness digest")
          }

          test "receipt distinguishes zero and many matched modules deterministically" {
              temporaryProject
                  [ "Zero.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Only.fsi\" /></ItemGroup></Project>"
                    "Only.fsi", "/// marker\ntype Marker = class end"
                    "Many.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"A.fsi\" /><Compile Include=\"A.fs\" /><Compile Include=\"B.fsi\" /><Compile Include=\"B.fs\" /></ItemGroup></Project>"
                    "A.fsi", "/// A\nmodule A\n/// a\nval a: int"
                    "A.fs", "module A\nlet a = 1"
                    "B.fsi", "/// B\nmodule B\n/// b\nval b: int"
                    "B.fs", "module B\nlet b = 2"
                    ".fsgg/fsharp-surface.json", "{\"declaredGlob\":\"**/*.fsi\"}" ]
                  (fun root ->
                      let zero = receipt root "Zero.fsproj" false false true request
                      let many = receipt root "Many.fsproj" false false true request
                      Expect.equal (zero.MatchedModuleCount, zero.Cardinality) (0, "zero") "zero is explicit"
                      Expect.equal (many.MatchedModuleCount, many.Cardinality) (2, "many") "many is explicit"
                      Expect.equal (receiptJson many) (receiptJson many) "many output is deterministic")
          }

          test "configured glob selects repo-relative compiled signatures for zero one and many cardinality" {
              temporaryProject
                  [ "Probe.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fsi\" /><Compile Include=\"Domain.fs\" /><Compile Include=\"src/PublicOne.fsi\" /><Compile Include=\"src/PublicOne.fs\" /><Compile Include=\"src/nested/PublicTwo.fsi\" /><Compile Include=\"src/nested/PublicTwo.fs\" /><Compile Include=\"src/nested/Internal.fsi\" /><Compile Include=\"src/nested/Internal.fs\" /></ItemGroup></Project>"
                    "Domain.fsi", "/// Domain\nmodule Domain\n/// value\nval value: int"
                    "Domain.fs", "module Domain\nlet value = 1"
                    "src/PublicOne.fsi", "/// Public one\nmodule PublicOne\n/// value\nval value: int"
                    "src/PublicOne.fs", "module PublicOne\nlet value = 1"
                    "src/nested/PublicTwo.fsi", "/// Public two\nmodule PublicTwo\n/// value\nval value: int"
                    "src/nested/PublicTwo.fs", "module PublicTwo\nlet value = 2"
                    "src/nested/Internal.fsi", "/// Internal\nmodule Internal\n/// value\nval value: int"
                    "src/nested/Internal.fs", "module Internal\nlet value = 3"
                    ".fsgg/fsharp-surface.json", "{\"declaredGlob\":\"src/**/Public*.fsi\"}" ]
                  (fun root ->
                      let many = receipt root "Probe.fsproj" false false true request
                      Expect.equal many.MatchedModules [ "src/PublicOne.fsi"; "src/nested/PublicTwo.fsi" ] "positive and nested matches exclude root and non-Public signatures"
                      Expect.equal (many.MatchedModuleCount, many.Cardinality) (2, "many") "many derives from filtered matches"
                      File.WriteAllText(Path.Combine(root, ".fsgg", "fsharp-surface.json"), "{\"declaredGlob\":\"src/**/PublicOne.fsi\"}")
                      let one = receipt root "Probe.fsproj" false false true request
                      Expect.equal (one.MatchedModules, one.Cardinality) ([ "src/PublicOne.fsi" ], "one") "one derives from the same configured selection"
                      File.WriteAllText(Path.Combine(root, ".fsgg", "fsharp-surface.json"), "{\"declaredGlob\":\"src/**/Missing*.fsi\"}")
                      let zero = receipt root "Probe.fsproj" false false true request
                      Expect.equal (zero.MatchedModules, zero.Cardinality) ([], "zero") "zero cannot be bypassed by an out-of-glob paired signature")
          }

          test "root signature does not satisfy a src-only declared glob" {
              temporaryProject
                  [ "Probe.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fsi\" /><Compile Include=\"Domain.fs\" /></ItemGroup></Project>"
                    "Domain.fsi", "/// Domain\nmodule Domain\n/// value\nval value: int"
                    "Domain.fs", "module Domain\nlet value = 1"
                    ".fsgg/fsharp-surface.json", "{\"declaredGlob\":\"src/**/Public*.fsi\"}" ]
                  (fun root ->
                      let result = receipt root "Probe.fsproj" false false true request
                      Expect.equal result.DeclaredGlob "src/**/Public*.fsi" "configured glob is reported"
                      Expect.equal (result.MatchedModules, result.MatchedModuleCount, result.Cardinality) ([], 0, "zero") "paired root Domain.fsi remains outside the declared surface")
          }

          test "Rogue3-shaped many implementations and zero signatures reports applicable zero matches" {
              temporaryProject
                  [ "Game.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><OutputType>Exe</OutputType><ItemGroup><Compile Include=\"Program.fs\" /><Compile Include=\"World.fs\" /></ItemGroup></Project>"
                    "Program.fs", "[<EntryPoint>] let main _ = 0"
                    "World.fs", "module World\nlet tick = 1" ]
                  (fun root ->
                      let result = receipt root "Game.fsproj" false false true request
                      Expect.isTrue result.Applicable "non-test executable is applicable"
                      Expect.equal result.Applicability "applicable" "disposition is not inferred from count"
                      Expect.equal (result.MatchedModuleCount, result.Cardinality) (0, "zero") "matches signatures, not implementations")
          } ]
