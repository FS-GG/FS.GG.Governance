// Curated public signature contract for the row-local `.fsgg/release.yml` declaration adapter (F055).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Declaration.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// the YamlDotNet node helpers, the per-family rule/expectation/layout readers, and the token recognizers
// live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Declaration.fs body
// exists (Principle I). This is a PURE leaf (no MVU ceremony — Principle IV): `parse` is total over the
// raw file lines and never touches the filesystem (the interpreter reads the bytes through the F014
// `Loader.FileReader` port and hands the content here). It is a ROW-LOCAL adapter: it parses the new
// release-declaration surface into the EXACT inputs the cores need — an F053 `ReleaseRule list`, an F054
// `ReleaseExpectations`, and an F054 `SourceLayout` — WITHOUT editing F014 `Config`'s frozen four-file
// schema, schema version, or surface baselines (research D2, confirmed planning decision).
//
// PRODUCT-NEUTRAL (FR-014): every value — surface id, version baseline, required field names, expected
// pins, posture/trusted-publishing/provenance tokens, and the six source paths — comes from the file; the
// adapter hardcodes none. FAIL-SAFE (FR-010): an absent OR malformed declaration is an `Error DeclError`
// (input-unavailable, never partial facts, never a fabricated `Met`). DETERMINISTIC: the produced
// `ReleaseRule list` is normalized to the F053 stable composite key order, so identical file content
// yields a structurally identical declaration. It reuses YamlDotNet in parse-to-node mode only (the F014
// `Schema.fs` precedent) — NO new dependency.

namespace FS.GG.Governance.ReleaseCommand

open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Declaration =

    /// The typed result of parsing `.fsgg/release.yml` — exactly the inputs the F053/F054 cores need,
    /// nothing more. `Rules` is the F053 declared rule list (one per declared family, composite-key
    /// ordered); `Expectations` is the F054 per-family "met" criteria + the governed `Surface`; `Layout`
    /// is the F054 per-family relative source paths. No raw YAML, host path, or timestamp is carried.
    type ReleaseDeclaration =
        { Rules: ReleaseRule list
          Expectations: ReleaseExpectations
          Layout: SourceLayout }

    /// A closed, explained reason a `release.yml` was rejected (the F014 `Diagnostic` spirit, row-local):
    /// actionable, product-neutral text identifying the missing/invalid declaration. Distinct from a
    /// sensing `Unrecoverable` family — this is the whole declaration being unavailable (⇒ exit 3).
    type DeclError = { Reason: string }

    /// Parse the raw lines of a `.fsgg/release.yml` into a `ReleaseDeclaration`. PURE and TOTAL — a
    /// malformed document (non-mapping root, unknown rule kind token, unrecognized severity/maturity
    /// token, missing required section, absent expectation/layout value) is an `Error DeclError`, never an
    /// exception and never partial facts. The `Rules` list is normalized to the F053 stable composite key
    /// order; every value is read from the file (product-neutral, FR-014). Reads NO filesystem — the
    /// content arrives from the F014 `Loader.FileReader` port at the interpreter edge.
    val parse: lines: string list -> Result<ReleaseDeclaration, DeclError>
