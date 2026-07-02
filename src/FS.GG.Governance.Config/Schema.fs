// The PURE validation core for the `.fsgg` schemas (F014). Visibility lives in
// Schema.fsi (Principle II). `validate` is total and performs NO I/O: it takes an
// already-read `RawSource` and returns a `Validation`, never throwing (Principle IV,
// research D3). YamlDotNet is used in parse-to-node mode only (research D2); EVERY
// strictness rule below is our own code over the node tree.

namespace FS.GG.Governance.Config

open System
open System.IO
open YamlDotNet.RepresentationModel
open Fsgg
open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Schema =

    // ── Input value (produced by the Loader edge) ──

    type FileSlot =
        | Absent
        | Present of content: string
        | Unreadable of error: string

    type RawSource =
        { Root: GovernedPath
          Project: FileSlot
          Policy: FileSlot
          Capabilities: FileSlot
          Tooling: FileSlot }

    // ── Supported versions (F23 D1: per-file) ──

    // Single-sourced from the org-shared contract package `FS.GG.Contracts` (Fsgg.Schemas):
    // the supported `.fsgg` schema version for each Governance-owned file is the version
    // constant the contract declares — caps=2, governance/policy/tooling=1 — so the value
    // lives in exactly one place across the org (FS.GG.Governance#14), not as a literal here.
    // `Project` is the `governance.yml` file (renamed from project.yml), hence `governanceVersion`.
    let supportedVersionFor (file: FsggFile) : SchemaVersion =
        match file with
        | Project -> SchemaVersion Schemas.governanceVersion
        | Policy -> SchemaVersion Schemas.policyVersion
        | Capabilities -> SchemaVersion Schemas.capabilitiesVersion
        | Tooling -> SchemaVersion Schemas.toolingVersion

    /// The migration-guidance pointer named by an unsupported-`capabilities.yml` diagnostic (SC-006).
    /// A repo-relative doc path — never a host-absolute path (SC-002/SC-005).
    let migrationDoc = "specs/058-generated-product-capabilities/contracts/migration.md"

    // ── Diagnostic constructor ──

    let private diag id file field idOpt line message : Diagnostic =
        { Id = id
          File = file
          Locator = { Field = field; Id = idOpt; Line = line }
          Message = message }

    // ── YamlDotNet node helpers (parse-to-node only) ──

    /// Parse a file's content to its root mapping node. A YAML parse failure (including a
    /// duplicate mapping key, which YamlDotNet raises) and a non-mapping root both surface
    /// as a located message — never an exception (validate is total).
    let private loadRoot (content: string) : Result<YamlMappingNode, string> =
        try
            let stream = YamlStream()
            use reader = new StringReader(content)
            stream.Load reader
            if stream.Documents.Count = 0 then
                Error "the file contains no YAML document"
            else
                match stream.Documents.[0].RootNode with
                | :? YamlMappingNode as m -> Ok m
                | _ -> Error "the top level must be a mapping of fields"
        with ex ->
            Error ex.Message

    let private scalarValue (n: YamlNode) : string option =
        match n with
        | :? YamlScalarNode as s -> Option.ofObj s.Value
        | _ -> None

    let private lineOf (n: YamlNode) : int option =
        let l = int n.Start.Line
        if l > 0 then Some l else None

    /// The textual key of a mapping entry, if it is a scalar key (YamlDotNet may return null).
    let private keyName (n: YamlNode) : string option =
        match n with
        | :? YamlScalarNode as k -> Option.ofObj k.Value
        | _ -> None

    /// Look up a field's value node within a mapping by exact key.
    let private getField (m: YamlMappingNode) (name: string) : YamlNode option =
        m.Children
        |> Seq.tryPick (fun kv ->
            match keyName kv.Key with
            | Some k when k = name -> Some kv.Value
            | _ -> None)

    /// Every key the schema does not define is an `UnknownField` (FR-006). Located at the
    /// offending key's line.
    let private unknownFields (m: YamlMappingNode) (allowed: Set<string>) (file: FsggFile) : Diagnostic list =
        m.Children
        |> Seq.choose (fun kv ->
            match keyName kv.Key with
            | Some k when not (allowed.Contains k) ->
                Some(
                    diag
                        UnknownField
                        file
                        (Some k)
                        None
                        (lineOf kv.Key)
                        (sprintf "unknown field '%s'; the schema does not define it — remove it or fix the spelling" k)
                )
            | _ -> None)
        |> List.ofSeq

    // ── Path normalization (pure string logic — never Path.GetFullPath, research D5) ──

    /// Normalize a declared path relative to the governed root: unify `/` and `\`, drop `.`
    /// and empty segments, resolve `..`. A `..` that would escape the root yields `Error`
    /// (→ `PathEscapesRoot`). Case is preserved. Pure string logic so no absolute host path
    /// can leak (SC-002/SC-005).
    let private normalizePath (raw: string) : Result<GovernedPath, unit> =
        // Single-sourced from Model.normalizePath (research D7): the SAME governed-path form F015
        // routing and F016 sensing consume. That normalizer is total and RETAINS an unpoppable `..`
        // as a literal segment; F014 rejects root escape by detecting that segment here.
        let (GovernedPath p) = Model.normalizePath raw
        if p.Split('/') |> Array.exists (fun s -> s = "..") then Error() else Ok(GovernedPath p)

    // ── schemaVersion handling ──

    /// The supported-version message for a file, naming the actual version and (for `capabilities.yml`)
    /// the v1→v2 migration pointer (SC-006). Per-file as of F23 (D1).
    let private unsupportedMessage (file: FsggFile) (actual: int) : string =
        let (SchemaVersion want) = supportedVersionFor file
        match file with
        | Capabilities ->
            sprintf
                "schemaVersion %d is not supported; capabilities.yml requires version %d — see %s for v1→v2 migration guidance"
                actual
                want
                migrationDoc
        | _ -> sprintf "schemaVersion %d is not supported; this build understands version %d" actual want

    let private readSchemaVersion (m: YamlMappingNode) (file: FsggFile) : Result<SchemaVersion, Diagnostic> =
        let (SchemaVersion want) = supportedVersionFor file
        match getField m "schemaVersion" with
        | None ->
            Error(diag MissingSchemaVersion file (Some "schemaVersion") None None (sprintf "every `.fsgg` file must declare 'schemaVersion: %d'" want))
        | Some node ->
            match scalarValue node with
            | Some v ->
                match Int32.TryParse v with
                | true, n ->
                    if n = want then
                        Ok(SchemaVersion n)
                    else
                        Error(diag UnsupportedSchemaVersion file (Some "schemaVersion") None (lineOf node) (unsupportedMessage file n))
                | _ ->
                    Error(
                        diag
                            MalformedSchemaVersion
                            file
                            (Some "schemaVersion")
                            None
                            (lineOf node)
                            "schemaVersion must be an integer"
                    )
            | None ->
                Error(
                    diag MalformedSchemaVersion file (Some "schemaVersion") None (lineOf node) "schemaVersion must be an integer scalar"
                )

    // ── Enum readers ──

    let private parseCost =
        function
        | "cheap" -> Some Cheap
        | "medium" -> Some Medium
        | "high" -> Some High
        | "exhaustive" -> Some Exhaustive
        | _ -> None

    let private parseEnvironment =
        function
        | "local" -> Some Local
        | "ci" -> Some Ci
        | "local-or-ci" -> Some LocalOrCi
        | "release" -> Some Release
        | _ -> None

    let private parseMaturity =
        function
        | "observe" -> Some Observe
        | "warn" -> Some Warn
        | "block-on-pr" -> Some BlockOnPr
        | "block-on-ship" -> Some BlockOnShip
        | "block-on-release" -> Some BlockOnRelease
        | _ -> None

    let private parseSurfaceClass =
        function
        | "routine" -> Some Routine
        | "governedRoot" -> Some GovernedRoot
        | "protected" -> Some ProtectedSurface
        | "generatedView" -> Some GeneratedView
        | "release" -> Some ReleaseSurface
        // — F23 product kinds (single-sourced with Model.surfaceClassToken) —
        | "package" -> Some PackageSurface
        | "docs" -> Some DocsSurface
        | "skill" -> Some SkillSurface
        | "design" -> Some DesignSurface
        | "sampleApp" -> Some SampleAppSurface
        | "generatedProduct" -> Some GeneratedProductRoot
        | _ -> None

    let private parseTier =
        function
        | "structuralScan" -> Some StructuralScan
        | "restoreBuild" -> Some RestoreBuild
        | "focusedTests" -> Some FocusedTests
        | "fullVerify" -> Some FullVerify
        | "releaseValidation" -> Some ReleaseValidation
        | _ -> None

    let private environmentToken =
        function
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "local-or-ci"
        | Release -> "release"

    // ── Field readers (each records its own diagnostics into the accumulator) ──
    // Each helper takes a `ResizeArray<Diagnostic>` accumulator — a local mutable used only
    // to gather a file's diagnostics in authored order (Principle III disclosure). `validate`
    // stays pure: these accumulators never escape a single parse call.

    let private addMissing (diags: ResizeArray<Diagnostic>) file name =
        diags.Add(diag MissingRequiredField file (Some name) None None (sprintf "required field '%s' is missing" name))

    let private addMalformed (diags: ResizeArray<Diagnostic>) file name node msg =
        diags.Add(diag MalformedValue file (Some name) None (lineOf node) msg)

    let private reqString (diags: ResizeArray<Diagnostic>) m file name : string option =
        match getField m name with
        | None ->
            addMissing diags file name
            None
        | Some node ->
            match scalarValue node with
            | Some v when v.Trim() <> "" -> Some v
            | _ ->
                addMalformed diags file name node (sprintf "field '%s' must be a non-empty scalar" name)
                None

    let private optString (diags: ResizeArray<Diagnostic>) m file name : string option =
        match getField m name with
        | None -> None
        | Some node ->
            match scalarValue node with
            | Some v when v.Trim() <> "" -> Some v
            | _ ->
                addMalformed diags file name node (sprintf "field '%s', when present, must be a non-empty scalar" name)
                None

    let private reqInt (diags: ResizeArray<Diagnostic>) m file name : int option =
        match getField m name with
        | None ->
            addMissing diags file name
            None
        | Some node ->
            match scalarValue node with
            | Some v ->
                match Int32.TryParse v with
                | true, n -> Some n
                | _ ->
                    addMalformed diags file name node (sprintf "field '%s' must be an integer" name)
                    None
            | None ->
                addMalformed diags file name node (sprintf "field '%s' must be an integer scalar" name)
                None

    let private reqBool (diags: ResizeArray<Diagnostic>) m file name : bool option =
        match getField m name with
        | None ->
            addMissing diags file name
            None
        | Some node ->
            match scalarValue node with
            | Some v ->
                match Boolean.TryParse v with
                | true, b -> Some b
                | _ ->
                    addMalformed diags file name node (sprintf "field '%s' must be 'true' or 'false'" name)
                    None
            | None ->
                addMalformed diags file name node (sprintf "field '%s' must be a boolean scalar" name)
                None

    let private reqEnum (parse: string -> 'e option) (diags: ResizeArray<Diagnostic>) m file name : 'e option =
        match getField m name with
        | None ->
            addMissing diags file name
            None
        | Some node ->
            match scalarValue node with
            | Some v ->
                match parse v with
                | Some e -> Some e
                | None ->
                    addMalformed diags file name node (sprintf "field '%s' has an out-of-set value '%s'" name v)
                    None
            | None ->
                addMalformed diags file name node (sprintf "field '%s' must be a scalar" name)
                None

    /// An optional closed-enum scalar (absent → None; present-but-out-of-set → `MalformedValue`).
    let private optEnum (parse: string -> 'e option) (diags: ResizeArray<Diagnostic>) m file name : 'e option =
        match getField m name with
        | None -> None
        | Some node ->
            match scalarValue node with
            | Some v ->
                match parse v with
                | Some e -> Some e
                | None ->
                    addMalformed diags file name node (sprintf "field '%s' has an out-of-set value '%s'" name v)
                    None
            | None ->
                addMalformed diags file name node (sprintf "field '%s' must be a scalar" name)
                None

    let private reqPath (diags: ResizeArray<Diagnostic>) m file name : GovernedPath option =
        match reqString diags m file name with
        | None -> None
        | Some raw ->
            match normalizePath raw with
            | Ok p -> Some p
            | Error() ->
                diags.Add(diag PathEscapesRoot file (Some name) None None (sprintf "path '%s' escapes the governed root" raw))
                None

    let private optPath (diags: ResizeArray<Diagnostic>) m file name : GovernedPath option =
        match getField m name with
        | None -> None
        | Some node ->
            match scalarValue node with
            | Some raw ->
                match normalizePath raw with
                | Ok p -> Some p
                | Error() ->
                    diags.Add(diag PathEscapesRoot file (Some name) None (lineOf node) (sprintf "path '%s' escapes the governed root" raw))
                    None
            | None ->
                addMalformed diags file name node (sprintf "field '%s' must be a path scalar" name)
                None

    /// A required sequence of non-empty scalar strings (may be an empty list). Absent → missing.
    let private reqStringList (diags: ResizeArray<Diagnostic>) m file name : string list option =
        match getField m name with
        | None ->
            addMissing diags file name
            None
        | Some node ->
            match node with
            | :? YamlSequenceNode as seq ->
                let items = seq.Children |> List.ofSeq
                let parsed = items |> List.map scalarValue
                if parsed |> List.forall (function Some v -> v.Trim() <> "" | None -> false) then
                    Some(parsed |> List.map Option.get)
                else
                    items
                    |> List.iteri (fun i c ->
                        match scalarValue c with
                        | Some v when v.Trim() <> "" -> ()
                        | _ -> addMalformed diags file (sprintf "%s[%d]" name i) c "list entry must be a non-empty scalar")
                    None
            | _ ->
                addMalformed diags file name node (sprintf "field '%s' must be a list" name)
                None

    /// A required sequence of normalized, in-root paths.
    let private reqPathList (diags: ResizeArray<Diagnostic>) m file name : GovernedPath list option =
        match reqStringList diags m file name with
        | None -> None
        | Some raws ->
            let parsed = raws |> List.map (fun r -> r, normalizePath r)
            if parsed |> List.forall (fun (_, r) -> Result.isOk r) then
                Some(parsed |> List.map (fun (_, r) -> match r with Ok p -> p | Error() -> GovernedPath ""))
            else
                parsed
                |> List.iter (fun (raw, r) ->
                    match r with
                    | Ok _ -> ()
                    | Error() ->
                        diags.Add(diag PathEscapesRoot file (Some name) None None (sprintf "path '%s' escapes the governed root" raw)))
                None

    /// An optional sequence of mapping nodes (absent → empty list).
    let private optMappingSeq (diags: ResizeArray<Diagnostic>) m file name : YamlMappingNode list =
        match getField m name with
        | None -> []
        | Some node ->
            match node with
            | :? YamlSequenceNode as seq ->
                let items = seq.Children |> List.ofSeq
                items
                |> List.iteri (fun i c ->
                    match c with
                    | :? YamlMappingNode -> ()
                    | _ -> addMalformed diags file (sprintf "%s[%d]" name i) c "list entry must be a mapping")
                items |> List.choose (function :? YamlMappingNode as mm -> Some mm | _ -> None)
            | _ ->
                addMalformed diags file name node (sprintf "field '%s' must be a list" name)
                []

    /// An optional single mapping node (absent → None).
    let private optMapping (diags: ResizeArray<Diagnostic>) m file name : YamlMappingNode option =
        match getField m name with
        | None -> None
        | Some node ->
            match node with
            | :? YamlMappingNode as mm -> Some mm
            | _ ->
                addMalformed diags file name node (sprintf "field '%s' must be a mapping" name)
                None

    // ── Duplicate-id detection (within a list) ──

    let private checkDuplicates (diags: ResizeArray<Diagnostic>) file fieldName (ids: string list) =
        ids
        |> List.countBy id
        |> List.iter (fun (k, n) ->
            if n > 1 then
                diags.Add(
                    diag DuplicateId file (Some fieldName) (Some k) None (sprintf "id '%s' is declared %d times in '%s'; ids must be unique" k n fieldName)
                ))

    // ── Ordering helpers (FR-012, D8 — sort every emitted list by a stable key) ──

    let private sortByKey (keyOf: 'a -> string) (xs: 'a list) : 'a list = xs |> List.sortBy keyOf

    // ── Per-entry parsers ──

    let private parsePathMapEntry (diags: ResizeArray<Diagnostic>) (m: YamlMappingNode) : PathMapEntry option =
        diags.AddRange(unknownFields m (set [ "glob"; "capability" ]) Capabilities)
        let glob = reqPath diags m Capabilities "glob"
        let capability = reqString diags m Capabilities "capability" |> Option.map DomainId
        match glob, capability with
        | Some g, Some c -> Some { Glob = g; Capability = c }
        | _ -> None

    let private parseSurface (diags: ResizeArray<Diagnostic>) (m: YamlMappingNode) : Surface option =
        diags.AddRange(unknownFields m (set [ "id"; "kind"; "paths"; "owner"; "maturity"; "evidenceTag"; "templateProfile"; "baseline" ]) Capabilities)
        let id = reqString diags m Capabilities "id" |> Option.map SurfaceId
        let kind = reqEnum parseSurfaceClass diags m Capabilities "kind"
        let paths = reqPathList diags m Capabilities "paths"
        let owner = reqString diags m Capabilities "owner" |> Option.map Owner
        let maturity = reqEnum parseMaturity diags m Capabilities "maturity"
        // F23 optional product attributes — all None for an MVP-shaped surface (data-model §1.4).
        let evidenceTag = optString diags m Capabilities "evidenceTag" |> Option.map EvidenceTag
        let templateProfile = optString diags m Capabilities "templateProfile" |> Option.map TemplateProfile
        let baseline = optString diags m Capabilities "baseline" |> Option.map Baseline
        match id, kind, paths, owner, maturity with
        | Some i, Some k, Some ps, Some o, Some mt ->
            Some
                { Id = i
                  Class = k
                  Paths = ps |> sortByKey (fun (GovernedPath p) -> p)
                  Owner = o
                  Maturity = mt
                  EvidenceTag = evidenceTag
                  TemplateProfile = templateProfile
                  Baseline = baseline }
        | _ -> None

    let private parseCheck (diags: ResizeArray<Diagnostic>) (m: YamlMappingNode) : Check option =
        diags.AddRange(unknownFields m (set [ "id"; "domain"; "command"; "owner"; "cost"; "environment"; "maturity"; "tier" ]) Capabilities)
        let id = reqString diags m Capabilities "id" |> Option.map CheckId
        let domain = reqString diags m Capabilities "domain" |> Option.map DomainId
        let command = optString diags m Capabilities "command" |> Option.map CommandId
        let owner = reqString diags m Capabilities "owner" |> Option.map Owner
        let cost = reqEnum parseCost diags m Capabilities "cost"
        let env = reqEnum parseEnvironment diags m Capabilities "environment"
        let maturity = reqEnum parseMaturity diags m Capabilities "maturity"
        // F23 optional tier — present on cost-tiered generated-product checks (data-model §1.5).
        let tier = optEnum parseTier diags m Capabilities "tier"
        match id, domain, owner, cost, env, maturity with
        | Some i, Some d, Some o, Some c, Some e, Some mt ->
            Some
                { Id = i
                  Domain = d
                  Command = command
                  Owner = o
                  Cost = c
                  Environment = e
                  Maturity = mt
                  Tier = tier }
        | _ -> None

    let private parseCommandSpec (diags: ResizeArray<Diagnostic>) (m: YamlMappingNode) : CommandSpec option =
        diags.AddRange(unknownFields m (set [ "id"; "command"; "timeout"; "environment" ]) Tooling)
        let id = reqString diags m Tooling "id" |> Option.map CommandId
        let command = reqString diags m Tooling "command"
        let timeout = reqInt diags m Tooling "timeout" |> Option.map (fun s -> TimeoutLimit s)
        let env = reqEnum parseEnvironment diags m Tooling "environment"
        match id, command, timeout, env with
        | Some i, Some c, Some t, Some e -> Some { Id = i; Command = c; Timeout = t; Environment = e }
        | _ -> None

    let private parseExternalTool (diags: ResizeArray<Diagnostic>) (m: YamlMappingNode) : ExternalToolReq option =
        diags.AddRange(unknownFields m (set [ "tool"; "minVersion" ]) Tooling)
        let tool = reqString diags m Tooling "tool"
        let minVersion = reqString diags m Tooling "minVersion"
        match tool, minVersion with
        | Some t, Some v -> Some { Tool = t; MinVersion = v }
        | _ -> None

    // ── Per-file parsers ──

    let private finish (diags: ResizeArray<Diagnostic>) (build: unit -> 'f) : Result<'f, Diagnostic list> =
        if diags.Count = 0 then Ok(build ()) else Error(List.ofSeq diags)

    let private parseProject (m: YamlMappingNode) : Result<ProjectFacts, Diagnostic list> =
        let diags = ResizeArray<Diagnostic>()
        diags.AddRange(unknownFields m (set [ "schemaVersion"; "id"; "governedRoot"; "domains"; "packageSurfaces"; "policyRef"; "capabilitiesRef" ]) Project)
        let sv = match readSchemaVersion m Project with Ok v -> Some v | Error d -> diags.Add d; None
        let id = reqString diags m Project "id" |> Option.map ProjectId
        let governedRoot = reqPath diags m Project "governedRoot"
        let domains = reqStringList diags m Project "domains"
        domains |> Option.iter (checkDuplicates diags Project "domains")
        let packageSurfaces =
            match getField m "packageSurfaces" with
            | None -> Some []
            | Some _ -> reqPathList diags m Project "packageSurfaces"
        let policyRef = optPath diags m Project "policyRef"
        let capabilitiesRef = optPath diags m Project "capabilitiesRef"
        finish diags (fun () ->
            { SchemaVersion = sv.Value
              Id = id.Value
              Domains = domains.Value |> List.map DomainId |> sortByKey (fun (DomainId d) -> d)
              GovernedRoot = governedRoot.Value
              PackageSurfaces = packageSurfaces.Value |> sortByKey (fun (GovernedPath p) -> p)
              PolicyRef = policyRef
              CapabilitiesRef = capabilitiesRef })

    let private parsePolicy (m: YamlMappingNode) : Result<PolicyFacts, Diagnostic list> =
        let diags = ResizeArray<Diagnostic>()
        diags.AddRange(unknownFields m (set [ "schemaVersion"; "defaultProfile"; "profiles"; "branchPolicy"; "reviewBudget" ]) Policy)
        let sv = match readSchemaVersion m Policy with Ok v -> Some v | Error d -> diags.Add d; None
        let profiles = reqStringList diags m Policy "profiles"
        profiles |> Option.iter (checkDuplicates diags Policy "profiles")
        let defaultProfile = reqString diags m Policy "defaultProfile" |> Option.map ProfileId
        let branchPolicy =
            optMapping diags m Policy "branchPolicy"
            |> Option.bind (fun bm ->
                diags.AddRange(unknownFields bm (set [ "pattern"; "requirePr" ]) Policy)
                let pattern = reqString diags bm Policy "pattern"
                let requirePr = reqBool diags bm Policy "requirePr"
                match pattern, requirePr with
                | Some p, Some r -> Some { Pattern = p; RequirePr = r }
                | _ -> None)
        let reviewBudget =
            optMapping diags m Policy "reviewBudget"
            |> Option.bind (fun rm ->
                diags.AddRange(unknownFields rm (set [ "maxReviews" ]) Policy)
                reqInt diags rm Policy "maxReviews" |> Option.map (fun n -> { MaxReviews = n }))
        finish diags (fun () ->
            { SchemaVersion = sv.Value
              Profiles = profiles.Value |> List.map ProfileId |> sortByKey (fun (ProfileId p) -> p)
              DefaultProfile = defaultProfile.Value
              BranchPolicy = branchPolicy
              ReviewBudget = reviewBudget })

    let private parseCapabilities (m: YamlMappingNode) : Result<CapabilityFacts, Diagnostic list> =
        let diags = ResizeArray<Diagnostic>()
        diags.AddRange(unknownFields m (set [ "schemaVersion"; "domains"; "pathMap"; "surfaces"; "checks" ]) Capabilities)
        let sv = match readSchemaVersion m Capabilities with Ok v -> Some v | Error d -> diags.Add d; None
        let domains = reqStringList diags m Capabilities "domains"
        domains |> Option.iter (checkDuplicates diags Capabilities "domains")
        let pathMap = optMappingSeq diags m Capabilities "pathMap" |> List.choose (parsePathMapEntry diags)
        let surfaces = optMappingSeq diags m Capabilities "surfaces" |> List.choose (parseSurface diags)
        checkDuplicates diags Capabilities "surfaces" (surfaces |> List.map (fun s -> let (SurfaceId i) = s.Id in i))
        let checks = optMappingSeq diags m Capabilities "checks" |> List.choose (parseCheck diags)
        checkDuplicates diags Capabilities "checks" (checks |> List.map (fun c -> let (CheckId i) = c.Id in i))
        finish diags (fun () ->
            { SchemaVersion = sv.Value
              Domains = domains.Value |> List.map DomainId |> sortByKey (fun (DomainId d) -> d)
              PathMap = pathMap |> sortByKey (fun e -> let (GovernedPath g) = e.Glob in g)
              Surfaces = surfaces |> sortByKey (fun s -> let (SurfaceId i) = s.Id in i)
              Checks = checks |> sortByKey (fun c -> let (CheckId i) = c.Id in i) })

    let private parseTooling (m: YamlMappingNode) : Result<ToolingFacts, Diagnostic list> =
        let diags = ResizeArray<Diagnostic>()
        diags.AddRange(unknownFields m (set [ "schemaVersion"; "commands"; "environmentClasses"; "externalTools" ]) Tooling)
        let sv = match readSchemaVersion m Tooling with Ok v -> Some v | Error d -> diags.Add d; None
        let commands = optMappingSeq diags m Tooling "commands" |> List.choose (parseCommandSpec diags)
        checkDuplicates diags Tooling "commands" (commands |> List.map (fun c -> let (CommandId i) = c.Id in i))
        let environmentClasses =
            match getField m "environmentClasses" with
            | None -> []
            | Some(:? YamlSequenceNode as seq) ->
                seq.Children
                |> Seq.choose (fun c ->
                    match scalarValue c with
                    | Some v ->
                        match parseEnvironment v with
                        | Some e -> Some e
                        | None ->
                            addMalformed diags Tooling "environmentClasses" c (sprintf "'%s' is not a known environment class" v)
                            None
                    | None ->
                        addMalformed diags Tooling "environmentClasses" c "environment class must be a scalar"
                        None)
                |> List.ofSeq
            | Some node ->
                addMalformed diags Tooling "environmentClasses" node "field 'environmentClasses' must be a list"
                []
        let externalTools = optMappingSeq diags m Tooling "externalTools" |> List.choose (parseExternalTool diags)
        finish diags (fun () ->
            { SchemaVersion = sv.Value
              Commands = commands |> sortByKey (fun c -> let (CommandId i) = c.Id in i)
              EnvironmentClasses = environmentClasses |> List.distinct |> sortByKey environmentToken
              ExternalTools = externalTools |> sortByKey (fun t -> t.Tool) })

    // ── Cross-reference resolution (FR-009) ──

    let private resolveCrossRefs
        (caps: CapabilityFacts)
        (policyOpt: PolicyFacts option)
        (toolingOpt: ToolingFacts option)
        : Diagnostic list =
        let domainSet = caps.Domains |> List.map (fun (DomainId d) -> d) |> Set.ofList
        let commandSet =
            match toolingOpt with
            | Some t -> t.Commands |> List.map (fun c -> let (CommandId i) = c.Id in i) |> Set.ofList
            | None -> Set.empty
        let dangling file field target message =
            diag DanglingReference file (Some field) (Some target) None message
        let pathMapDiags =
            caps.PathMap
            |> List.choose (fun e ->
                let (DomainId d) = e.Capability
                if domainSet.Contains d then None
                else Some(dangling Capabilities "pathMap.capability" d (sprintf "pathMap entry references undeclared capability domain '%s'" d)))
        let checkDomainDiags =
            caps.Checks
            |> List.choose (fun c ->
                let (DomainId d) = c.Domain
                if domainSet.Contains d then None
                else Some(dangling Capabilities "check.domain" d (sprintf "check '%s' references undeclared domain '%s'" (let (CheckId i) = c.Id in i) d)))
        let checkCommandDiags =
            caps.Checks
            |> List.choose (fun c ->
                match c.Command with
                | Some(CommandId cmd) when not (commandSet.Contains cmd) ->
                    Some(dangling Capabilities "check.command" cmd (sprintf "check '%s' references command '%s' not declared in tooling.yml" (let (CheckId i) = c.Id in i) cmd))
                | _ -> None)
        let profileDiags =
            match policyOpt with
            | Some p ->
                let (ProfileId dp) = p.DefaultProfile
                if p.Profiles |> List.exists (fun (ProfileId x) -> x = dp) then []
                else [ dangling Policy "defaultProfile" dp (sprintf "defaultProfile '%s' is not a declared profile" dp) ]
            | None -> []
        pathMapDiags @ checkDomainDiags @ checkCommandDiags @ profileDiags

    // ── Deterministic diagnostic ordering (by file, then locator, then id) ──

    let private fileRank =
        function
        | Project -> 0
        | Policy -> 1
        | Capabilities -> 2
        | Tooling -> 3

    let private sortDiagnostics (ds: Diagnostic list) : Diagnostic list =
        ds
        |> List.sortBy (fun d ->
            fileRank d.File,
            defaultArg d.Locator.Field "",
            defaultArg d.Locator.Id "",
            defaultArg d.Locator.Line 0,
            diagnosticIdToken d.Id)

    // ── The pure validation entry point ──

    let private isWhitespace (s: string) = String.IsNullOrWhiteSpace s

    let private parseError file msg =
        diag MalformedValue file None None None (sprintf "could not parse YAML: %s" msg)

    let validate (source: RawSource) : Validation =
        let handleRequired file slot (parser: YamlMappingNode -> Result<'f, Diagnostic list>) : Result<'f, Diagnostic list> =
            match slot with
            | Absent -> Error [ diag MissingRequiredFile file None None None "this required `.fsgg` file is absent" ]
            | Unreadable err -> Error [ diag UnreadableFile file None None None (sprintf "this `.fsgg` file could not be read: %s" err) ]
            | Present content when isWhitespace content -> Error [ diag EmptyFile file None None None "this `.fsgg` file is empty" ]
            | Present content ->
                match loadRoot content with
                | Error msg -> Error [ parseError file msg ]
                | Ok root -> parser root

        let handleOptional file slot (parser: YamlMappingNode -> Result<'f, Diagnostic list>) : Result<'f option, Diagnostic list> =
            match slot with
            | Absent -> Ok None
            // A present-but-unreadable optional file must FAIL, never degrade to `None` (an absent optional) —
            // a genuine read error is surfaced with its cause, distinct from `EmptyFile` (Principle VI).
            | Unreadable err -> Error [ diag UnreadableFile file None None None (sprintf "this `.fsgg` file could not be read: %s" err) ]
            | Present content when isWhitespace content -> Error [ diag EmptyFile file None None None "this `.fsgg` file is empty" ]
            | Present content ->
                match loadRoot content with
                | Error msg -> Error [ parseError file msg ]
                | Ok root -> parser root |> Result.map Some

        let projectR = handleRequired Project source.Project parseProject
        let capabilitiesR = handleRequired Capabilities source.Capabilities parseCapabilities
        let policyR = handleOptional Policy source.Policy parsePolicy
        let toolingR = handleOptional Tooling source.Tooling parseTooling

        let errOf =
            function
            | Ok _ -> []
            | Error ds -> ds

        let fileDiags = errOf projectR @ errOf policyR @ errOf capabilitiesR @ errOf toolingR

        match fileDiags, projectR, capabilitiesR, policyR, toolingR with
        | [], Ok project, Ok caps, Ok policyOpt, Ok toolingOpt ->
            match resolveCrossRefs caps policyOpt toolingOpt with
            | [] -> Valid { Project = project; Policy = policyOpt; Capabilities = caps; Tooling = toolingOpt }
            | crossDiags -> Invalid(sortDiagnostics crossDiags)
        | _ -> Invalid(sortDiagnostics fileDiags)
