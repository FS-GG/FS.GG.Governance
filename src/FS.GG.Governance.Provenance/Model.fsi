// Curated public signature contract for the provenance types (F033).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, YAML-free values the `Provenance.build` /
// `canonicalId` operations construct and project over. They REUSE three sibling cores' vocabulary verbatim
// (FR-010) — opened, never redefined: F029 `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash` from
// `FS.GG.Governance.FreshnessKey.Model`, F032 `CommandRecord` from `FS.GG.Governance.CommandRecord.Model`,
// and F014 `EnvironmentClass` from `FS.GG.Governance.Config.Model`. The only genuinely new vocabulary is
// `BuilderIdentity` (who/what built it) and `ProvenanceIdentity` (the canonical identity). Nothing here
// carries raw bytes, a clock reading, or product vocabulary.

namespace FS.GG.Governance.Provenance

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// Who or what produced the evidence — a CI runner, an agent, a user. Opaque, comparable — the F029
    /// opaque-token discipline (no validation, no parsing; an empty string is a literal value). The ONLY
    /// genuinely new fact type this row adds (D2).
    type BuilderIdentity = BuilderIdentity of string

    /// The byte-stable canonical identity over the REPRODUCIBLE facts (FR-006). The wrapped string is the
    /// canonical rendering (`contracts/provenance-identity-format.md`); equality is exact byte equality.
    /// Mirrors F032's `CommandIdentity`.
    type ProvenanceIdentity = ProvenanceIdentity of string

    /// The complete provenance value (FR-001) — one flat closed record carrying ALL eight declared facts of
    /// a build, none dropped, stringly-typed away, or optional-by-omission. Base and head are the two
    /// `Revision`s of one "base/head" fact. The three revisions (`SourceCommit`/`Base`/`Head`) share the
    /// F029 `Revision` type but are DISTINCT facts and distinct identity segments (`src`/`base`/`head`), so
    /// the same revision string in two fields yields different identity segments (D2, injective across
    /// fields). `ArtifactDigests` is carried verbatim but treated as a SET in the identity (order/dup
    /// ignored — D4); `CommandRecords` is carried WHOLE and in ORDER and is order-significant in the
    /// identity (D4). The SENSED metadata lives INSIDE the embedded F032 records: each `CommandRecord` holds
    /// its sensed `Duration` in a field structurally apart from its `Reproducible` facts (F032 D2), reachable
    /// via `provenance.CommandRecords.[i].Duration` and structurally excluded from `canonicalId` (D3). There
    /// is no provenance-level sensed field — no wall-clock timestamp this row.
    type Provenance =
        { SourceCommit: Revision
          Base: Revision
          Head: Revision
          RuleHash: RuleHash
          GeneratorVersion: GeneratorVersion
          ArtifactDigests: ArtifactHash list
          CommandRecords: CommandRecord list
          Environment: EnvironmentClass
          Builder: BuilderIdentity }
