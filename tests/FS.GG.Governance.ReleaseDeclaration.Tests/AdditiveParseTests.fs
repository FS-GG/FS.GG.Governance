module FS.GG.Governance.ReleaseDeclaration.Tests.AdditiveParseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseDeclaration.Tests.Support

// The 065 ADDITIVE sections: `packableProjects` (SurfaceId * pack GateCommand * baseline option) and the
// optional declared exhaustive `matrix`. GD-3 backward-compat: a release.yml with neither section parses
// with `PackableProjects = []` and `Matrix = None`. A malformed packable/matrix entry ⇒ `Error DeclError`
// (never partial facts). Every value comes from the file (product-neutral).

[<Tests>]
let tests =
    testList
        "Declaration.additive"
        [ test "packableProjects are parsed into surface + pack GateCommand + baseline" {
              let d = okDecl releaseYmlWithPackablesAndMatrix
              Expect.equal (d.PackableProjects |> List.length) 2 "two declared packable projects"

              let p0 = d.PackableProjects.[0]
              Expect.equal p0.Surface (SurfaceId "pkg") "first surface"
              Expect.equal p0.Baseline (Some "1.2.0") "first baseline"
              let (Executable exe) = p0.PackCommand.Executable
              Expect.equal exe "dotnet" "executable"
              Expect.equal (p0.PackCommand.Arguments |> List.map (fun (Argument a) -> a)) [ "pack"; "src/Pkg/Pkg.fsproj"; "-c"; "Release" ] "arguments in order"
              let (WorkingDirectory wd) = p0.PackCommand.WorkingDirectory
              Expect.equal wd "." "working directory"
              let (TimeoutLimit secs) = p0.PackCommand.Timeout
              Expect.equal secs 300 "timeout seconds"
              Expect.equal p0.PackCommand.CapturedOutput NoCapturedOutput "no captured-output target"
          }

          test "a packable project with no baseline is a first release (Baseline = None)" {
              let d = okDecl releaseYmlWithPackablesAndMatrix
              let p1 = d.PackableProjects.[1]
              Expect.equal p1.Surface (SurfaceId "pkg-first") "second surface"
              Expect.equal p1.Baseline None "no baseline ⇒ first release"
              // workingDirectory defaults to '.'; timeout defaults to 600 when omitted.
              let (WorkingDirectory wd) = p1.PackCommand.WorkingDirectory
              Expect.equal wd "." "default working directory"
              let (TimeoutLimit secs) = p1.PackCommand.Timeout
              Expect.equal secs 600 "default timeout"
          }

          test "the declared exhaustive matrix is parsed (name + cost + dimensions)" {
              let d = okDecl releaseYmlWithPackablesAndMatrix

              Expect.equal
                  d.Matrix
                  (Some { Name = "cross-target"; Cost = Exhaustive; Dimensions = [ "net8"; "net9"; "net10" ] })
                  "declared matrix verbatim"
          }

          test "GD-3: a release.yml with NO packableProjects/matrix parses with [] and None" {
              let d = okDecl releaseYmlAllBlocking
              Expect.equal d.PackableProjects [] "no packable projects ⇒ []"
              Expect.equal d.Matrix None "no matrix ⇒ None"
              // ...and the F055 trio is unaffected by the additive parse.
              Expect.equal (d.Rules |> List.length) 6 "six rules still"
          }

          test "a packableProjects entry missing its packCommand is malformed" {
              let yml =
                  releaseYmlAllBlocking
                  + "packableProjects:\n  - surface: pkg\n    baseline: \"1.0.0\"\n"

              Expect.isTrue (isErr yml) "missing packCommand rejected"
          }

          test "a packCommand missing its executable is malformed" {
              let yml =
                  releaseYmlAllBlocking
                  + "packableProjects:\n  - surface: pkg\n    packCommand:\n      arguments: [pack]\n"

              Expect.isTrue (isErr yml) "missing executable rejected"
          }

          test "a non-positive timeoutSeconds is malformed" {
              let yml =
                  releaseYmlAllBlocking
                  + "packableProjects:\n  - surface: pkg\n    packCommand:\n      executable: dotnet\n      arguments: [pack]\n      timeoutSeconds: nope\n"

              Expect.isTrue (isErr yml) "non-integer timeout rejected"
          }

          test "a matrix with an unrecognized cost is malformed" {
              let yml =
                  releaseYmlAllBlocking
                  + "matrix:\n  name: m\n  cost: gigantic\n  dimensions: [a]\n"

              Expect.isTrue (isErr yml) "unrecognized cost rejected"
          }

          test "a matrix missing its dimensions is malformed" {
              let yml = releaseYmlAllBlocking + "matrix:\n  name: m\n  cost: cheap\n"
              Expect.isTrue (isErr yml) "missing dimensions rejected"
          } ]
