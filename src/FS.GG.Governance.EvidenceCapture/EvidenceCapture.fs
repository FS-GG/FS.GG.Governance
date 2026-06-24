// The pure evidence-capture bridge (F049). Visibility lives in EvidenceCapture.fsi (Principle II) — NO
// `private`/`internal`/`public` modifiers here. Both bodies are one-line compositions of already-merged
// operations: `referenceOf` wraps the F032 canonical identity as an `EvidenceRef`; `capture` folds it into the
// store via the F030 `record` convention verbatim. PURE and TOTAL (FR-007): no I/O, no clock, no process, no
// hashing — F032's `canonicalId` already did the byte-stable rendering.

namespace FS.GG.Governance.EvidenceCapture

open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceCapture =

    let referenceOf (record: CommandRecord) : EvidenceRef =
        EvidenceRef(CommandRecord.identityValue (CommandRecord.canonicalId record))

    let capture (inputs: FreshnessInputs) (record: CommandRecord) (store: ReuseStore) : ReuseStore =
        EvidenceReuse.record inputs (referenceOf record) store
