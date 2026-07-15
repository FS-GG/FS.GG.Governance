namespace FS.GG.Governance.ProvenanceJson

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model

// The F25 provenance.json projection (US4). Renders the F033 `AuditSnapshot` into the deterministic,
// versioned `fsgg.provenance/v1` document via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk — the
// net10.0 shared-framework mechanism, so NO new dependency. PURE and TOTAL: no I/O, no git, no clock, never
// throws. Identity is reused VERBATIM: the top-level `identity` is `Provenance.canonicalId` and each run's
// `identity` is `CommandRecord.canonicalId` (via `Audit.runIdentity`) — the projection computes no new
// fingerprint. The only sensed field is each run's `durationNanos`, rendered as audit metadata that never
// affects any identity. `artifactDigests` is rendered SORTED (set semantics in the F033 identity);
// `commandRuns` is rendered in carried order (order-significant in F033). No access modifiers — the surface
// is ProvenanceJson.fsi (Principle II).

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)
open FS.GG.Governance.JsonWriters // JSON-3: the shared value un-wrappers + writeRun (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProvenanceJson =

    let schemaVersion = "fsgg.provenance/v1"

    // ── internal writer plumbing + value/token helpers (hidden — absent from ProvenanceJson.fsi) ──
    // JSON-3: the seven newtype un-wrappers and `writeRun` now live in the shared JsonWriters leaf (they were
    // byte-identical to AttestationJson's copies); referenced module-qualified as `JsonWriters.<name>`.

    // ── the public entry point ──

    let ofSnapshot (snapshot: AuditSnapshot) : string =
        let p = snapshot.Provenance

        let sortedDigests =
            p.ArtifactDigests
            |> List.map JsonWriters.artifactValue
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("identity", Audit.snapshotIdentity snapshot)
            w.WriteString("sourceCommit", JsonWriters.revisionValue p.SourceCommit)
            w.WriteString("base", JsonWriters.revisionValue p.Base)
            w.WriteString("head", JsonWriters.revisionValue p.Head)
            w.WriteString("ruleHash", JsonWriters.ruleHashValue p.RuleHash)
            w.WriteString("generatorVersion", JsonWriters.generatorVersionValue p.GeneratorVersion)
            w.WriteString("environment", JsonTokens.environmentToken p.Environment)
            w.WriteString("builder", JsonWriters.builderValue p.Builder)

            w.WritePropertyName "artifactDigests"
            w.WriteStartArray()
            for d in sortedDigests do
                w.WriteStringValue d
            w.WriteEndArray()

            w.WritePropertyName "commandRuns"
            w.WriteStartArray()
            for run in snapshot.Runs do
                JsonWriters.writeRun w run
            w.WriteEndArray()

            w.WriteEndObject())
