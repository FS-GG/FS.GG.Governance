module FS.GG.Governance.ScaffoldManifestJson.Tests.Support

open System
open System.IO
open System.Text.Json
open FS.GG.Governance.Scaffold.Model

// Fixture builders for the projection tests — REAL `ScaffoldManifest` values of the exact type the seam
// folds, inspected through the EMITTED BYTES via a read-only `JsonDocument` parse (the RouteJson tests'
// discipline). No I/O, no clock.

// ── repo root (for the surface-drift baseline check, Principle II) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext =
            File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))

        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// ── manifest builders ──

let providerTuple (id: string) (major: int) (minor: int) : ProviderId * ProviderContractVersion =
    ProviderId id, { Major = major; Minor = minor }

let generated (paths: string list) : GeneratedPath list =
    paths
    |> List.map (fun p ->
        { RelativePath = p
          Ownership = ProviderOwned })

/// A scaffolded manifest for a provider + the generated paths (order as given — the projection sorts).
let scaffoldedManifest (id: string) (paths: string list) : ScaffoldManifest =
    { Provider = Some(providerTuple id 1 0)
      Outcome = Scaffolded
      Generated = generated paths
      Collisions = [] }

let refusedManifest (id: string) (refusal: Refusal) (collisions: string list) : ScaffoldManifest =
    { Provider = Some(providerTuple id 1 0)
      Outcome = Refused refusal
      Generated = []
      Collisions = collisions }

let noProviderManifest: ScaffoldManifest =
    { Provider = None
      Outcome = NoProvider
      Generated = []
      Collisions = [] }

// ── JsonDocument read helpers ──

let parse (json: string) : JsonDocument = JsonDocument.Parse json

let private reqStr (el: JsonElement) : string =
    match el.GetString() with
    | null -> failwith "expected a JSON string but found null"
    | s -> s

let strField (el: JsonElement) (name: string) : string = reqStr (el.GetProperty name)

let topLevelFieldOrder (doc: JsonDocument) : string list =
    [ for p in doc.RootElement.EnumerateObject() -> p.Name ]

let fieldOrder (el: JsonElement) : string list =
    [ for p in el.EnumerateObject() -> p.Name ]

let isNull (doc: JsonDocument) (name: string) : bool =
    doc.RootElement.GetProperty(name).ValueKind = JsonValueKind.Null

let generatedEntries (doc: JsonDocument) : JsonElement list =
    [ for g in doc.RootElement.GetProperty("generated").EnumerateArray() -> g ]

let generatedPaths (doc: JsonDocument) : string list =
    generatedEntries doc |> List.map (fun g -> strField g "path")

let collisions (doc: JsonDocument) : string list =
    [ for c in doc.RootElement.GetProperty("collisions").EnumerateArray() -> reqStr c ]

let stringArrayProp (el: JsonElement) (name: string) : string list =
    [ for x in el.GetProperty(name).EnumerateArray() -> reqStr x ]
