// The row-local `.fsgg/release.yml` declaration adapter (F055). Visibility lives in Declaration.fsi
// (Principle II) — this file carries NO top-level access modifiers; the YamlDotNet node helpers and the
// token recognizers are hidden by their absence from the .fsi. It parses the new release-declaration
// surface into the EXACT F053/F054 inputs (`ReleaseRule list`, `ReleaseExpectations`, `SourceLayout`)
// WITHOUT editing F014 `Config`'s frozen four-file schema (research D2). YamlDotNet is used in
// parse-to-node mode only (the F014 `Schema.fs` precedent — NO new dependency). PURE and TOTAL: a
// malformed/absent value is an `Error DeclError`, never an exception and never partial facts. Every value
// is read from the file (product-neutral, FR-014).

namespace FS.GG.Governance.ReleaseCommand

open YamlDotNet.RepresentationModel
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Declaration =

    type ReleaseDeclaration =
        { Rules: ReleaseRule list
          Expectations: ReleaseExpectations
          Layout: SourceLayout }

    type DeclError = { Reason: string }

    // ── YamlDotNet node helpers (parse-to-node only; hidden — absent from Declaration.fsi) ──

    /// Parse the joined content to a single mapping-node root; an empty document, a non-mapping root, or a
    /// YamlDotNet parse error (e.g. a duplicate key) is an `Error` (the F014 `Schema.loadRoot` precedent).
    let loadRoot (content: string) : Result<YamlMappingNode, string> =
        try
            let stream = YamlStream()
            stream.Load(new System.IO.StringReader(content))

            if stream.Documents.Count = 0 then
                Error "release.yml is empty"
            else
                match stream.Documents.[0].RootNode with
                | :? YamlMappingNode as m -> Ok m
                | _ -> Error "release.yml root must be a mapping"
        with ex ->
            Error(sprintf "release.yml is not valid YAML: %s" ex.Message)

    let asScalar (n: YamlNode) : string option =
        match n with
        | :? YamlScalarNode as s -> Option.ofObj s.Value
        | _ -> None

    let asMapping (n: YamlNode) : YamlMappingNode option =
        match n with
        | :? YamlMappingNode as m -> Some m
        | _ -> None

    let asSeq (n: YamlNode) : YamlNode list option =
        match n with
        | :? YamlSequenceNode as s -> Some(List.ofSeq s.Children)
        | _ -> None

    /// The value of a scalar-keyed mapping entry by name, if present.
    let childByKey (m: YamlMappingNode) (name: string) : YamlNode option =
        m.Children
        |> Seq.tryPick (fun kv ->
            match kv.Key with
            | :? YamlScalarNode as k when k.Value = name -> Some kv.Value
            | _ -> None)

    let scalarField (m: YamlMappingNode) (name: string) : string option =
        childByKey m name |> Option.bind asScalar

    /// A sequence of scalars as a string list; a non-sequence or a non-scalar element ⇒ `None` (malformed).
    let scalarList (n: YamlNode) : string list option =
        match asSeq n with
        | None -> None
        | Some items ->
            let vals = items |> List.choose asScalar
            if vals.Length = items.Length then Some vals else None

    /// A mapping of scalar→scalar as an association list, sorted by key for determinism.
    let scalarPairs (m: YamlMappingNode) : (string * string) list =
        m.Children
        |> Seq.choose (fun kv ->
            match asScalar kv.Key, asScalar kv.Value with
            | Some k, Some v -> Some(k, v)
            | _ -> None)
        |> List.ofSeq
        |> List.sortBy fst

    // ── token recognizers (hidden) — kebab/camel/underscore tolerant, product-neutral ──

    /// Normalize a token for comparison: drop separators, lowercase. Lets a declaration write `version-bump`,
    /// `versionBump`, or `version_bump` interchangeably for the same family — the value is the family NAME,
    /// not product identity (FR-014).
    let normalizeToken (s: string) : string =
        s.Replace("-", "").Replace("_", "").ToLowerInvariant()

    let recognizeKind (raw: string) : ReleaseRuleKind option =
        match normalizeToken raw with
        | "versionbump" -> Some VersionBump
        | "packagemetadata" -> Some PackageMetadata
        | "templatepins" -> Some TemplatePins
        | "publishplan" -> Some PublishPlan
        | "trustedpublishing" -> Some TrustedPublishing
        | "provenance" -> Some Provenance
        | _ -> None

    let recognizeSeverity (raw: string) : Severity option =
        match normalizeToken raw with
        | "blocking" -> Some Blocking
        | "advisory" -> Some Advisory
        | _ -> None

    let recognizeMaturity (raw: string) : Maturity option =
        match normalizeToken raw with
        | "observe" -> Some Observe
        | "warn" -> Some Warn
        | "blockonpr" -> Some BlockOnPr
        | "blockonship" -> Some BlockOnShip
        | "blockonrelease" -> Some BlockOnRelease
        | _ -> None

    let allFamilies: ReleaseRuleKind list =
        [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]

    // ── per-section parsers (hidden) ──

    let parseRule (surface: SurfaceId) (n: YamlNode) : Result<ReleaseRule, string> =
        match asMapping n with
        | None -> Error "each rules[] entry must be a mapping with kind/severity/maturity"
        | Some m ->
            match scalarField m "kind" with
            | None -> Error "a rules[] entry is missing its 'kind'"
            | Some kindRaw ->
                match recognizeKind kindRaw with
                | None -> Error(sprintf "unrecognized rule kind: %s" kindRaw)
                | Some kind ->
                    match scalarField m "severity" |> Option.bind recognizeSeverity with
                    | None -> Error(sprintf "rule '%s' has a missing/unrecognized severity (expected blocking|advisory)" kindRaw)
                    | Some severity ->
                        match scalarField m "maturity" |> Option.bind recognizeMaturity with
                        | None -> Error(sprintf "rule '%s' has a missing/unrecognized maturity" kindRaw)
                        | Some maturity ->
                            Ok
                                { Kind = kind
                                  Surface = surface
                                  BaseSeverity = severity
                                  Maturity = maturity }

    /// Fold a `Result` list into a `Result` of the list, first error wins (total, no exceptions).
    let sequenceResults (rs: Result<'a, string> list) : Result<'a list, string> =
        (Ok [], rs)
        ||> List.fold (fun acc r ->
            match acc, r with
            | Error e, _ -> Error e
            | Ok xs, Ok x -> Ok(xs @ [ x ])
            | Ok _, Error e -> Error e)

    let parseRules (surface: SurfaceId) (root: YamlMappingNode) : Result<ReleaseRule list, string> =
        match childByKey root "rules" |> Option.bind asSeq with
        | None -> Error "release.yml is missing its 'rules' sequence"
        | Some entries ->
            match entries |> List.map (parseRule surface) |> sequenceResults with
            | Error e -> Error e
            | Ok rules ->
                // Require exactly the six families, one each, so the verdict always covers six families
                // (FR-013/SC-006). The ordering is normalized to the F053 stable composite key.
                let kinds = rules |> List.map (fun r -> r.Kind)
                let missing = allFamilies |> List.filter (fun k -> not (List.contains k kinds))
                let duplicated = kinds |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

                if not (List.isEmpty missing) then
                    Error(sprintf "release.yml does not declare every release family (missing: %s)" (missing |> List.map Release.releaseRuleKindToken |> String.concat ", "))
                elif not (List.isEmpty duplicated) then
                    Error(sprintf "release.yml declares a family more than once (%s)" (duplicated |> List.map Release.releaseRuleKindToken |> String.concat ", "))
                else
                    Ok(rules |> List.sortBy (fun r -> Release.releaseRuleKindOrdinal r.Kind, (let (SurfaceId s) = r.Surface in s)))

    let parseExpectations (surface: SurfaceId) (root: YamlMappingNode) : ReleaseExpectations =
        // The whole section is optional; every family criterion is optional (an absent criterion ⇒ that
        // family senses `Unrecoverable`, the allowed edge). All values come from the file (FR-014).
        let exp = childByKey root "expectations" |> Option.bind asMapping

        let listField name =
            exp |> Option.bind (fun m -> childByKey m name) |> Option.bind scalarList

        let pinsField =
            exp
            |> Option.bind (fun m -> childByKey m "expectedPins")
            |> Option.bind asMapping
            |> Option.map (fun m -> scalarPairs m |> Map.ofList)

        { Surface = surface
          VersionBaseline = exp |> Option.bind (fun m -> scalarField m "versionBaseline")
          RequiredMetadataFields = listField "requiredMetadataFields"
          ExpectedPins = pinsField
          RequiredPublishPosture = listField "requiredPublishPosture"
          RequiredTrustedPublishing = listField "requiredTrustedPublishing"
          RequiredProvenance = listField "requiredProvenance" }

    let parseLayout (root: YamlMappingNode) : Result<SourceLayout, string> =
        match childByKey root "layout" |> Option.bind asMapping with
        | None -> Error "release.yml is missing its 'layout' mapping"
        | Some m ->
            let path name = scalarField m name

            match path "versionPath", path "metadataPath", path "pinsPath", path "publishPlanPath", path "trustedPublishingPath", path "provenancePath" with
            | Some v, Some md, Some p, Some pp, Some tp, Some pr ->
                Ok
                    { VersionPath = v
                      MetadataPath = md
                      PinsPath = p
                      PublishPlanPath = pp
                      TrustedPublishingPath = tp
                      ProvenancePath = pr }
            | _ -> Error "release.yml 'layout' must declare all six source paths (versionPath, metadataPath, pinsPath, publishPlanPath, trustedPublishingPath, provenancePath)"

    // ── the public entry point ──

    let parse (lines: string list) : Result<ReleaseDeclaration, DeclError> =
        let content = String.concat "\n" lines

        let result =
            match loadRoot content with
            | Error e -> Error e
            | Ok root ->
                match scalarField root "surface" with
                | None -> Error "release.yml is missing its 'surface'"
                | Some s ->
                    let surface = SurfaceId s

                    match parseRules surface root with
                    | Error e -> Error e
                    | Ok rules ->
                        match parseLayout root with
                        | Error e -> Error e
                        | Ok layout ->
                            Ok
                                { Rules = rules
                                  Expectations = parseExpectations surface root
                                  Layout = layout }

        result |> Result.mapError (fun reason -> { Reason = reason })
