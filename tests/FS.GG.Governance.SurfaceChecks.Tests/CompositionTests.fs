module FS.GG.Governance.SurfaceChecks.Tests.CompositionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks.Dispatch
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

[<Tests>]
let tests =
    testList
        "Composition.requestsOf/domainOf"
        [ test "domainOf maps the four product classes and everything else to None" {
              Expect.equal (Composition.domainOf PackageSurface) (Some SC.PackageDomain) "package"
              Expect.equal (Composition.domainOf DocsSurface) (Some SC.DocsDomain) "docs"
              Expect.equal (Composition.domainOf SkillSurface) (Some SC.SkillDomain) "skill"
              Expect.equal (Composition.domainOf DesignSurface) (Some SC.DesignDomain) "design"

              for cls in [ Routine; GovernedRoot; ProtectedSurface; GeneratedView; ReleaseSurface; SampleAppSurface; GeneratedProductRoot ] do
                  Expect.equal (Composition.domainOf cls) None (sprintf "%A ⇒ None" cls)
          }

          test "requestsOf over package + docs + skill ⇒ three requests with the right domain + declared tag" {
              let facts =
                  typedFacts
                      [ surface "pkg" PackageSurface (Some "pkg-tag")
                        surface "doc" DocsSurface (Some "doc-tag")
                        surface "skl" SkillSurface None ]

              let rep =
                  report
                      [ classification "src/Foo.fsi" "pkg" PackageSurface
                        classification "docs/g.md" "doc" DocsSurface
                        classification "skills/s/SKILL.md" "skl" SkillSurface ]

              let requests = Composition.requestsOf facts rep
              Expect.hasLength requests 3 "three applicable requests"

              let byDomain =
                  requests |> List.map (fun r -> r.Domain, r.EvidenceTag) |> List.sortBy (fun (d, _) -> SC.checkDomainOrdinal d)

              Expect.equal
                  byDomain
                  [ SC.PackageDomain, Some(EvidenceTag "pkg-tag")
                    SC.DocsDomain, Some(EvidenceTag "doc-tag")
                    SC.SkillDomain, None ]
                  "each request carries the surface's declared tag"
          }

          test "requestsOf over only boundary classes ⇒ []" {
              let facts = typedFacts [ surface "r" Routine None; surface "g" GeneratedView None ]
              let rep = report [ classification "a" "r" Routine; classification "b" "g" GeneratedView ]
              Expect.isEmpty (Composition.requestsOf facts rep) "boundary classes yield no requests"
          } ]
