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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttestationJson =

    let schemaVersion = "fsgg.attestation/v1"

    let private complianceToken = "compatible-shape-not-formal-compliance"

    let private complianceNote =
        "SLSA/in-toto-shaped, reproducible metadata; NOT a claim of formal SLSA-level or in-toto attestation conformance."

    // ── internal writer plumbing + value/token helpers (hidden — absent from AttestationJson.fsi) ──

    let private revisionValue (Revision s) = s
    let private ruleHashValue (RuleHash s) = s
    let private generatorVersionValue (GeneratorVersion s) = s
    let private artifactValue (ArtifactHash s) = s
    let private builderValue (BuilderIdentity s) = s
    let private exitCodeValue (ExitCode i) = i
    let private durationNanos (SensedDuration n) = n

    let private complianceMarkerToken (marker: ComplianceMarker) : string =
        match marker with
        | CompatibleShapeNotFormalCompliance -> complianceToken

    let private writeSubject (w: Utf8JsonWriter) (s: AttestationSubject) =
        w.WriteStartObject()
        w.WriteString("name", s.Name)
        w.WriteString("version", s.Version)
        w.WriteString("digest", artifactValue s.Digest)
        w.WriteEndObject()

    /// One command run — field order `kind`, `identity`, `exitCode`, `durationNanos`. `identity` reuses the
    /// F032 identity verbatim (via Audit.runIdentity); `durationNanos` is sensed metadata, never identity.
    let private writeRun (w: Utf8JsonWriter) (run: KindedCommandRun) =
        w.WriteStartObject()
        w.WriteString("kind", Audit.kindToken run.Kind)
        w.WriteString("identity", Audit.runIdentity run)
        w.WriteNumber("exitCode", exitCodeValue run.Record.Reproducible.ExitCode)
        w.WriteNumber("durationNanos", durationNanos run.Record.Duration)
        w.WriteEndObject()

    // ── the public entry point ──

    let ofAttestation (summary: AttestationSummary) : string =
        let m = summary.Materials

        let sortedSubjects =
            summary.Subjects
            |> List.sortWith (fun a b -> String.CompareOrdinal(a.Name, b.Name))

        let sortedDigests =
            m.ArtifactDigests
            |> List.map artifactValue
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("compliance", complianceMarkerToken summary.Compliance)
            w.WriteString("complianceNote", complianceNote)
            w.WriteString("identity", summary.Identity)
            w.WriteString("builder", builderValue summary.Builder)

            w.WritePropertyName "subjects"
            w.WriteStartArray()
            for s in sortedSubjects do
                writeSubject w s
            w.WriteEndArray()

            w.WritePropertyName "materials"
            w.WriteStartObject()
            w.WriteString("ruleHash", ruleHashValue m.RuleHash)
            w.WriteString("generatorVersion", generatorVersionValue m.GeneratorVersion)
            w.WriteString("sourceCommit", revisionValue m.SourceCommit)
            w.WriteString("base", revisionValue m.BaseRevision)
            w.WriteString("head", revisionValue m.HeadRevision)

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
                writeRun w run
            w.WriteEndArray()
            w.WriteEndObject()

            w.WriteEndObject())
