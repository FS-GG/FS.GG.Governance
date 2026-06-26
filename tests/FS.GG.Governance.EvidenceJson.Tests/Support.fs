module FS.GG.Governance.EvidenceJson.Tests.Support

open System.IO
open System.Text.Json
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceJson

// Shared builders + a read-only JsonDocument parse for the 069 projection tests (Principle V — every document
// below is a real `EvidenceDocument` built from real Kernel `EvidenceState`/`GraphError`, real
// `EvidenceReuse.RecomputeCause`, and real `FreshnessResolution.MissingFact`, never a hand-built JSON oracle).
// The emitted document is inspected by `System.Text.Json.JsonDocument` — the kernel `Json` / sibling
// projection-test pattern. No I/O beyond repo-root resolution.

/// Build one evidence node.
let mkNode (id: string) (declared: EvidenceState) (effective: EvidenceState) (freshness: NodeFreshness) (source: string) : EvidenceNode =
    { Id = id
      Declared = declared
      Effective = effective
      Freshness = freshness
      Source = source }

/// A well-formed document.
let wellFormed (nodes: EvidenceNode list) (deps: (string * string) list) (disclosures: (string * string) list) : EvidenceDocument =
    { Content = WellFormed(nodes, deps)
      Disclosures = disclosures }

/// A malformed document.
let malformed (failure: GraphError<string>) (disclosures: (string * string) list) : EvidenceDocument =
    { Content = Malformed failure
      Disclosures = disclosures }

/// Render and parse the emitted bytes into a read-only DOM.
let parse (document: EvidenceDocument) : JsonElement =
    let json = EvidenceJson.ofReport document
    JsonDocument.Parse(json).RootElement.Clone()

/// The non-null string value of a JSON element (or fail).
let str (el: JsonElement) : string =
    match el.GetString() with
    | null -> failwith "expected a non-null JSON string"
    | s -> s

/// The string value of a property (or fail).
let strProp (name: string) (el: JsonElement) : string =
    match el.GetProperty(name).GetString() with
    | null -> failwithf "property %s was null" name
    | s -> s

// ── Repo root (for the surface baseline path) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(System.AppContext.BaseDirectory))
