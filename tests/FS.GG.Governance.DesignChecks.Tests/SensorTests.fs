module FS.GG.Governance.DesignChecks.Tests.SensorTests

open System
open System.IO
open Expecto
open FS.GG.Governance.DesignChecks
open FS.GG.Governance.DesignChecks.Model
open FS.GG.Governance.DesignChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-design-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(dir, "design")) |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let private layout = "design/tokens.json", "design/captures.json", "design/controls.json", "design/contrast.json"
let private req = requestFor "design" "design/surface.txt" (Some "design-evidence")

let private writeCatalogs (repo: string) =
    let d = Path.Combine(repo, "design")
    File.WriteAllText(Path.Combine(d, "tokens.json"), """["color.primary","color.bg"]""")
    File.WriteAllText(Path.Combine(d, "captures.json"), """["home.png"]""")
    File.WriteAllText(Path.Combine(d, "controls.json"), """["Button"]""")
    File.WriteAllText(Path.Combine(d, "contrast.json"), """{"fg-on-bg":{"ratio":4.6,"threshold":4.5},"low":{"ratio":2.0,"threshold":4.5}}""")

[<Tests>]
let tests =
    testList
        "DesignChecks.sensor"
        [ test "senseDesign resolves referenced entries + every contrast pair against the real catalogs" {
              withTempRepo (fun repo ->
                  writeCatalogs repo

                  File.WriteAllText(
                      Path.Combine(repo, "design", "surface.txt"),
                      "token: color.primary\ntoken: color.missing\ncapture: home.png\ncontrol: Button\n"
                  )

                  let facts = Interpreter.senseDesign (Interpreter.realPort repo layout) req

                  Expect.contains (facts.Tokens |> List.map (fun t -> t.Token, t.Outcome)) ("color.primary", Resolves) "primary resolves"
                  Expect.contains (facts.Tokens |> List.map (fun t -> t.Token, t.Outcome)) ("color.missing", Absent "color.missing") "missing absent"
                  Expect.contains (facts.Captures |> List.map (fun c -> c.Outcome)) Resolves "capture resolves"
                  Expect.contains (facts.Controls |> List.map (fun c -> c.Outcome)) Resolves "control resolves"

                  let meets = facts.Contrasts |> List.filter (fun c -> c.Meets) |> List.length
                  let fails = facts.Contrasts |> List.filter (fun c -> not c.Meets) |> List.length
                  Expect.equal meets 1 "one contrast meets threshold"
                  Expect.equal fails 1 "one contrast is sub-threshold"
                  Expect.isEmpty facts.CatalogUnavailable "all catalogs readable")
          }

          test "absent catalog ⇒ recorded in CatalogUnavailable, never a fabricated pass (FR-012)" {
              withTempRepo (fun repo ->
                  // Descriptor present, catalogs absent.
                  File.WriteAllText(Path.Combine(repo, "design", "surface.txt"), "token: color.primary\n")
                  let facts = Interpreter.senseDesign (Interpreter.realPort repo layout) req
                  Expect.isNonEmpty facts.CatalogUnavailable "absent catalogs recorded")
          } ]
