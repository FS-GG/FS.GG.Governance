module FS.GG.Governance.SurfaceChecks.Tests.OrderIndependenceTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks.Dispatch
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private facts =
    typedFacts
        [ surface "pkg" PackageSurface (Some "pkg-tag")
          surface "doc" DocsSurface (Some "doc-tag")
          surface "skl" SkillSurface (Some "skl-tag") ]

let private bundle: Composition.DomainFactBundle =
    { Composition.emptyBundle with
        Package = Map.ofList [ SurfaceId "pkg", packageDriftFacts "src/Foo.fsi" ]
        Docs = Map.ofList [ SurfaceId "doc", docsDanglingFacts "docs/g.md" ]
        Skill = Map.ofList [ SurfaceId "skl", skillBrokenFacts "skl" ] }

[<Tests>]
let tests =
    testList
        "Composition.run order-independence"
        [ test "a change touching package + docs + skill ⇒ three independent findings" {
              let rep =
                  report
                      [ classification "src/Foo.fsi" "pkg" PackageSurface
                        classification "docs/g.md" "doc" DocsSurface
                        classification "skills/s/SKILL.md" "skl" SkillSurface ]

              let findings = Composition.run facts rep bundle
              Expect.hasLength findings 3 "one finding per domain"

              let domains = findings |> List.map (fun f -> f.Domain) |> List.distinct |> List.length
              Expect.equal domains 3 "three independent domains represented"
          }

          test "shuffling the classifications yields byte-identical output (SC-008)" {
              let order1 =
                  report
                      [ classification "src/Foo.fsi" "pkg" PackageSurface
                        classification "docs/g.md" "doc" DocsSurface
                        classification "skills/s/SKILL.md" "skl" SkillSurface ]

              let order2 =
                  report
                      [ classification "skills/s/SKILL.md" "skl" SkillSurface
                        classification "src/Foo.fsi" "pkg" PackageSurface
                        classification "docs/g.md" "doc" DocsSurface ]

              Expect.equal (Composition.run facts order1 bundle) (Composition.run facts order2 bundle) "order-independent"
          }

          test "a surface absent from the bundle contributes nothing (FR-015)" {
              let rep = report [ classification "src/Foo.fsi" "pkg" PackageSurface ]
              // Bundle has no package facts for "pkg".
              let findings = Composition.run facts rep Composition.emptyBundle
              Expect.isEmpty findings "no sensed facts ⇒ no findings"
          } ]
