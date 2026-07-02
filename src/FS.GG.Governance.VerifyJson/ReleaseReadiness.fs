// The verify.json release-readiness writer (076 Phase C seam). Visibility lives in ReleaseReadiness.fsi
// (Principle II) — no top-level access modifiers here; the `rr*`/`writePack*` helpers are hidden by their
// absence from the .fsi (they previously carried a redundant `private`, dropped per Principle II). PURE:
// walks a caller-owned `Utf8JsonWriter`. Lifted verbatim from VerifyJson.fs; the composing `VerifyJson` entry
// calls `ReleaseReadiness.writeReleaseReadiness` under the same `Some/None` preview guard.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json
open FS.GG.Governance.Config.Model          // SurfaceId
open FS.GG.Governance.FreshnessKey.Model     // ArtifactHash
open FS.GG.Governance.Ship.Model             // Verdict (Pass/Fail)
open FS.GG.Governance.PackEvidence.Model      // PackEvidenceSet / PackVerdict / VersionVerdict / PackOutcome
open FS.GG.Governance.Attestation.Model       // AttestationSummary
open FS.GG.Governance.AttestationJson         // schemaVersion / complianceToken — the canonical attestation tokens
open FS.GG.Governance.ReleaseReport.Model     // VerifyReleasePreview

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReleaseReadiness =

    // ── F26: the additive `releaseReadiness` advisory preview block. Mirrors the release.json v2
    //    packageEvidence/versionPolicy/attestation shape (the cores own their own writers — the codebase
    //    convention each projection duplicates its token helpers). `advisory` is ALWAYS true; verify's exit
    //    code is decided WITHOUT it (FR-005). Written ONLY when a preview is present; absent ⇒ byte-identical
    //    to the pre-F26 projection (no schemaVersion bump — fsgg.verify/v1 stays). ──

    let rrSurfaceValue (SurfaceId s) : string = s
    let rrArtifactHashValue (ArtifactHash s) : string = s

    let rrVerdictToken (verdict: Verdict) : string =
        match verdict with
        | Pass -> "pass"
        | Fail -> "fail"

    let rrPackOutcomeToken (outcome: PackOutcome) : string =
        match outcome with
        | Packed _ -> "packed"
        | PackedNoArtifact _ -> "packedNoArtifact"
        | PackFailed _ -> "packFailed"

    let rrVersionVerdictToken (v: VersionVerdict) : string =
        match v with
        | Bumped _ -> "bumped"
        | Unbumped _ -> "unbumped"
        | Downgraded _ -> "downgraded"
        | NoBaseline _ -> "noBaseline"
        | NotPackable -> "notPackable"

    let rrNullableString (w: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some s -> w.WriteString(name, s)
        | None -> w.WritePropertyName name; w.WriteNullValue()

    let rrNullableInt (w: Utf8JsonWriter) (name: string) (value: int option) =
        match value with
        | Some i -> w.WriteNumber(name, i)
        | None -> w.WritePropertyName name; w.WriteNullValue()

    let rrPathOf (v: PackVerdict) : string =
        match v.Outcome with
        | Packed(art, _) -> art.ArtifactPath
        | PackedNoArtifact _
        | PackFailed _ -> ""

    let writePackProject (w: Utf8JsonWriter) (verdict: PackVerdict) =
        let artifactPath, packedVersion, digest =
            match verdict.Outcome with
            | Packed(art, _) -> Some art.ArtifactPath, Some art.PackedVersion, Some(rrArtifactHashValue art.Digest)
            | PackedNoArtifact _
            | PackFailed _ -> None, None, None

        let sentinel =
            match verdict.Outcome with
            | PackFailed(_, s, _) -> Some s
            | Packed _
            | PackedNoArtifact _ -> None

        w.WriteStartObject()
        w.WriteString("surface", rrSurfaceValue verdict.Surface)
        w.WriteString("outcome", rrPackOutcomeToken verdict.Outcome)
        rrNullableString w "artifactPath" artifactPath
        rrNullableString w "packedVersion" packedVersion
        rrNullableString w "digest" digest
        rrNullableInt w "sentinel" sentinel
        w.WriteString("reason", verdict.Reason)
        w.WriteEndObject()

    let writePackageEvidence (w: Utf8JsonWriter) (pack: PackEvidenceSet) =
        let sorted =
            pack.Verdicts
            |> List.sortWith (fun a b ->
                let s = System.String.CompareOrdinal(rrSurfaceValue a.Surface, rrSurfaceValue b.Surface)
                if s <> 0 then s else System.String.CompareOrdinal(rrPathOf a, rrPathOf b))

        w.WritePropertyName "packageEvidence"
        w.WriteStartObject()
        w.WriteBoolean("noPackableProjects", pack.NoPackableProjects)
        w.WritePropertyName "projects"
        w.WriteStartArray()
        for v in sorted do
            writePackProject w v
        w.WriteEndArray()
        w.WriteEndObject()

    let writeVersionPolicy (w: Utf8JsonWriter) (pack: PackEvidenceSet) =
        let baselineAndPacked (v: VersionVerdict) =
            match v with
            | Bumped(b, p) -> Some b, Some p
            | Downgraded(b, p) -> Some b, Some p
            | Unbumped p -> None, Some p
            | NoBaseline p -> None, Some p
            | NotPackable -> None, None

        let sorted =
            pack.Verdicts
            |> List.sortWith (fun a b -> System.String.CompareOrdinal(rrSurfaceValue a.Surface, rrSurfaceValue b.Surface))

        w.WritePropertyName "versionPolicy"
        w.WriteStartObject()
        w.WritePropertyName "projects"
        w.WriteStartArray()
        for v in sorted do
            let baseline, packed = baselineAndPacked v.Version
            w.WriteStartObject()
            w.WriteString("surface", rrSurfaceValue v.Surface)
            w.WriteString("verdict", rrVersionVerdictToken v.Version)
            rrNullableString w "baseline" baseline
            rrNullableString w "packed" packed
            w.WriteEndObject()
        w.WriteEndArray()
        w.WriteEndObject()

    let writeAttestationRef (w: Utf8JsonWriter) (attestation: AttestationSummary) =
        w.WritePropertyName "attestation"
        w.WriteStartObject()
        w.WriteString("schemaVersion", AttestationJson.schemaVersion)
        w.WriteString("identity", attestation.Identity)
        w.WriteString("compliance", AttestationJson.complianceToken)
        w.WriteNumber("subjectCount", List.length attestation.Subjects)
        w.WriteEndObject()

    let writeReleaseReadiness (w: Utf8JsonWriter) (preview: VerifyReleasePreview) =
        w.WritePropertyName "releaseReadiness"
        w.WriteStartObject()
        w.WriteBoolean("advisory", preview.Advisory)
        w.WriteString("verdict", rrVerdictToken preview.Verdict)
        writePackageEvidence w preview.Package
        writeVersionPolicy w preview.Package
        writeAttestationRef w preview.Attestation
        w.WriteEndObject()
