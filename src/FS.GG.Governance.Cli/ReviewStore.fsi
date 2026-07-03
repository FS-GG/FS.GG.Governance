// Curated public signature for the review-store persistence edge module (Phase E, 077).
//
// ReviewStore is the impure load/save of RecordedReview values keyed by review key. It
// owns store-root resolution (--review-store or ~/.cache/fs-gg-governance/reviews), key
// sanitization, and verdict (de)serialization. Compiles after ArtifactReading, before Program.
//
// The .fsi exposes only the two entry points the host effect interpreter calls; the
// store-root, safeFileName, keyHash, storeFileName, verdictText, and parseVerdict helpers
// stay HIDDEN.
//
// Key-collision safety (#55, M-CLI-2): the on-disk filename is `<sanitized>-<sha256[..7]>.txt`
// so two distinct keys that sanitize identically never share a file, and the key is stored as
// line 0 of the entry and verified on load — a mismatch is a miss, never a wrong-verdict hit.
// The old test-fixture backdoors (a `review-store-unavailable` / `review-dispatch-failed`
// substring on the repo root) are GONE from the shipped binary; failure paths are exercised
// through the injected store seam / a real unwritable store root, not a magic path.

namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReviewStore =

    /// Load a recorded review by key. `Ok None` when absent (or on a stored-key mismatch),
    /// `Ok (Some review)` when found for this exact key, `Error reason` for a malformed entry
    /// or OS error. Called by the host `LoadReview` effect. (Relocated from Program.loadReview.)
    val loadReview: request: RunRequest -> key: string -> Result<RecordedReview option, string>

    /// Persist a recorded review. `Error reason` for an OS error. Called by the host
    /// `RecordVerdict` effect. (Relocated from Program.saveReview.)
    val saveReview: request: RunRequest -> review: RecordedReview -> Result<unit, string>
