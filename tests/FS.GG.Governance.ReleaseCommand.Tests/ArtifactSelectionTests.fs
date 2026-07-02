module FS.GG.Governance.ReleaseCommand.Tests.ArtifactSelectionTests

open Expecto
open FS.GG.Governance.ReleaseCommand

// M-CLI-4: `Interpreter.chooseSurfaceArtifact` must isolate THIS surface's artifact from the shared
// `~/.local/share/nuget-local` feed, which accumulates every package's nupkgs and stale versions. The old
// reader took the newest-mtime `*.nupkg` with NO name filter — so a different package or a stale file could be
// attested. The selection is pure: filter by the exact package id, then the HIGHEST semantic version, never
// wall-clock mtime.

let private choose = Interpreter.chooseSurfaceArtifact

[<Tests>]
let tests =
    testList
        "ArtifactSelectionTests"
        [ test "picks the surface's own nupkg, never a co-resident foreign package" {
              let feed =
                  [ "Other.Package.9.9.9.nupkg"; "FS.GG.Governance.ReleaseRules.1.0.0.nupkg"; "Unrelated.2.0.0.nupkg" ]

              Expect.equal
                  (choose "FS.GG.Governance.ReleaseRules" feed)
                  (Some "FS.GG.Governance.ReleaseRules.1.0.0.nupkg")
                  "the package-id filter must exclude foreign packages"
          }

          test "among several versions of the surface, the highest semver wins (not lexical, not mtime)" {
              // Ordinal ordering would pick 1.10.0 below 1.9.0; mtime is order-of-listing here. Semver wins.
              let feed = [ "Pkg.1.9.0.nupkg"; "Pkg.1.10.0.nupkg"; "Pkg.1.2.0.nupkg" ]
              Expect.equal (choose "Pkg" feed) (Some "Pkg.1.10.0.nupkg") "highest semantic version"
          }

          test "a released version outranks a co-resident pre-release of the same package" {
              let feed = [ "Pkg.2.0.0-alpha.1.nupkg"; "Pkg.2.0.0.nupkg" ]
              Expect.equal (choose "Pkg" feed) (Some "Pkg.2.0.0.nupkg") "release ranks above its pre-release"
          }

          test "an exact-id match rejects a sibling-prefixed package the glob would catch" {
              // `Foo.*` would match `Foo.Bar.*`; the exact `<id>.<version>.nupkg` reconstruction rejects it.
              let feed = [ "Foo.Bar.3.0.0.nupkg" ]
              Expect.equal (choose "Foo" feed) None "Foo must not attest the sibling package Foo.Bar"
          }

          test "no matching artifact ⇒ None" {
              Expect.equal (choose "Pkg" []) None "empty feed"
              Expect.equal (choose "Pkg" [ "Other.1.0.0.nupkg" ]) None "only foreign packages"
          }

          test "selection is deterministic regardless of listing order" {
              let a = choose "Pkg" [ "Pkg.1.2.0.nupkg"; "Pkg.2.0.0.nupkg"; "Pkg.1.10.0.nupkg" ]
              let b = choose "Pkg" [ "Pkg.2.0.0.nupkg"; "Pkg.1.10.0.nupkg"; "Pkg.1.2.0.nupkg" ]
              Expect.equal a b "same set, any order ⇒ same choice"
              Expect.equal a (Some "Pkg.2.0.0.nupkg") "highest wins"
          } ]
