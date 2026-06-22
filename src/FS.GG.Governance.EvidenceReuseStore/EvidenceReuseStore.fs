// The pure, total WRITE HALF of the evidence-reuse store (F047). The public surface is fixed by
// EvidenceReuseStore.fsi (Principle II); no top-level binding here carries an access modifier — every writer
// helper below is hidden by its absence from the .fsi (the F042 `CacheEligibilityJson` / F025 `AuditJson` /
// kernel `Json.fs` precedent). All three operations are pure, total, and deterministic (FR-009): no clock,
// filesystem, git, environment, or network; identical inputs always yield identical output. `serialise` is the
// byte-stable INVERSE of the F046 `FreshnessSensing` reader; `retain`/`prune` reuse F030 `entries` and F029
// `matches` VERBATIM (FR-010) and remove only whole entries — never mutate or fabricate. This row computes NO
// persistence, produces NO real evidence, runs NO gate, and bumps NO schema version.

namespace FS.GG.Governance.EvidenceReuseStore

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceReuseStore =

    let schemaVersion = "fsgg.evidence-reuse-store/v1"

    let defaultRetentionBound = 256

    // ── internal writer plumbing (hidden — absent from EvidenceReuseStore.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact, byte-stable output (the `Json.fs`
    /// `writeToString` precedent shared by F020/F021/F025/F042; research D2).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    /// The EXHAUSTIVE inverse of the F046 reader's `parseEnv`, with NO wildcard (research D6): a future
    /// `EnvironmentClass` case is a compile error here, never a silently mis-tokened field.
    let environmentToken (env: EnvironmentClass) : string =
        match env with
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "local-or-ci"
        | Release -> "release"

    /// One entry — the fixed field order (research D7) the F046 `parseEntry` consumes. Optional `command` /
    /// `commandVersion` are OMITTED on `None` (research D4 — `optStr` maps absent to `None`); `coveredArtifacts`
    /// is emitted in the entry's STORED list order, verbatim — never sorted/de-duped (research D5, so round-trip
    /// identity holds). Every newtype string + the opaque `EvidenceRef` is rendered VERBATIM via its unwrapped
    /// value (FR-004 — never parsed, re-hashed, or interpreted).
    let writeEntry (w: Utf8JsonWriter) (entry: RecordedEvidence) =
        let i = entry.Inputs
        let (CheckId check) = i.Check
        let (DomainId domain) = i.Domain
        let (RuleHash ruleHash) = i.RuleHash
        let (GeneratorVersion generatorVersion) = i.GeneratorVersion
        let (Revision baseRev) = i.Base
        let (Revision headRev) = i.Head

        w.WriteStartObject()
        w.WriteString("check", check)
        w.WriteString("domain", domain)

        match i.Command with
        | Some(CommandId command) -> w.WriteString("command", command)
        | None -> ()

        w.WriteString("environment", environmentToken i.Environment)
        w.WriteString("ruleHash", ruleHash)

        w.WritePropertyName "coveredArtifacts"
        w.WriteStartArray()
        for ArtifactHash art in i.CoveredArtifacts do
            w.WriteStringValue art
        w.WriteEndArray()

        match i.CommandVersion with
        | Some(CommandVersion commandVersion) -> w.WriteString("commandVersion", commandVersion)
        | None -> ()

        w.WriteString("generatorVersion", generatorVersion)
        w.WriteString("base", baseRev)
        w.WriteString("head", headRev)
        w.WriteString("evidence", EvidenceReuse.referenceValue entry.Evidence)
        w.WriteEndObject()

    // ── the public operations ──

    let serialise (store: ReuseStore) : string =
        // One linear walk: the top-level object in the FIXED order schemaVersion → recorded, entries emitted in
        // the store's existing newest-first list order, re-sorting NOTHING (research D7). The empty store yields
        // a present, empty `recorded` array — a well-formed document, never throwing (FR-005/FR-009).
        writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WritePropertyName "recorded"
            w.WriteStartArray()
            for entry in EvidenceReuse.entries store do
                writeEntry w entry
            w.WriteEndArray()
            w.WriteEndObject())

    let retain (maxEntries: int) (store: ReuseStore) : ReuseStore =
        // Keep the newest `maxEntries` entries (the head of the newest-first list). `max 0` guards negatives
        // (⇒ empty); `List.truncate` returns the list unchanged when shorter than the bound (idempotent
        // at/under bound — no reorder/rewrite). Removes only whole entries — never mutates or fabricates
        // (research D8, FR-006/FR-009).
        EvidenceReuse.entries store
        |> List.truncate (max 0 maxEntries)
        |> ReuseStore

    let prune (store: ReuseStore) : ReuseStore =
        // Remove every entry a STRICTLY-NEWER entry already full-matches: a single fold over the newest-first
        // list keeping an entry iff no already-kept (newer) entry `FreshnessKey.matches` it — i.e. `record`'s
        // full-match dedup generalised across the whole store. Reuses the F029 `matches` relation VERBATIM
        // (FR-010 — no new policy). Survivors are a subset in their original newest-first order; verdict-
        // preserving hence recompute-safe (research D9, FR-007/FR-008/FR-009).
        EvidenceReuse.entries store
        |> List.fold
            (fun kept entry ->
                if kept |> List.exists (fun k -> FreshnessKey.matches k.Inputs entry.Inputs) then
                    kept
                else
                    entry :: kept)
            []
        |> List.rev
        |> ReuseStore
