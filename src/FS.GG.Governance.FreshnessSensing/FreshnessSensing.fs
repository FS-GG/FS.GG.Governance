// The SHARED freshness-sensing edge (F046) — the impure sensing code extracted VERBATIM from F044's
// interpreter (research D1). Visibility lives in FreshnessSensing.fsi (Principle II). It owns the real
// SHA-256 catalog/source sensing, the read-only evidence-reuse-store deserializer, and the `SensedFacts`
// assembly the two host commands previously lacked. It NEVER fabricates an unsensed fact (a `None` accessor
// result is unsensed, not an empty value — L3/L4), reaches NO network, and computes NO freshness key / cache
// decision (FR-013). The degrade-on-error policy is NOT here: `senseFreshness`/`loadStore` return faithful
// `Result`s; the calling command's pure `update` decides how to degrade (research D2). Only
// System.Security.Cryptography / System.IO / System.Text.Json from the net10.0 shared framework — no
// third-party dependency.

namespace FS.GG.Governance.FreshnessSensing

open System
open System.IO
open System.Text
open System.Text.Json
open System.Security.Cryptography
open FS.GG.Governance.Config.Model // CommandId, CheckId, DomainId, EnvironmentClass
open FS.GG.Governance.Gates.Model // Gate
open FS.GG.Governance.FreshnessKey.Model // RuleHash, ArtifactHash, CommandVersion, GeneratorVersion, Revision, FreshnessInputs
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts
open FS.GG.Governance.EvidenceReuse // empty
open FS.GG.Governance.EvidenceReuse.Model // ReuseStore, RecordedEvidence, EvidenceRef

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FreshnessSensing =

    type FreshnessSensor =
        { SenseRuleHash: unit -> RuleHash option
          SenseGeneratorVersion: unit -> GeneratorVersion option
          SenseCoveredArtifacts: Gate -> ArtifactHash list option
          SenseCommandVersion: CommandId -> CommandVersion option }

    type StoreReader = string -> Result<ReuseStore option, string>

    // ── real BCL-crypto freshness sensing (carried verbatim from F044's interpreter) ──

    let sha256Hex (bytes: byte[]) : string =
        use sha = SHA256.Create()
        sha.ComputeHash bytes |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

    // Injective concatenation of byte segments: each segment is preceded by its big-endian 4-byte length,
    // so a reader (and the hash) can never confuse where one segment ends and the next begins. This is the
    // same length-prefix discipline FreshnessKey/Provenance use to keep their pre-images prefix-free (#56/B11):
    // without it, `(name="ab",content="c")` and `(name="a",content="bc")` share a pre-image and collide onto
    // one RuleHash cache slot.
    let lenPrefixed (segments: byte[] list) : byte[] =
        use ms = new MemoryStream()

        for s in segments do
            let len = s.Length
            ms.WriteByte(byte (len >>> 24))
            ms.WriteByte(byte (len >>> 16))
            ms.WriteByte(byte (len >>> 8))
            ms.WriteByte(byte len)
            ms.Write(s, 0, s.Length)

        ms.ToArray()

    // The rule-pack hash: a SHA-256 over the repo's `.fsgg/*.yml` catalog bytes (filename + content, sorted),
    // so it is content-addressed and working-directory independent. `None` when no catalog is present.
    let senseCatalogHash (repo: string) : string option =
        let dir = Path.Combine(repo, ".fsgg")

        if not (Directory.Exists dir) then
            None
        else
            let files =
                Directory.GetFiles(dir, "*.yml", SearchOption.TopDirectoryOnly)
                |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))

            if files.Length = 0 then
                None
            else
                let nameBytes (f: string) =
                    match Path.GetFileName f with
                    | null -> Array.empty<byte>
                    | n -> Encoding.UTF8.GetBytes n

                // Length-prefix EACH (name, content) so the flattened stream stays injective across files
                // (#56/B11) — a colliding name/content split can no longer forge the same catalog hash.
                let segments =
                    files
                    |> Array.collect (fun f -> [| nameBytes f; File.ReadAllBytes f |])
                    |> Array.toList

                Some(sha256Hex (lenPrefixed segments))

    // The covered-artifact hashes: a SHA-256 per file under the repo's `src/**` package surface,
    // ordinal-sorted, over the repo-RELATIVE path AND the content (both length-prefixed). MVP scope — every
    // gate shares the repo surface; finer PER-GATE scoping is a documented later refinement. Hashing the
    // relative path keeps it working-directory independent (the path is relative to `repo`, not absolute)
    // while making a rename/move with identical content observable (#56/B11) — a pure content hash could not
    // tell a moved file from an unchanged one. A missing `src/` ⇒ `[]` (sensed-empty, a legitimate resolved
    // value — L4), distinct from the `None` an unsensable surface yields.
    let senseSrcHashes (repo: string) : ArtifactHash list =
        let srcDir = Path.Combine(repo, "src")

        if not (Directory.Exists srcDir) then
            []
        else
            Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories)
            |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
            |> Array.map (fun f ->
                let rel = Path.GetRelativePath(repo, f).Replace('\\', '/')
                let preImage = lenPrefixed [ Encoding.UTF8.GetBytes rel; File.ReadAllBytes f ]
                ArtifactHash(sha256Hex preImage))
            |> Array.toList

    let toolVersion () : string =
        let asm = System.Reflection.Assembly.GetExecutingAssembly()

        match asm.GetName().Version with
        | null -> "0.0.0"
        | v -> v.ToString()

    // ── read-only evidence-reuse store deserializer (fsgg.evidence-reuse-store/v1, artifacts §A5) ──

    let storeSchemaVersion = "fsgg.evidence-reuse-store/v1"

    let parseEnv (s: string) : EnvironmentClass =
        match s with
        | "local" -> EnvironmentClass.Local
        | "ci" -> EnvironmentClass.Ci
        | "local-or-ci" -> EnvironmentClass.LocalOrCi
        | "release" -> EnvironmentClass.Release
        | other -> failwithf "unknown environment class: %s" other

    let reqStr (el: JsonElement) (name: string) : string =
        match el.TryGetProperty name with
        | true, v when v.ValueKind = JsonValueKind.String ->
            match v.GetString() with
            | null -> failwithf "field %s is null" name
            | s -> s
        | _ -> failwithf "missing or non-string field: %s" name

    let optStr (el: JsonElement) (name: string) : string option =
        match el.TryGetProperty name with
        | true, v ->
            match v.ValueKind with
            | JsonValueKind.Null -> None
            | JsonValueKind.String -> v.GetString() |> Option.ofObj
            | _ -> failwithf "field %s must be string or null" name
        | _ -> None

    let strArr (el: JsonElement) (name: string) : string list =
        match el.TryGetProperty name with
        | true, v when v.ValueKind = JsonValueKind.Array ->
            [ for x in v.EnumerateArray() ->
                  match x.ValueKind with
                  | JsonValueKind.String ->
                      match x.GetString() with
                      | null -> failwithf "null element in %s" name
                      | s -> s
                  | _ -> failwithf "non-string element in %s" name ]
        | _ -> failwithf "missing or non-array field: %s" name

    let parseEntry (el: JsonElement) : RecordedEvidence =
        // Built via the public F029/F030 constructors only — computes NO hash/key/digest (FR-013); the
        // opaque newtype strings are taken verbatim from the document.
        let inputs: FreshnessInputs =
            { Check = CheckId(reqStr el "check")
              Domain = DomainId(reqStr el "domain")
              Command = optStr el "command" |> Option.map CommandId
              Environment = parseEnv (reqStr el "environment")
              RuleHash = RuleHash(reqStr el "ruleHash")
              CoveredArtifacts = strArr el "coveredArtifacts" |> List.map ArtifactHash
              CommandVersion = optStr el "commandVersion" |> Option.map CommandVersion
              GeneratorVersion = GeneratorVersion(reqStr el "generatorVersion")
              Base = Revision(reqStr el "base")
              Head = Revision(reqStr el "head") }

        { Inputs = inputs
          Evidence = EvidenceRef(reqStr el "evidence") }

    let parseStore (json: string) : Result<ReuseStore, string> =
        try
            use doc = JsonDocument.Parse json
            let root = doc.RootElement

            let schema =
                match root.TryGetProperty "schemaVersion" with
                | true, v when v.ValueKind = JsonValueKind.String -> v.GetString()
                | _ -> failwith "missing schemaVersion"

            if schema <> storeSchemaVersion then
                failwithf "unknown store schema: %s" schema

            let recorded =
                match root.TryGetProperty "recorded" with
                | true, v when v.ValueKind = JsonValueKind.Array -> [ for el in v.EnumerateArray() -> parseEntry el ]
                | _ -> failwith "missing recorded array"

            Ok(ReuseStore recorded)
        with e ->
            Error e.Message

    let realStoreReader: StoreReader =
        fun path ->
            try
                if not (File.Exists path) then
                    Ok None
                else
                    parseStore (File.ReadAllText path) |> Result.map Some
            with e ->
                Error e.Message

    // ── realSensor — wire the real freshness-sensing port ──

    let realSensor (repo: string) : FreshnessSensor =
        // Sense the rule pack + the covered surface ONCE; closed over by the sensor (deterministic).
        let catalogHash = senseCatalogHash repo
        let covered = senseSrcHashes repo
        let gv = toolVersion ()

        { SenseRuleHash = fun () -> catalogHash |> Option.map RuleHash
          SenseGeneratorVersion = fun () -> Some(GeneratorVersion gv)
          // MVP: a gate covers the repo's `src/**` surface (finer per-gate scoping deferred).
          SenseCoveredArtifacts = fun _gate -> Some covered
          // MVP coarse command version: a short digest of the command id stamped against the rule pack
          // it is declared in (changes when the rule pack changes). Cheap, real, deterministic; richer
          // command-version sensing is a later refinement. `None` when no catalog (unsensed, no-hide).
          SenseCommandVersion =
            fun (CommandId c) ->
                catalogHash
                |> Option.map (fun h -> CommandVersion((sha256Hex (Encoding.UTF8.GetBytes(c + "@" + h))).Substring(0, 12))) }

    // ── senseFreshness — assemble SensedFacts (the F044 SenseFreshness handler body); TOTAL/guarded ──

    let senseFreshness
        (sensor: FreshnessSensor)
        (gates: Gate list)
        (baseHead: Revision option * Revision option)
        : Result<SensedFacts, string> =
        // Assemble SensedFacts from base/head (passed through from RepoSnapshot.Range) + the FreshnessSensor
        // over each selected gate / declared command. A present Map key means SENSED (even when the value is
        // empty); an absent key means NOT SENSED — never fabricated (L3/L4).
        let baseOpt, headOpt = baseHead

        try
            let ruleHash = sensor.SenseRuleHash()
            let genVer = sensor.SenseGeneratorVersion()

            let covered =
                gates
                |> List.choose (fun g -> sensor.SenseCoveredArtifacts g |> Option.map (fun hs -> g.Id, hs))
                |> Map.ofList

            let commandVersions =
                gates
                |> List.choose (fun g -> g.FreshnessKey.Command)
                |> List.distinct
                |> List.choose (fun cid -> sensor.SenseCommandVersion cid |> Option.map (fun v -> cid, v))
                |> Map.ofList

            let facts: SensedFacts =
                { RuleHash = ruleHash
                  GeneratorVersion = genVer
                  Base = baseOpt
                  Head = headOpt
                  CoveredArtifacts = covered
                  CommandVersions = commandVersions }

            Ok facts
        with e ->
            Error e.Message

    // ── loadStore — load the read-only store (the F044 LoadStore handler body); absent ⇒ empty ──

    let loadStore (reader: StoreReader) (path: string) : Result<ReuseStore, string> =
        try
            match reader path with
            | Ok None -> Ok EvidenceReuse.empty // absent ⇒ empty (FR-006)
            | Ok(Some store) -> Ok store
            | Error e -> Error e
        with e ->
            Error e.Message
