module FS.GG.Governance.SurfaceChecks.Tests.NeutralityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks.Dispatch
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model
module Design = FS.GG.Governance.DesignChecks.Model
module DesignPack = FS.GG.Governance.DesignChecks.DesignChecks

// T053 — product neutrality: no product/surface/path identity is hardcoded in the core, dispatcher, or any
// pack. Findings reflect the caller-supplied input verbatim; every design entry is caller-supplied.

let private runFor (sid: string) (path: string) =
    let facts = typedFacts [ surface sid PackageSurface (Some(sid + "-tag")) ]
    let rep = report [ classification path sid PackageSurface ]
    let bundle = { Composition.emptyBundle with Package = Map.ofList [ SurfaceId sid, packageDriftFacts path ] }
    Composition.run facts rep bundle

[<Tests>]
let tests =
    testList
        "SurfaceChecks.neutrality"
        [ test "two fixtures with different invented ids ⇒ findings reflect each input verbatim" {
              let a = runFor "alpha-surface" "invented/alpha/Api.fsi"
              let b = runFor "beta-surface" "invented/beta/Api.fsi"

              Expect.equal (List.head a).Surface (SurfaceId "alpha-surface") "fixture A id carried verbatim"
              Expect.equal (List.head b).Surface (SurfaceId "beta-surface") "fixture B id carried verbatim"
              Expect.equal (List.head a).EvidenceTag (Some(EvidenceTag "alpha-surface-tag")) "fixture A tag verbatim"
          }

          test "DesignChecks hardcodes no token/capture/control identity — every entry is caller-supplied" {
              let req: SC.SurfaceCheckRequest =
                  { Domain = SC.DesignDomain
                    Surface = SurfaceId "d"
                    Class = DesignSurface
                    Path = normalizePath "d/surface.txt"
                    EvidenceTag = None }

              let invented = "totally.invented.token.xyz"

              let facts: Design.DesignFacts =
                  { Tokens = [ { Token = invented; Outcome = Design.Absent invented } ]
                    Captures = []
                    Controls = []
                    Contrasts = []
                    CatalogUnavailable = [] }

              let f = List.head (DesignPack.evaluate req facts)
              Expect.stringContains f.Message invented "the invented token appears verbatim (no hardcoded catalog)"
          } ]
