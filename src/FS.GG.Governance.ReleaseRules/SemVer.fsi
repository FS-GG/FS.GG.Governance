// The single semantic-version comparator shared by every release-family producer (review M-ADPT-1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// SemVer.fs carries NO access modifiers; the core/pre-release/identifier sub-comparators are ABSENT here
// (private by omission). Extracted verbatim from PackEvidence's comparator (the correct one) so the pack
// verdict and the F054 release-facts sensing can never disagree on `2.0.0-alpha.1` vs `2.0.0`. Every operation
// is PURE, TOTAL, DETERMINISTIC (never a clock/filesystem/process, never throws, byte-identical for identical
// input).

namespace FS.GG.Governance.ReleaseRules

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SemVer =

    /// Strip build metadata (`+…`) and split the optional pre-release (`-…`) off the numeric core, yielding
    /// `(core, preRelease option)`. PURE, TOTAL, never throws.
    val splitMeta: v: string -> string * string option

    /// Compare two versions by semantic-version precedence: numeric core segments numerically
    /// (`1.10.0 > 1.9.0`), missing trailing segments treated as 0 (`1.2` = `1.2.0`), build metadata ignored,
    /// and a pre-release lower than its release (`2.0.0-alpha.1 < 2.0.0`). Returns <0, 0, or >0. PURE, TOTAL,
    /// never throws.
    val compareVersions: a: string -> b: string -> int
