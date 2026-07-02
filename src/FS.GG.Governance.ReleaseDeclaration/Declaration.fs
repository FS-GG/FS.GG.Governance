// The SHARED `.fsgg/release.yml` declaration adapter (065 — F26 host wiring). Visibility lives in
// Declaration.fsi (Principle II) — this file carries NO top-level access modifiers; the YamlDotNet node
// helpers and token recognizers are hidden by their absence from the .fsi. The rules/expectations/layout
// parse is the F055 `ReleaseCommand.Declaration` behaviour preserved VERBATIM; two additive sections —
// `packableProjects` and the optional `matrix` — are layered on. YamlDotNet is used in parse-to-node mode
// only (the F014 `Schema.fs` precedent — NO new dependency). PURE and TOTAL: a malformed/absent value is an
// `Error DeclError`, never an exception and never partial facts. Every value is read from the file
// (product-neutral, FR-014).

namespace FS.GG.Governance.ReleaseDeclaration

open YamlDotNet.RepresentationModel
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Declaration =

    type PackableProject =
        { Surface: SurfaceId
          PackCommand: GateCommand
          Baseline: string option }

    type ReleaseDeclaration =
        { Rules: ReleaseRule list
          Expectations: ReleaseExpectations
          Layout: SourceLayout
          PackableProjects: PackableProject list
          Matrix: ExhaustiveMatrix option }

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
        // 088: the additive ApiCompatibility family is RECOGNIZED so a release.yml may declare it (e.g. an
        // advisory breaking-change rule), but it is NOT in the required `allFamilies` set below — declaring
        // it is OPTIONAL, so existing six-family declarations keep parsing unchanged (additivity).
        | "apicompatibility" -> Some ApiCompatibility
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

    /// The declared exhaustive-matrix cost ceiling (F26 `ExhaustiveMatrix.Cost`); product-neutral token.
    let recognizeCost (raw: string) : Cost option =
        match normalizeToken raw with
        | "cheap" -> Some Cheap
        | "medium" -> Some Medium
        | "high" -> Some High
        | "exhaustive" -> Some Exhaustive
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
                // Require exactly the six core families, one each, so the verdict always covers them
                // (FR-013/SC-006). 088: ApiCompatibility is an OPTIONAL additive seventh family — recognized
                // and allowed (at most once), but not required, so existing declarations are unchanged. The
                // ordering is normalized to the F053 stable composite key.
                let kinds = rules |> List.map (fun r -> r.Kind)
                let missing = allFamilies |> List.filter (fun k -> not (List.contains k kinds))
                let duplicated = kinds |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

                if not (List.isEmpty missing) then
                    Error(sprintf "release.yml does not declare every release family (missing: %s)" (missing |> List.map Release.releaseRuleKindToken |> String.concat ", "))
                elif not (List.isEmpty duplicated) then
                    Error(sprintf "release.yml declares a family more than once (%s)" (duplicated |> List.map Release.releaseRuleKindToken |> String.concat ", "))
                else
                    Ok(rules |> List.sortBy (fun r -> Release.releaseRuleKindOrdinal r.Kind, (let (SurfaceId s) = r.Surface in s)))

    let parseExpectations (surface: SurfaceId) (root: YamlMappingNode) : Result<ReleaseExpectations, string> =
        // The whole section is optional, and every criterion within it is optional (an ABSENT criterion ⇒
        // that family senses `Unrecoverable`, the allowed edge). But a criterion (or the section) that is
        // PRESENT with the wrong SHAPE is MALFORMED, not absent — it must surface as `Error`, never silently
        // degrade to `None`. `None` is indistinguishable from "not declared" and would drop a declared
        // criterion, violating this file's "never partial facts" header. Mirrors `parseMatrix`/`parsePackables`
        // (Ok None ⇒ absent, Error ⇒ present-but-malformed). All values come from the file (FR-014).
        let empty =
            { Surface = surface
              VersionBaseline = None
              RequiredMetadataFields = None
              ExpectedPins = None
              RequiredPublishPosture = None
              RequiredTrustedPublishing = None
              RequiredProvenance = None }

        match childByKey root "expectations" with
        | None -> Ok empty
        | Some node ->
            match asMapping node with
            | None -> Error "release.yml 'expectations' must be a mapping"
            | Some m ->
                // Each helper: an ABSENT key ⇒ Ok None (the allowed edge); a PRESENT node of the wrong shape
                // ⇒ Error (malformed, never a silent None).
                let scalarCriterion name : Result<string option, string> =
                    match childByKey m name with
                    | None -> Ok None
                    | Some n ->
                        match asScalar n with
                        | Some s -> Ok(Some s)
                        | None -> Error(sprintf "release.yml 'expectations.%s' must be a scalar" name)

                let listCriterion name : Result<string list option, string> =
                    match childByKey m name with
                    | None -> Ok None
                    | Some n ->
                        match scalarList n with
                        | Some xs -> Ok(Some xs)
                        | None -> Error(sprintf "release.yml 'expectations.%s' must be a sequence of scalars" name)

                let pinsCriterion: Result<Map<string, string> option, string> =
                    match childByKey m "expectedPins" with
                    | None -> Ok None
                    | Some n ->
                        match asMapping n with
                        | Some pm -> Ok(Some(scalarPairs pm |> Map.ofList))
                        | None -> Error "release.yml 'expectations.expectedPins' must be a scalar→scalar mapping"

                match scalarCriterion "versionBaseline" with
                | Error e -> Error e
                | Ok versionBaseline ->

                match listCriterion "requiredMetadataFields" with
                | Error e -> Error e
                | Ok requiredMetadataFields ->

                match pinsCriterion with
                | Error e -> Error e
                | Ok expectedPins ->

                match listCriterion "requiredPublishPosture" with
                | Error e -> Error e
                | Ok requiredPublishPosture ->

                match listCriterion "requiredTrustedPublishing" with
                | Error e -> Error e
                | Ok requiredTrustedPublishing ->

                match listCriterion "requiredProvenance" with
                | Error e -> Error e
                | Ok requiredProvenance ->
                    Ok
                        { Surface = surface
                          VersionBaseline = versionBaseline
                          RequiredMetadataFields = requiredMetadataFields
                          ExpectedPins = expectedPins
                          RequiredPublishPosture = requiredPublishPosture
                          RequiredTrustedPublishing = requiredTrustedPublishing
                          RequiredProvenance = requiredProvenance }

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

    // ── additive section parsers (065) — packable projects + the optional matrix ──

    /// An empty environment delta — a pack command inherits the host environment (the common case; the F051
    /// `GateCommand` carries a three-class DELTA, not a full snapshot).
    let emptyEnv: EnvironmentDelta = { Added = []; Changed = []; Removed = [] }

    /// Build one project's pack `GateCommand` from its declared `executable`/`arguments`/`workingDirectory?`/
    /// `timeoutSeconds?`. `executable` + `arguments` are required (a pack with no program is malformed);
    /// `workingDirectory` defaults to `.` and `timeoutSeconds` to 600. No captured-output target (the F051
    /// common case). Every value comes from the file (product-neutral).
    let parsePackCommand (m: YamlMappingNode) : Result<GateCommand, string> =
        match scalarField m "executable" with
        | None -> Error "a packableProjects[] entry's packCommand is missing its 'executable'"
        | Some exe ->
            let argsResult =
                match childByKey m "arguments" with
                | None -> Ok []
                | Some n ->
                    match scalarList n with
                    | Some xs -> Ok xs
                    | None -> Error "a packCommand 'arguments' must be a sequence of scalars"

            match argsResult with
            | Error e -> Error e
            | Ok args ->
                let workDir = scalarField m "workingDirectory" |> Option.defaultValue "."

                let timeoutResult =
                    match scalarField m "timeoutSeconds" with
                    | None -> Ok 600
                    | Some raw ->
                        match System.Int32.TryParse raw with
                        | true, n when n > 0 -> Ok n
                        | _ -> Error(sprintf "a packCommand 'timeoutSeconds' must be a positive integer, got: %s" raw)

                match timeoutResult with
                | Error e -> Error e
                | Ok timeout ->
                    Ok
                        { Executable = Executable exe
                          Arguments = args |> List.map Argument
                          WorkingDirectory = WorkingDirectory workDir
                          Environment = emptyEnv
                          Timeout = TimeoutLimit timeout
                          CapturedOutput = NoCapturedOutput }

    let parsePackable (n: YamlNode) : Result<PackableProject, string> =
        match asMapping n with
        | None -> Error "each packableProjects[] entry must be a mapping with surface/packCommand"
        | Some m ->
            match scalarField m "surface" with
            | None -> Error "a packableProjects[] entry is missing its 'surface'"
            | Some s ->
                match childByKey m "packCommand" |> Option.bind asMapping with
                | None -> Error(sprintf "packableProjects[] entry '%s' is missing its 'packCommand' mapping" s)
                | Some cmdNode ->
                    match parsePackCommand cmdNode with
                    | Error e -> Error e
                    | Ok command ->
                        Ok
                            { Surface = SurfaceId s
                              PackCommand = command
                              Baseline = scalarField m "baseline" }

    /// The optional `packableProjects` sequence. ABSENT ⇒ `Ok []` (GD-3 backward-compat — vacuously
    /// satisfied). Present-but-not-a-sequence, or a malformed entry, ⇒ `Error` (never partial facts).
    let parsePackables (root: YamlMappingNode) : Result<PackableProject list, string> =
        match childByKey root "packableProjects" with
        | None -> Ok []
        | Some n ->
            match asSeq n with
            | None -> Error "release.yml 'packableProjects' must be a sequence"
            | Some entries -> entries |> List.map parsePackable |> sequenceResults

    /// The optional `matrix` mapping. ABSENT ⇒ `Ok None` (GD-3 — `NotDeclared`, never invented).
    /// Present-but-malformed (missing name/cost/dimensions, unrecognized cost) ⇒ `Error`.
    let parseMatrix (root: YamlMappingNode) : Result<ExhaustiveMatrix option, string> =
        match childByKey root "matrix" with
        | None -> Ok None
        | Some n ->
            match asMapping n with
            | None -> Error "release.yml 'matrix' must be a mapping"
            | Some m ->
                match scalarField m "name" with
                | None -> Error "release.yml 'matrix' is missing its 'name'"
                | Some name ->
                    match scalarField m "cost" |> Option.bind recognizeCost with
                    | None -> Error "release.yml 'matrix' has a missing/unrecognized 'cost' (expected cheap|medium|high|exhaustive)"
                    | Some cost ->
                        match childByKey m "dimensions" |> Option.bind scalarList with
                        | None -> Error "release.yml 'matrix' must declare a 'dimensions' sequence of scalars"
                        | Some dimensions ->
                            Ok(Some { Name = name; Cost = cost; Dimensions = dimensions })

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
                            match parsePackables root with
                            | Error e -> Error e
                            | Ok packables ->
                                match parseMatrix root with
                                | Error e -> Error e
                                | Ok matrix ->
                                    match parseExpectations surface root with
                                    | Error e -> Error e
                                    | Ok expectations ->
                                        Ok
                                            { Rules = rules
                                              Expectations = expectations
                                              Layout = layout
                                              PackableProjects = packables
                                              Matrix = matrix }

        result |> Result.mapError (fun reason -> { Reason = reason })
