namespace FS.GG.Governance.Attestation

open System
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model

// The F26 attestation-summary projection (P2). PURE and TOTAL: projects the F25 AuditSnapshot (+ pack
// subjects) into the SLSA/in-toto-SHAPED summary. Subjects come ONLY from the Packed outcomes (no fabricated
// subject for a failed build, FR-008); Materials/Invocation are the snapshot's Provenance/Runs verbatim;
// Identity is `Audit.snapshotIdentity` (= Provenance.canonicalId, F033 verbatim — duration never affects it).
// Compliance is ALWAYS CompatibleShapeNotFormalCompliance. The surface is Attestation.fsi (Principle II) — no
// access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Attestation =

    let summarize (snapshot: AuditSnapshot) (pack: PackEvidenceSet) : AttestationSummary =
        let p = snapshot.Provenance

        let subjects =
            pack.Verdicts
            |> List.choose (fun v ->
                match v.Outcome with
                | Packed(art, _) ->
                    Some
                        { Name = art.ArtifactPath
                          Digest = art.Digest
                          Version = art.PackedVersion }
                | PackedNoArtifact _
                | PackFailed _ -> None)
            |> List.sortWith (fun a b -> String.CompareOrdinal(a.Name, b.Name))

        { Subjects = subjects
          Builder = p.Builder
          Materials =
            { RuleHash = p.RuleHash
              GeneratorVersion = p.GeneratorVersion
              BaseRevision = p.Base
              HeadRevision = p.Head
              SourceCommit = p.SourceCommit
              ArtifactDigests = p.ArtifactDigests
              Environment = p.Environment }
          Invocation = { Runs = snapshot.Runs }
          Identity = Audit.snapshotIdentity snapshot
          Compliance = CompatibleShapeNotFormalCompliance }
