// Curated public signature for the review-store persistence edge module (Phase E, 077).
//
// ReviewStore is the impure load/save of RecordedReview values keyed by review key. It
// owns store-root resolution (--review-store or ~/.cache/fs-gg-governance/reviews), key
// sanitization, verdict (de)serialization, and the `review-store-unavailable` fixture
// short-circuit. Compiles after ArtifactReading, before Program.
//
// The .fsi exposes only the two entry points the host effect interpreter calls; the
// store-root, safeFileName, verdictText, and parseVerdict helpers stay HIDDEN.
//
// Byte-identity contract (US2 Acceptance 2/3, SC-001): key sanitization, verdict
// serialization, and store path resolution are byte-for-byte identical to before; the
// `review-store-unavailable` fixture root still yields the same failure reason. (The
// `review-dispatch-failed` budget path stays in Program.runHost — host policy, not store
// I/O — research D4.)

namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReviewStore =

    /// Load a recorded review by key. `Ok None` when absent, `Ok (Some review)` when found,
    /// `Error reason` for the unavailable fixture / malformed entry / OS error. Called by the
    /// host `LoadReview` effect. (Relocated from Program.loadReview.)
    val loadReview:
        request: RunRequest -> snapshot: ProjectSnapshot -> key: string -> Result<RecordedReview option, string>

    /// Persist a recorded review. `Error reason` for the unavailable fixture / OS error.
    /// Called by the host `RecordVerdict` effect. (Relocated from Program.saveReview.)
    val saveReview:
        request: RunRequest -> snapshot: ProjectSnapshot -> review: RecordedReview -> Result<unit, string>
