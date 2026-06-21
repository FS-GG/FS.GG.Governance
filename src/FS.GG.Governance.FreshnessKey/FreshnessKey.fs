// Freshness-key operations for the freshness-key core (F029). The public surface is fixed by
// FreshnessKey.fsi (Principle II); no top-level binding here carries an access modifier. All three
// operations are pure, total, and deterministic (FR-002, FR-003, FR-008): no clock, filesystem, git,
// environment, or network; identical inputs (covered artifacts compared as a set) always render the
// identical key. BCL string building only; no hashing (FR-013). The canonical encoding is fixed by
// contracts/freshness-key-format.md.

namespace FS.GG.Governance.FreshnessKey

open System.Text
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FreshnessKey =

    // ── Segment encoders (internal; hidden by FreshnessKey.fsi) ──

    // UTF-8 byte length of a value — the length prefix that makes the encoding injective: a reader knows
    // exactly how many bytes a value occupies, so no value can masquerade as another field (FR-006).
    let byteLen (s: string) : int = Encoding.UTF8.GetByteCount s

    // A required string value: presence digit '1', then "<byteLen>:<value>".
    let req (tag: string) (s: string) : string = sprintf "%s=1%d:%s" tag (byteLen s) s

    // An optional string value: presence '0' (None, no payload) or the required encoding (Some).
    let opt (tag: string) (s: string option) : string =
        match s with
        | None -> sprintf "%s=0" tag
        | Some v -> req tag v

    // The fixed total token for an environment class (contracts/freshness-key-format.md).
    let environmentToken (env: EnvironmentClass) : string =
        match env with
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "localOrCi"
        | Release -> "release"

    // The canonical SET of covered-artifact strings: unwrap, deduplicate, ordinal-sort.
    let artifactSet (arts: ArtifactHash list) : string list =
        arts
        |> List.map (fun (ArtifactHash h) -> h)
        |> List.distinct
        |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))

    // The covered-artifact segment: "art=<count>;<len1>:<v1>;<len2>:<v2>;…" (empty set ⇒ "art=0;").
    let artSegment (arts: ArtifactHash list) : string =
        let elems = artifactSet arts
        let body = elems |> List.map (fun v -> sprintf "%d:%s" (byteLen v) v) |> String.concat ";"
        sprintf "art=%d;%s" (List.length elems) body

    let compute (inputs: FreshnessInputs) : Key =
        let (CheckId check) = inputs.Check
        let (DomainId domain) = inputs.Domain
        let command = inputs.Command |> Option.map (fun (CommandId c) -> c)
        let (RuleHash rule) = inputs.RuleHash
        let commandVersion = inputs.CommandVersion |> Option.map (fun (CommandVersion v) -> v)
        let (GeneratorVersion genv) = inputs.GeneratorVersion
        let (Revision baseRev) = inputs.Base
        let (Revision headRev) = inputs.Head

        // Fixed field order, joined by '\n', no trailing newline (contracts/freshness-key-format.md).
        [ req "check" check
          req "domain" domain
          opt "cmd" command
          req "env" (environmentToken inputs.Environment)
          req "rule" rule
          artSegment inputs.CoveredArtifacts
          opt "cmdv" commandVersion
          req "genv" genv
          req "base" baseRev
          req "head" headRev ]
        |> String.concat "\n"
        |> Key

    let matches (a: FreshnessInputs) (b: FreshnessInputs) : bool = compute a = compute b

    let diff (a: FreshnessInputs) (b: FreshnessInputs) : InputCategory list =
        // Compare field-by-field in the fixed category order; covered artifacts compared as a SET so a
        // reorder/duplicate is never reported. Returns exactly the differing categories (FR-007).
        [ if a.Check <> b.Check then CheckIdentity
          if a.Domain <> b.Domain then DomainIdentity
          if a.Command <> b.Command then CommandIdentity
          if a.Environment <> b.Environment then EnvironmentClassCat
          if a.RuleHash <> b.RuleHash then RuleHashCat
          if artifactSet a.CoveredArtifacts <> artifactSet b.CoveredArtifacts then CoveredArtifactsCat
          if a.CommandVersion <> b.CommandVersion then CommandVersionCat
          if a.GeneratorVersion <> b.GeneratorVersion then GeneratorVersionCat
          if a.Base <> b.Base then BaseRevisionCat
          if a.Head <> b.Head then HeadRevisionCat ]

    let value (key: Key) : string =
        let (Key s) = key
        s
