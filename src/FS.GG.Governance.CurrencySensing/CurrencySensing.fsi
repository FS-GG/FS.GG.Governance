// Curated public signature contract for the F070 generated-view currency EDGE SENSING (impure core).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// CurrencySensing.fs carries NO `private`/`internal`/`public` modifiers — the YamlDotNet node helpers, the
// digest/lock readers, and the maturity recognizer are hidden by their ABSENCE here.
//
// This is the host-shared seam (`fsgg verify` / `fsgg ship` reuse it) that turns a repo path into the
// stale-generated-view findings, reusing the F057 refresh determination VERBATIM (no new staleness detection,
// FR-007). It is impure (filesystem reads), so it lives at the host edge per Principle IV; the pure decision
// is the leaf's `decideCurrency`/`findingsOf`.

namespace FS.GG.Governance.CurrencySensing

open FS.GG.Governance.Config.Model
open FS.GG.Governance.RefreshJson.RefreshModel

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CurrencySensing =

    /// Parse `.fsgg/refresh.yml` content into the currency-enforcement dial + the per-view entries needed to
    /// decide currency (`ViewId`/`Kind`/`Sources`/`GeneratorBasis`; `OutputPath`/`Generator` are left empty —
    /// the currency decision does not use them). PURE and TOTAL: malformed/empty input ⇒ `None`. Reuses the
    /// existing `Maturity` vocabulary and `RefreshModel.viewKindOfToken`.
    val parseManifest: lines: string list -> (Maturity option * GenerationEntry list) option

    /// Sense generated-view currency for a repository working directory: parse `.fsgg/refresh.yml`, read each
    /// view's recorded provenance from `.fsgg/refresh.lock.json`, digest the declared sources + sense the
    /// generator version, decide currency (the F057 determination via the leaf's `decideCurrency`), and apply
    /// the manifest's `currency-enforcement` opt-in gate (`findingsOf`). TOTAL & SAFE: any I/O failure ⇒ `[]`
    /// (a per-view undeterminable source still surfaces as a `StaleUnresolved` finding, never a fabricated
    /// "current" pass — FR-008). An absent manifest or an absent dial ⇒ `[]` ⇒ byte-identical (FR-004).
    val senseRepo: repo: string -> FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list
