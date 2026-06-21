// Provenance operations for the provenance core (F033). The public surface is fixed by Provenance.fsi
// (Principle II); no top-level binding here carries an access modifier. `build` is pure record construction;
// `canonicalId` renders ONLY the reproducible facts to a byte-stable identity in the F029/F032 tagged,
// length-prefixed, injective discipline (contracts/provenance-identity-format.md): the artifact digests as a
// SET (order/dup invariant), the command records in ORDER (order-significant), each command record folded
// via F032 `CommandRecord.canonicalId` so the sensed durations — held apart inside the embedded F032 records
// (D3) — are never read. All three operations are pure, total, deterministic: no clock, filesystem, git,
// environment, or network; no process spawn; no hashing. BCL string building only (FR-011).

namespace FS.GG.Governance.Provenance

open System.Text
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Provenance =

    // ── The single pure, total assembly (US1) ──

    let build
        (sourceCommit: Revision)
        (baseRevision: Revision)
        (headRevision: Revision)
        (ruleHash: RuleHash)
        (generatorVersion: GeneratorVersion)
        (artifactDigests: ArtifactHash list)
        (commandRecords: CommandRecord list)
        (environment: EnvironmentClass)
        (builder: BuilderIdentity)
        : Provenance =
        // Verbatim carriage — no normalization, no sorting, no dedup (canonicalization is `canonicalId`'s
        // job — L-B4). The sensed durations stay structurally apart inside the embedded F032 records (D3).
        { SourceCommit = sourceCommit
          Base = baseRevision
          Head = headRevision
          RuleHash = ruleHash
          GeneratorVersion = generatorVersion
          ArtifactDigests = artifactDigests
          CommandRecords = commandRecords
          Environment = environment
          Builder = builder }

    // ── Segment encoders (internal; hidden by Provenance.fsi) — the F029/F032 discipline (D5) ──

    // UTF-8 byte length of a value — the length prefix that makes the encoding injective: a reader knows
    // exactly how many bytes a value occupies, so no value can masquerade as another field (FR-006).
    let byteLen (s: string) : int = Encoding.UTF8.GetByteCount s

    // A required string value: presence digit '1', then "<byteLen>:<value>" (e.g. "c0ffee" -> "src=16:c0ffee").
    let req (tag: string) (s: string) : string = sprintf "%s=1%d:%s" tag (byteLen s) s

    // The artifact-digest segment: "art=<count>;<len1>:<a1>;<len2>:<a2>;…" with the digest strings
    // DEDUPLICATED and ordinal-SORTED (the F029 set discipline) so supply order and duplicates never affect
    // the identity (L-I3). Empty set ⇒ "art=0;".
    let artSegment (digests: ArtifactHash list) : string =
        let canon =
            digests
            |> List.map (fun (ArtifactHash a) -> a)
            |> List.distinct
            |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))

        let body =
            canon
            |> List.map (fun a -> sprintf "%d:%s" (byteLen a) a)
            |> String.concat ";"

        sprintf "art=%d;%s" (List.length canon) body

    // The command-records segment: "cmds=<count>;<len1>:<id1>;<len2>:<id2>;…" rendered in given ORDER, NOT
    // sorted or deduplicated (run order is reproducible provenance; a repeated run is a real repeat — D4,
    // L-I4). Each entry is the FULL F032 canonical-id string via `CommandRecord.identityValue
    // (CommandRecord.canonicalId record)` (duration-free — F032 D2), length-prefixed so its embedded
    // '\n'/':'/';'/'=' cannot bleed across the boundary. Empty list ⇒ "cmds=0;".
    let cmdsSegment (records: CommandRecord list) : string =
        let body =
            records
            |> List.map (fun r ->
                let id = CommandRecord.identityValue (CommandRecord.canonicalId r)
                sprintf "%d:%s" (byteLen id) id)
            |> String.concat ";"

        sprintf "cmds=%d;%s" (List.length records) body

    // The environment-class token: the SAME four tokens F029's internal `environmentToken` uses (D5); F029's
    // helper is not public, so this core replicates the four-case total match locally (F029 stays untouched).
    let environmentToken (env: EnvironmentClass) : string =
        match env with
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "localOrCi"
        | Release -> "release"

    // ── The pure, total canonical identity over the reproducible facts (US2) ──

    let canonicalId (provenance: Provenance) : ProvenanceIdentity =
        // Computed ONLY over the reproducible facts; the embedded records' durations are never read (D3) —
        // each command record contributes its duration-free F032 canonical id.
        let (Revision src) = provenance.SourceCommit
        let (Revision baseRev) = provenance.Base
        let (Revision headRev) = provenance.Head
        let (RuleHash rule) = provenance.RuleHash
        let (GeneratorVersion gen) = provenance.GeneratorVersion
        let (BuilderIdentity bld) = provenance.Builder

        // Fixed field order, joined by '\n', no trailing newline
        // (contracts/provenance-identity-format.md).
        [ req "src" src
          req "base" baseRev
          req "head" headRev
          req "rule" rule
          req "gen" gen
          artSegment provenance.ArtifactDigests
          cmdsSegment provenance.CommandRecords
          req "env" (environmentToken provenance.Environment)
          req "bld" bld ]
        |> String.concat "\n"
        |> ProvenanceIdentity

    let identityValue (identity: ProvenanceIdentity) : string =
        let (ProvenanceIdentity s) = identity
        s
