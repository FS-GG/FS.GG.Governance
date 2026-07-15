namespace FS.GG.Governance.AttestationJson

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.Attestation.Model

// The F26 attestation.json projection (P2). Renders the SLSA/in-toto-SHAPED AttestationSummary into the
// deterministic, versioned `fsgg.attestation/v1` document via a hand-driven System.Text.Json Utf8JsonWriter
// walk — the net10.0 shared-framework mechanism, so NO new dependency (the F25 ProvenanceJson precedent).
// PURE and TOTAL: no I/O, no git, no clock, never throws. Identity is reused VERBATIM (the summary's Identity
// = Provenance.canonicalId; each run's identity via Audit.runIdentity). `subjects` sorted by name;
// `artifactDigests` sorted (set); `invocation.runs` in carried order; `durationNanos` is sensed metadata that
// never affects any identity; the compliance marker + note are ALWAYS present. The surface is
// AttestationJson.fsi (Principle II) — no access modifiers here.

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.JsonTokens // 073: the shared closed-enum token helpers (module-qualified)
open FS.GG.Governance.JsonWriters // JSON-3: the shared value un-wrappers + writeRun (module-qualified)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttestationJson =

    let schemaVersion = "fsgg.attestation/v1"

    // Public (M-JSON-1): the ReleaseJson and VerifyJson `attestation` reference embeds reference this instead
    // of hard-coding the literal, so a v2 bump cannot silently strand those two projections.
    let complianceToken = "compatible-shape-not-formal-compliance"

    let complianceNote =
        "SLSA/in-toto-shaped, reproducible metadata; NOT a claim of formal SLSA-level or in-toto attestation conformance."

    // ── internal writer plumbing + value/token helpers (hidden — absent from AttestationJson.fsi) ──
    // JSON-3: the seven newtype un-wrappers and `writeRun` now live in the shared JsonWriters leaf (they were
    // byte-identical to ProvenanceJson's copies); referenced module-qualified as `JsonWriters.<name>`.

    let complianceMarkerToken (marker: ComplianceMarker) : string =
        match marker with
        | CompatibleShapeNotFormalCompliance -> complianceToken

    let writeSubject (w: Utf8JsonWriter) (s: AttestationSubject) =
        w.WriteStartObject()
        w.WriteString("name", s.Name)
        w.WriteString("version", s.Version)
        w.WriteString("digest", JsonWriters.artifactValue s.Digest)
        w.WriteEndObject()

    // ── the public entry point ──

    let ofAttestation (summary: AttestationSummary) : string =
        let m = summary.Materials

        let sortedSubjects =
            summary.Subjects
            |> List.sortWith (fun a b -> String.CompareOrdinal(a.Name, b.Name))

        let sortedDigests =
            m.ArtifactDigests
            |> List.map JsonWriters.artifactValue
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("compliance", complianceMarkerToken summary.Compliance)
            w.WriteString("complianceNote", complianceNote)
            w.WriteString("identity", summary.Identity)
            w.WriteString("builder", JsonWriters.builderValue summary.Builder)

            w.WritePropertyName "subjects"
            w.WriteStartArray()
            for s in sortedSubjects do
                writeSubject w s
            w.WriteEndArray()

            w.WritePropertyName "materials"
            w.WriteStartObject()
            w.WriteString("ruleHash", JsonWriters.ruleHashValue m.RuleHash)
            w.WriteString("generatorVersion", JsonWriters.generatorVersionValue m.GeneratorVersion)
            w.WriteString("sourceCommit", JsonWriters.revisionValue m.SourceCommit)
            w.WriteString("base", JsonWriters.revisionValue m.BaseRevision)
            w.WriteString("head", JsonWriters.revisionValue m.HeadRevision)

            w.WritePropertyName "artifactDigests"
            w.WriteStartArray()
            for d in sortedDigests do
                w.WriteStringValue d
            w.WriteEndArray()

            w.WriteString("environment", JsonTokens.environmentToken m.Environment)
            w.WriteEndObject()

            w.WritePropertyName "invocation"
            w.WriteStartObject()
            w.WritePropertyName "runs"
            w.WriteStartArray()
            for run in summary.Invocation.Runs do
                JsonWriters.writeRun w run
            w.WriteEndArray()
            w.WriteEndObject()

            w.WriteEndObject())
