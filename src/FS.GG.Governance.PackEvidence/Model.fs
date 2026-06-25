namespace FS.GG.Governance.PackEvidence

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandKind.Model

// The F26 packed-evidence + version-policy type vocabulary (P1). Pure, product-neutral values — no raw YAML,
// host-absolute path, clock reading, or process exit code beyond the recorded run. The surface is Model.fsi
// (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type PackArtifact =
        { Surface: SurfaceId
          ArtifactPath: string
          PackedVersion: string
          Digest: ArtifactHash }

    type NoArtifactReason =
        | NoArtifactEmitted
        | ArtifactUnreadable of string

    type PackOutcome =
        | Packed of artifact: PackArtifact * run: KindedCommandRun
        | PackedNoArtifact of surface: SurfaceId * reason: NoArtifactReason * run: KindedCommandRun
        | PackFailed of surface: SurfaceId * sentinel: int * run: KindedCommandRun

    type VersionVerdict =
        | Bumped of baseline: string * packed: string
        | Unbumped of version: string
        | Downgraded of baseline: string * packed: string
        | NoBaseline of packed: string
        | NotPackable

    type PackVerdict =
        { Surface: SurfaceId
          Outcome: PackOutcome
          Version: VersionVerdict
          Reason: string }

    type PackEvidenceSet =
        { Verdicts: PackVerdict list
          Runs: KindedCommandRun list
          NoPackableProjects: bool }
