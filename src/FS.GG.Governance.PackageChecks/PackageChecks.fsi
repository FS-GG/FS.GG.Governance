// The pure package/API rule pack for F24 (P1). Visibility lives here (Constitution Principle II);
// PackageChecks.fs carries NO access modifiers.

namespace FS.GG.Governance.PackageChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PackageChecks =

    /// PURE and TOTAL (no I/O, no clock). Baseline drift + transcript findings, sorted by (locus, source,
    /// code). `BaselineMatches` / `TranscriptPasses` ⇒ no finding. `BaselineDrift` / compile-fail /
    /// result-change ⇒ Blocking deterministic findings naming what changed (FR-001, FR-003, SC-001).
    /// `BaselineAbsent` ⇒ an `IsInputState` finding ("baseline generated, commit it"), never a silent pass.
    /// `*Unreadable` / `*Unlocatable` ⇒ `IsInputState` findings naming the source (FR-012). Each finding
    /// carries the request's declared `EvidenceTag` (FR-009). Identical facts ⇒ byte-identical findings.
    val evaluate:
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.PackageFacts ->
            FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
