// The pure docs/examples rule pack for F24 (P2). Visibility lives here (Constitution Principle II);
// DocsChecks.fs carries NO access modifiers.

namespace FS.GG.Governance.DocsChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DocsChecks =

    /// PURE and TOTAL (no I/O). Dangling link ⇒ Blocking `docs.link-currency` naming file + link + target;
    /// stale reference ⇒ Blocking `docs.reference-currency` naming the stale symbol; all-resolve ⇒ zero
    /// findings (zero false positives, SC-002). A judgement-heavy example staleness ⇒ an Advisory
    /// `docs.example-freshness` finding that never blocks (C3, FR-011, US5). An unreadable source ⇒
    /// `IsInputState` `docs.source-unreadable` (FR-012). Each finding carries the request's declared
    /// `EvidenceTag` (FR-009). Sorted by (source, locus, code); identical facts ⇒ byte-identical findings.
    val evaluate:
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.DocsFacts ->
            FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
